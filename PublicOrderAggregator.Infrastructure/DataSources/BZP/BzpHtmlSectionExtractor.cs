using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PublicOrderAggregator.Domain.Interfaces;

namespace PublicOrderAggregator.Infrastructure.DataSources.BZP
{
    public class BzpHtmlSectionExtractor : IHtmlSectionExtractor
    {
        private readonly ILogger<BzpHtmlSectionExtractor> _logger;

        public BzpHtmlSectionExtractor(ILogger<BzpHtmlSectionExtractor> logger)
        {
            _logger = logger;
        }

        public string ExtractSectionForClassification(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                return string.Empty;
            }

            try
            {
                // Extract Section IV for classification
                var sectionIV = ExtractSection(htmlContent, "IV", "V");
                
                if (string.IsNullOrWhiteSpace(sectionIV))
                {
                    _logger.LogDebug("BZP Section IV not found in HTML content");
                    return string.Empty;
                }

                // Sanitize HTML tags
                var sanitized = SanitizeHtml(sectionIV);
                
                _logger.LogDebug($"Extracted BZP Section IV, original length: {sectionIV.Length}, sanitized length: {sanitized.Length}");
                
                return sanitized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting BZP section for classification");
                return string.Empty;
            }
        }

        public string ExtractSectionsForRequirements(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                return string.Empty;
            }

            try
            {
                // Extract Section V
                var sectionV = ExtractSection(htmlContent, "V", "VI");
                
                // Extract Section VI
                var sectionVI = ExtractSection(htmlContent, "VI", "VII");
                
                // Combine both sections
                var combined = $"{sectionV}\n\n{sectionVI}";
                
                if (string.IsNullOrWhiteSpace(combined.Trim()))
                {
                    _logger.LogDebug("BZP Sections V and VI not found in HTML content");
                    return string.Empty;
                }

                // Sanitize HTML tags
                var sanitized = SanitizeHtml(combined);
                
                _logger.LogDebug($"Extracted BZP Sections V and VI, original length: {combined.Length}, sanitized length: {sanitized.Length}");
                
                return sanitized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting BZP sections for requirements");
                return string.Empty;
            }
        }

        private string ExtractSection(string htmlContent, string sectionNumber, string nextSectionNumber)
        {
            try
            {
                // Pattern to find BZP section headers like "Sekcja IV:" or "SEKCJA IV." or "IV." etc.
                var sectionStartPattern = $@"(?i)(Sekcja\s+{sectionNumber}[:\.\s]|{sectionNumber}\.\s)";
                var sectionEndPattern = $@"(?i)(Sekcja\s+{nextSectionNumber}[:\.\s]|{nextSectionNumber}\.\s)";
                
                var startMatch = Regex.Match(htmlContent, sectionStartPattern);
                if (!startMatch.Success)
                {
                    // Try alternative patterns for BZP HTML structure
                    sectionStartPattern = $@"(?i)<h[1-6][^>]*>.*?Sekcja\s+{sectionNumber}.*?</h[1-6]>";
                    startMatch = Regex.Match(htmlContent, sectionStartPattern);
                    
                    if (!startMatch.Success)
                    {
                        _logger.LogDebug($"BZP Section {sectionNumber} not found with any pattern");
                        return string.Empty;
                    }
                }
                
                var startIndex = startMatch.Index;
                
                // Find the end of the section
                var endMatch = Regex.Match(htmlContent.Substring(startIndex), sectionEndPattern);
                var endIndex = endMatch.Success ? startIndex + endMatch.Index : htmlContent.Length;
                
                // Extract the section content
                var sectionContent = htmlContent.Substring(startIndex, endIndex - startIndex);
                
                return sectionContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting BZP section {sectionNumber}");
                return string.Empty;
            }
        }

        private string SanitizeHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            try
            {
                // Remove script and style elements
                html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                // Replace br tags with newlines
                html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
                
                // Replace p and div tags with newlines
                html = Regex.Replace(html, @"</?(p|div)[^>]*>", "\n", RegexOptions.IgnoreCase);
                
                // Replace list items with bullet points
                html = Regex.Replace(html, @"<li[^>]*>", "• ", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"</li>", "\n", RegexOptions.IgnoreCase);
                
                // Remove all remaining HTML tags
                html = Regex.Replace(html, @"<[^>]+>", "", RegexOptions.IgnoreCase);
                
                // Decode HTML entities
                html = System.Net.WebUtility.HtmlDecode(html);
                
                // Clean up excessive whitespace
                html = Regex.Replace(html, @"[ \t]+", " ");
                html = Regex.Replace(html, @"\n{3,}", "\n\n");
                html = html.Trim();
                
                return html;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sanitizing HTML");
                return html; // Return original if sanitization fails
            }
        }
    }
}
