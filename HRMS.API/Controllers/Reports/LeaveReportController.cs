using HRMS.Application.Common;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.API.Controllers.Reports;

/// <summary>Leave utilisation report — summary and Excel export.</summary>
[ApiController]
[Route("api/reports/leave")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[EnableRateLimiting("reports")]   // BLOCKER-11: 10 report requests/min per IP
public class LeaveReportController : BaseController
{
    private readonly IReportService _svc;
    private readonly IStreamingReportService _streaming;

    public LeaveReportController(IReportService svc, IStreamingReportService streaming)
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
    /// Leave request summary for a given month/year.
    /// Pass month=0 to get the full year.
    /// H-09: The per-request detail list is paginated; aggregate counts (TotalRequests,
    /// Approved, etc.) always reflect the full period regardless of pagination.
    /// </summary>
    [HttpGet("monthly")]
    public async Task<IActionResult> Monthly(
        [FromQuery] int? companyId,
        [FromQuery] int  month,
        [FromQuery] int  year,
        [FromQuery] int  page     = 1,
        [FromQuery] int  pageSize = 50)
    {
        if (month < 0 || month > 12) return BadRequest(ApiResponse.Fail("month must be 0–12 (0 = full year)."));
        if (year < 2000) return BadRequest(ApiResponse.Fail("year must be ≥ 2000."));
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 500);

        var report       = await _svc.GetLeaveReportAsync(EffectiveCompanyId(companyId), month, year);
        var pagedDetails = report.Details.ToPagedResult(page, pageSize);

        var response = new
        {
            report.Month,
            report.Year,
            report.CompanyId,
            report.TotalRequests,
            report.Approved,
            report.Rejected,
            report.Pending,
            report.TotalDaysApproved,
            Details = pagedDetails
        };

        return Ok(ApiResponse<object>.Ok(response));
    }

    /// <summary>Export leave report as Excel (full dataset — no pagination).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] int? companyId,
        [FromQuery] int  month,
        [FromQuery] int  year)
    {
        if (month < 0 || month > 12) return BadRequest(ApiResponse.Fail("month must be 0–12."));
        if (year < 2000) return BadRequest(ApiResponse.Fail("year must be ≥ 2000."));
        var bytes = await _svc.ExportLeaveReportAsync(EffectiveCompanyId(companyId), month, year);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"LeaveReport_{year}_{month:D2}.xlsx");
    }

    /// <summary>
    /// Memory-efficient streaming Excel export for large leave datasets.
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
        if (month < 0 || month > 12) return BadRequest(ApiResponse.Fail("month must be 0–12."));
        if (year < 2000) return BadRequest(ApiResponse.Fail("year must be ≥ 2000."));
        var bytes = await _streaming.ExportLeaveReportStreamAsync(EffectiveCompanyId(companyId), month, year, ct);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"LeaveReport_{year}_{month:D2}_Stream.xlsx");
    }
}
