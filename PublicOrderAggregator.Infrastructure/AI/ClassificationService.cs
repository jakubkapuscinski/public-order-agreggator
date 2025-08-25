using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using PublicOrderAggregator.Domain.Entities;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Domain.Models.DTOs;

namespace PublicOrderAggregator.Infrastructure.AI
{
    public class ClassificationService : IClassificationService
    {
        private readonly ILogger<ClassificationService> _logger;
        private readonly IPromptService _promptService;
        private readonly IHtmlSectionExtractorFactory? _htmlSectionExtractorFactory;
        private readonly IOpenAIRateLimiter _rateLimiter;
        private readonly ChatClient _chatClient;
        private readonly int _maxTokens;
        private readonly float _temperature;
        private readonly int _maxContentLength;

        public ClassificationService(
            IConfiguration configuration, 
            ILogger<ClassificationService> logger,
            IPromptService promptService,
            IOpenAIRateLimiter rateLimiter,
            IHtmlSectionExtractorFactory? htmlSectionExtractorFactory = null)
        {
            _logger = logger;
            _promptService = promptService;
            _rateLimiter = rateLimiter;
            _htmlSectionExtractorFactory = htmlSectionExtractorFactory;
            
            var apiKey = configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured");
            }
            
            var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
            _maxTokens = configuration.GetValue("OpenAI:Classification:MaxTokens", 10);
            _temperature = configuration.GetValue("OpenAI:Classification:Temperature", 0.0f);
            _maxContentLength = configuration.GetValue("OpenAI:Classification:MaxContentLength", 4000);
            
            var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey));
            _chatClient = openAiClient.GetChatClient(model);
            
            _logger.LogInformation("ClassificationService initialized with model: {Model}", model);
        }

        public async Task<ClassificationResultDto> ClassifyOrderAsync(string orderContent, string subject, string orderUrl, string? sourceType = null)
        {
            var result = new ClassificationResultDto 
            { 
                OrderUrl = orderUrl, 
                OrderTitle = subject, 
                IsRelevant = false
            };

            try
            {
                // Extract content for classification
                var extractedContent = !string.IsNullOrEmpty(sourceType) && _htmlSectionExtractorFactory != null
                    ? _htmlSectionExtractorFactory.CreateExtractor(sourceType).ExtractSectionForClassification(orderContent)
                    : orderContent;

                if (string.IsNullOrWhiteSpace(extractedContent))
                {
                    _logger.LogWarning("Brak treści do klasyfikacji dla: {Subject}", subject);
                    return result;
                }

                if (extractedContent.Length > _maxContentLength)
                {
                    extractedContent = extractedContent.Substring(0, _maxContentLength) + "...";
                }

                // Load and prepare prompt
                var promptTemplate = await _promptService.GetClassificationPromptAsync();
                var keywords = await _promptService.GetKeywordsAsync();
                var exclusions = await _promptService.GetExclusionsAsync();
                
                var classificationPrompt = promptTemplate
                    .Replace("{TITLE}", subject)
                    .Replace("{CONTENT}", extractedContent)
                    .Replace("{KEYWORDS}", string.Join(", ", keywords))
                    .Replace("{EXCLUSIONS}", string.Join(", ", exclusions));

                var messages = new List<ChatMessage>
                {
                    new UserChatMessage(classificationPrompt)
                };

                var chatCompletionOptions = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = _maxTokens,
                    Temperature = _temperature
                };

                // Simple rate limiting - just wait
                await _rateLimiter.WaitAsync();
                
                _logger.LogInformation("Klasyfikuję: {Subject}", subject);
                
                var response = await _chatClient.CompleteChatAsync(messages, chatCompletionOptions);
                var responseText = response.Value.Content[0].Text.Trim().ToUpper();
                
                result.IsRelevant = responseText.Contains("TAK");
                
                _logger.LogInformation("Klasyfikacja '{Subject}': {Decision}", subject, result.IsRelevant ? "TAK" : "NIE");
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas klasyfikacji zamówienia: {Subject}", subject);
                return result; // Return false on error
            }
        }

        public async Task<List<ClassificationResultDto>> ClassifyBatchAsync(IEnumerable<PublicOrder> orders, string? sourceType = null)
        {
            var results = new List<ClassificationResultDto>();
            var ordersList = orders.ToList();
            
            _logger.LogInformation("Rozpoczynam klasyfikację {Count} zamówień", ordersList.Count);
            
            var currentIndex = 1;
            foreach (var order in ordersList)
            {
                _logger.LogInformation("Klasyfikuję ogłoszenie {Current}/{Total}: {Subject}", 
                    currentIndex, ordersList.Count, order.Subject);
                
                var result = await ClassifyOrderAsync(order.HtmlContent ?? string.Empty, order.Subject, order.OriginalUrl, sourceType);
                results.Add(result);
                currentIndex++;
            }
            
            var relevantCount = results.Count(r => r.IsRelevant);
            _logger.LogInformation("Klasyfikacja zakończona: {Relevant}/{Total} zamówień jest istotnych", 
                relevantCount, results.Count);
            
            return results;
        }
    }
}
