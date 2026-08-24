using HRMS.Application.Interfaces;
// FIX HIGH-12: Implementations of IPayrollBulkLockService.
// Two implementations registered based on Redis availability:
//   RedisPayrollBulkLockService  — distributed lock across multiple API replicas (preferred)
//   InMemoryPayrollBulkLockService — single-instance semaphore (fallback when Redis absent)

using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Redis-backed distributed payroll bulk lock.
/// Uses SET NX EX (set-if-not-exists with TTL) — the standard Redis distributed lock primitive.
/// TTL of 10 minutes ensures a dead process cannot permanently block payroll for a company.
/// </summary>
public sealed class RedisPayrollBulkLockService : IPayrollBulkLockService
{
    private readonly IConnectionMultiplexer            _redis;
    private readonly ILogger<RedisPayrollBulkLockService> _log;
    private const    int                               TtlSeconds = 600; // 10 minutes max run time

    public RedisPayrollBulkLockService(
        IConnectionMultiplexer redis,
        ILogger<RedisPayrollBulkLockService> log)
    {
        _redis = redis;
        _log   = log;
    }

    public async Task<IPayrollBulkLockHandle?> TryAcquireAsync(
        int companyId, int month, int year, CancellationToken ct = default)
    {
        var db  = _redis.GetDatabase();
        var key = $"hrms:payroll:bulk-lock:{companyId}:{year}:{month:D2}";
        var token = Guid.NewGuid().ToString("N");

        // SET key token NX EX 600 — atomic check-and-set
        var acquired = await db.StringSetAsync(
            key, token, TimeSpan.FromSeconds(TtlSeconds), When.NotExists).ConfigureAwait(false);

        if (!acquired)
        {
            _log.LogWarning(
                "[PayrollBulkLock] Company {CompanyId} month {Month}/{Year} already locked — rejecting concurrent run.",
                companyId, month, year);
            return null;
        }

        _log.LogInformation(
            "[PayrollBulkLock] Acquired lock for company {CompanyId} {Month}/{Year} (TTL {Ttl}s).",
            companyId, month, year, TtlSeconds);

        return new RedisLockHandle(db, key, token, _log);
    }

    private sealed class RedisLockHandle : IPayrollBulkLockHandle
    {
        private readonly IDatabase _db;
        private readonly string    _key;
        private readonly string    _token;
        private readonly ILogger   _log;
        private          bool      _released;

        public RedisLockHandle(IDatabase db, string key, string token, ILogger log)
        {
            _db = db; _key = key; _token = token; _log = log;
        }

        public async ValueTask DisposeAsync()
        {
            if (_released) return;
            _released = true;

            // Lua script: only delete the key if it still holds our token
            // (prevents releasing a lock re-acquired by another process after TTL expiry)
            const string lua = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";
            try
            {
                await _db.ScriptEvaluateAsync(lua,
                    new RedisKey[] { _key },
                    new RedisValue[] { _token }).ConfigureAwait(false);
                _log.LogInformation("[PayrollBulkLock] Released lock for key {Key}.", _key);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[PayrollBulkLock] Failed to release lock for key {Key} — TTL will expire it.", _key);
            }
        }
    }
}

/// <summary>
/// In-memory fallback payroll bulk lock. Works only on a single API instance.
/// Used automatically when Redis is not configured.
/// </summary>
public sealed class InMemoryPayrollBulkLockService : IPayrollBulkLockService
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim>
        Semaphores = new(StringComparer.Ordinal);

    private readonly ILogger<InMemoryPayrollBulkLockService> _log;

    public InMemoryPayrollBulkLockService(ILogger<InMemoryPayrollBulkLockService> log)
        => _log = log;

    public async Task<IPayrollBulkLockHandle?> TryAcquireAsync(
        int companyId, int month, int year, CancellationToken ct = default)
    {
        var key = $"{companyId}:{year}:{month:D2}";
        var sem = Semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        var acquired = await sem.WaitAsync(TimeSpan.Zero, ct).ConfigureAwait(false);
        if (!acquired)
        {
            _log.LogWarning(
                "[PayrollBulkLock][InMemory] Company {CompanyId} {Month}/{Year} already locked.",
                companyId, month, year);
            return null;
        }

        _log.LogInformation("[PayrollBulkLock][InMemory] Acquired for {Key}.", key);
        return new SemaphoreLockHandle(sem, key, _log);
    }

    private sealed class SemaphoreLockHandle : IPayrollBulkLockHandle
    {
        private readonly SemaphoreSlim _sem;
        private readonly string        _key;
        private readonly ILogger       _log;
        private          bool          _released;

        public SemaphoreLockHandle(SemaphoreSlim sem, string key, ILogger log)
        {
            _sem = sem; _key = key; _log = log;
        }

        public ValueTask DisposeAsync()
        {
            if (!_released) { _released = true; _sem.Release(); _log.LogInformation("[PayrollBulkLock][InMemory] Released {Key}.", _key); }
            return ValueTask.CompletedTask;
        }
    }
}
