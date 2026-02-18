namespace EximEdge.Notification.Infrastructure.Caching;

/// <summary>
/// Shared Redis / HybridCache options bound from the "Cache" configuration section.
/// Used by all hosts. Environment overrides via <c>Cache__RedisConnectionString</c> etc.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>When false, HybridCache operates in L1-only mode (in-memory). Redis is not connected.</summary>
    public bool Enabled { get; set; }

    /// <summary>StackExchange.Redis connection string. Supports <c>host:port,password=…,ssl=true</c> format.</summary>
    public string RedisConnectionString { get; set; } = string.Empty;

    /// <summary>Redis key prefix to avoid collisions across applications sharing a cluster.</summary>
    public string InstanceName { get; set; } = "EximEdge:";

    /// <summary>Default absolute expiration for the L2 distributed cache (minutes).</summary>
    public int DefaultExpirationMinutes { get; set; } = 30;

    /// <summary>Default expiration for the L1 in-memory cache (minutes). Keep shorter than L2.</summary>
    public int LocalCacheExpirationMinutes { get; set; } = 5;

    /// <summary>Maximum serialized payload per cache entry (bytes). Entries exceeding this are not cached.</summary>
    public int MaximumPayloadBytes { get; set; } = 1_048_576; // 1 MB

    /// <summary>Maximum length of a cache key.</summary>
    public int MaximumKeyLength { get; set; } = 1024;
}
