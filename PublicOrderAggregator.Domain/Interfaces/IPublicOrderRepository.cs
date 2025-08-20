using System.Collections.Generic;
using System.Threading.Tasks;
using PublicOrderAggregator.Domain.Entities;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IPublicOrderRepository
    {
        Task<PublicOrder> GetByIdAsync(int id);
        Task<IEnumerable<PublicOrder>> GetAllAsync();
        Task<IEnumerable<PublicOrder>> GetByDataSourceAsync(string dataSource);
        Task<PublicOrder> AddAsync(PublicOrder order);
        Task UpdateAsync(PublicOrder order);
        Task DeleteAsync(int id);
        Task<bool> ExistsAsync(string originalUrl);
    }
}