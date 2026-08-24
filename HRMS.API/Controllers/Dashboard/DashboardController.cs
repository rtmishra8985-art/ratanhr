using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Dashboard;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "RequireMfaCompleted")]
public class DashboardController : BaseController
{
    private readonly IReportService _report;

    public DashboardController(IReportService report) => _report = report;

    /// <summary>Admin dashboard stats.</summary>
    [HttpGet("admin")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> AdminStats()
    {
        var companyIdClaim = User.FindFirst("companyId")?.Value;
        int? companyId = int.TryParse(companyIdClaim, out int cid) ? cid : (int?)null;
        var stats = await _report.GetAdminDashboardStatsAsync(companyId);
        return Ok(ApiResponse<object>.Ok(stats));
    }

    /// <summary>Super admin dashboard stats.</summary>
    [HttpGet("superadmin")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> SuperAdminStats()
    {
        var stats = await _report.GetSuperAdminDashboardStatsAsync();
        return Ok(ApiResponse<object>.Ok(stats));
    }

    /// <summary>
    /// Employee dashboard — real attendance, leave balances, last payslip, and upcoming holidays.
    /// </summary>
    [HttpGet("employee")]
    [Authorize(Roles = AppRoles.Employee)]
    public async Task<IActionResult> EmployeeStats()
    {
        var empId = User.FindFirst("employeeId")?.Value;
        if (string.IsNullOrEmpty(empId)) return Unauthorized();
        var companyIdClaim = User.FindFirst("companyId")?.Value;
        int? companyId = int.TryParse(companyIdClaim, out int eid) ? eid : (int?)null;
        var stats = await _report.GetEmployeeDashboardStatsAsync(empId, companyId);
        return Ok(ApiResponse<object>.Ok(stats));
    }
}
