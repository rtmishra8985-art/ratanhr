using HRMS.Application.Interfaces.Biometric;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// Realtime biometric device provider — DISABLED by default.
///
/// Status: Stub. Full implementation requires the Realtime Biometrics SDK or HTTP API.
///
/// To enable this provider:
///   1. Set Biometric__EnableRealtime=true in environment variables.
///   2. Provide connection details: Biometric__RealtimeHost, Biometric__RealtimePort.
///   3. Obtain the Realtime SDK or REST API credentials from https://www.realtime.co.in/.
///   4. Replace the method bodies below with real SDK/HTTP calls.
///   5. Add "Realtime" to BiometricCapabilityService._implementedVendors.
///
/// While disabled, this provider:
///   - Returns an empty punch log list from FetchLogsAsync (no fake attendance data).
///   - Returns 0 from SyncUsersAsync.
///   - Returns IsOnline=false from GetDeviceStatusAsync.
///   - Is excluded from BiometricHostedService auto-polling.
///   - The /api/biometric/sync and /api/biometric/status/realtime endpoints return HTTP 501.
///
/// DO NOT set IsImplemented=true in BiometricCapabilityService until this provider is fully
/// implemented and tested — doing so will enable auto-polling with empty results.
/// </summary>
public sealed class RealtimeProvider : IBiometricProvider
{
    private readonly ILogger<RealtimeProvider> _logger;
    private readonly bool _enabled;

    /// <summary>
    /// Configuration keys:
    ///   Biometric:EnableRealtime  — set to "true" to activate (default: false).
    ///   Biometric:RealtimeHost   — hostname/IP of the Realtime device or server.
    ///   Biometric:RealtimePort   — TCP port (default varies by model).
    /// </summary>
    public RealtimeProvider(
        ILogger<RealtimeProvider> logger,
        IConfiguration configuration)
    {
        _logger  = logger;
        _enabled = configuration.GetValue<bool>("Biometric:EnableRealtime", defaultValue: false);

        if (_enabled)
        {
            logger.LogWarning(
                "[Realtime] Provider is ENABLED via Biometric:EnableRealtime=true but the " +
                "Realtime SDK is not yet integrated. All calls will return empty/stub data. " +
                "Complete the integration before using this provider in production.");
        }
    }

    public string VendorName => "Realtime";

    /// <inheritdoc />
    public Task<IReadOnlyList<BiometricPunchLog>> FetchLogsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            _logger.LogDebug(
                "[Realtime] FetchLogsAsync skipped — provider is disabled " +
                "(Biometric:EnableRealtime=false). Returning empty log set.");
        }
        else
        {
            _logger.LogWarning(
                "[Realtime] FetchLogsAsync called but the Realtime SDK is not integrated. " +
                "Returning empty log set. Complete the Realtime integration to enable this provider.");
        }
        return Task.FromResult<IReadOnlyList<BiometricPunchLog>>(Array.Empty<BiometricPunchLog>());
    }

    /// <inheritdoc />
    public Task<int> SyncUsersAsync(
        IReadOnlyList<BiometricUser> users, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            _logger.LogDebug(
                "[Realtime] SyncUsersAsync skipped — provider is disabled " +
                "(Biometric:EnableRealtime=false). Returning 0.");
        }
        else
        {
            _logger.LogWarning(
                "[Realtime] SyncUsersAsync called but the Realtime SDK is not integrated. " +
                "Returning 0. Complete the Realtime integration to enable this provider.");
        }
        return Task.FromResult(0);
    }

    /// <inheritdoc />
    public Task<BiometricDeviceStatus> GetDeviceStatusAsync(CancellationToken ct = default)
    {
        var message = _enabled
            ? "Realtime provider is enabled via config but NOT yet integrated — " +
              "implement the SDK calls before setting IsImplemented=true in BiometricCapabilityService."
            : "Realtime provider is disabled (Biometric:EnableRealtime=false). " +
              "Set Biometric__EnableRealtime=true and complete the SDK integration to activate.";

        return Task.FromResult(new BiometricDeviceStatus(
            IsOnline:           false,
            FirmwareVersion:    null,
            EnrolledUserCount:  null,
            LastError:          message));
    }
}
