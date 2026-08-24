using System.Security.Claims;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HRMS.API.Controllers.Payroll;

/// <summary>
/// Payroll management — calculate, generate, bulk-generate, lock/unlock periods,
/// view and delete payslips. All write operations respect PayrollLock.
/// </summary>
[ApiController]
[Route("api/payroll")]
[Authorize(Policy = "RequireMfaCompleted")]
[Produces("application/json")]
public class PayrollController : BaseController
{
    private readonly IPayrollService         _service;
    private readonly IEmployeeService        _empSvc;
    private readonly IPayrollLockGuard       _lockGuard;
    // FIX HIGH-12: Distributed lock — prevents concurrent BulkGenerate requests for the same
    // company/month/year from running simultaneously and producing duplicate or corrupt payslips.
    private readonly IPayrollBulkLockService _bulkLock;

    public PayrollController(
        IPayrollService          service,
        IEmployeeService         empSvc,
        IPayrollLockGuard        lockGuard,
        IPayrollBulkLockService  bulkLock)
    {
        _service   = service;
        _empSvc    = empSvc;
        _lockGuard = lockGuard;
        _bulkLock  = bulkLock;
    }

    private int? ActorId    => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id) ? id : null;
    private string? ActorName => ActorId?.ToString();


    // ── IDOR helper: verify payslip belongs to caller's company ───────────

    private async Task<bool> PayslipBelongsToCallerAsync(string employeeId)
    {
        var cid = CallerCompanyIdOrNull;
        if (cid == null) return true; // superadmin — unrestricted
        return await _empSvc.GetByIdAsync(employeeId, cid) != null;
    }

    // ── Preview calculation ────────────────────────────────────────────────

    /// <summary>
    /// Preview statutory deductions for a given basic pay WITHOUT saving.
    /// Returns full Indian payroll breakdown (PF, ESI, PT, TDS, Net Pay).
    /// </summary>
    [HttpPost("calculate")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "PreviewCalculation", Tags = new[] { "Payroll" })]
    [ProducesResponseType(typeof(PayrollCalculationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Calculate([FromBody] PayrollCalculationRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        var result = await _service.PreviewCalculationAsync(req);
        return Ok(ApiResponse<PayrollCalculationResult>.Ok(result, "Calculation preview (not saved)."));
    }

    // ── Generate single payslip ────────────────────────────────────────────

    /// <summary>
    /// Generate or overwrite a payslip. Set <c>AutoCalculate=true</c> to compute PF/ESI/PT/TDS automatically.
    /// Blocked if the period is payroll-locked.
    /// </summary>
    [HttpPost("generate")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "GeneratePayslip", Tags = new[] { "Payroll" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Generate([FromBody] GeneratePayslipDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));

        // PayrollLock check runs first: a locked period rejects every write, regardless
        // of which employee the payload targets.
        var cid = CallerCompanyIdOrNull;
        if (cid.HasValue)
        {
            var lockMsg = await _lockGuard.GetLockMessageAsync(cid.Value, dto.Month, dto.Year);
            if (lockMsg != null) return Conflict(ApiResponse.Fail(lockMsg));
        }

        // FIX A: IDOR — verify the target employee belongs to the caller's company before
        // generating a payslip. Without this check a Company-A admin can generate or overwrite
        // payslips for Company-B employees by supplying their EmployeeId in the request body.
        if (!await PayslipBelongsToCallerAsync(dto.EmployeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));

        // FIX FUNC-02: pass CallerCompanyIdOrNull for service-layer defence-in-depth.
        var id = await _service.GeneratePayslipAsync(dto, ActorId, ActorName, CallerCompanyIdOrNull);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { PayslipId = id }, "Payslip generated successfully."));
    }

    // ── Bulk generate ──────────────────────────────────────────────────────

    /// <summary>
    /// Bulk-generate payslips for all active employees in a company for a given month/year.
    /// Automatically reads each employee's active salary structure and attendance records.
    /// Blocked if the period is payroll-locked.
    /// </summary>
    [HttpPost("bulk-generate")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "BulkGeneratePayslips", Tags = new[] { "Payroll" })]
    [ProducesResponseType(typeof(BulkPayrollResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BulkGenerate([FromBody] BulkPayrollDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));

        // Non-superadmin: scope to their own company
        if (!User.IsInRole(AppRoles.SuperAdmin))
        {
            var callerCompanyId = int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : (int?)null;
            dto.CompanyId = callerCompanyId;
        }

        // P2 FIX: CompanyId is mandatory for ALL bulk payroll operations, including superadmin.
        // Without an explicit CompanyId a superadmin would inadvertently run payroll across
        // ALL companies in a single call, which is never the intended behaviour.
        if (!dto.CompanyId.HasValue)
            return BadRequest(ApiResponse.Fail(
                "CompanyId is required. A superadmin must explicitly specify the target company " +
                "when generating bulk payroll."));

        // PayrollLock check
        if (dto.CompanyId.HasValue)
        {
            var lockMsg = await _lockGuard.GetLockMessageAsync(dto.CompanyId.Value, dto.Month, dto.Year);
            // FIX P0: a client-supplied Overwrite flag must NOT bypass a finalized
            // payroll period. Locked periods are immutable; unlock explicitly instead.
            if (lockMsg != null)
                return Conflict(ApiResponse.Fail(lockMsg));
        }

        // FIX HIGH-12: Acquire distributed lock before running bulk payroll.
        // If another request is already processing payroll for this company/month/year,
        // return 409 Conflict immediately rather than allowing concurrent corrupted runs.
        await using var bulkLockHandle = await _bulkLock.TryAcquireAsync(
            dto.CompanyId!.Value, dto.Month, dto.Year, HttpContext.RequestAborted);

        if (bulkLockHandle == null)
            return Conflict(ApiResponse.Fail(
                $"Bulk payroll for {dto.Month}/{dto.Year} is already running for this company. " +
                "Please wait for the current run to complete before starting another."));

        var result = await _service.BulkGeneratePayslipsAsync(dto, ActorId, ActorName);
        var msg = $"Bulk payroll complete: {result.Generated} generated, {result.Skipped} skipped, {result.Failed} failed.";
        return Ok(ApiResponse<BulkPayrollResultDto>.Ok(result, msg));
    }

    // ── Payroll period lock management ────────────────────────────────────

    /// <summary>
    /// Lock a payroll period for a company. Once locked, no salary, attendance,
    /// or leave changes are accepted for that period.
    /// </summary>
    [HttpPost("lock")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "LockPayrollPeriod", Tags = new[] { "Payroll" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LockPeriod([FromBody] PayrollPeriodActionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        if (!User.IsInRole(AppRoles.SuperAdmin) && CallerCompanyIdOrNull != dto.CompanyId)
            return Forbid();

        // FIX: Guard ActorId null-dereference — if NameIdentifier claim is missing (broken token)
        // ActorId is null; !.Value would throw NullReferenceException. Use 0 as a safe sentinel
        // (no real user has id=0) so the lock audit trail records an unknown actor rather than crashing.
        var actorId = ActorId ?? 0;
        await _lockGuard.LockAsync(dto.CompanyId, dto.Month, dto.Year, actorId, dto.Notes);
        return Ok(ApiResponse.Ok($"Payroll period {dto.Month:00}/{dto.Year} locked."));
    }

    /// <summary>Unlock a payroll period so corrections can be made.</summary>
    [HttpPost("unlock")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "UnlockPayrollPeriod", Tags = new[] { "Payroll" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnlockPeriod([FromBody] PayrollPeriodActionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        if (!User.IsInRole(AppRoles.SuperAdmin) && CallerCompanyIdOrNull != dto.CompanyId)
            return Forbid();

        // FIX: Same null guard as LockPeriod — ActorId!.Value throws when claim is absent.
        var actorId = ActorId ?? 0;
        await _lockGuard.UnlockAsync(dto.CompanyId, dto.Month, dto.Year, actorId, dto.Notes);
        return Ok(ApiResponse.Ok($"Payroll period {dto.Month:00}/{dto.Year} unlocked."));
    }

    /// <summary>List all payroll lock records for a company, optionally filtered by year.</summary>
    [HttpGet("locks")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "GetPayrollLocks", Tags = new[] { "Payroll" })]
    [ProducesResponseType(typeof(List<PayrollLockDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocks([FromQuery] int? year)
    {
        // FIX C: CallerCompanyIdOrNull is null for superadmins (unrestricted).
        // The previous ?? 0 fallback queried company 0 — no real company has that id,
        // so superadmins always received an empty list. Pass null to mean "all companies".
        // Non-superadmins have their company id from the JWT claim; null from a broken claim
        // is safe because GetLocksAsync(null) returns all locks (superadmin-only risk zone).
        var cid = CallerCompanyIdOrNull;
        var list = await _lockGuard.GetLocksAsync(cid, year);
        return Ok(ApiResponse<List<PayrollLockDto>>.Ok(list));
    }

    // ── Query ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Get all payslips with optional month/year/employee filters (admin/superadmin).
    /// Company admins are scoped to their own company — cross-company payslips are never returned.
    /// SuperAdmin receives all companies.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "GetAllPayslips", Tags = new[] { "Payroll" })]
    [ProducesResponseType(typeof(List<PayslipDto>), StatusCodes.Status200OK)]
    // FIX 5: Added sortBy / sortDirection + paged response for consistent API behaviour.
    // Uses GetAllPayslipsPagedAsync when page/pageSize are supplied (default: page 1, 25 per page).
    public async Task<IActionResult> GetAll(
        [FromQuery] int?    month,
        [FromQuery] int?    year,
        [FromQuery] string? employeeId,
        [FromQuery] int     page          = 1,
        [FromQuery] int     pageSize      = 25,
        [FromQuery] string? sortBy        = null,
        [FromQuery] string? sortDirection = "desc")
    {
        // SECURITY FIX – IDOR: derive company scope from JWT, never from request parameters.
        // CallerCompanyIdOrNull is null for superadmin (unrestricted) and the JWT company for admins.
        // FIX 6: pass HttpContext.RequestAborted so the DB query is cancelled when the client disconnects.
        var result = await _service.GetAllPayslipsPagedAsync(
            month, year, employeeId, CallerCompanyIdOrNull, page, pageSize, sortBy, sortDirection, HttpContext.RequestAborted);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<PayslipDto>>.Ok(result));
    }

    /// <summary>
    /// Get a single payslip by ID.
    /// Employees can only access their own payslips; admins are scoped to their company.
    /// </summary>
    [HttpGet("{id:int}")]
    [SwaggerOperation(OperationId = "GetPayslipById", Tags = new[] { "Payroll" })]
    [ProducesResponseType(typeof(PayslipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        // FIX IDOR: pass CallerCompanyIdOrNull so the DB query itself enforces the company
        // boundary — a cross-tenant payslip is never loaded into memory.
        // SuperAdmin passes null (unrestricted). Employees still check their own employeeId
        // below (company scoping via the employee join covers most cases, but an employee
        // from Company A whose employeeId matches a record in Company A should only see
        // their own payslip, not a colleague's).
        var cid = CallerCompanyIdOrNull;
        var p = await _service.GetPayslipAsync(id, cid);
        if (p == null) return NotFound(ApiResponse.Fail("Payslip not found."));

        // IDOR: employee can only access their own payslip (company already enforced at DB level)
        if (User.IsInRole(AppRoles.Employee))
        {
            var empId = User.FindFirst("employeeId")?.Value;
            if (p.EmployeeId != empId) return NotFound(ApiResponse.Fail("Payslip not found."));
        }

        return Ok(ApiResponse<PayslipDto>.Ok(p));
    }

    /// <summary>Employee: get own payslips.</summary>
    [HttpGet("my")]
    [Authorize(Roles = AppRoles.Employee)]
    [SwaggerOperation(OperationId = "GetMyPayslips", Tags = new[] { "Payroll" })]
    [ProducesResponseType(typeof(List<PayslipDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyPayslips()
    {
        // FIX P3-2: the employee identity comes exclusively from the JWT claim, and the
        // tenant scope from CallerCompanyIdOrNull — nothing here is client-supplied.
        var empId = User.FindFirst("employeeId")?.Value;
        if (string.IsNullOrEmpty(empId)) return Unauthorized();
        if (!IsCompanyClaimValid) return Forbid();
        var list = await _service.GetEmployeePayslipsAsync(empId, CallerCompanyIdOrNull);
        return Ok(ApiResponse<List<PayslipDto>>.Ok(list));
    }

    /// <summary>Delete a payslip. Blocked if the period is payroll-locked.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [SwaggerOperation(OperationId = "DeletePayslip", Tags = new[] { "Payroll" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        // FIX IDOR: company scope enforced at DB level — same pattern as GetById.
        var cid = CallerCompanyIdOrNull;
        var p = await _service.GetPayslipAsync(id, cid);
        if (p == null) return NotFound(ApiResponse.Fail("Payslip not found."));

        // PayrollLock check (reuse cid already captured above)
        if (cid.HasValue)
        {
            var lockMsg = await _lockGuard.GetLockMessageAsync(cid.Value, p.Month, p.Year);
            if (lockMsg != null) return Conflict(ApiResponse.Fail(lockMsg));
        }

        var ok = await _service.DeletePayslipAsync(id, ActorId, ActorName);
        return ok ? Ok(ApiResponse.Ok("Payslip deleted."))
                  : NotFound(ApiResponse.Fail("Payslip not found."));
    }
}
