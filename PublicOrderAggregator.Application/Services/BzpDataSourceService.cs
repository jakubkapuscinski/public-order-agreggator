using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private readonly IGPTClassificationService _gptClassificationService;
        private readonly IHtmlSanitizer _htmlSanitizer;
        private readonly BzpDataSourceMapper _mapper;

        public BzpDataSourceService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<BzpDataSourceService> logger,
            IPublicOrderRepository repository,
            IGPTSummaryService gptService,
            IGPTClassificationService gptClassificationService,
            IHtmlSanitizer htmlSanitizer)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _repository = repository;
            _gptService = gptService;
            _gptClassificationService = gptClassificationService;
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
                var baseUrl = _configuration["DataSources:BZP:BaseUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                    throw new InvalidOperationException("Brak ustawienia DataSources:BZP:BaseUrl w konfiguracji.");

                // Removed filterKeywords - now processing ALL orders with GPT classification

                var noticeTypes = _configuration.GetSection("DataSources:BZP:NoticeTypes").Get<List<string>>();
                if (noticeTypes == null)
                    throw new InvalidOperationException("Brak sekcji DataSources:BZP:NoticeTypes w konfiguracji.");

                var pageSize = _configuration.GetValue<int?>("DataSources:BZP:PageSize");
                if (!pageSize.HasValue)
                    throw new InvalidOperationException("Brak ustawienia DataSources:BZP:PageSize w konfiguracji.");
                int pageSizeValue = pageSize.Value;

                var daysBack = _configuration.GetValue<int?>("DataSources:BZP:DaysBack");
                if (!daysBack.HasValue)
                    throw new InvalidOperationException("Brak ustawienia DataSources:BZP:DaysBack w konfiguracji.");
                int daysBackValue = daysBack.Value;
                
                var fromDate = DateTime.Now.AddDays(-daysBackValue).ToString("yyyy-MM-ddTHH:mm:ss");
                var toDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                
                // Process each notice type configured
                foreach (var noticeType in noticeTypes)
                {
                    var url = $"{baseUrl}/notice?NoticeType={noticeType}&PublicationDateFrom={fromDate}&PublicationDateTo={toDate}&PageSize={pageSizeValue}";
                    await ProcessNoticeType(url, orders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching data from BZP API");
            }

            return orders;
        }

        private async Task ProcessNoticeType(string url, List<PublicOrder> orders)
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

                var allOrders = new List<PublicOrder>();

                // STEP 1: Create PublicOrder objects for all notices (no filtering)
                foreach (var notice in notices)
                {
                    try
                    {
                        if (await _repository.ExistsAsync($"https://ezamowienia.gov.pl/mo-client-board/bzp/notice-details/id/{notice.ObjectId}"))
                        {
                            _logger.LogDebug($"Notice {notice.ObjectId} already exists, skipping");
                            continue;
                        }

                        var order = _mapper.MapToPublicOrder(notice);
                        
                        // Store clean text in RawData for GPT classification
                        if (!string.IsNullOrEmpty(notice.HtmlBody))
                        {
                            var cleanText = _htmlSanitizer.ExtractPlainText(notice.HtmlBody);
                            order.RawData = cleanText;
                        }
                        
                        allOrders.Add(order);
                        _logger.LogDebug($"Mapped notice {notice.ObjectId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing notice {notice.ObjectId}");
                    }
                }

                if (!allOrders.Any())
                {
                    _logger.LogInformation("No new orders to process");
                    return;
                }

                _logger.LogInformation($"STEP 1: Created {allOrders.Count} orders for GPT classification");

                // STEP 2: GPT Classification (TAK/NIE) with reasoning
                var classificationResults = await _gptClassificationService.ClassifyBatchAsync(allOrders);
                var relevantResults = classificationResults.Where(r => r.IsRelevant).ToList();
                var classifiedOrders = allOrders.Where(o => relevantResults.Any(r => r.OrderUrl == o.OriginalUrl)).ToList();

                _logger.LogInformation($"STEP 2: GPT classified {classifiedOrders.Count}/{allOrders.Count} orders as relevant");

                // Add classification reasoning to orders
                foreach (var order in classifiedOrders)
                {
                    var reasoning = relevantResults.FirstOrDefault(r => r.OrderUrl == order.OriginalUrl)?.Reasoning;
                    if (!string.IsNullOrEmpty(reasoning))
                    {
                        order.ClassificationReasoning = reasoning;
                    }
                }

                if (!classifiedOrders.Any())
                {
                    _logger.LogInformation("No orders classified as relevant by GPT");
                    return;
                }

                // STEP 3: Generate detailed summaries for classified orders only
                _logger.LogInformation($"STEP 3: Generating summaries for {classifiedOrders.Count} classified orders");
                var summaries = await _gptService.GenerateBatchSummariesAsync(classifiedOrders);
                _logger.LogInformation($"Received {summaries.Count} summaries from GPT service");
                
                // STEP 4: Save to database
                foreach (var order in classifiedOrders)
                {
                    _logger.LogDebug($"Processing classified order {order.OriginalUrl}");
                    
                    if (summaries.ContainsKey(order.OriginalUrl))
                    {
                        order.Requirements = summaries[order.OriginalUrl];
                        _logger.LogDebug($"Found summary for {order.OriginalUrl}: {order.Requirements.Substring(0, Math.Min(100, order.Requirements.Length))}...");
                    }
                    else
                    {
                        order.Requirements = "Nie udało się wygenerować streszczenia.";
                        _logger.LogWarning($"No summary found for {order.OriginalUrl}");
                    }
                    
                    // Ensure classification reasoning is set (fallback if not already set)
                    if (string.IsNullOrEmpty(order.ClassificationReasoning))
                    {
                        var reasoning = relevantResults.FirstOrDefault(r => r.OrderUrl == order.OriginalUrl)?.Reasoning;
                        order.ClassificationReasoning = reasoning ?? "Zamówienie zostało zakwalifikowane przez AI";
                    }
                    
                    // Store original JSON in RawData
                    var originalNotice = notices.FirstOrDefault(n => order.OriginalUrl.Contains(n.ObjectId));
                    if (originalNotice != null)
                    {
                        order.RawData = JsonSerializer.Serialize(originalNotice, new JsonSerializerOptions { WriteIndented = false });
                    }
                    
                    await _repository.AddAsync(order);
                    orders.Add(order);
                }
                
                _logger.LogInformation($"STEP 4: Successfully saved {classifiedOrders.Count} classified and processed orders");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notice type from BZP API");
            }
        }
    }
}