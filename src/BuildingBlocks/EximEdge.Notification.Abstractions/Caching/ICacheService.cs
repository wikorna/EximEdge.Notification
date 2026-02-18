namespace EximEdge.Notification.Abstractions.Caching;

/// <summary>
/// Transport-agnostic caching abstraction.
/// Application layer depends on this; Infrastructure implements it via HybridCache + Redis.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="cacheKey"/>, creating it via <paramref name="factory"/>
    /// on a cache miss. HybridCache prevents cache stampede internally (single-flight).
    /// If the cache layer fails, the factory is called directly as a fail-open fallback.
    /// </summary>
    Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        CacheEntrySettings? settings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly writes a value into both L1 (in-memory) and L2 (distributed) cache layers.
    /// Useful for pre-warming cache after writes.
    /// </summary>
    Task SetAsync<T>(
        string cacheKey,
        T value,
        CacheEntrySettings? settings = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a single cache entry by key from both L1 and L2.</summary>
    Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>Removes all cache entries associated with the given tag from both L1 and L2.</summary>
    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-entry cache options. When <c>null</c>, the global defaults from <c>CacheOptions</c> are used.
/// </summary>
public sealed record CacheEntrySettings
{
    /// <summary>Absolute expiration for the distributed (L2) cache. Default: from global config.</summary>
    public TimeSpan? Expiration { get; init; }

    /// <summary>
    /// Expiration for the local in-memory (L1) layer. Shorter values reduce stale reads
    /// in multi-instance deployments. Default: from global config.
    /// </summary>
    public TimeSpan? LocalCacheExpiration { get; init; }

    /// <summary>Tags for group-based cache invalidation via <see cref="ICacheService.RemoveByTagAsync"/>.</summary>
    public IReadOnlyCollection<string>? Tags { get; init; }
}
