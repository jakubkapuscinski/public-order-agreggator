namespace PublicOrderAggregator.Domain.Models.DTOs
{
    public class ClassificationResultDto
    {
        public bool IsRelevant { get; set; }
        public string OrderUrl { get; set; } = string.Empty;
        public string OrderTitle { get; set; } = string.Empty;
    }
}