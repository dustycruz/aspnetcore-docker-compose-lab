using Microsoft.EntityFrameworkCore;
using MyApi.Contracts;
using MyApi.Data;
using MyApi.Models;

namespace MyApi.Services;

public class ProductService(AppDbContext db, IProductCache cache)
{
    public async Task<IReadOnlyList<ProductResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);

        return products.Select(ToResponse).ToList();
    }

    /// <summary>Reads a product using cache-aside: Redis first, then SQL Server (which then fills the cache).</summary>
    public async Task<(ProductResponse? Product, bool FromCache)> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetAsync(id);
        if (cached is not null)
        {
            return (cached, true);
        }

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return (null, false);
        }

        var response = ToResponse(product);
        await cache.SetAsync(response);
        return (response, false);
    }

    public async Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Name = request.Name!.Trim(),
            Description = request.Description,
            Price = request.Price,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(product);
    }

    public async Task<ProductResponse?> UpdateAsync(int id, ProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        product.Name = request.Name!.Trim();
        product.Description = request.Description;
        product.Price = request.Price;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(id);
        return ToResponse(product);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return false;
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(id);
        return true;
    }

    private static ProductResponse ToResponse(Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.Price,
        product.CreatedAtUtc,
        product.UpdatedAtUtc);
}
