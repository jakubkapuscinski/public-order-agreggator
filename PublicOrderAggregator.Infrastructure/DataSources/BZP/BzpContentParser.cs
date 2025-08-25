using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Domain.Models.DTOs;

namespace PublicOrderAggregator.Infrastructure.DataSources.BZP
{
    public class BzpContentParser : IContentParser
    {
        private readonly ILogger<BzpContentParser> _logger;
        private readonly IHtmlSanitizer _htmlSanitizer;

        public BzpContentParser(
            ILogger<BzpContentParser> logger,
            IHtmlSanitizer htmlSanitizer)
        {
            _logger = logger;
            _htmlSanitizer = htmlSanitizer;
        }

        public bool CanParse(string sourceType)
        {
            return sourceType.Equals("BZP", StringComparison.OrdinalIgnoreCase);
        }

        public Task<ParsedOrderDataDto> ParseAsync(RawOrderDataDto rawData)
        {
            var parsed = new ParsedOrderDataDto();
            
            // Extract metadata
            if (rawData.Metadata.TryGetValue("Organizer", out var organizer))
                parsed.Organizer = organizer?.ToString() ?? string.Empty;
            
            if (rawData.Metadata.TryGetValue("Location", out var location))
                parsed.Location = location?.ToString() ?? string.Empty;
            
            if (rawData.Metadata.TryGetValue("Subject", out var subject))
                parsed.Subject = subject?.ToString() ?? string.Empty;
            
            if (rawData.Metadata.TryGetValue("TenderDate", out var tenderDate) && tenderDate is DateTime td)
                parsed.TenderDate = td;
            
            if (rawData.Metadata.TryGetValue("SubmissionDeadline", out var deadline) && deadline is DateTime dl)
                parsed.SubmissionDeadline = dl;
            
            if (!string.IsNullOrWhiteSpace(rawData.Content))
            {
                // Extract Section IV for classification (przedmiot zamówienia)
                var sectionIV = ExtractSection(rawData.Content, "IV", "V");
                if (!string.IsNullOrWhiteSpace(sectionIV))
                {
                    parsed.ContentForClassification = _htmlSanitizer.ExtractPlainText(sectionIV);
                }
                
                // Extract Sections V and VI for requirements
                var sectionV = ExtractSection(rawData.Content, "V", "VI");
                var sectionVI = ExtractSection(rawData.Content, "VI", "VII");
                
                if (!string.IsNullOrWhiteSpace(sectionV) || !string.IsNullOrWhiteSpace(sectionVI))
                {
                    var combinedSections = $"{sectionV}\n\n{sectionVI}";
                    parsed.ContentForRequirements = _htmlSanitizer.ExtractPlainText(combinedSections);
                }
                
                parsed.FullContent = _htmlSanitizer.ExtractPlainText(rawData.Content);
            }
            
            return Task.FromResult(parsed);
        }

        private string ExtractSection(string htmlContent, string sectionNumber, string nextSectionNumber)
        {
            try
            {
                var sectionStartPattern = $@"(?i)(Sekcja\s+{sectionNumber}[:\.\s]|SECTION\s+{sectionNumber}[:\.\s]|{sectionNumber}\.\s)";
                var sectionEndPattern = $@"(?i)(Sekcja\s+{nextSectionNumber}[:\.\s]|SECTION\s+{nextSectionNumber}[:\.\s]|{nextSectionNumber}\.\s)";
                
                var startMatch = Regex.Match(htmlContent, sectionStartPattern);
                if (!startMatch.Success)
                {
                    // Try alternative patterns for BZP HTML structure
                    sectionStartPattern = $@"(?i)<h[1-6][^>]*>.*?(Sekcja|SECTION)\s+{sectionNumber}.*?</h[1-6]>";
                    startMatch = Regex.Match(htmlContent, sectionStartPattern);
                    
                    if (!startMatch.Success)
                    {
                        _logger.LogDebug($"BZP Section {sectionNumber} not found");
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
    }
}