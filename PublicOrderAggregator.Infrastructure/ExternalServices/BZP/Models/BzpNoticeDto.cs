using System;
using System.Collections.Generic;

namespace PublicOrderAggregator.Infrastructure.ExternalServices.BZP.Models
{
    public class BzpNoticeDto
    {
        public string ObjectId { get; set; }
        public string ClientType { get; set; }
        public string OrderType { get; set; }
        public string TenderType { get; set; }
        public string NoticeType { get; set; }
        public string NoticeNumber { get; set; }
        public string BzpNumber { get; set; }
        public bool? IsTenderAmountBelowEU { get; set; }
        public DateTime PublicationDate { get; set; }
        public string OrderObject { get; set; }
        public string CpvCode { get; set; }
        public DateTime? SubmittingOffersDate { get; set; }
        public string ProcedureResult { get; set; }
        public string OrganizationName { get; set; }
        public string OrganizationCity { get; set; }
        public string OrganizationProvince { get; set; }
        public string OrganizationCountry { get; set; }
        public string OrganizationNationalId { get; set; }
        public string OrganizationId { get; set; }
        public string TenderId { get; set; }
        public string HtmlBody { get; set; }
        public List<BzpContractorDto> Contractors { get; set; }
    }

    public class BzpContractorDto
    {
        public string ContractorName { get; set; }
        public string ContractorCity { get; set; }
        public string ContractorProvince { get; set; }
        public string ContractorCountry { get; set; }
        public string ContractorNationalId { get; set; }
    }
}