using System.Text.Json;
using MyApi.Contracts;
using StackExchange.Redis;

namespace MyApi.Services;

/// <summary>
/// Cache-aside store for single products in Redis. Redis failures are logged and treated as
/// cache misses, so the API keeps working (directly against SQL Server) when Redis is down.
/// </summary>
public class RedisProductCache(
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    ILogger<RedisProductCache> logger) : IProductCache
{
    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(configuration.GetValue("Redis:ProductTtlSeconds", 60));

    public static string KeyFor(int id) => $"myapi:products:{id}";

    public async Task<ProductResponse?> GetAsync(int id)
    {
        try
        {
            var value = await redis.GetDatabase().StringGetAsync(KeyFor(id));
            return value.HasValue ? JsonSerializer.Deserialize<ProductResponse>(value.ToString()) : null;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or JsonException)
        {
            logger.LogWarning(ex, "Redis read failed for product {ProductId}; falling back to the database", id);
            return null;
        }
    }

    public async Task SetAsync(ProductResponse product)
    {
        try
        {
            await redis.GetDatabase().StringSetAsync(KeyFor(product.Id), JsonSerializer.Serialize(product), _ttl);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Redis write failed for product {ProductId}", product.Id);
        }
    }

    public async Task RemoveAsync(int id)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(KeyFor(id));
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Redis delete failed for product {ProductId}", id);
        }
    }
}
