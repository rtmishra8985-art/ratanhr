using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces.Biometric;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace HRMS.API.Controllers.Attendance;

/// <summary>
/// Biometric device integration endpoints.
/// Supports ZKTeco, eSSL, Matrix, Suprema, Anviz, Hikvision, and future vendors
/// via the pluggable <see cref="IBiometricProviderFactory"/> pattern.
///
/// PHASE 2 (P2-BIO-REALTIME): Realtime vendor integration is DEFERRED.
/// See <c>Biometric/BIOMETRIC_RELEASE_DECISION.md</c> for the release decision.
/// The /realtime endpoint is gated behind a feature flag and returns HTTP 501
/// with a structured response when the flag is disabled (default: disabled).
/// The UI biometric-realtime.html entry point has been hidden from the sidebar.
/// </summary>
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[Route("api/biometric")]
public class BiometricController : BaseController
{
    private readonly IBiometricSyncService        _sync;
    private readonly IBiometricProviderFactory    _factory;
    private readonly IBiometricDeviceService      _deviceService;
    private readonly ILogger<BiometricController> _logger;
    private readonly IConfiguration               _config;

    public BiometricController(
        IBiometricSyncService        sync,
        IBiometricProviderFactory    factory,
        IBiometricDeviceService      deviceService,
        ILogger<BiometricController> logger,
        IConfiguration               config)
    {
        _sync          = sync;
        _factory       = factory;
        _deviceService = deviceService;
        _logger        = logger;
        _config        = config;
    }

    // ── Feature flag helper ───────────────────────────────────────────────────

    /// <summary>
    /// Returns true only when the Realtime biometric feature flag is explicitly enabled.
    /// Default: disabled. Set <c>Features:BiometricRealtime=true</c> in config when
    /// the Realtime SDK integration is complete and tested.
    /// </summary>
    private bool RealtimeEnabled =>
        _config.GetValue<bool>("Features:BiometricRealtime");

    // ── Company-ID guard ───────────────────────────────
    //
    // FIX (audit gap): BaseController.CompanyId returns -1 when the companyId JWT
    // claim is absent or unparseable (SuperAdmin tokens have no companyId claim by
    // design). Sync/GetSettings/UpdateSettings/GetDashboard/GetRealtime previously
    // passed this raw -1 sentinel straight into IBiometricDeviceService/
    // IBiometricSyncService methods, which take a non-nullable `int companyId` with
    // no "unrestricted" escape hatch (unlike, e.g., RecruitmentController's
    // CallerCompanyIdOrNull pattern) — so a SuperAdmin calling these endpoints
    // without first impersonating a tenant got a silent, misleading result (empty
    // dashboard, a settings/device row filtered on the impossible company_id=-1)
    // instead of a clear error. This mirrors the explicit-403 guard pattern already
    // used by AssetsController.TryGetCompanyId() for the same class of module
    // (no cross-tenant SuperAdmin view is architecturally supported here —
    // SuperAdmin must impersonate a specific tenant before accessing biometric data).
    private bool TryGetCompanyId(out int companyId)
    {
        companyId = CompanyId;   // BaseController.CompanyId returns -1 on failure
        return companyId != -1;
    }

    // ── Existing endpoints (preserved) ───────────────────────────────────────

    [HttpGet("providers")]
    public ActionResult<ApiResponse<IReadOnlyList<string>>> GetProviders()
        => Ok(ApiResponse<IReadOnlyList<string>>.Ok(_factory.RegisteredVendors));

    [HttpGet("vendors")]
    public ActionResult<ApiResponse<IReadOnlyList<string>>> GetVendors()
        => Ok(ApiResponse<IReadOnlyList<string>>.Ok(_factory.RegisteredVendors));

