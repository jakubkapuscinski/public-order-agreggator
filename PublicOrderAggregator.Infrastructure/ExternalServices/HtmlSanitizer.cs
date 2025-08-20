using System.Text.RegularExpressions;
using HtmlAgilityPack;
using PublicOrderAggregator.Domain.Interfaces;

namespace PublicOrderAggregator.Infrastructure.ExternalServices
{
    public class HtmlSanitizer : IHtmlSanitizer
    {
        public string SanitizeHtml(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
                return string.Empty;

            var plainText = ExtractPlainText(htmlContent);
            
            plainText = Regex.Replace(plainText, @"\s+", " ");
            plainText = Regex.Replace(plainText, @"\r\n|\r|\n", "\n");
            plainText = Regex.Replace(plainText, @"\n{3,}", "\n\n");
            
            return plainText.Trim();
        }

        public string ExtractPlainText(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
                return string.Empty;

            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var scripts = doc.DocumentNode.SelectNodes("//script");
            if (scripts != null)
            {
                foreach (var script in scripts)
                {
                    script.Remove();
                }
            }

            var styles = doc.DocumentNode.SelectNodes("//style");
            if (styles != null)
            {
                foreach (var style in styles)
                {
                    style.Remove();
                }
            }

            var text = doc.DocumentNode.InnerText;
            
            text = System.Web.HttpUtility.HtmlDecode(text);

            var lines = text.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join("\n", lines);
        }
    }
}