namespace PublicOrderAggregator.Domain.Models.DTOs
{
    public class RawOrderDataDto
    {
        public string UniqueId { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime FetchedAt { get; set; } = DateTime.Now;
    }
}
