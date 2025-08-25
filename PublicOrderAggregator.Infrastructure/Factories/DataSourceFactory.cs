using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Infrastructure.DataSources.BZP;

namespace PublicOrderAggregator.Infrastructure.Factories
{
    public class DataSourceFactory : IDataSourceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DataSourceFactory> _logger;

        public DataSourceFactory(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<DataSourceFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        public IDataSource CreateDataSource(string sourceType)
        {
            _logger.LogDebug($"Creating data source for type: {sourceType}");
            
            return sourceType.ToUpperInvariant() switch
            {
                "BZP" => _serviceProvider.GetRequiredService<BzpDataSource>(),
                _ => throw new NotSupportedException($"Data source type '{sourceType}' is not supported")
            };
        }

        public IContentParser GetParser(string sourceType)
        {
            _logger.LogDebug($"Getting parser for source type: {sourceType}");
            
            return sourceType.ToUpperInvariant() switch
            {
                "BZP" => _serviceProvider.GetRequiredService<BzpContentParser>(),
                _ => throw new NotSupportedException($"Parser for source type '{sourceType}' is not supported")
            };
        }

        public IEnumerable<string> GetAvailableSourceTypes()
        {
            var availableSources = new List<string>();
            
            if (_configuration.GetValue<bool>("DataSources:BZP:Enabled", false))
            {
                availableSources.Add("BZP");
                _logger.LogDebug("BZP data source is enabled");
            }
            
            // Miejsce na przyszłe źródła danych
            
            _logger.LogInformation($"Found {availableSources.Count} enabled data sources: {string.Join(", ", availableSources)}");
            return availableSources;
        }
    }
}
