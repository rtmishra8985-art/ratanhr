using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using HRMS.Infrastructure.Security;

namespace HRMS.API.Controllers.Logo;

[ApiController]
[Route("api/logo")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class LogoController : BaseController
{
    private readonly ICompanyService _companyService;

    public LogoController(ICompanyService companyService) => _companyService = companyService;

    // ── IDOR guard ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the route {companyId} matches the caller's JWT companyId claim.
    /// Superadmins bypass this check (unrestricted cross-tenant access).
    /// </summary>
    private bool CallerOwnsCompany(int companyId)
    {
        if (User.IsInRole(AppRoles.SuperAdmin)) return true;
        return int.TryParse(User.FindFirst("companyId")?.Value, out int cid) && cid == companyId;
    }

    // ── Endpoints ──────────────────────────────────────────────────────────

    /// <summary>Upload logo for a company</summary>
    [HttpPost("{companyId:int}")]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [EnableRateLimiting("upload")]   // BLOCKER-11: 20 uploads/min per IP
    public async Task<IActionResult> Upload(int companyId, [FromForm] UploadLogoRequest request)
    {
        if (!CallerOwnsCompany(companyId))
            return Forbid();

        var logo = request?.Logo;
        // Audit item 9 — MimeValidator (signature-only) replaced by the shared
        // UploadValidator (Image profile), which additionally enforces the extension
        // allow-list, extension/MIME agreement and the size ceiling, and returns a
        // caller-safe message. CompanyService re-validates on the persistence path.
        var upload = UploadValidator.Validate(logo, UploadProfile.Image);
        if (!upload.IsValid) return BadRequest(ApiResponse.Fail(upload.Error!));
        var ok = await _companyService.UpdateLogoAsync(companyId, logo!);
        return ok ? Ok(ApiResponse.Ok("Logo uploaded successfully."))
                  : NotFound(ApiResponse.Fail("Company not found."));
    }
}

/// <summary>
/// Multipart form payload for <see cref="LogoController.Upload"/>.
/// Swashbuckle cannot generate an operation for a bare [FromForm] IFormFile
/// parameter; wrapping the file in a form DTO produces a correct
/// multipart/form-data request body schema.
/// </summary>
public sealed class UploadLogoRequest
{
    /// <summary>The company logo image file.</summary>
    public IFormFile? Logo { get; set; }
}
