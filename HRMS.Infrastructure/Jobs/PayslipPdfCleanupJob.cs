using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job that deletes payslip PDF files older than
/// <see cref="MaxAgeHours"/> hours from the output directory.
///
/// Register once at startup:
///   RecurringJob.AddOrUpdate&lt;PayslipPdfCleanupJob&gt;(
///       "payslip-pdf-cleanup",
///       j => j.RunAsync(),
///       Cron.Hourly);
/// </summary>
public class PayslipPdfCleanupJob
{
    /// <summary>Delete files not accessed within this many hours.</summary>
    private const int MaxAgeHours = 24;

    private readonly ILogger<PayslipPdfCleanupJob> _log;

    public PayslipPdfCleanupJob(ILogger<PayslipPdfCleanupJob> log) => _log = log;

    public Task RunAsync()
    {
        // Find all known output directories — walk each WebRoot variant in case the
        // host is configured with a non-default wwwroot path.
        var dirsToScan = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", PayslipPdfJob.OutputSubDir),
        };

        var cutoff  = DateTime.UtcNow.AddHours(-MaxAgeHours);
        var deleted = 0;

        foreach (var dir in dirsToScan)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.GetFiles(dir, "*.pdf"))
            {
                try
                {
                    var lastWrite = File.GetLastWriteTimeUtc(file);
                    if (lastWrite < cutoff)
                    {
                        File.Delete(file);
                        deleted++;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "PayslipPdfCleanupJob: could not delete {File}", file);
                }
            }
        }

        _log.LogInformation("PayslipPdfCleanupJob: deleted {Count} expired PDF(s) (older than {Hours}h)",
            deleted, MaxAgeHours);
        return Task.CompletedTask;
    }
}
