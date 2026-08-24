using HRMS.Application.Common;
using HRMS.Application.DTOs.Leave;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HRMS.API.Controllers.Leave;

/// <summary>
/// Leave Management — leave types, employee applications, approval/rejection,
/// balance adjustments, and year-end carry forward.
/// Approve/Reject and employee Cancel are blocked when the affected period is payroll-locked.
/// GetById enforces IDOR so admins can only access their own company's leave requests.
/// </summary>
[ApiController]
[Route("api/leave")]
[Authorize(Policy = "RequireMfaCompleted")]
[Produces("application/json")]
public class LeaveController : BaseController
{
    private readonly ILeaveService     _service;
    private readonly IPayrollLockGuard _lockGuard;

    public LeaveController(ILeaveService service, IPayrollLockGuard lockGuard)
    {
        _service   = service;
        _lockGuard = lockGuard;
    }

    // FIX F: The original 'private new int? CompanyId' shadow returned null when
    // the JWT companyId claim was absent. LeaveService treats null as the superadmin
    // "unrestricted" sentinel — so a non-superadmin user with a missing or malformed
    // companyId claim would get unrestricted read/write access to leave types across
    // all companies. Removed: CallerCompanyIdOrNull (inherited from BaseController)
    // already returns null only for users whose IsInRole(AppRoles.SuperAdmin) is true, and
    // -1 (a safe no-match sentinel) for all other users with a missing claim.


    // ── Leave Types ────────────────────────────────────────────────────────

