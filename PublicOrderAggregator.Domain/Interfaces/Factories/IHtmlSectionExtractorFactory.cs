namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IHtmlSectionExtractorFactory
    {
        IHtmlSectionExtractor CreateExtractor(string sourceType);
    }
}
