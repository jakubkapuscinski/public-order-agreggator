using System.Collections.Generic;
using System.Threading.Tasks;
using PublicOrderAggregator.Domain.Entities;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IGPTSummaryService
    {
        Task<string> GenerateSummaryAsync(string htmlContent);
        Task<Dictionary<string, string>> GenerateBatchSummariesAsync(IEnumerable<PublicOrder> orders);
    }
}