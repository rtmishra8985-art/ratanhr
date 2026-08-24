using HRMS.Application.Common;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.API.Controllers.Reports;

[ApiController]
[Route("api/reports/attendance")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[EnableRateLimiting("reports")]   // BLOCKER-11: 10 report requests/min per IP
public class AttendanceReportController : BaseController
{
    private readonly IReportService _svc;
    private readonly IStreamingReportService _streaming;

    public AttendanceReportController(IReportService svc, IStreamingReportService streaming)
    {
        _svc = svc;
        _streaming = streaming;
    }

    // ── IDOR guard ─────────────────────────────────────────────────────────
    // Pattern reference: AnalyticsController.ResolveCompanyId()
    // See: HRMS.API/Controllers/Analytics/AnalyticsController.cs
    // Rule: non-SuperAdmin callers are ALWAYS scoped to their JWT companyId claim.
    // The caller-supplied ?companyId query parameter is overridden for non-SuperAdmins.
    // FIX SEC-01: return -1 (fail-closed sentinel) on parse failure, NOT null.
    // Returning null on a failed parse was identical to the SuperAdmin bypass
    // path, so a non-SuperAdmin with a malformed/absent companyId claim could
    // supply any ?companyId= query parameter and receive cross-tenant data.
    // -1 can never match a real company PK (auto-increment starts at 1) so it
    // always produces an empty result set — fail closed.
    private new int? CompanyId =>
        User.IsInRole(AppRoles.SuperAdmin) ? null
        : int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : (int?)-1;

    private int? EffectiveCompanyId(int? requestedId) => CompanyId ?? requestedId;

    /// <summary>
    /// Monthly attendance summary by employee.
    /// H-09: Results are paginated — use page and pageSize to navigate large datasets.
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

        var report = await _svc.GetMonthlyAttendanceReportAsync(EffectiveCompanyId(companyId), month, year);
        var paged  = report.ToPagedResult(page, pageSize);
        return Ok(ApiResponse<PagedResult<MonthlyAttendanceReportDto>>.Ok(paged));
    }

    /// <summary>
    /// Daily attendance breakdown for a date range.
    /// H-09: Results are paginated — use page and pageSize to navigate large datasets.
    /// </summary>
    [HttpGet("daily")]
    public async Task<IActionResult> Daily(
        [FromQuery] int?    companyId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 50)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 500);

        var report = await _svc.GetDailyAttendanceReportAsync(EffectiveCompanyId(companyId), from, to);
        var paged  = report.ToPagedResult(page, pageSize);
        return Ok(ApiResponse<PagedResult<DailyAttendanceReportDto>>.Ok(paged));
    }

    /// <summary>Export attendance report as Excel (full dataset — no pagination).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] int? companyId,
        [FromQuery] int  month,
        [FromQuery] int  year)
    {
        var bytes = await _svc.ExportAttendanceReportAsync(EffectiveCompanyId(companyId), month, year);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Attendance_{year}_{month:D2}.xlsx");
    }

    /// <summary>
    /// Memory-efficient streaming Excel export for large attendance datasets.
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
        var bytes = await _streaming.ExportAttendanceReportStreamAsync(EffectiveCompanyId(companyId), month, year, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Attendance_{year}_{month:D2}_Stream.xlsx");
    }
}
