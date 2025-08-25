using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PublicOrderAggregator.Domain.Interfaces;

namespace PublicOrderAggregator.Infrastructure.AI
{
    public class PromptService : IPromptService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PromptService> _logger;
        private readonly string _promptsPath;

        public PromptService(IConfiguration configuration, ILogger<PromptService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            var basePath = _configuration.GetValue<string>("Prompts:BasePath") ?? "./Prompts";
            
            if (!Path.IsPathRooted(basePath))
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                var solutionRoot = Path.GetDirectoryName(currentDirectory) ?? currentDirectory;
                _promptsPath = Path.Combine(solutionRoot, basePath);
            }
            else
            {
                _promptsPath = basePath;
            }
            
            _logger.LogInformation("Prompts path resolved to: {PromptsPath}", _promptsPath);
        }

        public async Task<string> GetClassificationPromptAsync()
        {
            try
            {
                var templatePath = Path.Combine(_promptsPath, "classification.md");
                if (!File.Exists(templatePath))
                {
                    _logger.LogError("Classification.md file not found at path: {Path}", templatePath);
                    throw new FileNotFoundException($"Classification.md file not found at path: {templatePath}. Application stopped.");
                }

                var template = await File.ReadAllTextAsync(templatePath);
                _logger.LogDebug("Classification prompt template loaded from {Path}", templatePath);

                // Replace placeholders
                var keywords = await GetKeywordsAsync();
                var exclusions = await GetExclusionsAsync();

                var prompt = template
                    .Replace("{KEYWORDS}", string.Join(",", keywords))
                    .Replace("{EXCLUSIONS}", string.Join(",", exclusions));

                return prompt;
            }
            catch (Exception ex) when (!(ex is FileNotFoundException))
            {
                _logger.LogError(ex, "Error loading classification prompt template");
                throw new InvalidOperationException("Cannot load classification template. Application stopped.", ex);
            }
        }

        public async Task<string> GetSummaryPromptAsync()
        {
            try
            {
                var summaryPath = Path.Combine(_promptsPath, "summary.md");
                if (File.Exists(summaryPath))
                {
                    return await File.ReadAllTextAsync(summaryPath);
                }
                
                throw new FileNotFoundException($"Summary.md file not found at path: {summaryPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading summary prompt");
                throw new InvalidOperationException("Cannot load summary template. Application stopped.", ex);
            }
        }

        public async Task<string[]> GetKeywordsAsync()
        {
            try
            {
                var keywordsPath = Path.Combine(_promptsPath, "keywords.md");
                if (!File.Exists(keywordsPath))
                {
                    _logger.LogError("Keywords.md file not found at path: {Path}", keywordsPath);
                    throw new FileNotFoundException($"Keywords.md file not found at path: {keywordsPath}. Application stopped.");
                }

                var content = await File.ReadAllTextAsync(keywordsPath);
                var keywords = ExtractFromSection(content, [ 
                    "INSTALACJE", "AUTOMATYKA", "OBIEKTY", "PRACE", "DOKUMENTY", "CPV" 
                ]);

                if (keywords.Length == 0)
                {
                    _logger.LogError("No keywords found in keywords.md file");
                    throw new InvalidOperationException("No keywords found in keywords.md file. Application stopped.");
                }

                _logger.LogDebug("Loaded {Count} keywords", keywords.Length);
                return keywords;
            }
            catch (Exception ex) when (!(ex is FileNotFoundException || ex is InvalidOperationException))
            {
                _logger.LogError(ex, "Error loading keywords");
                throw new InvalidOperationException("Cannot load keywords. Application stopped.", ex);
            }
        }

        public async Task<string[]> GetExclusionsAsync()
        {
            try
            {
                var keywordsPath = Path.Combine(_promptsPath, "keywords.md");
                if (!File.Exists(keywordsPath))
                {
                    _logger.LogError("Keywords.md file not found at path: {Path}", keywordsPath);
                    throw new FileNotFoundException($"Keywords.md file not found at path: {keywordsPath}. Application stopped.");
                }

                var content = await File.ReadAllTextAsync(keywordsPath);
                var exclusions = ExtractFromSection(content, new[] { "WYKLUCZENIA" });

                if (exclusions.Length == 0)
                {
                    _logger.LogWarning("No exclusions found in keywords.md file");
                }

                _logger.LogDebug("Loaded {Count} exclusions", exclusions.Length);
                return exclusions;
            }
            catch (Exception ex) when (!(ex is FileNotFoundException))
            {
                _logger.LogError(ex, "Error loading exclusions");
                throw new InvalidOperationException("Cannot load exclusions. Application stopped.", ex);
            }
        }

        private string[] ExtractFromSection(string content, string[] sectionNames)
        {
            var results = new List<string>();
            var lines = content.Split('\n');

            foreach (var sectionName in sectionNames)
            {
                bool inSection = false;

                foreach (var line in lines)
                {
                    if (line.Contains(sectionName))
                    {
                        inSection = true;
                        continue;
                    }

                    if (inSection && line.StartsWith("##") && !line.Contains(sectionName))
                    {
                        break; // End of section
                    }

                    if (inSection && !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    {
                        // Comma-separated keywords on single lines
                        var keywords = line
                            .Split(',')
                            .Select(k => k.Trim())
                            .Where(k => !string.IsNullOrEmpty(k))
                            .ToArray();
                        
                        results.AddRange(keywords);
                    }
                }
            }

            return results.Distinct().ToArray();
        }
    }
}
