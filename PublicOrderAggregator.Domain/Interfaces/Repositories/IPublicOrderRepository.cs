using PublicOrderAggregator.Domain.Entities;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IPublicOrderRepository
    {
        Task<PublicOrder?> GetByIdAsync(int id);
        Task<IEnumerable<PublicOrder>> GetAllAsync();
        Task<IEnumerable<PublicOrder>> GetByDataSourceAsync(string dataSource);
        Task<PublicOrder> AddAsync(PublicOrder order);
        Task UpdateAsync(PublicOrder order);
        Task UpdateBatchAsync(IEnumerable<PublicOrder> orders);
        Task DeleteAsync(int id);
        Task<bool> ExistsAsync(string externalId, string dataSource);
        Task<IEnumerable<PublicOrder>> GetUnclassifiedOrdersAsync();
        Task<IEnumerable<PublicOrder>> GetUnsummarizedOrdersAsync();
        Task<IEnumerable<PublicOrder>> GetOrdersForReportAsync(DateTime? since = null);
        Task<DateTime?> GetLastReportGenerationDateAsync();
    }
}