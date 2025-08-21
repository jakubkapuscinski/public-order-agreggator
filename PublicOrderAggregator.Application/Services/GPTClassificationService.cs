using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using PublicOrderAggregator.Domain.Entities;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Domain.Models;

namespace PublicOrderAggregator.Application.Services
{
    public class GPTClassificationService : IGPTClassificationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GPTClassificationService> _logger;
        private readonly OpenAIClient _openAiClient;
        private readonly ChatClient _chatClient;
        private readonly int _maxTokens;
        private readonly float _temperature;
        private readonly int _maxContentLength;
        private readonly int _batchProcessingDelay;

        public GPTClassificationService(IConfiguration configuration, ILogger<GPTClassificationService> logger)
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
            
            _maxTokens = _configuration.GetValue<int>("OpenAI:Classification:MaxTokens", 50);
            _temperature = _configuration.GetValue<float>("OpenAI:Classification:Temperature", 0.1f);
            _maxContentLength = _configuration.GetValue<int>("OpenAI:Classification:MaxContentLength", 8000);
            _batchProcessingDelay = _configuration.GetValue<int>("OpenAI:BatchProcessingDelay", 300);
            
            _openAiClient = new OpenAIClient(apiKey);
            _chatClient = _openAiClient.GetChatClient(model);
            
