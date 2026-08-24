using HRMS.Application.Common;
using HRMS.Application.Interfaces.Biometric;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HRMS.API.Controllers.Attendance;

/// <summary>
/// Biometric provider capabilities — reports which hardware vendors are fully implemented
/// versus which are stubs awaiting SDK integration.
///
/// FIX-1: This endpoint resolves the production readiness gap where stub providers
/// silently returned empty attendance data with no indication to operators.
/// The UI should call this endpoint on load and display an integration-status banner
/// for any provider that IsImplemented = false.
/// </summary>
[ApiController]
[Route("api/biometric/capabilities")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[Produces("application/json")]
public class BiometricCapabilitiesController : ControllerBase
{
    private readonly IBiometricCapabilityService _capabilities;

    public BiometricCapabilitiesController(IBiometricCapabilityService capabilities)
        => _capabilities = capabilities;

    /// <summary>
    /// Returns capability status for every registered biometric provider.
    ///
    /// Providers with <c>isImplemented: true</c> support real hardware sync.
    /// Providers with <c>isImplemented: false</c> are stubs — they return empty data
    /// and are skipped by the background sync scheduler. The <c>pendingIntegration</c>
    /// field describes the SDK or API required to complete that provider.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        OperationId = "GetBiometricCapabilities",
        Summary     = "List biometric provider implementation status",
        Tags        = new[] { "Biometric" })]
    [ProducesResponseType(typeof(ApiResponse<CapabilitiesResponseDto>), StatusCodes.Status200OK)]
    public IActionResult GetCapabilities()
    {
        var all = _capabilities.GetAllCapabilities();
        var dto = new CapabilitiesResponseDto(
            ImplementedCount: all.Count(c => c.IsImplemented),
            StubCount:        all.Count(c => !c.IsImplemented),
            Providers:        all.Select(c => new ProviderCapabilityDto(
                VendorName:          c.VendorName,
                IsImplemented:       c.IsImplemented,
                StatusDescription:   c.StatusDescription,
                PendingIntegration:  c.PendingIntegration
            )).ToList()
        );

        return Ok(ApiResponse<CapabilitiesResponseDto>.Ok(dto,
            dto.StubCount > 0
                ? $"{dto.ImplementedCount} provider(s) active; {dto.StubCount} provider(s) require SDK integration before they can sync hardware."
                : "All biometric providers are fully implemented."));
    }

    /// <summary>
    /// Returns capability status for a single vendor.
    /// Returns 404 if the vendor is not registered.
    /// </summary>
    [HttpGet("{vendorName}")]
    [SwaggerOperation(
        OperationId = "GetBiometricCapabilityByVendor",
        Summary     = "Get capability status for a specific vendor",
        Tags        = new[] { "Biometric" })]
    [ProducesResponseType(typeof(ApiResponse<ProviderCapabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetByVendor(string vendorName)
    {
        var cap = _capabilities.GetCapability(vendorName);
        if (cap is null)
            return NotFound(ApiResponse.Fail($"Biometric vendor '{vendorName}' is not registered."));

        return Ok(ApiResponse<ProviderCapabilityDto>.Ok(new ProviderCapabilityDto(
            VendorName:         cap.VendorName,
            IsImplemented:      cap.IsImplemented,
            StatusDescription:  cap.StatusDescription,
            PendingIntegration: cap.PendingIntegration
        )));
    }
}

// ── Response DTOs ──────────────────────────────────────────────────────────

public sealed record CapabilitiesResponseDto(
    int ImplementedCount,
    int StubCount,
    IReadOnlyList<ProviderCapabilityDto> Providers);

public sealed record ProviderCapabilityDto(
    string  VendorName,
    bool    IsImplemented,
    string  StatusDescription,
    string? PendingIntegration);
