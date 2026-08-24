using HRMS.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Cache-aside implementation.
/// Uses <see cref="IDistributedCache"/> (Redis) when available, falling back
/// to <see cref="IMemoryCache"/> for single-instance deployments.
///
/// FIX: Now accepts <see cref="IConnectionMultiplexer"/> directly so that
/// <see cref="RemoveByPrefixAsync"/> can issue a Redis SCAN for cluster-wide
/// invalidation — not just per-instance invalidation via the in-memory key index.
/// </summary>
public sealed class CacheService : ICacheService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _memory;
    private readonly IDistributedCache? _distributed;    // null when Redis is not configured
    private readonly IConnectionMultiplexer? _mux;       // null when Redis is not configured
    private readonly ILogger<CacheService> _logger;

    private readonly ConcurrentDictionary<string, byte> _keyIndex = new();

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public CacheService(
        IMemoryCache memory,
        ILogger<CacheService> logger,
        IDistributedCache? distributed = null,
        IConnectionMultiplexer? mux = null)
    {
        _memory      = memory;
        _distributed = distributed;
        _mux         = mux;
        _logger      = logger;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        var expiry = ttl ?? DefaultTtl;

        // ── Try distributed cache (Redis) ─────────────────────────────────
        if (_distributed is not null)
        {
            try
            {
                var bytes = await _distributed.GetAsync(key, ct);
                if (bytes is not null)
                {
                    _logger.LogDebug("[Cache HIT  distributed] {Key}", key);
                    return JsonSerializer.Deserialize<T>(bytes, _json)!;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Cache MISS distributed READ ERROR] {Key} — falling back to factory", key);
            }

            var value = await factory();
            try
            {
                var serialized = JsonSerializer.SerializeToUtf8Bytes(value, _json);
                await _distributed.SetAsync(key, serialized,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry },
                    ct);
                _keyIndex.TryAdd(key, 0);
                _logger.LogDebug("[Cache SET  distributed] {Key} TTL={TTL}", key, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Cache SET  distributed FAILED] {Key}", key);
            }
            return value;
        }

        // ── Fall back to in-process memory cache ──────────────────────────
        if (_memory.TryGetValue(key, out T? cached))
        {
            _logger.LogDebug("[Cache HIT  memory] {Key}", key);
            return cached!;
        }

        var result = await factory();
        _memory.Set(key, result, expiry);
        _keyIndex.TryAdd(key, 0);
        _logger.LogDebug("[Cache SET  memory] {Key} TTL={TTL}", key, expiry);
        return result;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _memory.Remove(key);
        _keyIndex.TryRemove(key, out _);

        if (_distributed is not null)
        {
            try { await _distributed.RemoveAsync(key, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "[Cache REMOVE distributed FAILED] {Key}", key); }
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        // Always invalidate from the local key index (covers both memory and distributed writes
        // made by THIS instance — the index tracks every key this instance ever wrote).
        var keys = _keyIndex.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        var tasks = keys.Select(k => RemoveAsync(k, ct));
        await Task.WhenAll(tasks);

        // FIX: Multi-instance distributed invalidation via Redis SCAN.
        // The in-process _keyIndex only tracks keys written by THIS instance.
        // In a horizontally scaled deployment other replicas may have written keys
        // with the same prefix to Redis without this instance knowing about them.
        // We use the injected IConnectionMultiplexer to issue a server-side SCAN
        // so ALL matching keys are removed from Redis regardless of which instance
        // originally wrote them.
        if (_mux is not null && _distributed is not null)
        {
            try
            {
                var server = _mux.GetServer(_mux.GetEndPoints()[0]);
                var redisKeys = server.KeysAsync(pattern: $"{prefix}*", pageSize: 250);
                await foreach (var key in redisKeys.WithCancellation(ct))
                {
                    await _distributed.RemoveAsync(key!, ct);
                    _keyIndex.TryRemove(key!, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[Cache INVALIDATE] Redis SCAN for prefix={Prefix} failed — local index only", prefix);
            }
        }

        _logger.LogDebug("[Cache INVALIDATE] prefix={Prefix} removed={Count} keys (local index)", prefix, keys.Count);
    }
}
