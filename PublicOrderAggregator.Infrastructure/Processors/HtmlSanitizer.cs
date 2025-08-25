namespace PublicOrderAggregator.Infrastructure.Processors
{
    public class HtmlSanitizer : Domain.Interfaces.IHtmlSanitizer
    {
        private readonly Ganss.Xss.HtmlSanitizer _sanitizer;

        public HtmlSanitizer()
        {
            _sanitizer = new Ganss.Xss.HtmlSanitizer();
        }

        public string SanitizeHtml(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
                return string.Empty;

            return _sanitizer.Sanitize(htmlContent);
        }

        public string ExtractPlainText(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
                return string.Empty;

            // First sanitize the HTML
            var sanitizedHtml = SanitizeHtml(htmlContent);
            
            // Then extract plain text using AngleSharp (included with HtmlSanitizer)
            var parser = new AngleSharp.Html.Parser.HtmlParser();
            var document = parser.ParseDocument(sanitizedHtml);
            
            // Extract text content
            var text = document.Body?.TextContent ?? string.Empty;
            
            // Clean up whitespace
            var lines = text.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join("\n", lines);
        }
    }
}