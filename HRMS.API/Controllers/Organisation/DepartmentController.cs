using HRMS.Application.Common;
using HRMS.Application.DTOs.Department;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Organisation;

/// <summary>Department &amp; Designation master data management.</summary>
[ApiController]
[Route("api/organisation")]
[Authorize(Policy = "RequireMfaCompleted")]
public class DepartmentController : BaseController
{
    private readonly IDepartmentService _svc;
    public DepartmentController(IDepartmentService svc) => _svc = svc;

    // FIX: Removed `private new int? CompanyId` shadow.
    // The shadow returned null when the companyId claim was absent.
    // Services treat null as the superadmin "unrestricted" sentinel, so a
    // non-superadmin user with a missing/malformed claim would get cross-company
    // read/write access. BaseController.CallerCompanyIdOrNull handles this
    // correctly: it returns null ONLY for users whose IsInRole(AppRoles.SuperAdmin) is
    // true, and -1 (safe no-match sentinel) for all others with a missing claim.

    // ── Departments ────────────────────────────────────────────────────────

    /// <summary>
    /// List all active departments for the caller's company (paginated + sortable).
    /// FIX 5: Added sortBy and sortDirection query parameters.
    /// Allowed sort columns: Name, Description, IsActive, CreatedAt.
    /// </summary>
    [HttpGet("departments")]
    [HttpGet("/api/departments")]  // alias: canonical short path used by clients/tests
    public async Task<IActionResult> GetDepartments(
        [FromQuery] int     page          = 1,
        [FromQuery] int     pageSize      = 25,
        [FromQuery] string? sortBy        = null,
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] string? search        = null)
    {
        var companyId = CallerCompanyIdOrNull;
        var result = await _svc.GetDepartmentsPagedAsync(
            companyId, page, pageSize, sortBy, sortDirection, search);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<DepartmentDto>>.Ok(result));
    }

    /// <summary>Get a department by ID.</summary>
    [HttpGet("departments/{id:int}")]
    [HttpGet("/api/departments/{id:int}")]  // alias: canonical short path used by clients/tests
    public async Task<IActionResult> GetDepartment(int id)
    {
        // FIX [2] IDOR — pass CallerCompanyIdOrNull to enforce tenant check in service.
        var d = await _svc.GetDepartmentByIdAsync(id, CallerCompanyIdOrNull);
        return d == null
            ? NotFound(ApiResponse.Fail("Department not found."))
            : Ok(ApiResponse<DepartmentDto>.Ok(d));
    }

    /// <summary>Create a department (admin/superadmin).</summary>
    [HttpPost("departments")]
    [HttpPost("/api/departments")]  // alias: canonical short path used by clients/tests
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        var companyId = CallerCompanyIdOrNull;
        var d = await _svc.CreateDepartmentAsync(companyId, dto);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created, ApiResponse<DepartmentDto>.Ok(d, "Department created."));
    }

    /// <summary>Update a department (admin/superadmin).</summary>
    [HttpPut("departments/{id:int}")]
    [HttpPut("/api/departments/{id:int}")]  // alias: canonical short path used by clients/tests
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> UpdateDepartment(int id, [FromBody] CreateDepartmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        // FIX [2] IDOR — pass CallerCompanyIdOrNull to enforce tenant check in service.
        var ok = await _svc.UpdateDepartmentAsync(id, dto, CallerCompanyIdOrNull);
        return ok
            ? Ok(ApiResponse.Ok("Department updated."))
            : NotFound(ApiResponse.Fail("Department not found."));
    }

    /// <summary>Soft-delete a department (admin/superadmin).</summary>
    [HttpDelete("departments/{id:int}")]
    [HttpDelete("/api/departments/{id:int}")]  // alias: canonical short path used by clients/tests
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        // FIX [2] IDOR — pass CallerCompanyIdOrNull to enforce tenant check in service.
        var ok = await _svc.DeleteDepartmentAsync(id, CallerCompanyIdOrNull);
        return ok
            ? Ok(ApiResponse.Ok("Department removed."))
            : NotFound(ApiResponse.Fail("Department not found."));
    }

    // ── Designations ───────────────────────────────────────────────────────

    /// <summary>
    /// List all active designations for the caller's company (paginated + sortable).
    /// FIX 5: Added sortBy and sortDirection query parameters.
    /// </summary>
    [HttpGet("designations")]
    [HttpGet("/api/designations")]  // alias: canonical short path used by clients/tests
    public async Task<IActionResult> GetDesignations(
        [FromQuery] int     page          = 1,
        [FromQuery] int     pageSize      = 25,
        [FromQuery] string? sortBy        = null,
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] string? search        = null)
    {
        var companyId = CallerCompanyIdOrNull;
        var result = await _svc.GetDesignationsPagedAsync(
            companyId, page, pageSize, sortBy, sortDirection, search);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<DesignationDto>>.Ok(result));
    }

    /// <summary>Get a designation by ID.</summary>
    [HttpGet("designations/{id:int}")]
    [HttpGet("/api/designations/{id:int}")]  // alias: canonical short path used by clients/tests
    public async Task<IActionResult> GetDesignation(int id)
    {
        // FIX [2] IDOR — pass CallerCompanyIdOrNull to enforce tenant check in service.
        var d = await _svc.GetDesignationByIdAsync(id, CallerCompanyIdOrNull);
        return d == null
            ? NotFound(ApiResponse.Fail("Designation not found."))
            : Ok(ApiResponse<DesignationDto>.Ok(d));
    }

    /// <summary>Create a designation (admin/superadmin).</summary>
    [HttpPost("designations")]
    [HttpPost("/api/designations")]  // alias: canonical short path used by clients/tests
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> CreateDesignation([FromBody] CreateDesignationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        var companyId = CallerCompanyIdOrNull;
        var d = await _svc.CreateDesignationAsync(companyId, dto);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created, ApiResponse<DesignationDto>.Ok(d, "Designation created."));
    }

    /// <summary>Update a designation (admin/superadmin).</summary>
    [HttpPut("designations/{id:int}")]
    [HttpPut("/api/designations/{id:int}")]  // alias: canonical short path used by clients/tests
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> UpdateDesignation(int id, [FromBody] CreateDesignationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        // FIX [2] IDOR — pass CallerCompanyIdOrNull to enforce tenant check in service.
        var ok = await _svc.UpdateDesignationAsync(id, dto, CallerCompanyIdOrNull);
        return ok
            ? Ok(ApiResponse.Ok("Designation updated."))
            : NotFound(ApiResponse.Fail("Designation not found."));
    }

    /// <summary>Soft-delete a designation (admin/superadmin).</summary>
    [HttpDelete("designations/{id:int}")]
    [HttpDelete("/api/designations/{id:int}")]  // alias: canonical short path used by clients/tests
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> DeleteDesignation(int id)
    {
        // FIX [2] IDOR — pass CallerCompanyIdOrNull to enforce tenant check in service.
        var ok = await _svc.DeleteDesignationAsync(id, CallerCompanyIdOrNull);
        return ok
            ? Ok(ApiResponse.Ok("Designation removed."))
            : NotFound(ApiResponse.Fail("Designation not found."));
    }
}
