using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace MyApi.HealthChecks;

/// <summary>Pings Redis through the shared connection. Uses only the built-in health check abstractions.</summary>
public class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis responded in {latency.TotalMilliseconds:F1} ms.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Redis is unreachable.", ex);
        }
    }
}
