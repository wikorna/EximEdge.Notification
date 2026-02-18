using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace EximEdge.Notification.Infrastructure.Caching;

/// <summary>
/// Health check that pings the shared Redis <see cref="IConnectionMultiplexer"/>.
/// Gracefully reports Healthy when Redis is intentionally disabled (no multiplexer registered).
/// </summary>
internal sealed class RedisHealthCheck(IServiceProvider serviceProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var multiplexer = serviceProvider.GetService<IConnectionMultiplexer>();
        if (multiplexer is null)
            return HealthCheckResult.Healthy("Redis caching is disabled.");

        try
        {
            var db = multiplexer.GetDatabase();
            var latency = await db.PingAsync();
            return HealthCheckResult.Healthy($"Redis PING: {latency.TotalMilliseconds:F1}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable.", ex);
        }
    }
}
