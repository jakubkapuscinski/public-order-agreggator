using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PublicOrderAggregator.Domain.Entities;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Infrastructure.ExternalServices.BZP.Models;
using PublicOrderAggregator.Infrastructure.Mappers;

namespace PublicOrderAggregator.Application.Services
{
    public class BzpDataSourceService : IDataSourceService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BzpDataSourceService> _logger;
        private readonly IPublicOrderRepository _repository;
        private readonly IGPTSummaryService _gptService;
        private readonly IHtmlSanitizer _htmlSanitizer;
        private readonly BzpDataSourceMapper _mapper;

        public BzpDataSourceService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<BzpDataSourceService> logger,
            IPublicOrderRepository repository,
            IGPTSummaryService gptService,
            IHtmlSanitizer htmlSanitizer)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _repository = repository;
            _gptService = gptService;
            _htmlSanitizer = htmlSanitizer;
            _mapper = new BzpDataSourceMapper();
        }

        public async Task<IEnumerable<PublicOrder>> FetchAndProcessOrdersAsync()
        {
            var orders = new List<PublicOrder>();
            
            if (!_configuration.GetValue<bool>("DataSources:BZP:Enabled"))
            {
                _logger.LogInformation("BZP data source is disabled");
                return orders;
            }

            try
            {
                var baseUrl = _configuration["DataSources:BZP:BaseUrl"] ?? "https://ezamowienia.gov.pl/mo-board/api/v1";
                var filterKeywords = _configuration.GetSection("FilterKeywords").Get<List<string>>() ?? new List<string>();
                var noticeTypes = _configuration.GetSection("DataSources:BZP:NoticeTypes").Get<List<string>>() ?? new List<string> { "ContractNotice" };
                var pageSize = _configuration.GetValue<int>("DataSources:BZP:PageSize", 100);
                var daysBack = _configuration.GetValue<int>("DataSources:BZP:DaysBack", 3);
                
                var fromDate = DateTime.Now.AddDays(-daysBack).ToString("yyyy-MM-ddTHH:mm:ss");
                var toDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                
                // Process each notice type configured
                foreach (var noticeType in noticeTypes)
                {
                    var url = $"{baseUrl}/notice?NoticeType={noticeType}&PublicationDateFrom={fromDate}&PublicationDateTo={toDate}&PageSize={pageSize}";
                    await ProcessNoticeType(url, filterKeywords, orders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching data from BZP API");
            }

            return orders;
        }

        private async Task ProcessNoticeType(string url, List<string> filterKeywords, List<PublicOrder> orders)
        {
            try
            {
                _logger.LogInformation($"Fetching notices from BZP API: {url}");
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var notices = JsonSerializer.Deserialize<List<BzpNoticeDto>>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (notices == null || !notices.Any())
                {
                    _logger.LogInformation("No notices found from BZP API");
                    return;
                }

                var filteredOrders = new List<PublicOrder>();

                // First pass: filter and create orders without GPT summaries
                foreach (var notice in notices)
                {
                    try
                    {
                        if (await _repository.ExistsAsync($"https://ezamowienia.gov.pl/mo-client-board/bzp/notice-details/id/{notice.ObjectId}"))
                        {
                            _logger.LogDebug($"Notice {notice.ObjectId} already exists, skipping");
                            continue;
                        }

                        if (filterKeywords.Any() && !string.IsNullOrEmpty(notice.OrderObject))
                        {
                            var hasKeyword = filterKeywords.Any(keyword => 
                                notice.OrderObject.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                (notice.HtmlBody != null && notice.HtmlBody.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
                            
                            if (!hasKeyword)
                            {
                                _logger.LogDebug($"Notice {notice.ObjectId} doesn't match keywords, skipping");
                                continue;
                            }
                        }

                        var order = _mapper.MapToPublicOrder(notice);
                        
                        // Store clean text in RawData for batch GPT processing
                        if (!string.IsNullOrEmpty(notice.HtmlBody))
                        {
                            var cleanText = _htmlSanitizer.ExtractPlainText(notice.HtmlBody);
                            order.RawData = cleanText;
                        }
                        
                        filteredOrders.Add(order);
                        _logger.LogInformation($"Filtered and mapped notice {notice.ObjectId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing notice {notice.ObjectId}");
                    }
                }

                // Second pass: generate batch summaries for filtered orders
                if (filteredOrders.Any())
                {
                    _logger.LogInformation($"Generating batch summaries for {filteredOrders.Count} filtered orders");
                    var summaries = await _gptService.GenerateBatchSummariesAsync(filteredOrders);
                    _logger.LogInformation($"Received {summaries.Count} summaries from GPT service");
                    
                    foreach (var order in filteredOrders)
                    {
                        _logger.LogDebug($"Processing order {order.OriginalUrl}");
                        
                        if (summaries.ContainsKey(order.OriginalUrl))
                        {
                            order.Requirements = summaries[order.OriginalUrl];
                            _logger.LogDebug($"Found summary for {order.OriginalUrl}: {order.Requirements.Substring(0, Math.Min(100, order.Requirements.Length))}...");
                        }
                        else
                        {
                            order.Requirements = "Nie udało się wygenerować streszczenia.";
                            _logger.LogWarning($"No summary found for {order.OriginalUrl}. Available keys: {string.Join(", ", summaries.Keys)}");
                        }
                        
                        // Keep only essential data in RawData (original JSON)
                        var originalNotice = notices.FirstOrDefault(n => order.OriginalUrl.Contains(n.ObjectId));
                        if (originalNotice != null)
                        {
                            order.RawData = JsonSerializer.Serialize(originalNotice, new JsonSerializerOptions { WriteIndented = false });
                        }
                        
                        await _repository.AddAsync(order);
                        orders.Add(order);
                    }
                    
                    _logger.LogInformation($"Successfully processed {filteredOrders.Count} orders with summaries");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notice type from BZP API");
            }
        }
    }
}