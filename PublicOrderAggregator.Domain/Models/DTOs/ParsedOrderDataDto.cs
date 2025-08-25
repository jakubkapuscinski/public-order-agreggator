namespace PublicOrderAggregator.Domain.Models.DTOs
{
    public class ParsedOrderDataDto
    {
        public string Organizer { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime? TenderDate { get; set; }
        public DateTime? SubmissionDeadline { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string ContentForClassification { get; set; } = string.Empty;
        public string ContentForRequirements { get; set; } = string.Empty;
        public string FullContent { get; set; } = string.Empty;
    }
}
