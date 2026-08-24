using HRMS.Application.Common;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Controllers.Audit;

/// <summary>Filtered view of login events from the audit log.</summary>
[ApiController]
[Route("api/login-history")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class LoginHistoryController : BaseController
{
    private readonly ApplicationDbContext _db;
    public LoginHistoryController(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Get login history for a user or all users.
    /// Optionally filter by email, date range, and success flag.
    /// Returns the most recent 500 records max.
    ///
    /// FIX [3] — Cross-tenant scope:
    ///   • Superadmin (CallerCompanyIdOrNull == null): unrestricted — sees all tenants.
    ///   • Non-superadmin admin: results are restricted to login events whose
    ///     PerformedBy (User.Id) belongs to the caller's own company.
    ///     The join is AuditLogs → Users on PerformedBy = Users.Id,
    ///     then Users.CompanyId = callerCompanyId.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? email,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] bool? success,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 50;

        // Resolve caller's company — null means superadmin (cross-tenant access).
        var callerCompanyId = CallerCompanyIdOrNull;

        var q = _db.AuditLogs.Where(a => a.Action == "LOGIN" || a.Action == "LOGIN_FAILED");

        // Non-superadmin: scope to own company via Users join.
        // PerformedBy is User.Id (int?); Users.CompanyId is int?.
        // Anonymous events (PerformedBy == null) are excluded for non-superadmin callers
        // because they cannot be attributed to a specific company.
        if (callerCompanyId.HasValue)
        {
            var companyUserIds = _db.Users
                .Where(u => u.CompanyId == callerCompanyId.Value)
                .Select(u => (int?)u.Id);

            q = q.Where(a => a.PerformedBy != null &&
                             companyUserIds.Contains(a.PerformedBy));
        }
        // Superadmin (callerCompanyId == null): no additional filter — full cross-tenant view retained.

        if (!string.IsNullOrEmpty(email))
        {
            // Phase 2d: Replaced EF.Functions.ILike (PostgreSQL-only) with MySQL-compatible
            // case-insensitive search using EF.Functions.Like on lowercased values.
            // The hrms_db uses utf8mb4_unicode_ci collation, which is case-insensitive by
            // default, so the ToLower() is a belt-and-suspenders safety measure.
            var emailLower = email.ToLower();
            q = q.Where(a =>
                (a.PerformedByName != null && EF.Functions.Like(a.PerformedByName.ToLower(), $"%{emailLower}%")) ||
                (a.Details         != null && EF.Functions.Like(a.Details.ToLower(),         $"%{emailLower}%")));
        }
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var fromDate))
            q = q.Where(a => a.OccurredAt >= fromDate);
        if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var toDate))
            q = q.Where(a => a.OccurredAt <= toDate.AddDays(1));
        if (success.HasValue)
            q = q.Where(a => a.Success == success.Value);

        var total = await q.CountAsync();
        var rows  = await q
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new {
                a.Id, a.Action, a.PerformedBy, a.PerformedByName,
                a.IpAddress, a.Details, a.Success,
                OccurredAt = a.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new {
            Page = page, PageSize = pageSize, Total = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            Data = rows
        }));
    }
}
