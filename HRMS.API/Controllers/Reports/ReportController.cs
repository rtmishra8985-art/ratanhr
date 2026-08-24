using HRMS.Application.Common;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.API.Controllers.Reports;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[EnableRateLimiting("reports")]   // BLOCKER-11: 10 report requests/min per IP (expensive endpoints)
public class ReportController : BaseController
{
    private readonly IReportService _service;
    private readonly IEmployeeService _empService;

    public ReportController(IReportService service, IEmployeeService empService)
    { _service = service; _empService = empService; }

    /// <summary>Attendance report with filters (month, year, department, employeeId, type: web|excel)</summary>
    [HttpGet("attendance")]
    public async Task<IActionResult> AttendanceReport([FromQuery] AttendanceReportFilterDto filter)
    {
        // FIX LOW: Use BaseController helper instead of raw claim parsing for consistency.
        // SuperAdmins (CallerCompanyIdOrNull == null) may pass CompanyId via query param;
        // non-SuperAdmins are always restricted to their own company.
        if (!User.IsInRole(AppRoles.SuperAdmin))
            filter.CompanyId = CallerCompanyIdOrNull;
        var rows = await _service.GetAttendanceReportAsync(filter);
        return Ok(ApiResponse<List<AttendanceReportItemDto>>.Ok(rows));
    }

    /// <summary>Employee report with filters (department, designation, gender, status)</summary>
    [HttpGet("employees")]
    public async Task<IActionResult> EmployeeReport([FromQuery] EmployeeReportFilterDto filter)
    {
        // FIX LOW: Use BaseController helper instead of raw claim parsing for consistency.
        int? companyId = User.IsInRole(AppRoles.SuperAdmin) ? filter.CompanyId : CallerCompanyIdOrNull;

        var all = await _empService.GetAllAsync(companyId);
        var filtered = all.AsQueryable();
        if (!string.IsNullOrEmpty(filter.Department))
            filtered = filtered.Where(e => e.Department == filter.Department);
        if (!string.IsNullOrEmpty(filter.Designation))
            filtered = filtered.Where(e => e.Designation == filter.Designation);
        if (!string.IsNullOrEmpty(filter.Gender))
            filtered = filtered.Where(e => e.Gender == filter.Gender);
        if (!string.IsNullOrEmpty(filter.Status))
            filtered = filtered.Where(e => filter.Status == "active" ? e.IsActive : !e.IsActive);

        return Ok(ApiResponse<object>.Ok(filtered.ToList()));
    }
}
