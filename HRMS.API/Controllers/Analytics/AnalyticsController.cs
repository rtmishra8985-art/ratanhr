using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Analytics;

[ApiController]
[Route("api/analytics")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class AnalyticsController : BaseController
{
    private readonly IAnalyticsService _svc;

    public AnalyticsController(IAnalyticsService svc) => _svc = svc;

    // ── IDOR fix ──────────────────────────────────────────────────────────────
    // Non-superadmin callers are always scoped to their own company.
    // The `companyId` query-param is accepted only when the caller is superadmin;
    // all other roles have it overridden by the JWT claim to prevent cross-tenant
    // data leakage.
    //
    // FIX #5: The previous fallback was `CallerCompanyIdOrNull ?? 0`. When a
    // non-superadmin's companyId claim is absent or malformed, ?? 0 silently
    // resolves to company 0, which could match real data or return empty results
    // with no error. Callers now receive a -1 sentinel (same safe value used by
    // BaseController.CompanyId) so service-layer queries return nothing instead
    // of accidentally matching company 0.
    private int ResolveCompanyId(int requested) =>
        User.IsInRole(AppRoles.SuperAdmin) && requested > 0
            ? requested
            : CallerCompanyIdOrNull ?? -1;   // -1 = safe sentinel, no real company ID

    /// <summary>Headcount breakdown by active/inactive and department for a company.</summary>
    [HttpGet("headcount")]
    public async Task<IActionResult> Headcount([FromQuery] int companyId, [FromQuery] int year)
    {
        var cid = ResolveCompanyId(companyId);
        var result = await _svc.GetHeadcountAsync(cid, year > 0 ? year : DateTime.UtcNow.Year);
        return Ok(ApiResponse<object>.Ok(result, "Headcount analytics retrieved."));
    }

    /// <summary>Attendance summary for a company for a given period (YYYY-MM).</summary>
    [HttpGet("attendance")]
    public async Task<IActionResult> Attendance(
        [FromQuery] int companyId, [FromQuery] string period)
    {
        var cid = ResolveCompanyId(companyId);
        if (string.IsNullOrWhiteSpace(period))
            period = DateTime.UtcNow.ToString("yyyy-MM");

        var result = await _svc.GetAttendanceSummaryAsync(cid, period);
        return Ok(ApiResponse<object>.Ok(result, "Attendance analytics retrieved."));
    }

    /// <summary>Monthly payroll cost summary for a company in a given year.</summary>
    [HttpGet("payroll")]
    public async Task<IActionResult> Payroll([FromQuery] int companyId, [FromQuery] int year)
    {
        var cid = ResolveCompanyId(companyId);
        var result = await _svc.GetPayrollSummaryAsync(cid, year > 0 ? year : DateTime.UtcNow.Year);
        return Ok(ApiResponse<object>.Ok(result, "Payroll analytics retrieved."));
    }

    /// <summary>Employee turnover rate for a company in a given year.</summary>
    [HttpGet("turnover")]
    public async Task<IActionResult> Turnover([FromQuery] int companyId, [FromQuery] int year)
    {
        var cid = ResolveCompanyId(companyId);
        var result = await _svc.GetTurnoverAsync(cid, year > 0 ? year : DateTime.UtcNow.Year);
        return Ok(ApiResponse<object>.Ok(result, "Turnover analytics retrieved."));
    }
}
