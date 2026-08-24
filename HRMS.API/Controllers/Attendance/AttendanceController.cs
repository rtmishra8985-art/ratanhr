using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRMS.Infrastructure.Security;
using HRMS.API.Security;
using Swashbuckle.AspNetCore.Annotations;

namespace HRMS.API.Controllers.Attendance;

/// <summary>
/// Attendance management — employee web check-in/out, admin status edits (with mandatory reason
/// and back-dated edit window enforcement), and Excel batch upload.
/// All admin edits are audited and respect the configurable back-dated window
/// (<c>Attendance:BackDateEditWindowDays</c>) and the payroll-period lock.
/// </summary>
[ApiController]
[Route("api/attendance")]
[Authorize(Policy = "RequireMfaCompleted")]
[Produces("application/json")]
public class AttendanceController : BaseController
{
    private readonly IAttendanceService _service;

    public AttendanceController(IAttendanceService service) => _service = service;

    // ── Employee check-in / check-out ──────────────────────────────────────

    /// <summary>Employee records a check-in for today. Only one check-in per calendar day is allowed.</summary>
    [HttpPost("web/check-in")]
    [Authorize(Roles = AppRoles.Employee)]
    [SwaggerOperation(OperationId = "WebCheckIn", Tags = new[] { "Attendance" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckIn()
    {
        var empId = User.FindFirst("employeeId")?.Value;
        if (string.IsNullOrEmpty(empId)) return Unauthorized();
        var id = await _service.WebCheckInAsync(empId);
        return Ok(ApiResponse<object>.Ok(new { AttendanceId = id }, "Check-in recorded."));
    }

    /// <summary>
    /// Employee records a check-out. Status (Present/Half Day/Absent) is derived from hours worked.
    /// ≥8 h → Present; 4–8 h → Half Day; &lt;4 h → Absent.
    /// </summary>
    [HttpPost("web/check-out/{attendanceId:int}")]
    [Authorize(Roles = AppRoles.Employee)]
    [SwaggerOperation(OperationId = "WebCheckOut", Tags = new[] { "Attendance" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckOut(int attendanceId)
    {
        // IDOR: pass the caller's employee ID so the service can verify ownership.
        // An employee who guesses a different attendance record ID receives 404 rather
        // than silently checking out a colleague's record.
        var empId = User.FindFirst("employeeId")?.Value;
        if (string.IsNullOrEmpty(empId)) return Unauthorized();
        var ok = await _service.WebCheckOutAsync(attendanceId, empId);
        return ok ? Ok(ApiResponse.Ok("Check-out recorded."))
                  : NotFound(ApiResponse.Fail("Attendance record not found."));
    }

    // ── Soft delete ─────────────────────────────────────────────────────────

    /// <summary>
    /// Soft-delete an attendance record. Employees may only delete their own same-day record.
    /// Admins may delete any record within their company tenant. All deletions are audited.
    /// </summary>
    [HttpDelete("web/{attendanceId:int}")]
    [Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]
    [SwaggerOperation(OperationId = "DeleteAttendance", Tags = new[] { "Attendance" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAttendance(int attendanceId, [FromQuery] string reason = "Deleted by user")
    {
        var empId = User.FindFirst("employeeId")?.Value;
        if (string.IsNullOrEmpty(empId)) return Unauthorized();
        var isAdmin = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin);
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(ApiResponse.Fail("A reason is required for deletion."));
        bool ok;
        try
        {
            ok = await _service.SoftDeleteAttendanceAsync(attendanceId, empId, isAdmin, reason);
        }
        catch (InvalidOperationException ex)
        {
            // Thrown by SoftDeleteAttendanceAsync when the attendance record falls
            // inside a locked payroll period. 409 Conflict is the correct HTTP status.
            return Conflict(ApiResponse.Fail(ex.Message));
        }
        return ok ? Ok(ApiResponse.Ok("Attendance record deleted."))
                  : NotFound(ApiResponse.Fail("Record not found or you do not have permission to delete it."));
    }

    // ── Admin audited edit (back-dated + IDOR + PayrollLock) ──────────────

    /// <summary>
    /// HR/Admin: edit an attendance record status with a mandatory audit reason.
    /// Enforces the back-dated edit window (<c>Attendance:BackDateEditWindowDays</c> days).
    /// Blocked by payroll-period lock (409) or IDOR (404).
    /// All changes are written to AuditLog.
    /// </summary>
    [HttpPatch("web/{attendanceId:int}/edit")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "EditAttendance", Tags = new[] { "Attendance" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditAttendance(int attendanceId, [FromBody] EditAttendanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));

        // FIX 7: Use explicit flag instead of magic number (companyId = 0)
        var scope = CallerCompanyScope;
        var bypassTenantScoping = scope is CompanyScope.SuperAdmin;
        var actorCompanyId = scope switch
        {
            CompanyScope.SuperAdmin => 0,  // Superadmin: bypass
            CompanyScope.TenantAdmin admin => admin.CompanyId,  // TenantAdmin: use company ID
            _ => -1  // Invalid (shouldn't reach here due to [Authorize])
        };

        var (success, message) = await _service.EditWebAttendanceAsync(
            attendanceId, dto.Status, dto.Reason,
            actorUserId:      UserId,
            actorCompanyId:   actorCompanyId,
            isPrivilegedUser: IsPrivilegedUser);

        if (!success)
        {
            return message!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(ApiResponse.Fail(message))
                : Conflict(ApiResponse.Fail(message));
        }
        return Ok(ApiResponse.Ok(message!));
    }

    /// <summary>
    /// HR/Admin: legacy status-only PATCH (no reason required, no explicit audit).
    /// IDOR-checked: routes through the audited edit path to enforce company scoping.
    /// Prefer <c>PATCH /web/{id}/edit</c> for full back-dated compliance.
    /// </summary>
    [HttpPatch("web/{attendanceId:int}/status")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "UpdateAttendanceStatus", Tags = new[] { "Attendance" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(int attendanceId, [FromBody] UpdateStatusBody body)
    {
        // FIX 7: Use explicit flag instead of magic number (companyId = 0)
        var scope = CallerCompanyScope;
        var actorCompanyId = scope switch
        {
            CompanyScope.SuperAdmin => 0,  // Superadmin: bypass
            CompanyScope.TenantAdmin admin => admin.CompanyId,  // TenantAdmin: use company ID
            _ => -1  // Invalid (shouldn't reach here due to [Authorize])
        };

        var (ok, msg) = await _service.EditWebAttendanceAsync(
            attendanceId, body.Status, "Admin status update",
            actorUserId:      UserId,
            actorCompanyId:   actorCompanyId,
            isPrivilegedUser: true);   // admin override bypasses back-dated window check

        if (!ok)
        {
            return msg!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(ApiResponse.Fail(msg))
                : Conflict(ApiResponse.Fail(msg));
        }
        return Ok(ApiResponse.Ok("Status updated."));
    }

    // ── Query ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Admin/superadmin: list web attendance records with optional filters.
    /// Non-superadmin admins are automatically scoped to their own company (IDOR).
    /// </summary>
    [HttpGet("web")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "GetWebAttendance", Tags = new[] { "Attendance" })]
    [ProducesResponseType(typeof(List<WebAttendanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWebAttendance(
        [FromQuery] AttendanceFilterDto filter,
        [FromQuery] int     page          = 1,
        [FromQuery] int     pageSize      = 25,
        [FromQuery] string? sortBy        = null,
        [FromQuery] string? sortDirection = "desc")
    {
        // FIX 1: Use PaginationHelper for consistent bounds
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);
        
        // IDOR scope: regular admin sees only their company
        if (!User.IsInRole(AppRoles.SuperAdmin))
            filter.CompanyId = CallerCompanyIdOrNull;

        var result = await _service.GetWebAttendancePagedAsync(filter, page, pageSize, sortBy, sortDirection, HttpContext.RequestAborted);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<WebAttendanceDto>>.Ok(result));
    }

    /// <summary>
    /// Employee: get own web attendance records (paginated).
    /// FIX HIGH-OOM4: The previous unbounded GetWebAttendanceAsync could load the
    /// entire attendance history for an employee into memory. Now uses the paged
    /// overload with a server-side cap of 200 rows per page.
    /// </summary>
    [HttpGet("web/my")]
    [Authorize(Roles = AppRoles.Employee)]
    [SwaggerOperation(OperationId = "GetMyAttendance", Tags = new[] { "Attendance" })]
    [ProducesResponseType(typeof(HRMS.Application.Common.PagedResult<WebAttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyAttendance(
        [FromQuery] AttendanceFilterDto filter,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 25)
    {
        // FIX 1: Use PaginationHelper for consistent bounds
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);
        
        filter.EmployeeId = User.FindFirst("employeeId")?.Value;
        var result = await _service.GetWebAttendancePagedAsync(filter, page, pageSize, ct: HttpContext.RequestAborted);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<WebAttendanceDto>>.Ok(result));
    }

    // ── Excel batch upload ─────────────────────────────────────────────────

    /// <summary>
    /// Upload an attendance Excel file with columns: EmployeeId, Date (yyyy-MM-dd), Status, HoursWorked.
    /// Returns an <see cref="ExcelUploadResult"/> with per-row imported/skipped counts and any
    /// parse-error descriptions so callers can surface partial-failure details without a re-query.
    /// </summary>
    [HttpPost("excel/upload")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "UploadExcelAttendance", Tags = new[] { "Attendance" })]
    [ProducesResponseType(typeof(ExcelUploadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadExcel(IFormFile file)
    {
        // Audit item 9 — this is the one upload path that does not persist through
        // FileStorageService, so it validates explicitly against the Spreadsheet profile
        // (extension allow-list + declared MIME agreement + magic-byte signature).
        // A signature/extension mismatch yields HTTP 400 with the validator's message.
        var upload = UploadValidator.Validate(file, UploadProfile.Spreadsheet);
        if (!upload.IsValid) return BadRequest(ApiResponse.Fail(upload.Error!));
        // FIX LOW: Use BaseController helper instead of raw claim parsing for consistency.
        int? companyId = CallerCompanyIdOrNull;
        var result = await _service.UploadExcelAttendanceAsync(file, companyId);
        return Ok(ApiResponse<ExcelUploadResult>.Ok(result,
            $"Upload complete: {result.Imported} imported, {result.Skipped} skipped."));
    }

    /// <summary>Get Excel attendance records with optional employee/date/status filters.</summary>
    [HttpGet("excel")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "GetExcelAttendance", Tags = new[] { "Attendance" })]
    [ProducesResponseType(typeof(List<ExcelAttendanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExcelAttendance([FromQuery] AttendanceFilterDto filter, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        // FIX 1: Use PaginationHelper for consistent bounds
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);
        
        // Never trust a tenant supplied by the query string.  The service also
        // receives this value so the predicate remains enforced outside HTTP.
        if (!User.IsInRole(AppRoles.SuperAdmin))
            filter.CompanyId = CallerCompanyIdOrNull;

        var result = await _service.GetExcelAttendancePagedAsync(filter, page, pageSize, HttpContext.RequestAborted);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<ExcelAttendanceDto>>.Ok(result));
    }
}
