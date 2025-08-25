using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PublicOrderAggregator.Domain.Interfaces;

namespace PublicOrderAggregator.Infrastructure.AI
{
    public class OpenAIRateLimiter : IOpenAIRateLimiter
    {
        private static readonly object _lock = new object();
        private static DateTime _lastRequestTime = DateTime.MinValue;
        
        private readonly int _delayMs;
        private readonly ILogger<OpenAIRateLimiter> _logger;

        public OpenAIRateLimiter(IConfiguration configuration, ILogger<OpenAIRateLimiter> logger)
        {
            _delayMs = configuration.GetValue("OpenAI:SimpleDelay", 1000);
            _logger = logger;
            
            _logger.LogInformation("OpenAIRateLimiter: delay={Delay}ms między requestami", _delayMs);
        }

        public async Task WaitAsync(CancellationToken cancellationToken = default)
        {
            int waitTime = 0;
            
            lock (_lock)
            {
                var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
                var requiredDelay = TimeSpan.FromMilliseconds(_delayMs);
                
                if (timeSinceLastRequest < requiredDelay)
                {
                    waitTime = (int)(requiredDelay - timeSinceLastRequest).TotalMilliseconds;
                }
                
                _lastRequestTime = DateTime.UtcNow.AddMilliseconds(Math.Max(0, waitTime));
            }
            
            if (waitTime > 0)
            {
                _logger.LogDebug("Czekam {WaitTime}ms przed następnym requestem", waitTime);
                await Task.Delay(waitTime, cancellationToken);
            }
        }
    }
}
