using HRMS.Application.DTOs.Analytics;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _db;

    public AnalyticsService(ApplicationDbContext db) => _db = db;

    public async Task<HeadcountAnalyticsDto> GetHeadcountAsync(int companyId, int year)
    {
        // FIX: Push all aggregates to the database — no client-side evaluation.
        // Previously loaded the entire employees table into memory before counting/grouping.
        // Now uses three targeted DB-side queries and one GroupBy projection.

        var total    = await _db.Employees.CountAsync(e => e.CompanyId == companyId).ConfigureAwait(false);
        var active   = await _db.Employees.CountAsync(e => e.CompanyId == companyId && e.IsActive).ConfigureAwait(false);
        var inactive = total - active;

        // Department breakdown — fully translated to SQL GROUP BY
        var byDept = await _db.Employees
            .Where(e => e.CompanyId == companyId)
            .GroupBy(e => e.Department == null ? "Unknown" : e.Department)
            .Select(g => new { Department = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Department, x => x.Count)
            .ConfigureAwait(false);

        return new HeadcountAnalyticsDto
        {
            TotalEmployees = total,
            Active         = active,
            Inactive       = inactive,
            ByDepartment   = byDept
        };
    }

    public async Task<AttendanceAnalyticsDto> GetAttendanceSummaryAsync(int companyId, string period)
    {
        // period = "YYYY-MM"
        if (!DateTime.TryParse(period + "-01", out var dt))
            return new AttendanceAnalyticsDto { Period = period };

        var month = dt.Month;
        var year  = dt.Year;

        // Push the company join and status GroupBy to the database.
        var stats = await _db.WebAttendances
            .Where(a => a.AttDate.Month == month && a.AttDate.Year == year)
            .Join(_db.Employees.Where(e => e.CompanyId == companyId),
                  a => a.EmployeeId, e => e.EmployeeCode,
                  (a, _) => a.Status)
            .GroupBy(status => status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync()
            .ConfigureAwait(false);

        var total   = stats.Sum(x => x.Count);
        var present = stats.FirstOrDefault(x => x.Status == "Present")?.Count ?? 0;
        var absent  = stats.FirstOrDefault(x => x.Status == "Absent")?.Count ?? 0;
        var leave   = stats.FirstOrDefault(x => x.Status == "Leave")?.Count ?? 0;
        var pct     = total > 0 ? Math.Round((decimal)present / total * 100, 2) : 0;

        return new AttendanceAnalyticsDto
        {
            Period            = period,
            PresentDays       = present,
            AbsentDays        = absent,
            LeaveDays         = leave,
            AttendancePercent = pct
        };
    }

    public async Task<PayrollAnalyticsDto> GetPayrollSummaryAsync(int companyId, int year)
    {
        var empIds = await _db.Employees
            .Where(e => e.CompanyId == companyId)
            .Select(e => e.EmployeeCode)
            .ToListAsync()
            .ConfigureAwait(false);

        var payslips = await _db.Payslips
            .Where(p => p.Year == year && empIds.Contains(p.EmployeeId))
            .ToListAsync()
            .ConfigureAwait(false);

        var monthly = payslips
            .GroupBy(p => p.Month)
            .OrderBy(g => g.Key)
            .Select(g => new MonthlyPayrollSummary
            {
                Month           = new DateTime(year, g.Key, 1).ToString("MMMM"),
                TotalGross      = g.Sum(p => p.GrossEarnings),
                TotalDeductions = g.Sum(p => p.TotalDeductions),
                TotalNet        = g.Sum(p => p.NetPay),
                EmployeeCount   = g.Count()
            })
            .ToList();

        return new PayrollAnalyticsDto { Year = year, Monthly = monthly };
    }

    public async Task<TurnoverAnalyticsDto> GetTurnoverAsync(int companyId, int year)
    {
        var joined = await _db.Employees
            .CountAsync(e => e.CompanyId == companyId
                          && e.DateOfJoining.HasValue
                          && e.DateOfJoining.Value.Year == year)
            .ConfigureAwait(false);

        var exited = await _db.EmployeeExits
            .Where(x => x.LastWorkingDate.HasValue && x.LastWorkingDate.Value.Year == year)
            .Join(_db.Employees.Where(e => e.CompanyId == companyId),
                  x => x.EmployeeId, e => e.EmployeeCode, (x, e) => x)
            .CountAsync()
            .ConfigureAwait(false);

        var total = await _db.Employees
            .CountAsync(e => e.CompanyId == companyId)
            .ConfigureAwait(false);

        var rate = total > 0 ? Math.Round((decimal)exited / total * 100, 2) : 0;

        return new TurnoverAnalyticsDto
        {
            Year         = year,
            JoinedCount  = joined,
            ExitedCount  = exited,
            TurnoverRate = rate
        };
    }
}
