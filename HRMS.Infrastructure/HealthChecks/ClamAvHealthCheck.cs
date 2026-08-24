using Microsoft.Extensions.Diagnostics.HealthChecks;
using nClam;

namespace HRMS.Infrastructure.HealthChecks;

/// <summary>
/// ASP.NET Core health check that probes ClamAV TCP availability.
///
/// Registered as both a liveness check (name: "clamav") and a readiness tag
/// so <c>/healthz/ready</c> reports ClamAV status separately from /health.
///
/// PHASE 2 (P2-CLAM): ClamAV is MANDATORY in Production. File uploads are
/// rejected whenever ClamAV is unavailable regardless of environment, so this
/// check being Unhealthy means the upload subsystem is also unavailable.
/// </summary>
public sealed class ClamAvHealthCheck : IHealthCheck
{
    private readonly ClamAvOptions _options;

    public ClamAvHealthCheck(ClamAvOptions options)
        => _options = options;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var client = new ClamClient(_options.Host, _options.Port);
            var pong   = await client.PingAsync();

            return pong
                ? HealthCheckResult.Healthy(
                    $"ClamAV reachable at {_options.Host}:{_options.Port}.")
                : HealthCheckResult.Degraded(
                    $"ClamAV at {_options.Host}:{_options.Port} did not respond to PING.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                $"ClamAV health check timed out after {_options.TimeoutSeconds}s. " +
                "File uploads will be rejected until ClamAV is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"ClamAV unreachable at {_options.Host}:{_options.Port}: {ex.Message}. " +
                "File uploads will be rejected until ClamAV is reachable.",
                ex);
        }
    }
}

/// <summary>Configuration for <see cref="ClamAvHealthCheck"/>.</summary>
public sealed class ClamAvOptions
{
    public string Host           { get; init; } = "clamav";
    public int    Port           { get; init; } = 3310;
    public int    TimeoutSeconds { get; init; } = 5;
}
