using Microsoft.EntityFrameworkCore;
using MyApi.Contracts;
using MyApi.Data;
using MyApi.Models;
using MyApi.Services;

namespace MyApi.Tests;

public class ProductServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FakeProductCache _cache = new();
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        // A uniquely named EF Core in-memory database per test keeps tests isolated.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"products-{Guid.NewGuid()}")
            .Options;

        _db = new AppDbContext(options);
        _service = new ProductService(_db, _cache);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetByIdAsync_CacheMiss_ReadsDatabaseAndFillsCache()
    {
        var product = await SeedProductAsync("Desk", 199m);

        var (result, fromCache) = await _service.GetByIdAsync(product.Id);

        Assert.NotNull(result);
        Assert.False(fromCache);
        Assert.Equal("Desk", result.Name);
        Assert.True(_cache.Items.ContainsKey(product.Id));
    }

    [Fact]
    public async Task GetByIdAsync_CacheHit_ReturnsCachedValue()
    {
        var product = await SeedProductAsync("Desk", 199m);
        await _service.GetByIdAsync(product.Id);

        var (result, fromCache) = await _service.GetByIdAsync(product.Id);

        Assert.NotNull(result);
        Assert.True(fromCache);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var (result, fromCache) = await _service.GetByIdAsync(12345);

        Assert.Null(result);
        Assert.False(fromCache);
        Assert.Empty(_cache.Items);
    }

    [Fact]
    public async Task CreateAsync_PersistsTrimmedProduct()
    {
        var created = await _service.CreateAsync(new ProductRequest("  Lamp  ", "Desk lamp", 15.25m));

        var stored = await _db.Products.SingleAsync();
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal("Lamp", stored.Name);
        Assert.Equal(15.25m, stored.Price);
        Assert.Null(stored.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ChangesProductAndInvalidatesCache()
    {
        var product = await SeedProductAsync("Chair", 89m);
        await _service.GetByIdAsync(product.Id);

        var updated = await _service.UpdateAsync(product.Id, new ProductRequest("Office chair", null, 99m));

        Assert.NotNull(updated);
        Assert.Equal("Office chair", updated.Name);
        Assert.Equal(99m, updated.Price);
        Assert.NotNull(updated.UpdatedAtUtc);
        Assert.Contains(product.Id, _cache.RemovedIds);
        Assert.False(_cache.Items.ContainsKey(product.Id));
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        var updated = await _service.UpdateAsync(999, new ProductRequest("Ghost", null, 1m));

        Assert.Null(updated);
    }

    [Fact]
    public async Task DeleteAsync_RemovesProductAndInvalidatesCache()
    {
        var product = await SeedProductAsync("Cable", 5m);

        var deleted = await _service.DeleteAsync(product.Id);

        Assert.True(deleted);
        Assert.Empty(await _db.Products.ToListAsync());
        Assert.Contains(product.Id, _cache.RemovedIds);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        Assert.False(await _service.DeleteAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsProductsOrderedById()
    {
        await SeedProductAsync("First", 1m);
        await SeedProductAsync("Second", 2m);

        var products = await _service.GetAllAsync();

        Assert.Equal(["First", "Second"], products.Select(p => p.Name));
    }

    private async Task<Product> SeedProductAsync(string name, decimal price)
    {
        var product = new Product { Name = name, Price = price, CreatedAtUtc = DateTime.UtcNow };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return product;
    }
}
