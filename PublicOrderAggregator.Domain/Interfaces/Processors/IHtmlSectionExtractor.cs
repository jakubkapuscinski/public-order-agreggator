namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IHtmlSectionExtractor
    {
        string ExtractSectionForClassification(string htmlContent);
        string ExtractSectionsForRequirements(string htmlContent);
    }
}