using HRMS.Application.Common;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.API.Controllers.Reports;

/// <summary>Salary Register — comprehensive statutory payroll statement per month.</summary>
[ApiController]
[Route("api/reports/salary-register")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[EnableRateLimiting("reports")]   // BLOCKER-11: 10 report requests/min per IP
public class SalaryRegisterController : BaseController
{
    private readonly IReportService _svc;
    private readonly IStreamingReportService _streaming;

    public SalaryRegisterController(IReportService svc, IStreamingReportService streaming)
    {
        _svc = svc;
        _streaming = streaming;
    }

    // ── IDOR guard ─────────────────────────────────────────────────────────
    // FIX SEC-01: return -1 (fail-closed sentinel) on parse failure, not null.
    // See AttendanceReportController for full explanation.
    private new int? CompanyId =>
        User.IsInRole(AppRoles.SuperAdmin) ? null
        : int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : (int?)-1;

    private int? EffectiveCompanyId(int? requestedId) => CompanyId ?? requestedId;

    /// <summary>
    /// Get salary register for a given month/year.
    /// H-09: Per-employee rows are paginated; aggregate totals always reflect the full period.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? companyId,
        [FromQuery] int  month,
        [FromQuery] int  year,
        [FromQuery] int  page     = 1,
        [FromQuery] int  pageSize = 50)
    {
        if (month < 1 || month > 12) return BadRequest(ApiResponse.Fail("month must be 1–12."));
        if (year < 2000) return BadRequest(ApiResponse.Fail("year must be ≥ 2000."));
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 500);

        var register  = await _svc.GetSalaryRegisterAsync(EffectiveCompanyId(companyId), month, year);
        var pagedRows = register.Rows.ToPagedResult(page, pageSize);

        var response = new
        {
            register.Month,
            register.Year,
            register.EmployeeCount,
            register.TotalCTC,
            register.TotalGross,
            register.TotalPFEmployee,
            register.TotalPFEmployer,
            register.TotalESI,
            register.TotalPT,
            register.TotalTDS,
            register.TotalDeductions,
            register.TotalNetPay,
            Rows = pagedRows
        };

        return Ok(ApiResponse<object>.Ok(response));
    }

    /// <summary>Export salary register as Excel (full dataset — no pagination).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] int? companyId,
        [FromQuery] int  month,
        [FromQuery] int  year)
    {
        if (month < 1 || month > 12) return BadRequest(ApiResponse.Fail("month must be 1–12."));
        if (year < 2000) return BadRequest(ApiResponse.Fail("year must be ≥ 2000."));
        var bytes = await _svc.ExportSalaryRegisterAsync(EffectiveCompanyId(companyId), month, year);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"SalaryRegister_{year}_{month:D2}.xlsx");
    }

    /// <summary>
    /// Memory-efficient streaming Excel export for large salary register datasets.
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
        if (month < 1 || month > 12) return BadRequest(ApiResponse.Fail("month must be 1–12."));
        if (year < 2000) return BadRequest(ApiResponse.Fail("year must be ≥ 2000."));
        var bytes = await _streaming.ExportSalaryRegisterStreamAsync(EffectiveCompanyId(companyId), month, year, ct);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"SalaryRegister_{year}_{month:D2}_Stream.xlsx");
    }
}
