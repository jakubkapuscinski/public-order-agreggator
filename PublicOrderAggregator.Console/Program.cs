using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PublicOrderAggregator.Application.Services;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Infrastructure.Data;
using PublicOrderAggregator.Infrastructure.ExternalServices;
using PublicOrderAggregator.Infrastructure.Repositories;

namespace PublicOrderAggregator.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var serviceProvider = ConfigureServices(configuration);

            using (var scope = serviceProvider.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                
                try
                {
                    logger.LogInformation("=== Public Order Aggregator Started ===");
                    logger.LogInformation($"Start time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                    // Ensure database is created
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await dbContext.Database.EnsureCreatedAsync();
                    logger.LogInformation("Database ready");

                    // Fetch and process orders
                    var dataSourceService = scope.ServiceProvider.GetRequiredService<IDataSourceService>();
                    logger.LogInformation("Fetching orders from data sources...");
                    
                    var orders = await dataSourceService.FetchAndProcessOrdersAsync();
                    logger.LogInformation($"Processed {orders.Count()} new orders");

                    // Get all orders from repository for report
                    var repository = scope.ServiceProvider.GetRequiredService<IPublicOrderRepository>();
                    var allOrders = await repository.GetAllAsync();
                    logger.LogInformation($"Total orders in database: {allOrders.Count()}");

                    // Generate HTML report
                    var reportGenerator = scope.ServiceProvider.GetRequiredService<IReportGenerator>();
                    var htmlReport = await reportGenerator.GenerateHtmlReportAsync(allOrders);
                    
                    var reportPath = Path.Combine(Directory.GetCurrentDirectory(), $"report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                    await File.WriteAllTextAsync(reportPath, htmlReport);
                    
                    logger.LogInformation($"Report generated: {reportPath}");
                    
                    System.Console.WriteLine("\n=== Summary ===");
                    System.Console.WriteLine($"New orders processed: {orders.Count()}");
                    System.Console.WriteLine($"Total orders in database: {allOrders.Count()}");
                    System.Console.WriteLine($"Report saved to: {reportPath}");
                    
                    // Only try to read key if console is available and interactive
                    try
                    {
                        if (Environment.UserInteractive)
                        {
                            System.Console.WriteLine("\nPress any key to open the report in browser...");
                            System.Console.ReadKey();
                            
                            // Open report in default browser
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = reportPath,
                                UseShellExecute = true
                            });
                        }
                        else
                        {
                            System.Console.WriteLine($"\nReport ready at: {reportPath}");
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        System.Console.WriteLine($"\nReport ready at: {reportPath}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred during execution");
                    System.Console.WriteLine($"\nError: {ex.Message}");
                    System.Console.WriteLine("Check logs for details.");
                    System.Console.ReadKey();
                }
                finally
                {
                    logger.LogInformation("=== Public Order Aggregator Finished ===");
                }
            }
        }

        private static ServiceProvider ConfigureServices(IConfiguration configuration)
        {
            var services = new ServiceCollection();

            // Configuration
            services.AddSingleton<IConfiguration>(configuration);

            // Logging
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddConsole();
            });

            // Database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

            // Repositories
            services.AddScoped<IPublicOrderRepository, PublicOrderRepository>();

            // Services
            services.AddScoped<IHtmlSanitizer, HtmlSanitizer>();
            services.AddScoped<IGPTSummaryService, GptSummaryService>();
            services.AddScoped<IGPTClassificationService, GPTClassificationService>();
            services.AddScoped<IReportGenerator, HtmlReportGenerator>();
            
            // HTTP Client for BZP API
            services.AddHttpClient<IDataSourceService, BzpDataSourceService>(client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "PublicOrderAggregator/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            return services.BuildServiceProvider();
        }
    }
}
