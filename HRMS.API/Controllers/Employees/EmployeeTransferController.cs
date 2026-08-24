using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Employees;

[ApiController]
[Route("api/employees/{employeeId}/transfers")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class EmployeeTransferController : BaseController
{
    private readonly IEmployeeTransferService _svc;
    private readonly IEmployeeService         _empSvc;

    public EmployeeTransferController(IEmployeeTransferService svc, IEmployeeService empSvc)
    {
        _svc    = svc;
        _empSvc = empSvc;
    }

    // ── IDOR guard helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the caller's company ID from their JWT claim, or null for superadmin
    /// (unrestricted cross-tenant). Returns -1 on parse failure so IDOR checks fail
    /// closed rather than open.
    /// </summary>

    /// <summary>
    /// Confirms the employee exists and belongs to the caller's company.
    /// Superadmins bypass this check (CallerCompanyIdOrNull == null).
    /// </summary>
    private async Task<bool> EmployeeBelongsToCallerAsync(string employeeId)
    {
        var emp = await _empSvc.GetByIdAsync(employeeId, CallerCompanyIdOrNull);
        return emp != null;
    }

    // ── Endpoints ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(
        string employeeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));

        var result = await _svc.GetTransfersPagedAsync(employeeId, page, pageSize);
        return Ok(ApiResponse<PagedResult<EmployeeTransferDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create(string employeeId, [FromBody] CreateTransferDto dto)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));

        dto.EmployeeId = employeeId;
        var id = await _svc.CreateTransferAsync(dto);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, "Transfer initiated."));
    }

    // Approve / Reject are superadmin-only — no additional IDOR guard needed.

    [HttpPatch("{transferId:int}/approve")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Approve(string employeeId, int transferId)
    {
        var userId = UserId;
        var ok = await _svc.ApproveTransferAsync(transferId, userId);
        return ok ? Ok(ApiResponse.Ok("Transfer approved.")) : NotFound(ApiResponse.Fail("Transfer not found."));
    }

    [HttpPatch("{transferId:int}/reject")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Reject(string employeeId, int transferId)
    {
        var ok = await _svc.RejectTransferAsync(transferId);
        return ok ? Ok(ApiResponse.Ok("Transfer rejected.")) : NotFound(ApiResponse.Fail("Transfer not found."));
    }
}
