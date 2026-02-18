using EximEdge.Notification.Abstractions.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EximEdge.Notification.Infrastructure.Caching;

/// <summary>
/// Production-grade <see cref="ICacheService"/> backed by <see cref="HybridCache"/> (L1 in-memory + L2 Redis).
/// <list type="bullet">
///   <item>Exception-safe: cache infrastructure failures fall back to the factory — business logic is never broken.</item>
///   <item>Stampede-proof: <see cref="HybridCache"/> GetOrCreateAsync is single-flight per key.</item>
///   <item>Thread-safe: <see cref="HybridCache"/> is designed for concurrent access.</item>
/// </list>
/// </summary>
internal sealed partial class HybridCacheService(
    HybridCache cache,
    IOptions<CacheOptions> options,
    ILogger<HybridCacheService> logger) : ICacheService
{
    private readonly CacheOptions _options = options.Value;

    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        CacheEntrySettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entryOptions = BuildEntryOptions(settings);

            return await cache.GetOrCreateAsync<T>(
                cacheKey,
                async ct => await factory(ct),
                entryOptions,
                settings?.Tags,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail-open: cache failure must never break business logic
            LogGetOrCreateError(logger, cacheKey, ex);
            return await factory(cancellationToken);
        }
    }

    public async Task SetAsync<T>(
        string cacheKey,
        T value,
        CacheEntrySettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entryOptions = BuildEntryOptions(settings);
            await cache.SetAsync(cacheKey, value, entryOptions, settings?.Tags, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSetError(logger, cacheKey, ex);
        }
    }

    public async Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(cacheKey, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRemoveError(logger, cacheKey, ex);
        }
    }

    public async Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveByTagAsync(tag, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRemoveByTagError(logger, tag, ex);
        }
    }

    private HybridCacheEntryOptions BuildEntryOptions(CacheEntrySettings? settings) => new()
    {
        Expiration = settings?.Expiration
                     ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes),
        LocalCacheExpiration = settings?.LocalCacheExpiration
                               ?? TimeSpan.FromMinutes(_options.LocalCacheExpirationMinutes)
    };

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Cache GetOrCreateAsync failed for key '{CacheKey}'. Executing factory fallback.")]
    private static partial void LogGetOrCreateError(ILogger logger, string cacheKey, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Cache SetAsync failed for key '{CacheKey}'.")]
    private static partial void LogSetError(ILogger logger, string cacheKey, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Cache RemoveAsync failed for key '{CacheKey}'.")]
    private static partial void LogRemoveError(ILogger logger, string cacheKey, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Cache RemoveByTagAsync failed for tag '{Tag}'.")]
    private static partial void LogRemoveByTagError(ILogger logger, string tag, Exception exception);
}
