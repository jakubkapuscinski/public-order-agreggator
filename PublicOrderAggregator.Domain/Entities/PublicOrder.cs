using System;

namespace PublicOrderAggregator.Domain.Entities
{
    public class PublicOrder
    {
        public int Id { get; set; }
        public string ExternalId { get; set; } = string.Empty; // Original ID from external system (e.g., BZP ObjectId)
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
        public string? HtmlContent { get; set; } // Original HTML for section extraction
        
        public bool IsClassified { get; set; } = false;
        public DateTime? ClassifiedAt { get; set; }
        public bool IsRelevant { get; set; } = false;
        public bool IsSummarized { get; set; } = false;
        public DateTime? SummarizedAt { get; set; }
        public bool IsIncludedInReport { get; set; } = false;
        public DateTime? LastReportedAt { get; set; }
    }
}