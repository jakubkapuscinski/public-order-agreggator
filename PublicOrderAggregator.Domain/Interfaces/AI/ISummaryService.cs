using PublicOrderAggregator.Domain.Entities;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface ISummaryService
    {
        Task<string> GenerateSummaryAsync(string htmlContent, string? sourceType = null);
        Task<Dictionary<string, string>> GenerateBatchSummariesAsync(IEnumerable<PublicOrder> orders);
    }
}
