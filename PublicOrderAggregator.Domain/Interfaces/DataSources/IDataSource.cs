using PublicOrderAggregator.Domain.Models.DTOs;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IDataSource
    {
        string SourceName { get; }
        Task<IEnumerable<RawOrderDataDto>> FetchRawDataAsync();
    }
}