using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Surfaces email delivery problems on the /health endpoint instead of leaving them
/// only in log files. Two things are checked:
///   1. Is SMTP configured at all in Production? (Email:Host set)
///   2. Has the most recent send attempt failed? (set by EmailService)
///
/// Reports "Degraded" rather than "Unhealthy" — the app should keep serving traffic
/// (email is not on the critical path for login/attendance/payroll), but the status
/// should be visible to whoever is watching Docker healthchecks or an uptime monitor.
/// </summary>
public static class EmailHealthCheck
{
    // Set by EmailService on every send attempt; read here. Simple static state is
    // sufficient for a single-process health signal — no need for a database round-trip.
    public static DateTime? LastFailureUtc { get; set; }
    public static string? LastFailureReason { get; set; }
}

public class EmailHealthCheckService : IHealthCheck
{
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;

    public EmailHealthCheckService(IConfiguration config, IHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var host = _config["Email:Host"];

        if (string.IsNullOrWhiteSpace(host) && _env.IsProduction())
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "SMTP is not configured (Email:Host is empty). Password-reset, welcome, and " +
                "leave-decision emails are being dropped, not delivered. Set EMAIL_HOST in .env."));
        }

        if (EmailHealthCheck.LastFailureUtc is { } failedAt &&
            failedAt > DateTime.UtcNow.AddMinutes(-30))
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Most recent email send failed at {failedAt:u}: {EmailHealthCheck.LastFailureReason}"));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            string.IsNullOrWhiteSpace(host) ? "SMTP not configured (non-production)." : "SMTP configured."));
    }
}
