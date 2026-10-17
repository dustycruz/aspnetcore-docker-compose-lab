using MyApi.Contracts;
using MyApi.Services;

namespace MyApi.Tests;

/// <summary>In-memory stand-in for Redis so the service can be tested without a Redis server.</summary>
public class FakeProductCache : IProductCache
{
    public Dictionary<int, ProductResponse> Items { get; } = new();

    public List<int> RemovedIds { get; } = new();

    public Task<ProductResponse?> GetAsync(int id) =>
        Task.FromResult(Items.TryGetValue(id, out var product) ? product : null);

    public Task SetAsync(ProductResponse product)
    {
        Items[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(int id)
    {
        Items.Remove(id);
        RemovedIds.Add(id);
        return Task.CompletedTask;
    }
}
