using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Infrastructure.DataSources.BZP;

namespace PublicOrderAggregator.Infrastructure.Factories
{
    public class HtmlSectionExtractorFactory : IHtmlSectionExtractorFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HtmlSectionExtractorFactory> _logger;

        public HtmlSectionExtractorFactory(IServiceProvider serviceProvider, ILogger<HtmlSectionExtractorFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public IHtmlSectionExtractor CreateExtractor(string sourceType)
        {
            return sourceType?.ToUpperInvariant() switch
            {
                "BZP" => _serviceProvider.GetRequiredService<BzpHtmlSectionExtractor>(),
                _ => throw new NotSupportedException($"HTML section extractor for source type '{sourceType}' is not supported. Currently supported: BZP")
            };
        }
    }
}
