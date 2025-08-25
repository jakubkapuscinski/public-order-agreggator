namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IOpenAIRateLimiter
    {
        Task WaitAsync(CancellationToken cancellationToken = default);
    }
}
