namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IHtmlSanitizer
    {
        string SanitizeHtml(string htmlContent);
        string ExtractPlainText(string htmlContent);
    }
}