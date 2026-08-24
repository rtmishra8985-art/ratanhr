using HRMS.Application.Common;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HRMS.API.Controllers.Payroll;

/// <summary>
/// Bonus management — CRUD for employee bonuses.
/// All write operations are blocked when the affected payroll period is locked.
/// GetById enforces IDOR: admins can only access bonuses for their own company's employees.
/// </summary>
[ApiController]
[Route("api/bonuses")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[Produces("application/json")]
public class BonusController : BaseController
{
    private readonly IBonusDeductionService _svc;
    private readonly IEmployeeService       _empSvc;
    private readonly IPayrollLockGuard      _lockGuard;

    public BonusController(IBonusDeductionService svc, IEmployeeService empSvc, IPayrollLockGuard lockGuard)
    {
        _svc       = svc;
        _empSvc    = empSvc;
        _lockGuard = lockGuard;
    }

    // ── Identity helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the caller's company ID, or null when the caller is a superadmin
    /// (unrestricted cross-tenant access). Returns -1 on parse failure so IDOR
    /// checks fail closed rather than open.
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

    /// <summary>List bonuses with optional employee/month/year filters.</summary>
    [HttpGet]
    [SwaggerOperation(OperationId = "GetBonuses", Tags = new[] { "Bonuses" })]
    [ProducesResponseType(typeof(List<BonusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? employeeId,
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        // FIX IDOR: When no employeeId filter is supplied the original code skipped the
        // IDOR guard entirely (the check was gated on !string.IsNullOrEmpty(employeeId)),
        // allowing a non-superadmin company admin to enumerate all bonuses across all
        // companies with a plain GET /api/bonuses. Now we ALWAYS derive the effective
        // companyId from the caller's JWT claim and pass it to the service layer so the
        // query is tenant-scoped even when no explicit employeeId is provided.
        var callerCid = CallerCompanyIdOrNull; // null = superadmin (unrestricted)

        if (!string.IsNullOrEmpty(employeeId) && !await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));

        var result = await _svc.GetBonusesPagedScopedAsync(employeeId, callerCid, month, year, page, pageSize);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<BonusDto>>.Ok(result));
    }

    /// <summary>
    /// Get a single bonus by ID.
    /// Non-superadmin admins receive 404 if the bonus belongs to another company's employee (IDOR).
    /// </summary>
    [HttpGet("{id:int}")]
    [SwaggerOperation(OperationId = "GetBonusById", Tags = new[] { "Bonuses" })]
    [ProducesResponseType(typeof(BonusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        // FIX SEC-02: pass CallerCompanyIdOrNull so the service JOIN-scopes the query.
        // The service returns null when the bonus belongs to another tenant — caller
        // receives a clean 404 without exposing the record's existence.
        var bonus = await _svc.GetBonusByIdAsync(id, CallerCompanyIdOrNull);
        if (bonus == null) return NotFound(ApiResponse.Fail("Bonus not found."));

        // Defence-in-depth: also verify at the controller layer (belt-and-suspenders).
        if (!await EmployeeBelongsToCallerAsync(bonus.EmployeeId))
            return NotFound(ApiResponse.Fail("Bonus not found."));

        return Ok(ApiResponse<BonusDto>.Ok(bonus));
    }

    /// <summary>
    /// Create a bonus for an employee.
    /// Blocked when the specified month/year payroll period is locked (409).
    /// </summary>
    [HttpPost]
    [SwaggerOperation(OperationId = "CreateBonus", Tags = new[] { "Bonuses" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateBonusDto dto)
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
        var id = await _svc.AddBonusAsync(dto);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { Id = id }, "Bonus added."));
    }

    /// <summary>
    /// Update an existing bonus.
    /// Blocked when the bonus's payroll period is locked (409).
    /// Non-superadmin admins receive 404 for bonuses outside their company (IDOR).
    /// </summary>
    [HttpPut("{id:int}")]
    [SwaggerOperation(OperationId = "UpdateBonus", Tags = new[] { "Bonuses" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateBonusDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));

        // FIX SEC-02: service-level scoping — returns null for cross-tenant IDs.
        var bonus = await _svc.GetBonusByIdAsync(id, CallerCompanyIdOrNull);
        if (bonus == null) return NotFound(ApiResponse.Fail("Bonus not found."));

        // IDOR check before any mutation
        if (!await EmployeeBelongsToCallerAsync(bonus.EmployeeId))
            return NotFound(ApiResponse.Fail("Bonus not found."));

        // PayrollLock check on the existing bonus period
        var cid = CallerCompanyIdOrNull ?? 0;
        if (cid > 0)
        {
            var lockMsg = await _lockGuard.GetLockMessageAsync(cid, bonus.Month, bonus.Year);
            if (lockMsg != null) return Conflict(ApiResponse.Fail(lockMsg));
        }

        // Pass CallerCompanyIdOrNull so the service enforces ownership as a second
        // line of defence (the controller already verified above, but the service
        // layer must not rely on callers to pre-check — defence-in-depth).
        var ok = await _svc.UpdateBonusAsync(id, dto, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Bonus updated."))
                  : NotFound(ApiResponse.Fail("Bonus not found."));
    }

    /// <summary>
    /// Delete a bonus record.
    /// Blocked when the bonus's payroll period is locked (409).
    /// Non-superadmin admins receive 404 for bonuses outside their company (IDOR).
    /// </summary>
    [HttpDelete("{id:int}")]
    [SwaggerOperation(OperationId = "DeleteBonus", Tags = new[] { "Bonuses" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        // FIX SEC-02: service-level scoping — returns null for cross-tenant IDs.
        var bonus = await _svc.GetBonusByIdAsync(id, CallerCompanyIdOrNull);
        if (bonus == null) return NotFound(ApiResponse.Fail("Bonus not found."));

        // IDOR check before any mutation
        if (!await EmployeeBelongsToCallerAsync(bonus.EmployeeId))
            return NotFound(ApiResponse.Fail("Bonus not found."));

        // PayrollLock check on the bonus period
        var cid = CallerCompanyIdOrNull ?? 0;
        if (cid > 0)
        {
            var lockMsg = await _lockGuard.GetLockMessageAsync(cid, bonus.Month, bonus.Year);
            if (lockMsg != null) return Conflict(ApiResponse.Fail(lockMsg));
        }

        var ok = await _svc.DeleteBonusAsync(id, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Bonus deleted."))
                  : NotFound(ApiResponse.Fail("Bonus not found."));
    }
}
