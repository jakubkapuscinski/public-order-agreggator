using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using PublicOrderAggregator.Domain.Entities;
using PublicOrderAggregator.Domain.Interfaces;

namespace PublicOrderAggregator.Application.Services
{
    public class GptSummaryService : IGPTSummaryService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GptSummaryService> _logger;
        private readonly OpenAIClient _openAiClient;
        private readonly ChatClient _chatClient;
        private readonly int _maxTokens;
        private readonly float _temperature;
        private readonly int _maxContentLength;
        private readonly int _batchProcessingDelay;

        public GptSummaryService(IConfiguration configuration, ILogger<GptSummaryService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured");
            }
            
            var model = _configuration["OpenAI:Model"];
            if (string.IsNullOrEmpty(model))
            {
                throw new InvalidOperationException("Brak ustawienia OpenAI:Model w konfiguracji.");
            }
            
            _maxTokens = _configuration.GetValue<int>("OpenAI:MaxTokens");
            if (_maxTokens == 0)
            {
                throw new InvalidOperationException("Brak ustawienia OpenAI:MaxTokens w konfiguracji.");
            }
            
            _temperature = _configuration.GetValue<float>("OpenAI:Temperature");
            _maxContentLength = _configuration.GetValue<int>("OpenAI:MaxContentLength");
            if (_maxContentLength == 0)
            {
                throw new InvalidOperationException("Brak ustawienia OpenAI:MaxContentLength w konfiguracji.");
            }
            
            _batchProcessingDelay = _configuration.GetValue<int>("OpenAI:BatchProcessingDelay");
            if (_batchProcessingDelay == 0)
            {
                throw new InvalidOperationException("Brak ustawienia OpenAI:BatchProcessingDelay w konfiguracji.");
            }
            
            _openAiClient = new OpenAIClient(apiKey);
            _chatClient = _openAiClient.GetChatClient(model);
            
            _logger.LogInformation("GptSummaryService initialized with model: {Model}, MaxTokens: {MaxTokens}, Temperature: {Temperature}", 
                model, _maxTokens, _temperature);
        }

        public async Task<string> GenerateSummaryAsync(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                return "No content available for summary.";
            }

            try
            {
                // Truncate content to avoid token limits
                var truncatedContent = htmlContent.Length > _maxContentLength 
                    ? htmlContent.Substring(0, _maxContentLength) + "... [content truncated]"
                    : htmlContent;

                var prompt = @"Przeanalizuj poniższe ogłoszenie i napisz WYŁĄCZNIE merytoryczne wymagania techniczne i warunki realizacji w 200-250 słowach. 

NIE POWTARZAJ:
• Nazwy zamówienia/tytułu
• Nazwy zamawiającego
• Dat publikacji
• Podstawowych informacji już widocznych na karcie

SKUP SIĘ NA:
• Szczegółowych wymaganiach technicznych i specyfikacjach
• Parametrach technicznych urządzeń/materiałów
• Warunkach realizacji prac/usług
• Wymaganiach wobec wykonawcy (doświadczenie, certyfikaty)
• Kryteriach oceny ofert i ich wagach
• Terminach realizacji etapów
• Warunkach płatności i gwarancji
• Wymaganych dokumentach i atestach

Pisz konkretnie, bez wstępów. Każde zdanie = konkretna informacja.

TREŚĆ OGŁOSZENIA:
" + truncatedContent;

                var messages = new List<ChatMessage>
                {
                    new UserChatMessage(prompt)
                };

                var chatCompletionOptions = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = _maxTokens,
                    Temperature = _temperature
                };

                var response = await _chatClient.CompleteChatAsync(messages, chatCompletionOptions);
                
                _logger.LogInformation("Successfully generated GPT summary");
                return response.Value.Content[0].Text;
            }
            catch (ClientResultException ex) when (ex.Status == 429)
            {
                _logger.LogWarning(ex, "OpenAI rate limit exceeded, returning fallback summary");
                return "Summary generation temporarily unavailable due to rate limits. Please try again later.";
            }
            catch (ClientResultException ex) when (ex.Status == 413)
            {
                _logger.LogWarning(ex, "Content too large for OpenAI, returning fallback summary");
                return "Content too large for automatic summarization. Manual review required.";
            }
            catch (ClientResultException ex)
            {
                _logger.LogError(ex, "OpenAI API error: Status {Status}, Message: {Message}", ex.Status, ex.Message);
                return $"Error generating summary: API error (Status: {ex.Status})";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating GPT summary");
                return "Error generating summary: " + ex.Message;
            }
        }

        public async Task<Dictionary<string, string>> GenerateBatchSummariesAsync(IEnumerable<PublicOrder> orders)
        {
            var results = new Dictionary<string, string>();
            
            if (!orders.Any())
            {
                _logger.LogInformation("No orders provided for batch summary generation");
                return results;
            }

            _logger.LogInformation($"Starting batch summary generation for {orders.Count()} orders");

            try
            {
                // Instead of batch processing, process orders individually to avoid API issues
                _logger.LogInformation("Processing orders individually due to API limitations");
                
                var ordersList = orders.ToList();
                for (int i = 0; i < ordersList.Count; i++)
                {
                    var order = ordersList[i];
                    _logger.LogDebug($"Processing individual order {i + 1}/{ordersList.Count}: {order.Subject}");
                    
                    try
                    {
                        if (!string.IsNullOrEmpty(order.RawData))
                        {
                            var summary = await GenerateSummaryAsync(order.RawData);
                            results[order.OriginalUrl] = summary;
                            _logger.LogDebug($"Generated summary for order {i + 1}");
                        }
                        else
                        {
                            results[order.OriginalUrl] = "Brak danych do analizy.";
                        }
                        
                        // Add small delay to avoid rate limits
                        await Task.Delay(_batchProcessingDelay);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing individual order {i + 1}");
                        results[order.OriginalUrl] = $"Błąd podczas generowania streszczenia: {ex.Message}";
                    }
                }

                _logger.LogInformation($"Successfully processed individual summaries for {results.Count} orders");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating batch summaries. Exception details: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                
                // Check if it's an HTTP exception with more details
                if (ex is HttpRequestException httpEx)
                {
                    _logger.LogError("HTTP Exception details: {HttpMessage}", httpEx.Message);
                }
                
                // Fallback to individual summaries
                foreach (var order in orders)
                {
                    results[order.OriginalUrl] = $"Błąd podczas generowania streszczenia wsadowego: {ex.Message}";
                }
            }

            return results;
        }
    }
}