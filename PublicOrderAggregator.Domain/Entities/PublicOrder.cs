using System;

namespace PublicOrderAggregator.Domain.Entities
{
    public class PublicOrder
    {
        public int Id { get; set; }
        public string Organizer { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime TenderDate { get; set; }
        public DateTime SubmissionDeadline { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty; // GPT summary
        public string DataSource { get; set; } = string.Empty;
        public string OriginalUrl { get; set; } = string.Empty;
        public DateTime FetchedAt { get; set; }
        public string RawData { get; set; } = string.Empty; // JSON with original data
    }
}