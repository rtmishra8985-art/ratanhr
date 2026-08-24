using HRMS.Application.Common;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.API.Controllers.Reports;

[ApiController]
[Route("api/reports/payroll")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[EnableRateLimiting("reports")]   // BLOCKER-11: 10 report requests/min per IP (expensive endpoints)
public class PayrollReportController : BaseController
{
    private readonly IReportService _svc;
    private readonly IStreamingReportService _streaming;

    public PayrollReportController(IReportService svc, IStreamingReportService streaming)
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
    /// Payroll cost summary for a month.
    /// H-09: The per-employee line items are paginated; aggregate totals (TotalGross, etc.)
    /// always reflect the full dataset for the period regardless of pagination.
    /// </summary>
    [HttpGet("monthly")]
    public async Task<IActionResult> Monthly(
        [FromQuery] int? companyId,
        [FromQuery] int  month,
        [FromQuery] int  year,
        [FromQuery] int  page     = 1,
        [FromQuery] int  pageSize = 50)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 500);

        var report       = await _svc.GetPayrollReportAsync(EffectiveCompanyId(companyId), month, year);
        var pagedItems   = report.Items.ToPagedResult(page, pageSize);

        // Return totals + paged items together so the client can display summary
        // without fetching every page.
        var response = new
        {
            report.Month,
            report.Year,
            report.EmployeeCount,
            report.TotalGross,
            report.TotalDeductions,
            report.TotalNetPay,
            report.TotalPFEmployee,
            report.TotalPFEmployer,
            report.TotalESI,
            report.TotalPT,
            report.TotalTDS,
            Items = pagedItems
        };

        return Ok(ApiResponse<object>.Ok(response));
    }

    /// <summary>Export payroll report as Excel (full dataset — no pagination).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] int? companyId,
        [FromQuery] int  month,
        [FromQuery] int  year)
    {
        var bytes = await _svc.ExportPayrollReportAsync(EffectiveCompanyId(companyId), month, year);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Payroll_{year}_{month:D2}.xlsx");
    }

    /// <summary>
    /// Memory-efficient streaming Excel export for large payroll datasets.
    /// Uses OpenXmlWriter (O(batch) memory) instead of the in-memory ClosedXML approach.
    /// Prefer this endpoint for companies with more than 500 employees.
    /// </summary>
    [HttpGet("export/stream")]
    public async Task<IActionResult> ExportStream(
        [FromQuery] int? companyId,
        [FromQuery] int  month,
        [FromQuery] int  year,
        CancellationToken ct)
    {
        var bytes = await _streaming.ExportPayrollReportStreamAsync(EffectiveCompanyId(companyId), month, year, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Payroll_{year}_{month:D2}_Stream.xlsx");
    }
}