    /// <summary>List leave types available to the caller's company.</summary>
    [HttpGet("types")]
    [SwaggerOperation(OperationId = "GetLeaveTypes", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(List<LeaveTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTypes()
        => Ok(ApiResponse<List<LeaveTypeDto>>.Ok(await _service.GetLeaveTypesAsync(CallerCompanyIdOrNull)));

    /// <summary>Create a company-specific leave type (admin/superadmin).</summary>
    [HttpPost("types")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "CreateLeaveType", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(LeaveTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateType([FromBody] CreateLeaveTypeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<LeaveTypeDto>.Ok(await _service.CreateLeaveTypeAsync(CallerCompanyIdOrNull, dto)));
    }

    /// <summary>Update an existing leave type (admin/superadmin).</summary>
    [HttpPut("types/{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "UpdateLeaveType", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateType(int id, [FromBody] CreateLeaveTypeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        // FIX (MED-SENTINEL): Use CallerCompanyIdOrNull instead of manually casting CompanyId.
        // BaseController.CompanyId returns -1 when the claim is missing (not null), so
        // (int?)CompanyId would pass -1 as a valid tenant ID. CallerCompanyIdOrNull correctly
        // returns null for superadmin and null when the company claim is absent.
        var callerCompanyId = CallerCompanyIdOrNull;
        var ok = await _service.UpdateLeaveTypeAsync(id, callerCompanyId, dto);
        return ok ? Ok(ApiResponse.Ok("Leave type updated."))
                  : NotFound(ApiResponse.Fail("Leave type not found."));
    }

    /// <summary>Soft-delete a leave type (historical records are preserved).</summary>
    [HttpDelete("types/{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "DeleteLeaveType", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteType(int id)
    {
        // FIX (MED-SENTINEL): Use CallerCompanyIdOrNull for consistent tenant scoping.
        var callerCompanyId = CallerCompanyIdOrNull;
        var ok = await _service.DeleteLeaveTypeAsync(id, callerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Leave type deactivated."))
                  : NotFound(ApiResponse.Fail("Leave type not found."));
    }

    // ── Employee self-service ─────────────────────────────────────────────

    /// <summary>Employee applies for leave.</summary>
    [HttpPost("apply")]
    [Authorize(Roles = AppRoles.Employee)]
    [SwaggerOperation(OperationId = "ApplyForLeave", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Apply([FromBody] ApplyLeaveDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        if (string.IsNullOrEmpty(EmployeeId)) return Unauthorized();
        var (ok, message, id) = await _service.ApplyAsync(EmployeeId!, CallerCompanyIdOrNull, dto);
        // FIX: HTTP 201 Created on success (was 200 OK).
        return ok ? StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new { Id = id }, message))
                  : BadRequest(ApiResponse.Fail(message));
    }

    /// <summary>Employee's own leave requests.</summary>
    [HttpGet("my")]
    [Authorize(Roles = AppRoles.Employee)]
    [SwaggerOperation(OperationId = "GetMyLeaveRequests", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(List<LeaveRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MyRequests()
    {
        if (string.IsNullOrEmpty(EmployeeId)) return Unauthorized();
        return Ok(ApiResponse<List<LeaveRequestDto>>.Ok(await _service.GetMyRequestsAsync(EmployeeId!)));
    }

    /// <summary>Employee's remaining leave balance per leave type.</summary>
    [HttpGet("my/balance")]
    [Authorize(Roles = AppRoles.Employee)]
    [SwaggerOperation(OperationId = "GetMyLeaveBalance", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(List<LeaveBalanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MyBalance()
    {
        if (string.IsNullOrEmpty(EmployeeId)) return Unauthorized();
        return Ok(ApiResponse<List<LeaveBalanceDto>>.Ok(await _service.GetMyBalanceAsync(EmployeeId!, CallerCompanyIdOrNull)));
    }

    /// <summary>
    /// Employee cancels their own pending request.
    /// Blocked if the leave start month/year is payroll-locked.
    /// </summary>
    [HttpPost("my/{id}/cancel")]
    [Authorize(Roles = AppRoles.Employee)]
    [SwaggerOperation(OperationId = "CancelLeaveRequest", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id)
    {
        if (string.IsNullOrEmpty(EmployeeId)) return Unauthorized();

        // PayrollLock check: derive period from the leave request's start date
        if (CallerCompanyIdOrNull.HasValue)
        {
            var req = await _service.GetRequestByIdAsync(id, CallerCompanyIdOrNull);
            if (req != null && DateOnly.TryParse(req.StartDate, out var startDate))
            {
                var lockMsg = await _lockGuard.GetLockMessageAsync(
                    CallerCompanyIdOrNull.Value, startDate.Month, startDate.Year);
                if (lockMsg != null) return Conflict(ApiResponse.Fail(lockMsg));
            }
        }

        // IDOR FIX: pass CallerCompanyIdOrNull so the DB query is scoped pre-fetch.
        var ok = await _service.CancelAsync(EmployeeId!, id, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Leave request cancelled."))
                  : BadRequest(ApiResponse.Fail("Cannot cancel this request (not found or not pending)."));
    }

    // ── Admin ─────────────────────────────────────────────────────────────

    /// <summary>Admin/superadmin: list all leave requests, optionally filtered by status.</summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "GetAllLeaveRequests", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(List<LeaveRequestDto>), StatusCodes.Status200OK)]
    // FIX 5: Added sortBy and sortDirection query parameters for column-level sorting.
    // Allowed sort columns: CreatedAt, Status, EmployeeId, StartDate, EndDate.
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int     page          = 1,
        [FromQuery] int     pageSize      = 25,
        [FromQuery] string? sortBy        = null,
        [FromQuery] string? sortDirection = "desc")
    {
        var companyId = CallerCompanyIdOrNull;
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<LeaveRequestDto>>.Ok(
            await _service.GetAllRequestsPagedAsync(companyId, status, page, pageSize, sortBy, sortDirection)));
    }

    /// <summary>
    /// Admin/superadmin: get a single leave request by ID.
    /// Non-superadmin admins are IDOR-scoped: can only access requests from their own company.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "GetLeaveRequestById", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        // FIX HIGH-2: IDOR check pushed into DB query via callerCompanyId parameter.
        // The record is never loaded for a different tenant — no post-fetch check needed.
        var req = await _service.GetRequestByIdAsync(id, CallerCompanyIdOrNull);
        if (req == null) return NotFound(ApiResponse.Fail("Leave request not found."));
        return Ok(ApiResponse<LeaveRequestDto>.Ok(req));
    }

    /// <summary>
    /// Admin/superadmin: approve or reject a leave request.
    /// Blocked if the leave period is payroll-locked.
    /// </summary>
    [HttpPost("{id:int}/decision")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "DecideLeaveRequest", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Decide(int id, [FromBody] LeaveDecisionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));

        // PayrollLock check — use CallerCompanyIdOrNull (null = superadmin, unrestricted)
        var lockCompanyId = CallerCompanyIdOrNull;
        if (lockCompanyId.HasValue)
        {
            var req = await _service.GetRequestByIdAsync(id, CallerCompanyIdOrNull);
            if (req != null && DateOnly.TryParse(req.StartDate, out var startDate))
            {
                var lockMsg = await _lockGuard.GetLockMessageAsync(
                    lockCompanyId.Value, startDate.Month, startDate.Year);
                if (lockMsg != null) return Conflict(ApiResponse.Fail(lockMsg));
            }
        }

        // IDOR FIX: pass CallerCompanyIdOrNull so the DB query is scoped pre-fetch.
        var (ok, message) = await _service.DecideAsync(id, UserId, dto, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok(message)) : BadRequest(ApiResponse.Fail(message));
    }

    // ── Balance Adjustment (admin) ─────────────────────────────────────────

    /// <summary>
    /// Admin: manually credit or debit an employee's leave balance for a given year.
    /// Use positive Days to credit, negative to debit.
    /// </summary>
    [HttpPost("balance/adjust")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "AdjustLeaveBalance", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(LeaveBalanceAdjustmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdjustBalance([FromBody] CreateLeaveBalanceAdjustmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        try
        {
            // FIX: use CallerCompanyIdOrNull (null = SuperAdmin unrestricted, parsed int for others).
            // The previous code cast the raw CompanyId int (which returns -1 for SuperAdmin)
            // to int?, so SuperAdmins were incorrectly scoped to company -1 instead of being
            // unrestricted, and non-admins with a missing claim also got -1 as a valid-looking ID.
            var companyId = CallerCompanyIdOrNull;
            var adj = await _service.CreateBalanceAdjustmentAsync(UserId, companyId, dto);
            return Ok(ApiResponse<LeaveBalanceAdjustmentDto>.Ok(adj, "Leave balance adjusted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Admin: get leave balance adjustment history for an employee.
    /// Company admins are scoped to their own company — cross-company requests return 403.
    /// </summary>
    [HttpGet("balance/adjustments/{employeeId}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "GetLeaveBalanceAdjustments", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(List<LeaveBalanceAdjustmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdjustments(string employeeId, [FromQuery] int? year)
    {
        // SECURITY FIX – IDOR: derive company scope from JWT, never trust request parameters.
        // SuperAdmin (null) has unrestricted access; company admin is scoped to their company.
        var callerCompanyId = User.IsInRole(AppRoles.SuperAdmin) ? (int?)null : CompanyId;
        try
        {
            var list = await _service.GetBalanceAdjustmentsAsync(employeeId, year, callerCompanyId);
            return Ok(ApiResponse<List<LeaveBalanceAdjustmentDto>>.Ok(list));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("Employee does not belong to your company."));
        }
    }

    // ── Carry Forward (admin) ─────────────────────────────────────────────

    /// <summary>
    /// Admin/superadmin: carry forward unused leave balances from one year to the next.
    /// Set MaxDays to cap the carry-forward per leave type (0 = unlimited).
    /// </summary>
    [HttpPost("carry-forward")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "CarryForwardLeaveBalances", Tags = new[] { "Leave" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CarryForward([FromBody] LeaveCarryForwardDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        if (dto.FromYear >= dto.ToYear)
            return BadRequest(ApiResponse.Fail("ToYear must be greater than FromYear."));

        // SECURITY FIX – LeaveCarryForward IDOR: always derive CompanyId from JWT claims.
        // Ignore any CompanyId supplied in the request body — it could be forged to run
        // carry-forward against another company's employees.
        if (!User.IsInRole(AppRoles.SuperAdmin))
        {
            // Non-superadmin: force to caller's own company (ignore request body value).
            dto.CompanyId = CompanyId;
        }
        // SuperAdmin: may optionally filter by a specific company from the request body,
        // or omit CompanyId to process all companies.

        var (processed, skipped) = await _service.CarryForwardBalancesAsync(dto, UserId);
        return Ok(ApiResponse<object>.Ok(
            new { Processed = processed, Skipped = skipped },
            $"Carry forward complete. {processed} adjustment(s) created, {skipped} skipped (zero balance)."));
    }
}
