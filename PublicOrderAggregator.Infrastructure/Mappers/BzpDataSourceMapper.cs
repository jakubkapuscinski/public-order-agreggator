using System;
using System.Text.Json;
using PublicOrderAggregator.Domain.Entities;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Infrastructure.ExternalServices.BZP.Models;

namespace PublicOrderAggregator.Infrastructure.Mappers
{
    public class BzpDataSourceMapper : IDataSourceMapper<BzpNoticeDto>
    {
        public PublicOrder MapToPublicOrder(BzpNoticeDto sourceData)
        {
            if (sourceData == null)
                throw new ArgumentNullException(nameof(sourceData));

            return new PublicOrder
            {
                Organizer = sourceData.OrganizationName ?? string.Empty,
                Location = $"{sourceData.OrganizationCity}, {sourceData.OrganizationProvince}".Trim(' ', ','),
                TenderDate = sourceData.PublicationDate,
                SubmissionDeadline = sourceData.SubmittingOffersDate ?? sourceData.PublicationDate.AddDays(30),
                Subject = sourceData.OrderObject ?? string.Empty,
                Requirements = string.Empty, // Will be filled by GPT service after HTML sanitization
                DataSource = "BZP",
                OriginalUrl = $"https://ezamowienia.gov.pl/mo-client-board/bzp/notice-details/id/{sourceData.ObjectId}",
                FetchedAt = DateTime.UtcNow,
                RawData = JsonSerializer.Serialize(sourceData, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };
        }

        public bool CanHandle(string sourceType)
        {
            return sourceType?.ToUpperInvariant() == "BZP";
        }
    }
}