using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Domain.Models.DTOs;
using PublicOrderAggregator.Infrastructure.DataSources.BZP.Models;

namespace PublicOrderAggregator.Infrastructure.DataSources.BZP
{
    public class BzpDataSource : IDataSource
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BzpDataSource> _logger;

        public string SourceName => "BZP";

        public BzpDataSource(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<BzpDataSource> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IEnumerable<RawOrderDataDto>> FetchRawDataAsync()
        {
            var rawDataList = new List<RawOrderDataDto>();
            
            var baseUrl = _configuration["DataSources:BZP:BaseUrl"];
            var noticeTypes = _configuration.GetSection("DataSources:BZP:NoticeTypes").Get<List<string>>();
            var pageSize = _configuration.GetValue<int>("DataSources:BZP:PageSize", 100);
            var daysBack = _configuration.GetValue<int>("DataSources:BZP:DaysBack", 7);
            
            var fromDate = DateTime.Now.AddDays(-daysBack).ToString("yyyy-MM-ddTHH:mm:ss");
            var toDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            
            foreach (var noticeType in noticeTypes ?? new List<string>())
            {
                var url = $"{baseUrl}/notice?NoticeType={noticeType}&PublicationDateFrom={fromDate}&PublicationDateTo={toDate}&PageSize={pageSize}";
                await FetchNoticesFromUrl(url, rawDataList);
            }
            
            return rawDataList;
        }

        private async Task FetchNoticesFromUrl(string baseUrl, List<RawOrderDataDto> rawDataList)
        {
            string? searchAfter = null;
            int pageNumber = 1;

            while (true)
            {
                var url = baseUrl;
                if (!string.IsNullOrEmpty(searchAfter))
                {
                    url += $"&SearchAfter={Uri.EscapeDataString(searchAfter)}";
                }
                
                _logger.LogDebug($"Fetching page {pageNumber}: {url}");
                
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to fetch from {url}: {response.StatusCode}");
                    break;
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var notices = JsonSerializer.Deserialize<List<BzpNoticeDto>>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (notices == null || !notices.Any())
                {
                    break;
                }

                foreach (var notice in notices)
                {
                    var rawData = new RawOrderDataDto
                    {
                        UniqueId = notice.ObjectId,
                        SourceUrl = $"https://ezamowienia.gov.pl/mo-client-board/bzp/notice-details/id/{notice.ObjectId}",
                        Content = notice.HtmlBody ?? string.Empty,
                        FetchedAt = DateTime.Now,
                        Metadata = new Dictionary<string, object>
                        {
                            ["NoticeNumber"] = notice.NoticeNumber ?? string.Empty,
                            ["NoticeType"] = notice.NoticeType ?? string.Empty,
                            ["Organizer"] = notice.OrganizationName ?? string.Empty,
                            ["Location"] = $"{notice.OrganizationCity}, {notice.OrganizationProvince}".Trim(' ', ','),
                            ["Subject"] = notice.OrderObject ?? string.Empty,
                            ["PublicationDate"] = notice.PublicationDate,
                            ["TenderDate"] = notice.PublicationDate,
                            ["SubmissionDeadline"] = notice.SubmittingOffersDate ?? notice.PublicationDate.AddDays(30)
                        }
                    };
                    
                    rawDataList.Add(rawData);
                }

                searchAfter = notices.Last().ObjectId;
                pageNumber++;

                if (pageNumber > 1000) // Safety limit
                {
                    _logger.LogWarning("Reached maximum page limit");
                    break;
                }
            }
            
            _logger.LogInformation($"Fetched {rawDataList.Count} notices from BZP");
        }
    }
}