            _logger.LogInformation("GPTClassificationService initialized with model: {Model}, MaxTokens: {MaxTokens}, Temperature: {Temperature}", 
                model, _maxTokens, _temperature);
        }

        public async Task<ClassificationResult> ClassifyOrderAsync(string orderContent, string subject, string orderUrl)
        {
            var result = new ClassificationResult 
            { 
                OrderUrl = orderUrl, 
                OrderTitle = subject, 
                IsRelevant = false, 
                Reasoning = "Błąd klasyfikacji" 
            };

            if (string.IsNullOrWhiteSpace(orderContent))
            {
                result.Reasoning = "Brak treści do analizy";
                return result;
            }

            try
            {
                var truncatedContent = orderContent.Length > _maxContentLength 
                    ? orderContent.Substring(0, _maxContentLength) + "... [treść skrócona]"
                    : orderContent;

                var classificationPrompt = @"ZADANIE: Zdecyduj czy ten przetarg publiczny dotyczy specjalizacji SG TECHNOLOGIES i podaj zwięzłe uzasadnienie.

FORMAT ODPOWIEDZI:
DECYZJA: TAK/NIE
UZASADNIENIE: [1-2 zdania wyjaśniające dlaczego]

SPECJALIZACJE SG TECHNOLOGIES:
1. INSTALACJE ELEKTRYCZNE: rozdzielnice (SN/nN), transformatory, oświetlenie, zasilanie, UPS, agregaty, WLZ, instalacje odgromowe, uziemienia
2. INSTALACJE TELETECHNICZNE: SSP, SKD, CCTV, SSWiN, DSO, RCP, LAN, sieci teleinformatyczne, światłowody, domofony
3. INSTALACJE HVAC/SANITARNE: klimatyzacja, wentylacja, centrale wentylacyjne, chłodzenie, pompy ciepła, kotłownie, c.o., c.t., wodno-kanalizacyjne, gazowe, sprężone powietrze, systemy tryskaczowe
4. AUTOMATYKA: BMS, PMS, automatyka budynkowa/przemysłowa/HVAC, PLC, SCADA, HMI, integracja systemów, protokoły (Profibus, Modbus, BACnet, DALI)
5. OBIEKTY PRIORYTETOWE: data center, serwerownie, szpitale, oczyszczalnie ścieków, SUW, przepompownie
6. ZAKRES PRAC: generalne wykonawstwo, pod klucz, Design & Build, montaż, uruchomienie, modernizacje, przebudowy, serwis, konserwacja

WYKLUCZENIA:
- wyłącznie dostawa bez montażu
- tylko projekt bez wykonawstwa  
- drogi/mosty bez budynków
- malowanie/elewacje
- meble/wyposażenie
- sprzątanie/catering/transport

ZASADA: Jeśli przetarg zawiera 2+ elementy z różnych kategorii specjalizacji = TAK

TYTUŁ: " + subject + @"

TREŚĆ PRZETARGU:
" + truncatedContent + @"

ODPOWIEDŹ:";

                var messages = new List<ChatMessage>
                {
                    new UserChatMessage(classificationPrompt)
                };

                var chatCompletionOptions = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 150, // Increased for reasoning
                    Temperature = _temperature
                };

                var response = await _chatClient.CompleteChatAsync(messages, chatCompletionOptions);
                var responseText = response.Value.Content[0].Text.Trim();
                
                // Parse response
                var lines = responseText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var decision = "NIE";
                var reasoning = "Nie udało się przeanalizować odpowiedzi AI";

                foreach (var line in lines)
                {
                    if (line.StartsWith("DECYZJA:", StringComparison.OrdinalIgnoreCase))
                    {
                        decision = line.Replace("DECYZJA:", "").Trim().ToUpper();
                    }
                    else if (line.StartsWith("UZASADNIENIE:", StringComparison.OrdinalIgnoreCase))
                    {
                        reasoning = line.Replace("UZASADNIENIE:", "").Trim();
                    }
                }

                // Fallback parsing if format is not followed
                if (reasoning == "Nie udało się przeanalizować odpowiedzi AI")
                {
                    reasoning = responseText.Length > 200 ? responseText.Substring(0, 200) + "..." : responseText;
                    decision = responseText.ToUpper().Contains("TAK") ? "TAK" : "NIE";
                }

                result.IsRelevant = decision.Contains("TAK");
                result.Reasoning = reasoning;
                
                _logger.LogDebug("Classification for '{Subject}': {Decision} - {Reasoning}", subject, decision, reasoning);
                
                return result;
            }
            catch (ClientResultException ex) when (ex.Status == 429)
            {
                _logger.LogWarning(ex, "OpenAI rate limit exceeded for classification");
                result.Reasoning = "Przekroczono limit wywołań API OpenAI";
                return result;
            }
            catch (ClientResultException ex)
            {
                _logger.LogError(ex, "OpenAI API error during classification: Status {Status}", ex.Status);
                result.Reasoning = $"Błąd API OpenAI (Status: {ex.Status})";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during order classification for subject: {Subject}", subject);
                result.Reasoning = $"Błąd klasyfikacji: {ex.Message}";
                return result;
            }
        }

        public async Task<List<ClassificationResult>> ClassifyBatchAsync(IEnumerable<PublicOrder> orders)
        {
            var classificationResults = new List<ClassificationResult>();
            
            if (!orders.Any())
            {
                _logger.LogInformation("No orders provided for batch classification");
                return classificationResults;
            }

            _logger.LogInformation($"Starting batch classification for {orders.Count()} orders");

            var ordersList = orders.ToList();
            for (int i = 0; i < ordersList.Count; i++)
            {
                var order = ordersList[i];
                _logger.LogDebug($"Classifying order {i + 1}/{ordersList.Count}: {order.Subject}");
                
                try
                {
                    if (!string.IsNullOrEmpty(order.RawData))
                    {
                        var classificationResult = await ClassifyOrderAsync(order.RawData, order.Subject ?? "", order.OriginalUrl);
                        classificationResults.Add(classificationResult);
                        
                        if (classificationResult.IsRelevant)
                        {
                            _logger.LogInformation($"Order classified as relevant: {order.Subject} - {classificationResult.Reasoning}");
                        }
                        else
                        {
                            _logger.LogDebug($"Order classified as irrelevant: {order.Subject} - {classificationResult.Reasoning}");
                        }
                    }
                    
                    await Task.Delay(_batchProcessingDelay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error classifying order {i + 1}: {order.Subject}");
                }
            }

            var relevantCount = classificationResults.Count(r => r.IsRelevant);
            _logger.LogInformation($"Classification completed: {relevantCount}/{ordersList.Count} orders marked as relevant");
            return classificationResults;
        }
    }
}