using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.BackgroundServices;

/// <summary>
/// Nightly background job that prunes stale biometric punch logs that have already
/// been processed into attendance records.
///
/// Without periodic cleanup the biometric_logs table grows unbounded: high-volume
/// devices can punch thousands of times per day and rows are never removed, causing
/// full-table-scan slowdowns and excessive storage consumption.
///
/// Retention window (default 90 days) is configurable via
/// <c>Biometric:LogRetentionDays</c> in appsettings / environment variables.
/// Only <em>processed</em> logs older than the cutoff are deleted; unprocessed logs
/// are kept until they have been reconciled so no data is lost.
/// </summary>
public class BiometricLogCleanupService : BackgroundService
{
    private static readonly TimeSpan RunInterval     = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay    = TimeSpan.FromMinutes(5);
    private const           int      DefaultRetentionDays = 90;

    private readonly IServiceScopeFactory          _scopeFactory;
    private readonly ILogger<BiometricLogCleanupService> _logger;

    public BiometricLogCleanupService(
        IServiceScopeFactory             scopeFactory,
        ILogger<BiometricLogCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BiometricLogCleanupService started. Will run every {Hours}h.",
            RunInterval.TotalHours);

        // Give the API time to finish startup before the first run.
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupAsync(stoppingToken);
            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Allow operators to tune the retention window without redeploying.
            // BiometricSettings.LogRetentionDays is per-company; fall back to the
            // global default if no settings row exists.
            var companyRetentions = await db.BiometricSettings
                .AsNoTracking()
                .Select(s => new { s.CompanyId, Days = s.LogRetentionDays })
                .ToListAsync(ct);

            var retentionMap = companyRetentions
                .Where(x => x.CompanyId != null)
                .ToDictionary(x => x.CompanyId!.Value, x => x.Days);

            // Collect distinct company IDs that have processed logs.
            var companies = await db.BiometricLogs
                .Where(l => l.IsProcessed)
                .Select(l => l.CompanyId)
                .Distinct()
                .ToListAsync(ct);

            int totalDeleted = 0;

            foreach (var companyId in companies)
            {
                if (ct.IsCancellationRequested) break;

                var retentionDays = companyId.HasValue && retentionMap.TryGetValue(companyId.Value, out var d)
                    ? d
                    : DefaultRetentionDays;

                var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

                var deleted = await db.BiometricLogs
                    .Where(l => l.CompanyId == companyId
                             && l.IsProcessed
                             && l.PunchedAt < cutoff)
                    .ExecuteDeleteAsync(ct);

                totalDeleted += deleted;

                if (deleted > 0)
                    _logger.LogInformation(
                        "BiometricLogCleanup: removed {Count} processed logs older than {Days} days for companyId={Company}.",
                        deleted, retentionDays, companyId);
            }

            // Handle logs with no company (legacy / unassigned)
            var defaultCutoff = DateTime.UtcNow.AddDays(-DefaultRetentionDays);
            var nullDeleted   = await db.BiometricLogs
                .Where(l => l.CompanyId == null && l.IsProcessed && l.PunchedAt < defaultCutoff)
                .ExecuteDeleteAsync(ct);

            totalDeleted += nullDeleted;

            _logger.LogInformation(
                "BiometricLogCleanupService cycle complete. Total deleted: {Total}.", totalDeleted);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown — do not log as error.
        }
        catch (Exception ex)
        {
            // Log and continue — the job will retry on the next 24-hour cycle.
            _logger.LogError(ex, "BiometricLogCleanupService encountered an error during cleanup.");
        }
    }
}
