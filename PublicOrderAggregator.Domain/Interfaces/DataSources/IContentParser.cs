using PublicOrderAggregator.Domain.Models.DTOs;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IContentParser
    {
        bool CanParse(string sourceType);
        Task<ParsedOrderDataDto> ParseAsync(RawOrderDataDto rawData);
    }
}