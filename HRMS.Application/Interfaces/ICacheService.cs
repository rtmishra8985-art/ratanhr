namespace HRMS.Application.Interfaces;

/// <summary>
/// Lightweight cache-aside abstraction.
/// Implementations may use IMemoryCache (single-instance) or
/// IDistributedCache / Redis (multi-instance).
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or invokes
    /// <paramref name="factory"/> to produce it, caches the result, and returns it.
    /// </summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    /// <summary>Removes a single cache entry by key.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Removes all cache entries whose keys start with <paramref name="prefix"/>.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