    [HttpGet("status/{vendor}")]
    [ProducesResponseType(typeof(ApiResponse<BiometricDeviceStatus>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status501NotImplemented)]
    public async Task<ActionResult<ApiResponse<BiometricDeviceStatus>>> GetStatus(
        string vendor, CancellationToken ct)
    {
        try
        {
            var status = await _sync.GetDeviceStatusAsync(vendor, ct);
            return Ok(ApiResponse<BiometricDeviceStatus>.Ok(status));
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(
                "[Biometric] GetStatus requested for unregistered vendor '{Vendor}'. " +
                "Registered: {Registered}. Error: {Message}",
                vendor, string.Join(", ", _factory.RegisteredVendors), ex.Message);

            return StatusCode(StatusCodes.Status501NotImplemented,
                ApiResponse.Fail(
                    "Biometric hardware integration is not yet available for this vendor. " +
                    "Contact support to enable this feature."));
        }
    }

    [HttpPost("sync")]
    [ProducesResponseType(typeof(ApiResponse<BiometricSyncResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<BiometricSyncResultDto>>> Sync(
        [FromBody] BiometricSyncRequest request, CancellationToken ct)
    {
        if (!TryGetCompanyId(out var cid))
            return Forbid();
        try
        {
            var count = await _sync.SyncAttendanceAsync(
                request.Vendor, cid, request.From, request.To, ct);
            // BLOCKER-1 FIX: SyncAttendanceAsync returns int (records synced).
            // Wrap in BiometricSyncResultDto so the API response is structured.
            return Ok(ApiResponse<BiometricSyncResultDto>.Ok(new BiometricSyncResultDto(count)));
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(
                "[Biometric] Sync requested for unregistered vendor '{Vendor}'. Error: {Message}",
                request.Vendor, ex.Message);

            return StatusCode(StatusCodes.Status501NotImplemented,
                ApiResponse.Fail(
                    "Biometric sync is not implemented for this vendor. " +
                    "Check /api/biometric/capabilities for implemented providers."));
        }
    }

    [HttpGet("settings")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<BiometricSettingsDto>>> GetSettings(CancellationToken ct)
    {
        if (!TryGetCompanyId(out var cid))
            return Forbid();
        var settings = await _deviceService.GetSettingsAsync(cid, ct);
        return Ok(ApiResponse<BiometricSettingsDto>.Ok(settings));
    }

    [HttpPut("settings")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<BiometricSettingsDto>>> UpdateSettings(
        [FromBody] UpdateBiometricSettingsDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail("Invalid request"));
        if (!TryGetCompanyId(out var cid))
            return Forbid();
        var settings = await _deviceService.UpdateSettingsAsync(cid, dto, ct);
        return Ok(ApiResponse<BiometricSettingsDto>.Ok(settings, "Settings updated."));
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<BiometricDashboardDto>>> GetDashboard(CancellationToken ct)
    {
        if (!TryGetCompanyId(out var cid))
            return Forbid();
        var dashboard = await _deviceService.GetDashboardAsync(cid, ct);
        return Ok(ApiResponse<BiometricDashboardDto>.Ok(dashboard));
    }

    // ── Realtime endpoint — gated by feature flag ─────────────────────────────

    /// <summary>
    /// Realtime device status snapshot.
    ///
    /// PHASE 2 (P2-BIO-REALTIME): This endpoint is DISABLED until the Realtime
    /// SDK integration is complete. See Biometric/BIOMETRIC_RELEASE_DECISION.md.
    ///
    /// When disabled (default), returns HTTP 501 with a structured message.
    /// The UI sidebar entry for realtime monitoring has been hidden (sidebar-admin.html).
    /// Enable by setting <c>Features:BiometricRealtime=true</c> in appsettings when
    /// the integration is complete, tested, and signed off.
    /// </summary>
    [HttpGet("realtime")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BiometricDeviceStatusDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BiometricDeviceStatusDto>>>> GetRealtime(
        CancellationToken ct)
    {
        if (!RealtimeEnabled)
        {
            _logger.LogWarning(
                "[Biometric] Realtime endpoint called but Features:BiometricRealtime=false. " +
                "CompanyId={CompanyId} UserId={UserId}. " +
                "This feature is deferred — see Biometric/BIOMETRIC_RELEASE_DECISION.md.",
                CompanyId, UserId);

            return StatusCode(StatusCodes.Status501NotImplemented,
                ApiResponse.Fail(
                    "Realtime biometric monitoring is not available in this release. " +
                    "This feature requires completion of the Realtime SDK integration. " +
                    "Expected: see BIOMETRIC_RELEASE_DECISION.md for the target release."));
        }

        if (!TryGetCompanyId(out var cid))
            return Forbid();

        // Feature flag is enabled — serve live data.
        var dashboard = await _deviceService.GetDashboardAsync(cid, ct);
        return Ok(ApiResponse<IReadOnlyList<BiometricDeviceStatusDto>>.Ok(
            dashboard.DeviceStatuses));
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed class BiometricSyncRequest
{
    public string   Vendor { get; set; } = string.Empty;
    public DateTime From   { get; set; }
    public DateTime To     { get; set; }
}
