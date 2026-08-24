using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job that prunes old audit logs from the database.
/// 
/// FIX HIGH-4: Audit logs grow unbounded over time, consuming disk space and
/// slowing down queries. This job runs weekly (Sunday 2 AM UTC) and deletes
/// audit logs older than 90 days, keeping the table manageable while retaining
/// recent history for compliance and debugging.
/// 
/// Expected deletions on typical HRMS: 500-2000 rows per run (varies with activity).
/// 
/// Recurring schedule: 0 2 * * 0 (Sunday, 02:00 UTC)
/// </summary>
public class AuditLogPruneJob
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AuditLogPruneJob> _logger;

    private const int RetentionDays = 90;

    public AuditLogPruneJob(ApplicationDbContext db, ILogger<AuditLogPruneJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        try
        {
            _logger.LogInformation("AuditLogPruneJob started.");

            var cutoffDate = DateTime.UtcNow.AddDays(-RetentionDays);

            // Delete audit logs older than the retention period
            var deleted = await _db.AuditLogs
                .Where(a => a.OccurredAt < cutoffDate)
                .ExecuteDeleteAsync();

            _logger.LogInformation(
                "AuditLogPruneJob completed. Deleted {Count} logs older than {Days} days (cutoff: {Date:u}).",
                deleted, RetentionDays, cutoffDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuditLogPruneJob failed.");
            throw;
        }
    }
}
