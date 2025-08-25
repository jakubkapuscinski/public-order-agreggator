namespace PublicOrderAggregator.Infrastructure.DataSources.BZP.Models
{
    public class BzpNoticeDto
    {
        public string ObjectId { get; set; } = string.Empty;
        public string? NoticeType { get; set; }
        public string? NoticeNumber { get; set; }
        public DateTime PublicationDate { get; set; }
        public string? OrderObject { get; set; }
        public DateTime? SubmittingOffersDate { get; set; }
        public string? OrganizationName { get; set; }
        public string? OrganizationCity { get; set; }
        public string? OrganizationProvince { get; set; }
        public string? HtmlBody { get; set; }
    }
}