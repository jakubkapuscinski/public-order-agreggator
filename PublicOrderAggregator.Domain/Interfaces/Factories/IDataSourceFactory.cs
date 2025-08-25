using System.Collections.Generic;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IDataSourceFactory
    {
        IDataSource CreateDataSource(string sourceType);
        IContentParser GetParser(string sourceType);
        IEnumerable<string> GetAvailableSourceTypes();
    }
}