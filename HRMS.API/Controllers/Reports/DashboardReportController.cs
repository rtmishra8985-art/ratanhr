using HRMS.Application.Common;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.API.Controllers.Reports;

[ApiController]
[Route("api/reports/dashboard")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
[EnableRateLimiting("reports")]   // BLOCKER-11: 10 report requests/min per IP
public class DashboardReportController : BaseController
{
    private readonly IReportService _svc;

    public DashboardReportController(IReportService svc) => _svc = svc;

    // ── IDOR guard ─────────────────────────────────────────────────────────
    // Pattern reference: AnalyticsController.ResolveCompanyId()
    // See: HRMS.API/Controllers/Analytics/AnalyticsController.cs
    // Rule: non-SuperAdmin callers are ALWAYS scoped to their JWT companyId claim.
    // The caller-supplied ?companyId query parameter is overridden for non-SuperAdmins.
    // For regular admins this always returns their JWT companyId claim — the
    // caller-supplied ?companyId query parameter is IGNORED.
    // For superadmins this returns null (unrestricted), so the query parameter
    // can be used to target a specific tenant.
    // FIX SEC-01: return -1 (fail-closed sentinel) on parse failure, not null.
    // See AttendanceReportController for full explanation.
    private new int? CompanyId =>
        User.IsInRole(AppRoles.SuperAdmin) ? null
        : int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : (int?)-1;

    // Derive the effective company to query.
    //   • Admin   → always CompanyId (JWT claim); query param is ignored to prevent IDOR.
    //   • Superadmin → CompanyId is null so the query param is used as the override.
    private int? EffectiveCompanyId(int? requestedId) => CompanyId ?? requestedId;

    /// <summary>Combined dashboard KPIs — headcount, attendance today, payroll this month</summary>
    [HttpGet]
    public async Task<IActionResult> GetDashboard([FromQuery] int? companyId)
    {
        var kpis = await _svc.GetDashboardKpisAsync(EffectiveCompanyId(companyId));
        return Ok(ApiResponse<DashboardKpiDto>.Ok(kpis));
    }

    /// <summary>Real-time KPI summary (alias of GET /dashboard for convenience)</summary>
    [HttpGet("/api/reports/kpis")]
    public async Task<IActionResult> GetKpis()
    {
        // No companyId param on this endpoint — always uses JWT-derived CompanyId (already IDOR-safe).
        var kpis = await _svc.GetDashboardKpisAsync(CompanyId);
        return Ok(ApiResponse<DashboardKpiDto>.Ok(kpis));
    }
}
