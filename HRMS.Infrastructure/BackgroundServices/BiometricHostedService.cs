using HRMS.Application.Interfaces.Biometric;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.BackgroundServices;

/// <summary>
/// Automated background service that polls registered biometric devices on a configurable interval.
///
/// FIX-1 (Biometric gap resolution):
/// The service now consults <see cref="IBiometricCapabilityService"/> before attempting any
/// sync and SKIPS providers that are marked as stubs (IsImplemented = false).
/// This prevents the misleading behaviour where the scheduler would call stub providers
/// that returned empty data, creating BiometricSyncHistory records showing "0 records synced"
/// with no indication that the provider was never actually connected to hardware.
///
/// Only ZKTeco (the only fully-implemented provider) is polled. As additional providers
/// gain real SDK implementations they are added to BiometricCapabilityService._implementedVendors
/// and will automatically be picked up here.
///
/// Design:
///   - On each tick, loads all companies that have AutoSyncEnabled = true.
///   - For each company, iterates its ENABLED devices for IMPLEMENTED providers only.
///   - Records a BiometricSyncHistory entry for every sync run.
///   - Respects CancellationToken for clean shutdown.
///   - Uses IServiceScopeFactory for per-tick DI scopes (EF Core contexts are not thread-safe).
/// </summary>
public sealed class BiometricHostedService : BackgroundService
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory            _scopeFactory;
    private readonly ILogger<BiometricHostedService> _logger;
    private readonly IBiometricCapabilityService     _capabilities;

    public BiometricHostedService(
        IServiceScopeFactory            scopeFactory,
        ILogger<BiometricHostedService> logger,
        IBiometricCapabilityService     capabilities)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _capabilities = capabilities;
    }

    // ── Escalating back-off constants ─────────────────────────────────────────
    // When the poll cycle fails N consecutive times (DB outage, network partition, etc.)
    // the service backs off exponentially instead of hammering the DB every 5 minutes.
    // Cap: 60 minutes. After 10+ consecutive failures a critical alert is logged so
    // on-call engineers can act without waiting for a Sentry alert.
    private const int AlertAfterConsecutiveFailures = 10;
    private static readonly TimeSpan BaseRetryDelay  = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxRetryDelay   = TimeSpan.FromMinutes(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // FIX-1: Log capability summary at startup so operators can see at a glance
        // which vendors will be polled and which are awaiting SDK integration.
        var allCaps     = _capabilities.GetAllCapabilities();
        var implemented = _capabilities.GetImplementedVendors();
        var stubs       = allCaps.Where(c => !c.IsImplemented).Select(c => c.VendorName).ToList();

        _logger.LogInformation(
            "[BiometricHostedService] Started. Implemented providers (will poll): [{Implemented}]. " +
            "Stub providers (skipped — awaiting SDK integration): [{Stubs}].",
            string.Join(", ", implemented),
            string.Join(", ", stubs));

        if (implemented.Count == 0)
        {
            _logger.LogWarning(
                "[BiometricHostedService] No biometric providers are fully implemented. " +
                "Background polling is disabled. Integrate at least one vendor SDK and mark it " +
                "as IsImplemented=true in BiometricCapabilityService to enable auto-sync.");
            return;  // Nothing to poll — exit the background service cleanly
        }

        // Give the API 2 minutes to finish startup before the first poll
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        // FIX (HIGH): Consecutive-failure counter drives escalating exponential back-off.
        // A persistent DB/network outage previously caused the service to retry every
        // 5 minutes indefinitely with no back-off and no operator alert.
        int consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan nextInterval;
            try
            {
                nextInterval        = await RunPollCycleAsync(stoppingToken);
                consecutiveFailures = 0;   // reset on success
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;

                // Exponential back-off: 1 min, 2 min, 4 min … capped at 60 min
                var backoff = TimeSpan.FromTicks(
                    Math.Min(
                        BaseRetryDelay.Ticks * (long)Math.Pow(2, consecutiveFailures - 1),
                        MaxRetryDelay.Ticks));

                if (consecutiveFailures >= AlertAfterConsecutiveFailures)
                {
                    // CRITICAL alert — log at Error so Sentry / alerting picks it up
                    _logger.LogCritical(ex,
                        "[BiometricHostedService] PERSISTENT FAILURE — {Count} consecutive poll " +
                        "cycle errors. Service is backing off for {Backoff:g}. " +
                        "Investigate DB connectivity, Redis, and biometric network paths.",
                        consecutiveFailures, backoff);
                }
                else
                {
                    _logger.LogError(ex,
                        "[BiometricHostedService] Poll cycle failed ({Count} consecutive). " +
                        "Retrying in {Backoff:g}.",
                        consecutiveFailures, backoff);
                }

                nextInterval = backoff;
            }

            await Task.Delay(nextInterval, stoppingToken);
        }

        _logger.LogInformation("[BiometricHostedService] Stopped.");
    }

    /// <summary>
    /// One poll cycle: load all companies with auto-sync enabled, sync each IMPLEMENTED device.
    /// Returns the interval to wait before the next cycle.
    /// </summary>
    private async Task<TimeSpan> RunPollCycleAsync(CancellationToken ct)
    {
        using var scope     = _scopeFactory.CreateScope();
        var db              = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var syncService     = scope.ServiceProvider.GetRequiredService<IBiometricSyncService>();
        var historyRepo     = scope.ServiceProvider.GetRequiredService<IBiometricSyncHistoryRepository>();
        var deviceRepo      = scope.ServiceProvider.GetRequiredService<IBiometricDeviceRepository>();

        // Only consider vendors with a real implementation
        var implementedVendors = _capabilities.GetImplementedVendors()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Load companies with auto-sync enabled
        var settings = await db.Set<BiometricSettings>()
            .Where(s => s.AutoSyncEnabled)
            .ToListAsync(ct);

        if (!settings.Any())
        {
            _logger.LogDebug(
                "[BiometricHostedService] No companies have AutoSyncEnabled=true. Skipping cycle.");
            return DefaultInterval;
        }

        var now      = DateTime.UtcNow;
        var from     = now.AddHours(-1);   // fetch the last hour on each tick
        var minNextInterval = DefaultInterval;

        foreach (var companySetting in settings)
        {
            ct.ThrowIfCancellationRequested();

            // Load devices for this company that use an implemented provider
            var devices = await deviceRepo.GetAllAsync(companySetting.CompanyId ?? 0, ct);
            var eligibleDevices = devices
                .Where(d => d.IsEnabled && implementedVendors.Contains(d.ProviderType.ToString()))
                .ToList();

            if (!eligibleDevices.Any())
            {
                _logger.LogDebug(
                    "[BiometricHostedService] Company {CompanyId}: no enabled devices for implemented providers. " +
                    "Registered stubs are intentionally skipped.",
                    companySetting.CompanyId);
                continue;
            }

            foreach (var device in eligibleDevices)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var synced = await syncService.SyncAttendanceAsync(
                        device.ProviderType.ToString(),
                        companySetting.CompanyId ?? 0,
                        from, now, ct);

                    _logger.LogInformation(
                        "[BiometricHostedService] Company {CompanyId}, Device {DeviceId} ({Vendor}): " +
                        "synced {Count} punch record(s).",
                        companySetting.CompanyId, device.Id, device.ProviderType, synced);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[BiometricHostedService] Company {CompanyId}, Device {DeviceId} ({Vendor}): sync failed.",
                        companySetting.CompanyId, device.Id, device.ProviderType);
                }
            }

            // Use the per-company interval if configured; otherwise use the default
            if (companySetting.SyncIntervalMinutes > 0)
            {
                var companyInterval = TimeSpan.FromMinutes(companySetting.SyncIntervalMinutes);
                if (companyInterval < minNextInterval)
                    minNextInterval = companyInterval;
            }
        }

        return minNextInterval;
    }
}
