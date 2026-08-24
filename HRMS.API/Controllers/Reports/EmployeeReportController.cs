using HRMS.Application.Common;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.API.Controllers.Reports;

[ApiController]
[Route("api/reports/employees")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[EnableRateLimiting("reports")]   // BLOCKER-11: 10 report requests/min per IP
public class EmployeeReportController : BaseController
{
    private readonly IReportService _svc;
    private readonly IStreamingReportService _streaming;

    public EmployeeReportController(IReportService svc, IStreamingReportService streaming)
    {
        _svc = svc;
        _streaming = streaming;
    }

    // ── IDOR guard ─────────────────────────────────────────────────────────
    // Pattern reference: AnalyticsController.ResolveCompanyId()
    // See: HRMS.API/Controllers/Analytics/AnalyticsController.cs
    // Rule: non-SuperAdmin callers are ALWAYS scoped to their JWT companyId claim.
    // The caller-supplied ?companyId query parameter is overridden for non-SuperAdmins.
    // FIX SEC-01: return -1 (fail-closed sentinel) on parse failure, not null.
    // See AttendanceReportController for full explanation.
    private new int? CompanyId =>
        User.IsInRole(AppRoles.SuperAdmin) ? null
        : int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : (int?)-1;

    private int? EffectiveCompanyId(int? requestedId) => CompanyId ?? requestedId;

    /// <summary>
    /// Employee headcount and department summary.
    /// H-09: The per-employee detail list is paginated; aggregate counts (TotalEmployees,
    /// ByDepartment, etc.) always reflect the full dataset regardless of pagination.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] int? companyId,
        [FromQuery] int  page     = 1,
        [FromQuery] int  pageSize = 50)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 500);

        var report      = await _svc.GetEmployeeSummaryReportAsync(EffectiveCompanyId(companyId));
        var pagedDetails = report.Details.ToPagedResult(page, pageSize);

        // Aggregates are unaffected by pagination so the client can show headcount charts
        // even when viewing a single page of the detail list.
        var response = new
        {
            report.TotalEmployees,
            report.ActiveEmployees,
            report.InactiveEmployees,
            report.ByDepartment,
            report.ByDesignation,
            report.ByGender,
            Details = pagedDetails
        };

        return Ok(ApiResponse<object>.Ok(response));
    }

    /// <summary>Export full employee list as Excel (full dataset — no pagination).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] int? companyId)
    {
        var bytes = await _svc.ExportEmployeeReportAsync(EffectiveCompanyId(companyId));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Employees.xlsx");
    }

    /// <summary>
    /// Memory-efficient streaming Excel export for large employee datasets.
    /// Uses OpenXmlWriter (O(batch) memory) instead of the in-memory ClosedXML approach.
    /// Prefer this endpoint for companies with more than 500 employees.
    /// </summary>
    [HttpGet("export/stream")]
    public async Task<IActionResult> ExportStream([FromQuery] int? companyId, CancellationToken ct)
    {
        var bytes = await _streaming.ExportEmployeeReportStreamAsync(EffectiveCompanyId(companyId), ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Employees_Stream.xlsx");
    }
}
