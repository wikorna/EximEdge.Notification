namespace EximEdge.Notification.Abstractions.Caching;

/// <summary>
/// Helper for building normalized, consistent cache keys.
/// Usage: <c>CacheKey.Create("email", "user", userId)</c> → <c>"email:user:42"</c>
/// </summary>
public static class CacheKey
{
    private const char Separator = ':';

    public static string Create(params string[] segments) =>
        segments.Length == 0 ? string.Empty : string.Join(Separator, segments).ToLowerInvariant();
}
