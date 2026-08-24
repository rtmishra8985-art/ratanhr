using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.BackgroundServices;

/// <summary>
/// Nightly background job that purges expired and revoked refresh tokens
/// older than the configured retention window (default 30 days).
///
/// Without this, the refresh_tokens table grows unbounded: every login adds a row
/// and they are never removed, eventually causing full-table-scan slowdowns.
/// </summary>
public class TokenCleanupService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenCleanupService> _logger;

    public TokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<TokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ── Testable constructor (unit-test use only) ─────────────────────────
    private readonly ApplicationDbContext? _testDb;
    private readonly Func<DateTime>?       _testClock;

    internal TokenCleanupService(
        ApplicationDbContext          db,
        ILogger<TokenCleanupService>  logger,
        Func<DateTime>                nowFactory)
    {
        _testDb     = db;
        _logger     = logger;
        _testClock  = nowFactory;
        _scopeFactory = null!;   // not used in test path
    }

    /// <summary>Public entry-point for unit tests — executes one cleanup cycle.</summary>
    public async Task RunCleanupAsync(CancellationToken ct)
    {
        try
        {
            ApplicationDbContext db;
            if (_testDb != null)
            {
                db = _testDb;
            }
            else
            {
                // Production: resolve db from DI scope
                using var scope = _scopeFactory.CreateScope();
                db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            }

            var now    = _testClock != null ? _testClock() : DateTime.UtcNow;
            var cutoff = now.Subtract(RetentionWindow);

            // Use load-then-remove so this path is compatible with the EF Core
            // in-memory provider (which does not support ExecuteDeleteAsync).
            var expiredTokens = await db.RefreshTokens
                .Where(t => t.ExpiresAt < now || t.RevokedAt.HasValue)
                .ToListAsync(ct);
            db.RefreshTokens.RemoveRange(expiredTokens);
            await db.SaveChangesAsync(ct);
            var deleted = expiredTokens.Count;

            var expiredResets = await db.PasswordResetTokens
                .Where(t => t.ExpiresAt < now || t.UsedAt.HasValue)
                .ToListAsync(ct);
            db.PasswordResetTokens.RemoveRange(expiredResets);
            await db.SaveChangesAsync(ct);
            var resetDeleted = expiredResets.Count;

            if (deleted > 0 || resetDeleted > 0)
                _logger.LogInformation(
                    "TokenCleanup: removed {RT} expired refresh tokens, {PR} used/expired reset tokens.",
                    deleted, resetDeleted);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "TokenCleanupService encountered an error.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TokenCleanupService started. Will run every {Hours}h.", RunInterval.TotalHours);

        // Initial delay — give the API a minute to finish startup before running.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupInternalAsync(stoppingToken);
            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task RunCleanupInternalAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var cutoff = DateTime.UtcNow.Subtract(RetentionWindow);

            // Delete refresh tokens that are either:
            //   a) expired (ExpiresAt < cutoff) OR
            //   b) revoked more than RetentionWindow ago
            var deleted = await db.RefreshTokens
                .Where(t => t.ExpiresAt < cutoff || (t.RevokedAt.HasValue && t.RevokedAt < cutoff))
                .ExecuteDeleteAsync(ct);

            // Same for used or expired password-reset tokens
            var resetDeleted = await db.PasswordResetTokens
                .Where(t => t.ExpiresAt < cutoff || t.UsedAt.HasValue)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0 || resetDeleted > 0)
                _logger.LogInformation(
                    "TokenCleanup: removed {RT} expired refresh tokens, {PR} used/expired reset tokens.",
                    deleted, resetDeleted);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Log and continue — the job will retry on the next cycle.
            _logger.LogError(ex, "TokenCleanupService encountered an error.");
        }
    }
}
