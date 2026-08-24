using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.API.Controllers.Employees;

[ApiController]
[Route("api/employees")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class EmployeeController : BaseController
{
    private readonly IEmployeeService _service;

    public EmployeeController(IEmployeeService service) => _service = service;

    /// <summary>Register a new employee (multipart/form-data with file uploads)</summary>
    [HttpPost]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] CreateEmployeeDto dto)
    {
        // RUNTIME FIX: guard against non-multipart requests that would otherwise crash
        // when the service layer accesses Request.Form.Files.
        if (!Request.HasFormContentType)
            return BadRequest(ApiResponse.Fail(
                "Request must use Content-Type: multipart/form-data."));

        // A tenant admin may never choose the destination company from the
        // multipart body.  SuperAdmin is the only role allowed to create for
        // an explicitly supplied company.
        if (!User.IsInRole(AppRoles.SuperAdmin))
        {
            if (!IsCompanyClaimValid) return Forbid();
            dto.CompanyId = CallerCompanyIdOrNull;
        }

        var (empId, tempPassword) = await _service.CreateAsync(dto, Request.Form.Files);
        // NOTE: the temp password is only ever shown once, here, to the admin who just
        // created the account — it is never logged and is not derivable from the employee ID.
        // The employee is forced to change it on first login (MustChangePassword).
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(
            new { EmployeeId = empId, TemporaryPassword = tempPassword },
            $"Employee registered successfully! Employee ID: {empId}. Share the temporary password with the employee through a secure channel — they must change it on first login."));
    }

    /// <summary>
    /// Get all employees (paginated + sortable).
    /// FIX 5: Added sortBy and sortDirection query parameters.
    /// Allowed sort columns: FullName, Department, Designation, IsActive, CreatedAt.
    /// Defaults to FullName ascending when no sort is specified.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int     page          = 1,
        [FromQuery] int     pageSize      = 25,
        [FromQuery] string? sortBy        = null,
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] string? search        = null,
        [FromQuery] string? status        = null,
        [FromQuery] string? department    = null,
        [FromQuery] string? designation   = null)
    {
        int? companyId = CallerCompanyIdOrNull;

        var result = await _service.GetAllPagedAsync(
            companyId, page, pageSize, sortBy, sortDirection,
            search, status, department, designation);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<EmployeeListDto>>.Ok(result));
    }

    /// <summary>Get single employee by ID</summary>
    [HttpGet("{employeeId}")]
    public async Task<IActionResult> GetById(string employeeId)
    {
        // Company-scoped: a company admin cannot fetch another company's employee by ID
        // (IDOR fix). Superadmin is unrestricted, same as GetAll.
        int? companyId = null;
        if (!User.IsInRole(AppRoles.SuperAdmin))
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value;
            companyId = int.TryParse(companyIdClaim, out int cid) ? cid : -1; // -1 = no match if claim missing
        }

        var emp = await _service.GetByIdAsync(employeeId, companyId);
        if (emp == null) return NotFound(ApiResponse.Fail("Employee not found."));
        return Ok(ApiResponse<EmployeeDetailDto>.Ok(emp));
    }

    /// <summary>Update employee (multipart/form-data)</summary>
    [HttpPut("{employeeId}")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Update(string employeeId, [FromForm] CreateEmployeeDto dto)
    {
        // RUNTIME FIX: guard against non-multipart requests that would crash on
        // Request.Form.Files access inside the service.
        if (!Request.HasFormContentType)
            return BadRequest(ApiResponse.Fail(
                "Request must use Content-Type: multipart/form-data."));

        // IDOR fix: company admins may only update employees within their own company.
        // Superadmin is unrestricted (null = no filter).
        int? companyId = null;
        if (!User.IsInRole(AppRoles.SuperAdmin))
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value;
            companyId = int.TryParse(companyIdClaim, out int cid) ? cid : -1;
        }

        var ok = await _service.UpdateAsync(employeeId, dto, Request.Form.Files, companyId);
        return ok ? Ok(ApiResponse.Ok("Employee updated successfully."))
                  : NotFound(ApiResponse.Fail("Employee not found."));
    }

    /// <summary>Toggle employee active/inactive status</summary>
    [HttpPatch("{employeeId}/status")]
    public async Task<IActionResult> UpdateStatus(string employeeId, [FromBody] UpdateStatusRequest req)
    {
        // IDOR fix: company admins may only toggle status for their own company's employees.
        int? companyId = null;
        if (!User.IsInRole(AppRoles.SuperAdmin))
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value;
            companyId = int.TryParse(companyIdClaim, out int cid) ? cid : -1;
        }

        var ok = await _service.UpdateStatusAsync(employeeId, req.IsActive, companyId);
        return ok ? Ok(ApiResponse.Ok($"Employee {(req.IsActive ? "activated" : "deactivated")} successfully."))
                  : NotFound(ApiResponse.Fail("Employee not found."));
    }

    /// <summary>Delete employee</summary>
    [HttpDelete("{employeeId}")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Delete(string employeeId)
    {
        // Delete is superadmin-only; superadmin spans all companies, so no companyId filter.
        var ok = await _service.DeleteAsync(employeeId);
        return ok ? Ok(ApiResponse.Ok("Employee deleted."))
                  : NotFound(ApiResponse.Fail("Employee not found."));
    }

    /// <summary>
    /// FIX MED-9: Returns PII fields (Aadhaar, PAN, bank account) for an employee.
    /// Requires SuperAdmin role. Values are masked by default; pass ?unmask=true to
    /// receive raw values (same role check applies — no additional role needed today,
    /// but the flag is the hook for a future PII_VIEWER granular-permission role).
    /// FIX HIGH-1: Add rate limiting to prevent brute-force PII extraction.
    /// </summary>
    [HttpGet("{employeeId}/pii")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [EnableRateLimiting("sensitive")]  // 5 requests/min per IP
    public async Task<IActionResult> GetPii(string employeeId, [FromQuery] bool unmask = false)
    {
        // SuperAdmin is unrestricted across tenants; company admins cannot access PII.
        // includeRaw=unmask tells the service to populate Raw with unmasked values in one DB call.
        var dto = await _service.GetPiiAsync(employeeId, companyId: null, includeRaw: unmask);
        if (dto == null) return NotFound(ApiResponse.Fail("Employee not found."));
        return Ok(ApiResponse<EmployeePiiDto>.Ok(dto));
    }
}
