using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HRMS.Infrastructure.Redis;

/// <summary>
/// A true distributed sliding-window rate limiter backed by Redis.
///
/// Algorithm:
///   1. Each request uses Redis ZADD to add the current timestamp to a sorted set keyed
///      by {prefix}:{partition} (e.g. "ratelimit:login:192.168.1.1").
///   2. ZREMRANGEBYSCORE removes entries older than the window.
///   3. ZCARD returns the current count.
///   4. EXPIRE resets the TTL.
///   All four writes are pipelined in one round-trip.
///
/// Fail-safe behaviour:
///   If Redis is unavailable (connection failure, timeout, etc.), authentication
///   and other sensitive policies reject the request, while the general API policy
///   allows it through. Both decisions are logged at warning level.
///
/// This limiter is safe to use across multiple API instances because the counter
/// is stored in Redis, not in process memory.
/// </summary>
public sealed class RedisDistributedRateLimiter : RateLimiter
{
    private readonly IDatabase _redis;
    private readonly string    _key;
    private readonly string    _policyName;
    private readonly int       _permitLimit;
    private readonly TimeSpan  _window;
    private readonly ILogger?  _logger;

    public RedisDistributedRateLimiter(
        IConnectionMultiplexer mux,
        string key,
        string policyName,
        int permitLimit,
        int windowSeconds,
        ILogger? logger = null)
    {
        _redis       = mux.GetDatabase();
        _key         = key;
        _policyName  = policyName;
        _permitLimit = permitLimit;
        _window      = TimeSpan.FromSeconds(windowSeconds);
        _logger      = logger;
    }

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount, CancellationToken cancellationToken)
    {
        try
        {
            var now      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var windowMs = (long)_window.TotalMilliseconds;
            var cutoff   = now - windowMs;

            var batch = _redis.CreateBatch();
            var zadd  = batch.SortedSetAddAsync(_key, now.ToString(), now);
            var zrem  = batch.SortedSetRemoveRangeByScoreAsync(_key, double.NegativeInfinity, cutoff);
            var zcard = batch.SortedSetLengthAsync(_key);
            var ttl   = batch.KeyExpireAsync(_key, _window);
            batch.Execute();

            await Task.WhenAll(zadd, zrem, zcard, ttl);
            var count = await zcard;

            return count <= _permitLimit
                ? new Lease(true)
                : new Lease(false);
        }
        catch (RedisException ex)
        {
            var failClosed = _policyName is "login" or "sensitive";
            var decision = failClosed ? "REJECTED" : "ALLOWED";
            _logger?.LogWarning(ex,
                "Redis rate limiter failed for policy '{PolicyName}', key '{Key}' — decision: {Decision}. " +
                "Ensure Redis is healthy.", _policyName, _key, decision);
            return new Lease(!failClosed);
        }
        catch (Exception ex)
        {
            var failClosed = _policyName is "login" or "sensitive";
            var decision   = failClosed ? "REJECTED" : "ALLOWED";
            _logger?.LogWarning(ex,
                "Unexpected error in Redis rate limiter for policy '{PolicyName}', key '{Key}' — " +
                "decision: {Decision}.", _policyName, _key, decision);
            return new Lease(!failClosed);
        }
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
        => new Lease(false);  // Synchronous acquire not supported; use async path.

    private sealed class Lease : RateLimitLease
    {
        public Lease(bool isAcquired) => IsAcquired = isAcquired;
        public override bool IsAcquired { get; }
        public override IEnumerable<string> MetadataNames => Enumerable.Empty<string>();
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        { metadata = null; return false; }
        protected override void Dispose(bool disposing) { }
    }

    /// <summary>
    /// Factory helper for use in Program.cs AddRateLimiter configuration.
    /// </summary>
    public static RateLimitPartition<string> CreatePartition(
        IConnectionMultiplexer mux,
        string key,
        int permitLimit,
        int windowSeconds,
        ILogger? logger = null)
    {
        var policyName = key.Split(':', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1) ?? key;
        return RateLimitPartition.Get<string>(key, _ =>
            new RedisDistributedRateLimiter(mux, key, policyName, permitLimit, windowSeconds, logger));
    }
}
