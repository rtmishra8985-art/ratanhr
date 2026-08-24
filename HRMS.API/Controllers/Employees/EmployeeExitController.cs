using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Employees;

[ApiController]
[Route("api/employees/{employeeId}/exit")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class EmployeeExitController : BaseController
{
    private readonly IEmployeeExitService _svc;
    private readonly IEmployeeService     _empSvc;

    public EmployeeExitController(IEmployeeExitService svc, IEmployeeService empSvc)
    { _svc = svc; _empSvc = empSvc; }


    private async Task<bool> EmployeeBelongsToCallerAsync(string employeeId)
    {
        var cid = CallerCompanyIdOrNull;
        if (cid == null) return true;
        return await _empSvc.GetByIdAsync(employeeId, cid) != null;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string employeeId)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));
        var exit = await _svc.GetExitAsync(employeeId);
        if (exit == null) return NotFound(ApiResponse.Fail("No exit record found."));
        return Ok(ApiResponse<EmployeeExitDto>.Ok(exit));
    }

    [HttpPost]
    public async Task<IActionResult> Initiate(string employeeId, [FromBody] InitiateExitDto dto)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));
        dto.EmployeeId = employeeId;
        dto.InitiatedByUserId = UserId;
        var id = await _svc.InitiateExitAsync(dto);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new { Id = id }, "Exit process initiated."));
    }

    [HttpPatch("{exitId:int}/complete")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Complete(string employeeId, int exitId, [FromBody] CompleteExitDto dto)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));
        var ok = await _svc.CompleteExitAsync(exitId, dto);
        return ok ? Ok(ApiResponse.Ok("Exit process completed.")) : NotFound(ApiResponse.Fail("Exit record not found."));
    }
}
