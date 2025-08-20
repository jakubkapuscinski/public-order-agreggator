using PublicOrderAggregator.Domain.Entities;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IDataSourceMapper<T>
    {
        PublicOrder MapToPublicOrder(T sourceData);
        bool CanHandle(string sourceType);
    }
}