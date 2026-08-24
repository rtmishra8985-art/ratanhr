using HRMS.Infrastructure.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using nClam;

namespace HRMS.Infrastructure.Services;

// BLOCKER-1 FIX: IClamAvVirusScanService was referenced in EmployeeDocumentService
// and EmployeeDocumentIDORTests but was never declared anywhere in the solution.
// Defining it here, co-located with its sole implementation, because:
//   - ClamAvOptions lives in HRMS.Infrastructure (HealthChecks), so this is an
//     infrastructure concern, not an application abstraction.
//   - Both callers (EmployeeDocumentService, tests) already reference HRMS.Infrastructure.
//   - Keeping interface + implementation in the same namespace avoids adding an
//     Application-layer dependency on a third-party daemon concern (ClamAV).
public interface IClamAvVirusScanService
{
    /// <summary>
    /// Scans <paramref name="fileStream"/> for viruses.
    /// Returns a clean result when no threat is found.
    /// Throws <see cref="ClamAvUnavailableException"/> in Production when ClamAV
    /// is unreachable (fail-closed).
    /// </summary>
    Task<ScanResult> ScanAsync(Stream fileStream, string fileName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// ClamAV virus scan service.
///
/// PHASE 2 (P2-CLAM): fail-closed in all environments.
///
/// • The service is NOT optional in Production. If ClamAV is unreachable,
///   <see cref="ScanAsync"/> throws <see cref="ClamAvUnavailableException"/>
///   and the upload must be rejected by the caller.
/// • In Development, a connection failure is logged as a warning but the scan
///   result is returned as clean with <c>IsDevelopmentBypass = true</c> so
///   local development can proceed without a ClamAV daemon.
/// • The Production/Staging path never bypasses the scan.
///
/// The comment "ClamAV is optional" that previously appeared in this file and in
/// docker-compose.prod.yml has been removed (Phase 2 fix).
/// </summary>
public class ClamAvVirusScanService : IClamAvVirusScanService
{
    private readonly ClamAvOptions         _options;
    private readonly IHostEnvironment      _env;
    private readonly ILogger<ClamAvVirusScanService> _logger;

    public ClamAvVirusScanService(
        ClamAvOptions    options,
        IHostEnvironment env,
        ILogger<ClamAvVirusScanService> logger)
    {
        _options = options;
        _env     = env;
        _logger  = logger;
    }

    /// <inheritdoc />
    public async Task<ScanResult> ScanAsync(Stream fileStream, string fileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds + 5));

            var client = new ClamClient(_options.Host, _options.Port);
            var result = await client.SendAndScanFileAsync(fileStream);

            return result.Result switch
            {
                ClamScanResults.Clean =>
                    new ScanResult(IsClean: true,  Threat: null),

                ClamScanResults.VirusDetected =>
                    new ScanResult(IsClean: false, Threat: result.InfectedFiles?.FirstOrDefault()?.VirusName ?? "Unknown"),

                ClamScanResults.Error =>
                    throw new ClamAvScanErrorException(
                        $"ClamAV returned an error while scanning '{fileName}': {result.RawResult}"),

                _ =>
                    throw new ClamAvScanErrorException(
                        $"Unexpected ClamAV result '{result.Result}' for file '{fileName}'."),
            };
        }
        catch (Exception ex) when (ex is not ClamAvScanErrorException and not ClamAvUnavailableException)
        {
            if (_env.IsDevelopment())
            {
                _logger.LogWarning(ex,
                    "[ClamAV] Development bypass: ClamAV unreachable for file '{File}'. " +
                    "Returning clean result. This MUST NOT happen in Production.",
                    fileName);
                return new ScanResult(IsClean: true, Threat: null, IsDevelopmentBypass: true);
            }

            // Production / Staging — fail closed.
            _logger.LogCritical(ex,
                "[ClamAV] Scan failed for '{File}'. File upload will be rejected. " +
                "ClamAV host: {Host}:{Port}",
                fileName, _options.Host, _options.Port);

            throw new ClamAvUnavailableException(
                $"ClamAV is not reachable. File '{fileName}' cannot be accepted. " +
                "Contact the system administrator.", ex);
        }
    }
}

// ── Result and exception types ────────────────────────────────────────────────

/// <param name="IsClean">True when no threat was found.</param>
/// <param name="Threat">Threat name if detected; <c>null</c> when clean.</param>
/// <param name="IsDevelopmentBypass">
/// True when the result was produced without an actual scan (Development only).
/// Always <c>false</c> in Production/Staging.
/// </param>
public sealed record ScanResult(bool IsClean, string? Threat, bool IsDevelopmentBypass = false);

/// <summary>ClamAV daemon is unreachable. Uploads must be rejected.</summary>
public sealed class ClamAvUnavailableException : Exception
{
    public ClamAvUnavailableException(string message, Exception? inner = null)
        : base(message, inner) { }
}

/// <summary>ClamAV returned an error result (not a connection failure).</summary>
public sealed class ClamAvScanErrorException : Exception
{
    public ClamAvScanErrorException(string message) : base(message) { }
}
