namespace PublicOrderAggregator.Domain.Models
{
    public class ClassificationResult
    {
        public bool IsRelevant { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public string OrderUrl { get; set; } = string.Empty;
        public string OrderTitle { get; set; } = string.Empty;
    }
}