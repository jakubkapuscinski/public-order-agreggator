using Microsoft.EntityFrameworkCore;
using PublicOrderAggregator.Domain.Entities;

namespace PublicOrderAggregator.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<PublicOrder> PublicOrders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PublicOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Organizer).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Location).HasMaxLength(200);
                entity.Property(e => e.Subject).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.DataSource).IsRequired().HasMaxLength(100);
                entity.Property(e => e.OriginalUrl).IsRequired().HasMaxLength(500);
                entity.HasIndex(e => e.OriginalUrl);
                entity.HasIndex(e => e.DataSource);
                entity.HasIndex(e => e.TenderDate);
                entity.HasIndex(e => e.SubmissionDeadline);
            });
        }
    }
}