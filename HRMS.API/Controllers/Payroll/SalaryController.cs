using HRMS.Application.Common;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HRMS.API.Controllers.Payroll;

/// <summary>
/// Salary structure management — view active structure, view history, create or revise structure.
/// Write operations are blocked when the affected effective month/year is payroll-locked.
/// </summary>
[ApiController]
[Route("api/salary")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[Produces("application/json")]
public class SalaryController : BaseController
{
    private readonly ISalaryStructureService _svc;
    private readonly IEmployeeService        _empSvc;
    private readonly IPayrollLockGuard       _lockGuard;

    public SalaryController(
        ISalaryStructureService svc,
        IEmployeeService        empSvc,
        IPayrollLockGuard       lockGuard)
    {
        _svc       = svc;
        _empSvc    = empSvc;
        _lockGuard = lockGuard;
    }


    private async Task<bool> EmployeeBelongsToCallerAsync(string employeeId)
    {
        var cid = CallerCompanyIdOrNull;
        if (cid == null) return true;
        return await _empSvc.GetByIdAsync(employeeId, cid) != null;
    }

    /// <summary>Get the currently active salary structure for an employee.</summary>
    [HttpGet("{employeeId}")]
    [SwaggerOperation(OperationId = "GetActiveSalaryStructure", Tags = new[] { "Salary" })]
    [ProducesResponseType(typeof(SalaryStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActive(string employeeId)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));
        var s = await _svc.GetActiveAsync(employeeId);
        if (s == null) return NotFound(ApiResponse.Fail("No salary structure found."));
        return Ok(ApiResponse<SalaryStructureDto>.Ok(s));
    }

    /// <summary>Get the full salary revision history for an employee.</summary>
    [HttpGet("{employeeId}/history")]
    [SwaggerOperation(OperationId = "GetSalaryHistory", Tags = new[] { "Salary" })]
    [ProducesResponseType(typeof(List<SalaryStructureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(string employeeId)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));
        // FIX MEDIUM: Pass pagination params — defaults to page 1, 25 records.
        return Ok(ApiResponse<List<SalaryStructureDto>>.Ok(await _svc.GetHistoryAsync(employeeId, 1, 25)));
    }

    /// <summary>
    /// Create or revise salary structure for an employee (deactivates previous record).
    /// Blocked when the effective-from month/year is payroll-locked for the employee's company.
    /// </summary>
    [HttpPost("{employeeId}")]
    [SwaggerOperation(OperationId = "UpsertSalaryStructure", Tags = new[] { "Salary" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Upsert(string employeeId, [FromBody] CreateSalaryStructureDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));

        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));

        // PayrollLock check: block if the effective month/year is locked
        var cid = CallerCompanyIdOrNull;
        if (cid.HasValue)
        {
            var lockMsg = await _lockGuard.GetLockMessageAsync(
                cid.Value, dto.EffectiveFrom.Month, dto.EffectiveFrom.Year);
            if (lockMsg != null) return Conflict(ApiResponse.Fail(lockMsg));
        }

        dto.EmployeeId       = employeeId;
        dto.CreatedByUserId  = UserId;
        var id = await _svc.UpsertAsync(dto);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, "Salary structure saved."));
    }
}
