using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using PublicOrderAggregator.Domain.Entities;
using PublicOrderAggregator.Domain.Interfaces;

namespace PublicOrderAggregator.Infrastructure.AI
{
    public class SummaryService : ISummaryService
    {
        private readonly ILogger<SummaryService> _logger;
        private readonly IPromptService _promptService;
        private readonly IHtmlSectionExtractorFactory? _htmlSectionExtractorFactory;
        private readonly IOpenAIRateLimiter _rateLimiter;
        private readonly ChatClient _chatClient;
        private readonly int _maxTokens;
        private readonly float _temperature;
        private readonly int _maxContentLength;

        public SummaryService(
            IConfiguration configuration,
            ILogger<SummaryService> logger,
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
            _maxTokens = configuration.GetValue("OpenAI:Summary:MaxTokens", 500);
            _temperature = configuration.GetValue("OpenAI:Summary:Temperature", 0.3f);
            _maxContentLength = configuration.GetValue("OpenAI:Summary:MaxContentLength", 8000);
            
            var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey));
            _chatClient = openAiClient.GetChatClient(model);
            
            _logger.LogInformation("SummaryService initialized with model: {Model}", model);
        }

        public async Task<string> GenerateSummaryAsync(string htmlContent, string? sourceType = null)
        {
            try
            {
                // Extract content for summary (sections V and VI for BZP)
                var extractedContent = !string.IsNullOrEmpty(sourceType) && _htmlSectionExtractorFactory != null
                    ? _htmlSectionExtractorFactory.CreateExtractor(sourceType).ExtractSectionsForRequirements(htmlContent)
                    : htmlContent;

                if (string.IsNullOrWhiteSpace(extractedContent))
                {
                    _logger.LogWarning("Brak treści do streszczenia");
                    return string.Empty;
                }

                if (extractedContent.Length > _maxContentLength)
                {
                    extractedContent = extractedContent.Substring(0, _maxContentLength) + "...";
                }

                // Load prompt template
                var promptTemplate = await _promptService.GetSummaryPromptAsync();
                var summaryPrompt = promptTemplate
                    .Replace("{CONTENT}", extractedContent);

                var messages = new List<ChatMessage>
                {
                    new UserChatMessage(summaryPrompt)
                };

                var chatCompletionOptions = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = _maxTokens,
                    Temperature = _temperature
                };

                // Simple rate limiting
                await _rateLimiter.WaitAsync();
                
                _logger.LogInformation("Generuję streszczenie...");
                
                var response = await _chatClient.CompleteChatAsync(messages, chatCompletionOptions);
                var summaryText = response.Value.Content[0].Text.Trim();
                
                _logger.LogInformation("Wygenerowano streszczenie ({Length} znaków)", summaryText.Length);
                
                return summaryText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas generowania streszczenia");
                return string.Empty;
            }
        }

        public async Task<Dictionary<string, string>> GenerateBatchSummariesAsync(IEnumerable<PublicOrder> orders)
        {
            var results = new Dictionary<string, string>();
            var ordersList = orders.ToList();
            
            _logger.LogInformation("Rozpoczynam generowanie streszczeń dla {Count} zamówień", ordersList.Count);
            
            var currentIndex = 1;
            foreach (var order in ordersList)
            {
                _logger.LogInformation("Generuję streszczenie {Current}/{Total}: {Subject}", 
                    currentIndex, ordersList.Count, order.Subject);
                
                var summary = await GenerateSummaryAsync(order.HtmlContent ?? string.Empty, "BZP");
                results[order.OriginalUrl] = summary;
                currentIndex++;
            }
            
            var successCount = results.Count(r => !string.IsNullOrEmpty(r.Value));
            _logger.LogInformation("Generowanie streszczeń zakończone: {Success}/{Total} streszczeń wygenerowanych", 
                successCount, results.Count);
            
            return results;
        }
    }
}
