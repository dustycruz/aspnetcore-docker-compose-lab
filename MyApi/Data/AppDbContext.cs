using Microsoft.EntityFrameworkCore;
using MyApi.Models;

namespace MyApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(product =>
        {
            product.ToTable("Products");
            product.HasKey(p => p.Id);
            product.Property(p => p.Name).HasMaxLength(100).IsRequired();
            product.Property(p => p.Description).HasMaxLength(500);
            product.Property(p => p.Price).HasPrecision(18, 2);
            product.HasIndex(p => p.Name);

            // SQL Server datetime2 does not store DateTimeKind; mark values read back as UTC.
            product.Property(p => p.CreatedAtUtc)
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            product.Property(p => p.UpdatedAtUtc)
                .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

            // Seed data uses fixed timestamps so migrations stay deterministic.
            var seededAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            product.HasData(
                new Product { Id = 1, Name = "Keyboard", Description = "Mechanical keyboard", Price = 79.99m, CreatedAtUtc = seededAt },
                new Product { Id = 2, Name = "Mouse", Description = "Wireless mouse", Price = 29.50m, CreatedAtUtc = seededAt },
                new Product { Id = 3, Name = "Monitor", Description = "27-inch 1440p monitor", Price = 249.00m, CreatedAtUtc = seededAt });
        });
    }
}
