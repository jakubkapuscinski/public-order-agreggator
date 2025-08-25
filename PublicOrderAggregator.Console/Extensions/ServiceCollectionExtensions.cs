using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Infrastructure.Data;
using PublicOrderAggregator.Infrastructure.Repositories;
using PublicOrderAggregator.Infrastructure.AI;
using PublicOrderAggregator.Infrastructure.Processors;
using PublicOrderAggregator.Infrastructure.Factories;
using PublicOrderAggregator.Infrastructure.DataSources;
using PublicOrderAggregator.Infrastructure.DataSources.BZP;
using PublicOrderAggregator.Infrastructure.Services;

namespace PublicOrderAggregator.Console.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IPublicOrderRepository, PublicOrderRepository>();

            services.AddAIServices(configuration);

            services.AddProcessingServices();

            services.AddDataSourceServices();

            return services;
        }

        private static IServiceCollection AddAIServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IPromptService, PromptService>();
            services.AddSingleton<IOpenAIRateLimiter, OpenAIRateLimiter>();
            services.AddScoped<ISummaryService, SummaryService>();
            services.AddScoped<IClassificationService, ClassificationService>();

            return services;
        }

        private static IServiceCollection AddProcessingServices(this IServiceCollection services)
        {
            services.AddScoped<IHtmlSanitizer, HtmlSanitizer>();
            services.AddScoped<IReportGenerator, HtmlReportGenerator>();

            services.AddScoped<IHtmlSectionExtractorFactory, HtmlSectionExtractorFactory>();
            services.AddScoped<BzpHtmlSectionExtractor>();

            return services;
        }

        private static IServiceCollection AddDataSourceServices(this IServiceCollection services)
        {
            services.AddHttpClient<BzpDataSource>();
            
            services.AddScoped<IDataSourceFactory, DataSourceFactory>();
            services.AddScoped<BzpContentParser>();

            services.AddScoped<IDataSourceService, MultiSourceOrderProcessingService>();

            return services;
        }
    }
}
