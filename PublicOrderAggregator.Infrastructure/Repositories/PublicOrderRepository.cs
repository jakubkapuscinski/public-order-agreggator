using Microsoft.EntityFrameworkCore;
using PublicOrderAggregator.Domain.Entities;
using PublicOrderAggregator.Domain.Interfaces;
using PublicOrderAggregator.Infrastructure.Data;

namespace PublicOrderAggregator.Infrastructure.Repositories
{
    public class PublicOrderRepository : IPublicOrderRepository
    {
        private readonly ApplicationDbContext _context;

        public PublicOrderRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PublicOrder?> GetByIdAsync(int id)
        {
            return await _context.PublicOrders.FindAsync(id);
        }

        public async Task<IEnumerable<PublicOrder>> GetAllAsync()
        {
            return await _context.PublicOrders.ToListAsync();
        }

        public async Task<IEnumerable<PublicOrder>> GetByDataSourceAsync(string dataSource)
        {
            return await _context.PublicOrders
                .Where(x => x.DataSource == dataSource)
                .ToListAsync();
        }

        public async Task<PublicOrder> AddAsync(PublicOrder order)
        {
            _context.PublicOrders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task UpdateAsync(PublicOrder order)
        {
            _context.PublicOrders.Update(order);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var order = await _context.PublicOrders.FindAsync(id);
            if (order != null)
            {
                _context.PublicOrders.Remove(order);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(string externalId, string dataSource)
        {
            return await _context.PublicOrders.AnyAsync(x => x.ExternalId == externalId && x.DataSource == dataSource);
        }

        public async Task UpdateBatchAsync(IEnumerable<PublicOrder> orders)
        {
            _context.PublicOrders.UpdateRange(orders);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<PublicOrder>> GetUnclassifiedOrdersAsync()
        {
            return await _context.PublicOrders
                .Where(o => !o.IsClassified)
                .ToListAsync();
        }

        public async Task<IEnumerable<PublicOrder>> GetUnsummarizedOrdersAsync()
        {
            return await _context.PublicOrders
                .Where(o => o.IsClassified && o.IsRelevant && !o.IsSummarized)
                .ToListAsync();
        }

        public async Task<IEnumerable<PublicOrder>> GetOrdersForReportAsync(DateTime? since = null)
        {
            var query = _context.PublicOrders
                .Where(o => o.IsClassified && o.IsRelevant && o.IsSummarized && !o.IsIncludedInReport); // Only orders not yet included in any report
            
            if (since.HasValue)
            {
                query = query.Where(o => o.SummarizedAt >= since.Value);
            }
            
            return await query.ToListAsync();
        }

        public async Task<DateTime?> GetLastReportGenerationDateAsync()
        {
            // Get the latest LastReportedAt date from any order
            return await _context.PublicOrders
                .Where(o => o.IsIncludedInReport && o.LastReportedAt.HasValue)
                .MaxAsync(o => (DateTime?)o.LastReportedAt);
        }
    }
}