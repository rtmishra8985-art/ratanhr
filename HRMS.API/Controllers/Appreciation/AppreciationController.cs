using FluentValidation;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Appreciation;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using HRMS.Infrastructure.Security;

namespace HRMS.API.Controllers.Appreciation;

[ApiController]
[Route("api/appreciation")]
[Authorize(Policy = "RequireMfaCompleted")]
public class AppreciationController : BaseController
{
    private readonly IAppreciationService                  _service;
    private readonly IValidator<UploadAppreciationDto>     _validator;

    public AppreciationController(
        IAppreciationService              service,
        IValidator<UploadAppreciationDto> validator)
    {
        _service   = service;
        _validator = validator;
    }

    /// <summary>
    /// Upload appreciation document for an employee.
    /// FIX 6: Now uses typed UploadAppreciationDto + FluentValidation for consistent
    ///        validation, error responses, and max-file-size / extension enforcement.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [FromForm] string  employeeId,
        [FromForm] string? message,
        IFormFile?         file)
    {
        // Audit item 9 — signature/extension/MIME/size gate before anything else.
        // Appreciation attachments are images only (Image profile — this matches
        // AppreciationService, which persists via FileStorageService with the same
        // profile and would otherwise reject a PDF only after the DTO validator had
        // accepted it). The attachment is optional (required: false): a text-only
        // appreciation is still valid. A signature mismatch, spoofed extension or
        // oversized file returns HTTP 400 with the reason.
        var upload = UploadValidator.Validate(file, UploadProfile.Image, required: false);
        if (!upload.IsValid) return BadRequest(ApiResponse.Fail(upload.Error!));

        // Build the typed DTO so the validator can inspect it.
        var dto = new UploadAppreciationDto
        {
            EmployeeId    = employeeId,
            Message       = message,
            FileSize      = file?.Length,
            FileExtension = file != null ? Path.GetExtension(file.FileName) : null
        };

        var result = await _validator.ValidateAsync(dto);
        if (!result.IsValid)
            return BadRequest(ApiResponse.Fail(
                string.Join(", ", result.Errors.Select(e => e.ErrorMessage))));

        // Use the TryParse-safe UserId property from BaseController instead of
        // int.Parse which throws FormatException when the claim is absent or malformed.
        if (UserId == 0) return Unauthorized();
        var id = await _service.UploadAsync(employeeId, message, file, UserId);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { Id = id }, "Appreciation uploaded."));
    }

    /// <summary>Get single appreciation by ID</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> GetById(int id)
    {
        // FIX [2] IDOR — pass CallerCompanyIdOrNull so service enforces tenant ownership.
        var item = await _service.GetByIdAsync(id, CallerCompanyIdOrNull);
        if (item == null) return NotFound(ApiResponse.Fail("Appreciation not found."));
        return Ok(ApiResponse<AppreciationDto>.Ok(item));
    }

    /// <summary>Get all appreciations (admin/superadmin, optionally scoped to company) — paginated</summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 25)
    {
        var companyIdStr = User.FindFirst("companyId")?.Value;
        int? companyId = User.IsInRole(AppRoles.SuperAdmin) ? null
            : int.TryParse(companyIdStr, out var c) ? c : null;

        var result = await _service.GetAllPagedAsync(companyId, page, pageSize);
        return Ok(ApiResponse<PagedResult<AppreciationDto>>.Ok(result));
    }

    /// <summary>Employee – get own appreciations</summary>
    [HttpGet("my")]
    [Authorize(Roles = AppRoles.Employee)]
    public async Task<IActionResult> GetMyAppreciations()
    {
        var empId = User.FindFirst("employeeId")?.Value;
        if (string.IsNullOrEmpty(empId)) return Unauthorized();
        var list = await _service.GetByEmployeeAsync(empId);
        return Ok(ApiResponse<List<AppreciationDto>>.Ok(list));
    }

    /// <summary>Delete an appreciation record (admin/superadmin)</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        // FIX [2] IDOR — pass CallerCompanyIdOrNull so service enforces tenant ownership.
        var ok = await _service.DeleteAsync(id, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Appreciation deleted."))
                  : NotFound(ApiResponse.Fail("Appreciation not found."));
    }
}
