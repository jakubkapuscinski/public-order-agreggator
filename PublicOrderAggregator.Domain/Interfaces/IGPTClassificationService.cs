using PublicOrderAggregator.Domain.Entities;
using PublicOrderAggregator.Domain.Models;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IGPTClassificationService
    {
        Task<ClassificationResult> ClassifyOrderAsync(string orderContent, string subject, string orderUrl);
        Task<List<ClassificationResult>> ClassifyBatchAsync(IEnumerable<PublicOrder> orders);
    }
}