using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<PublicOrder> GetByIdAsync(int id)
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

        public async Task<bool> ExistsAsync(string originalUrl)
        {
            return await _context.PublicOrders.AnyAsync(x => x.OriginalUrl == originalUrl);
        }
    }
}