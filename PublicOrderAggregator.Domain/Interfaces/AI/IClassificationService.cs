using PublicOrderAggregator.Domain.Entities;
using PublicOrderAggregator.Domain.Models.DTOs;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IClassificationService
    {
        Task<ClassificationResultDto> ClassifyOrderAsync(string orderContent, string subject, string orderUrl, string? sourceType = null);
        Task<List<ClassificationResultDto>> ClassifyBatchAsync(IEnumerable<PublicOrder> orders, string? sourceType = null);
    }
}