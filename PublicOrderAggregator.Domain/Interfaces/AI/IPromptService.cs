namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IPromptService
    {
        Task<string> GetClassificationPromptAsync();
        Task<string> GetSummaryPromptAsync();
        Task<string[]> GetKeywordsAsync();
        Task<string[]> GetExclusionsAsync();
    }
}
