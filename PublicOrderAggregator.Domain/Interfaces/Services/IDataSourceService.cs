using PublicOrderAggregator.Domain.Entities;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IDataSourceService
    {
        Task<IEnumerable<PublicOrder>> FetchAndProcessOrdersAsync();
    }
}