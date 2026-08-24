using HRMS.Application.Common;
using HRMS.Application.DTOs.Timesheet;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Timesheet;

[ApiController]
[Route("api/timesheet")]
[Authorize(Policy = "RequireMfaCompleted")]
public class TimesheetController : BaseController
{
    private readonly ITimesheetService _svc;

    public TimesheetController(ITimesheetService svc) => _svc = svc;

    /// <summary>Employee: view own timesheet entries.</summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMine([FromQuery] PaginationQuery q)
    {
        var empId = User.FindFirst("employeeId")?.Value ?? string.Empty;
        // FIX 3: safe sentinel (-1 matches no company); was ?? 0
        var cid   = CallerCompanyIdOrNull ?? -1;
        var result = await _svc.GetByEmployeeAsync(empId, cid, q);
        return Ok(ApiResponse<object>.Ok(result, "Timesheets retrieved."));
    }

    /// <summary>Admin: list all Submitted entries pending approval.</summary>
    [HttpGet("pending")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> GetPending([FromQuery] PaginationQuery q)
    {
        // FIX 3: safe sentinel (-1 matches no company); was ?? 0
        var cid = CallerCompanyIdOrNull ?? -1;
        var result = await _svc.GetPendingApprovalsAsync(cid, q);
        return Ok(ApiResponse<object>.Ok(result, "Pending timesheets retrieved."));
    }

    /// <summary>Create a new timesheet entry (Draft).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTimesheetDto dto)
    {
        // FIX 3: safe sentinel (-1 matches no company); was ?? 0
        var cid = CallerCompanyIdOrNull ?? -1;
        var result = await _svc.CreateAsync(dto, cid);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<object>.Ok(result, "Timesheet entry created."));
    }

    /// <summary>Update a Draft timesheet entry.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateTimesheetDto dto)
    {
        var empId = User.FindFirst("employeeId")?.Value ?? string.Empty;
        var result = await _svc.UpdateAsync(id, dto, empId);
        return Ok(ApiResponse<object>.Ok(result, "Timesheet entry updated."));
    }

    /// <summary>Submit a Draft entry for approval.</summary>
    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        var empId = User.FindFirst("employeeId")?.Value ?? string.Empty;
        await _svc.SubmitAsync(id, empId);
        return Ok(ApiResponse.Ok("Timesheet submitted for approval."));
    }

    /// <summary>Admin: approve a submitted timesheet.</summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Approve(int id, [FromQuery] string? remarks)
    {
        // FIX 2b: pass caller's companyId so service can scope the entry lookup.
        var approveCid = CallerCompanyIdOrNull ?? -1;
        await _svc.ApproveAsync(id, UserId, approveCid, remarks);
        return Ok(ApiResponse.Ok("Timesheet approved."));
    }

    /// <summary>Admin: reject a submitted timesheet with mandatory remarks.</summary>
    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Reject(int id, [FromBody] TimesheetRejectDto dto)
    {
        // FIX 2c: pass caller's companyId so service can scope the entry lookup.
        var rejectCid = CallerCompanyIdOrNull ?? -1;
        await _svc.RejectAsync(id, UserId, rejectCid, dto.Remarks);
        return Ok(ApiResponse.Ok("Timesheet rejected."));
    }

    /// <summary>Delete a Draft timesheet entry.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var empId = User.FindFirst("employeeId")?.Value ?? string.Empty;
        await _svc.DeleteAsync(id, empId);
        return Ok(ApiResponse.Ok("Timesheet entry deleted."));
    }
}
