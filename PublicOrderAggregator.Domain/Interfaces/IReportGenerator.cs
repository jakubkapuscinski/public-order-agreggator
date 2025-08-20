using System.Collections.Generic;
using System.Threading.Tasks;
using PublicOrderAggregator.Domain.Entities;

namespace PublicOrderAggregator.Domain.Interfaces
{
    public interface IReportGenerator
    {
        Task<string> GenerateHtmlReportAsync(IEnumerable<PublicOrder> orders);
    }
}