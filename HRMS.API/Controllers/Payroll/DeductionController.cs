using HRMS.Application.Common;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HRMS.API.Controllers.Payroll;

/// <summary>
/// Deduction management — CRUD for employee custom deductions.
/// All write operations are blocked when the affected payroll period is locked.
/// GetById enforces IDOR: admins can only access deductions for their own company's employees.
/// </summary>
[ApiController]
[Route("api/deductions")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[Produces("application/json")]
public class DeductionController : BaseController
{
    private readonly IBonusDeductionService _svc;
    private readonly IEmployeeService       _empSvc;
    private readonly IPayrollLockGuard      _lockGuard;

    public DeductionController(IBonusDeductionService svc, IEmployeeService empSvc, IPayrollLockGuard lockGuard)
    {
        _svc       = svc;
        _empSvc    = empSvc;
        _lockGuard = lockGuard;
    }

    // ── Identity helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the caller's company ID, or null when the caller is a superadmin.
    /// Returns -1 on parse failure so IDOR checks fail closed rather than open.
    /// </summary>


    /// <summary>
    /// IDOR guard: verifies the given employeeId belongs to the caller's company.
    /// Superadmins (CallerCompanyIdOrNull == null) pass unconditionally.
    /// </summary>
    private async Task<bool> EmployeeBelongsToCallerAsync(string? employeeId)
    {
        if (string.IsNullOrEmpty(employeeId)) return true;
        var cid = CallerCompanyIdOrNull;
        if (cid == null) return true; // superadmin — unrestricted
        return await _empSvc.GetByIdAsync(employeeId, cid) != null;
    }

    // ── Endpoints ──────────────────────────────────────────────────────────

    /// <summary>List deductions with optional employee/month/year filters.</summary>
    [HttpGet]
    [SwaggerOperation(OperationId = "GetDeductions", Tags = new[] { "Deductions" })]
    [ProducesResponseType(typeof(List<DeductionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? employeeId,
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        // FIX IDOR: same as BonusController — always pass callerCid so the service
        // scopes to the caller's company even when no employeeId filter is supplied.
        var callerCid = CallerCompanyIdOrNull;

        if (!string.IsNullOrEmpty(employeeId) && !await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));

        var result = await _svc.GetDeductionsPagedScopedAsync(employeeId, callerCid, month, year, page, pageSize);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<DeductionDto>>.Ok(result));
    }

    /// <summary>
    /// Get a single deduction by ID.
    /// Non-superadmin admins receive 404 if the deduction belongs to another company's employee (IDOR).
    /// </summary>
    [HttpGet("{id:int}")]
    [SwaggerOperation(OperationId = "GetDeductionById", Tags = new[] { "Deductions" })]
    [ProducesResponseType(typeof(DeductionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        // FIX SEC-02: service-level scoping — returns null for cross-tenant IDs.
        var item = await _svc.GetDeductionByIdAsync(id, CallerCompanyIdOrNull);
        if (item == null) return NotFound(ApiResponse.Fail("Deduction not found."));

        // Defence-in-depth: also verify at the controller layer (belt-and-suspenders).
        if (!await EmployeeBelongsToCallerAsync(item.EmployeeId))
            return NotFound(ApiResponse.Fail("Deduction not found."));

        return Ok(ApiResponse<DeductionDto>.Ok(item));
    }

    /// <summary>
    /// Create a custom deduction for an employee.
    /// Blocked when the specified month/year payroll period is locked (409).
    /// </summary>
    [HttpPost]
    [SwaggerOperation(OperationId = "CreateDeduction", Tags = new[] { "Deductions" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateDeductionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));

        if (!await EmployeeBelongsToCallerAsync(dto.EmployeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));

        // PayrollLock: block writes on locked periods
        var cid = CallerCompanyIdOrNull ?? 0;
        if (cid > 0)
        {
            var lockMsg = await _lockGuard.GetLockMessageAsync(cid, dto.Month, dto.Year);
            if (lockMsg != null) return Conflict(ApiResponse.Fail(lockMsg));
        }

        dto.CreatedByUserId = UserId;
        var id = await _svc.AddDeductionAsync(dto);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { Id = id }, "Deduction added."));
    }

    /// <summary>
    /// Update an existing deduction.
    /// Blocked when the deduction's payroll period is locked (409).
    /// Non-superadmin admins receive 404 for deductions outside their company (IDOR).
    /// </summary>
    [HttpPut("{id:int}")]
    [SwaggerOperation(OperationId = "UpdateDeduction", Tags = new[] { "Deductions" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateDeductionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));

        // FIX SEC-02: service-level scoping — returns null for cross-tenant IDs.
        var item = await _svc.GetDeductionByIdAsync(id, CallerCompanyIdOrNull);
        if (item == null) return NotFound(ApiResponse.Fail("Deduction not found."));

        // Defence-in-depth IDOR check at the controller layer.
        if (!await EmployeeBelongsToCallerAsync(item.EmployeeId))
            return NotFound(ApiResponse.Fail("Deduction not found."));

        // PayrollLock check on the existing deduction period
        var cid = CallerCompanyIdOrNull ?? 0;
        if (cid > 0)
        {
            var lockMsg = await _lockGuard.GetLockMessageAsync(cid, item.Month, item.Year);
            if (lockMsg != null) return Conflict(ApiResponse.Fail(lockMsg));
        }

        // Pass CallerCompanyIdOrNull for service-layer defence-in-depth (IDOR fix).
        var ok = await _svc.UpdateDeductionAsync(id, dto, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Deduction updated."))
                  : NotFound(ApiResponse.Fail("Deduction not found."));
    }

    /// <summary>
    /// Delete a deduction record.
    /// Blocked when the deduction's payroll period is locked (409).
    /// Non-superadmin admins receive 404 for deductions outside their company (IDOR).
    /// </summary>
    [HttpDelete("{id:int}")]
    [SwaggerOperation(OperationId = "DeleteDeduction", Tags = new[] { "Deductions" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        // FIX SEC-02: service-level scoping — returns null for cross-tenant IDs.
        var item = await _svc.GetDeductionByIdAsync(id, CallerCompanyIdOrNull);
        if (item == null) return NotFound(ApiResponse.Fail("Deduction not found."));

        // Defence-in-depth IDOR check at the controller layer.
        if (!await EmployeeBelongsToCallerAsync(item.EmployeeId))
            return NotFound(ApiResponse.Fail("Deduction not found."));

        // PayrollLock check on the deduction period
        var cid = CallerCompanyIdOrNull ?? 0;
        if (cid > 0)
        {
            var lockMsg = await _lockGuard.GetLockMessageAsync(cid, item.Month, item.Year);
            if (lockMsg != null) return Conflict(ApiResponse.Fail(lockMsg));
        }

        var ok = await _svc.DeleteDeductionAsync(id, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Deduction deleted."))
                  : NotFound(ApiResponse.Fail("Deduction not found."));
    }
}
