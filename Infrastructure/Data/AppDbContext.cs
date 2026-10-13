using Microsoft.EntityFrameworkCore;
using SfWebAPIHeraldo.Models;

namespace SfWebAPIHeraldo.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var partner = modelBuilder.Entity<Partner>();
            partner.HasKey(entity => entity.Id);
            partner.Property(entity => entity.Name).HasMaxLength(100).IsRequired();
            partner.Property(entity => entity.TaxNumber).HasMaxLength(9).IsRequired();
            partner.Property(entity => entity.Type).HasConversion<string>().HasMaxLength(50);
            partner.Property(entity => entity.CreditLimit).HasColumnType("decimal(18, 2)");
            partner.Property(entity => entity.Version).IsRowVersion();
            partner.Property(entity => entity.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            partner.HasIndex(entity => entity.TaxNumber).IsUnique();
        }

        public DbSet<Partner> Partners => Set<Partner>();
    }
}
