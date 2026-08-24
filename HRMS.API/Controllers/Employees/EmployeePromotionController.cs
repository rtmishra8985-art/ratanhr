using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Employees;

[ApiController]
[Route("api/employees/{employeeId}/promotions")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class EmployeePromotionController : BaseController
{
    private readonly IEmployeePromotionService _svc;
    private readonly IEmployeeService          _empSvc;

    public EmployeePromotionController(IEmployeePromotionService svc, IEmployeeService empSvc)
    {
        _svc    = svc;
        _empSvc = empSvc;
    }


    private async Task<bool> EmployeeBelongsToCallerAsync(string employeeId)
    {
        var cid = CallerCompanyIdOrNull;
        if (cid == null) return true;
        return await _empSvc.GetByIdAsync(employeeId, cid) != null;
    }

    /// <summary>List all promotions for an employee — paginated</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        string employeeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));
        var result = await _svc.GetPromotionsPagedAsync(employeeId, page, pageSize);
        return Ok(ApiResponse<PagedResult<EmployeePromotionDto>>.Ok(result));
    }

    /// <summary>Record a promotion for an employee</summary>
    [HttpPost]
    public async Task<IActionResult> Create(string employeeId, [FromBody] CreatePromotionDto dto)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));

        dto.EmployeeId      = employeeId;
        dto.CreatedByUserId = UserId;
        var id = await _svc.CreatePromotionAsync(dto);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, "Promotion recorded."));
    }

    /// <summary>Delete a promotion record (superadmin only — used for data corrections)</summary>
    [HttpDelete("{promotionId:int}")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Delete(string employeeId, int promotionId)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));

        var ok = await _svc.DeletePromotionAsync(promotionId, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Promotion record deleted."))
                  : NotFound(ApiResponse.Fail("Promotion not found."));
    }
}
