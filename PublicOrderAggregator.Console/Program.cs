using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Infrastructure.Data;
using PublicOrderAggregator.Console.Extensions;

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

                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await dbContext.Database.EnsureCreatedAsync();
                    logger.LogInformation("Database ready");

                    var dataSourceService = scope.ServiceProvider.GetRequiredService<IDataSourceService>();
                    logger.LogInformation("Fetching orders from data sources...");
                    
                    var orders = await dataSourceService.FetchAndProcessOrdersAsync();
                    logger.LogInformation($"Processed {orders.Count()} new orders");

                    var repository = scope.ServiceProvider.GetRequiredService<IPublicOrderRepository>();
                    var lastReportDate = await repository.GetLastReportGenerationDateAsync();
                    var ordersForReport = await repository.GetOrdersForReportAsync(lastReportDate);
                    
                    if (!ordersForReport.Any())
                    {
                        logger.LogInformation("No new orders to include in report since last generation");
                        System.Console.WriteLine("\n=== Summary ===");
                        System.Console.WriteLine($"New orders processed: {orders.Count()}");
                        System.Console.WriteLine("No new orders for report generation");
                        return;
                    }
                    
                    logger.LogInformation($"Found {ordersForReport.Count()} orders for new report");

                    // Generate HTML report
                    var reportGenerator = scope.ServiceProvider.GetRequiredService<IReportGenerator>();
                    var htmlReport = await reportGenerator.GenerateHtmlReportAsync(ordersForReport);
                    
                    var reportPath = Path.Combine(Directory.GetCurrentDirectory(), $"report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                    await File.WriteAllTextAsync(reportPath, htmlReport);
                    
                    logger.LogInformation($"Report generated: {reportPath}");
                    
                    // Mark orders as included in report
                    foreach (var order in ordersForReport)
                    {
                        order.IsIncludedInReport = true;
                        order.LastReportedAt = DateTime.Now;
                    }
                    await repository.UpdateBatchAsync(ordersForReport);
                    
                    System.Console.WriteLine("\n=== Summary ===");
                    System.Console.WriteLine($"New orders processed: {orders.Count()}");
                    System.Console.WriteLine($"Orders in report: {ordersForReport.Count()}");
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

            // Application Services
            services.AddApplicationServices(configuration);

            return services.BuildServiceProvider();
        }
    }
}
