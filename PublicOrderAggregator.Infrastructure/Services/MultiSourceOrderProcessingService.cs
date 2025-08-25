using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PublicOrderAggregator.Domain.Entities;
using PublicOrderAggregator.Domain.Interfaces;
using System.Text.Json;

namespace PublicOrderAggregator.Infrastructure.Services
{
    public class MultiSourceOrderProcessingService : IDataSourceService
    {
        private readonly ILogger<MultiSourceOrderProcessingService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IDataSourceFactory _dataSourceFactory;
        private readonly IPublicOrderRepository _repository;
        private readonly IClassificationService _classificationService;
        private readonly ISummaryService _summaryService;
        private readonly IHtmlSanitizer _htmlSanitizer;

        public MultiSourceOrderProcessingService(
            ILogger<MultiSourceOrderProcessingService> logger,
            IConfiguration configuration,
            IDataSourceFactory dataSourceFactory,
            IPublicOrderRepository repository,
            IClassificationService classificationService,
            ISummaryService summaryService,
            IHtmlSanitizer htmlSanitizer)
        {
            _logger = logger;
            _configuration = configuration;
            _dataSourceFactory = dataSourceFactory;
            _repository = repository;
            _classificationService = classificationService;
            _summaryService = summaryService;
            _htmlSanitizer = htmlSanitizer;
        }

        public async Task<IEnumerable<PublicOrder>> FetchAndProcessOrdersAsync()
        {
            var allOrders = new List<PublicOrder>();
            var enabledSources = _dataSourceFactory.GetAvailableSourceTypes();

            if (!enabledSources.Any())
            {
                _logger.LogWarning("No data sources are enabled");
                return allOrders;
            }

            foreach (var sourceType in enabledSources)
            {
                try
                {
                    _logger.LogInformation($"Processing source: {sourceType}");
                    var orders = await ProcessSourceAsync(sourceType);
                    allOrders.AddRange(orders);
                    _logger.LogInformation($"Processed {orders.Count()} orders from {sourceType}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing source {sourceType}");
                    if (_configuration.GetValue<bool>("StopOnError", false))
                    {
                        throw;
                    }
                }
            }

            _logger.LogInformation($"Total orders processed from all sources: {allOrders.Count}");
            return allOrders;
        }

        private async Task<IEnumerable<PublicOrder>> ProcessSourceAsync(string sourceType)
        {
            try
            {
                var dataSource = _dataSourceFactory.CreateDataSource(sourceType);
                var parser = _dataSourceFactory.GetParser(sourceType);
                
                _logger.LogInformation($"Fetching raw data from {sourceType}...");
                var rawDataList = await dataSource.FetchRawDataAsync();
                var processedOrders = new List<PublicOrder>();
                
                _logger.LogInformation($"Fetched {rawDataList.Count()} items from {sourceType}");
                
                foreach (var rawData in rawDataList)
                {
                    try
                    {
                        // Check if order already exists
                        if (await _repository.ExistsAsync(rawData.UniqueId, sourceType))
                        {
                            _logger.LogDebug($"Order {rawData.UniqueId} already exists, skipping");
                            continue;
                        }
                        
                        // Parse the raw data
                        var parsedData = await parser.ParseAsync(rawData);
                        
                        // Map to PublicOrder entity
                        var order = MapToPublicOrder(rawData, parsedData, sourceType);
                        
                        // Save to database
                        await _repository.AddAsync(order);
                        processedOrders.Add(order);
                        
                        _logger.LogDebug($"Saved order {order.ExternalId} from {sourceType}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing item {rawData.UniqueId} from {sourceType}");
                        if (_configuration.GetValue<bool>("StopOnError", false))
                        {
                            throw;
                        }
                    }
                }
                
                _logger.LogInformation($"Saved {processedOrders.Count} new orders from {sourceType}");
                
                // Process classification and summarization
                if (processedOrders.Any())
                {
                    await ProcessClassificationAndSummarization(processedOrders, sourceType);
                }
                
                return processedOrders;
            }
            catch (NotSupportedException)
            {
                _logger.LogWarning($"Source type {sourceType} is not yet implemented");
                return Enumerable.Empty<PublicOrder>();
            }
        }
        
        private PublicOrder MapToPublicOrder(Domain.Models.DTOs.RawOrderDataDto rawData, Domain.Models.DTOs.ParsedOrderDataDto parsedData, string sourceType)
        {
            var order = new PublicOrder
            {
                ExternalId = rawData.UniqueId,
                DataSource = sourceType,
                OriginalUrl = rawData.SourceUrl,
                FetchedAt = rawData.FetchedAt,
                RawData = JsonSerializer.Serialize(rawData.Metadata),
                HtmlContent = rawData.Content,
                
                // From parsed data
                Organizer = parsedData.Organizer,
                Location = parsedData.Location,
                Subject = parsedData.Subject,
                TenderDate = parsedData.TenderDate ?? DateTime.Now,
                SubmissionDeadline = parsedData.SubmissionDeadline ?? DateTime.Now.AddDays(30),
                
                // Initial state
                IsClassified = false,
                IsRelevant = false,
                IsSummarized = false,
                IsIncludedInReport = false
            };
            
            // Extract plain text if HTML content exists
            if (!string.IsNullOrWhiteSpace(order.HtmlContent) && order.HtmlContent.Contains("<"))
            {
                order.Requirements = _htmlSanitizer.ExtractPlainText(order.HtmlContent);
            }
            else
            {
                order.Requirements = parsedData.FullContent;
            }
            
            return order;
        }
        
        private async Task ProcessClassificationAndSummarization(List<PublicOrder> orders, string sourceType)
        {
            try
            {
                _logger.LogInformation($"Starting classification for {orders.Count} orders from {sourceType}");
                
                // Classify orders
                var classificationResults = await _classificationService.ClassifyBatchAsync(orders, sourceType);
                var relevantOrders = new List<PublicOrder>();
                
                foreach (var order in orders)
                {
                    order.IsClassified = true;
                    order.ClassifiedAt = DateTime.Now;
                    
                    var result = classificationResults.FirstOrDefault(r => r.OrderUrl == order.OriginalUrl);
                    if (result != null && result.IsRelevant)
                    {
                        order.IsRelevant = true;
                        relevantOrders.Add(order);
                    }
                }
                
                // Update classification status in database
                await _repository.UpdateBatchAsync(orders);
                
                _logger.LogInformation($"Classification complete: {relevantOrders.Count}/{orders.Count} orders are relevant");
                
                // Generate summaries for relevant orders
                if (relevantOrders.Any())
                {
                    _logger.LogInformation($"Generating summaries for {relevantOrders.Count} relevant orders");
                    
                    var summaries = await _summaryService.GenerateBatchSummariesAsync(relevantOrders);
                    
                    foreach (var order in relevantOrders)
                    {
                        var summary = summaries.FirstOrDefault(kvp => kvp.Key == order.OriginalUrl);
                        if (!summary.Equals(default(KeyValuePair<string, string>)) && !string.IsNullOrWhiteSpace(summary.Value))
                        {
                            order.Requirements = summary.Value;
                            order.IsSummarized = true;
                            order.SummarizedAt = DateTime.Now;
                        }
                    }
                    
                    // Update summaries in database
                    await _repository.UpdateBatchAsync(relevantOrders);
                    
                    _logger.LogInformation($"Generated summaries for {relevantOrders.Count} orders");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during classification/summarization for {sourceType}");
                if (_configuration.GetValue<bool>("StopOnError", false))
                {
                    throw;
                }
            }
        }
    }
}
