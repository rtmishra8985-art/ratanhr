using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Phase 1 – C: Back-dated attendance edit window enforcement.
/// Tests that employees are blocked from editing records outside the window
/// and that HR/Admin override (isPrivilegedUser = true) bypasses the window check.
/// PayrollLock and IDOR checks are also exercised here.
/// </summary>
public class BackDatedAttendanceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static AttendanceService BuildService(int windowDays, bool periodLocked = false)
    {
        var db      = TestHelpers.CreateInMemoryDb();
        var audit   = new MockAuditService();
        var guard   = periodLocked
                      ? (IPayrollLockGuard)new MockLockedPayrollLockGuard()
                      : new MockPayrollLockGuard();
        var config  = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Attendance:BackDateEditWindowDays"] = windowDays.ToString()
            })
            .Build();
        return new AttendanceService(db, audit, guard, config, new MockLogger<AttendanceService>());
    }

    private static (AttendanceService svc, HRMS.Infrastructure.Data.ApplicationDbContext db) BuildWithDb(
        int windowDays = 7, bool periodLocked = false)
    {
        var db      = TestHelpers.CreateInMemoryDb();
        var audit   = new MockAuditService();
        var guard   = periodLocked
                      ? (IPayrollLockGuard)new MockLockedPayrollLockGuard()
                      : new MockPayrollLockGuard();
        var config  = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Attendance:BackDateEditWindowDays"] = windowDays.ToString()
            })
            .Build();
        return (new AttendanceService(db, audit, guard, config, new MockLogger<AttendanceService>()), db);
    }

    private static async Task<int> SeedAttendanceAsync(
        HRMS.Infrastructure.Data.ApplicationDbContext db,
        string employeeId, int companyId, int daysAgo)
    {
        // Seed the employee
        if (!db.Employees.Any(e => e.EmployeeCode == employeeId))
        {
            db.Employees.Add(new Employee { EmployeeId = 1, EmployeeCode = employeeId, FullName = "Test Employee",
                IsActive = true, CompanyId = companyId });
        }
        // Seed the attendance record
        var att = new WebAttendance
        {
            EmployeeId = employeeId,
            AttDate    = DateOnly.FromDateTime(DateTime.Today.AddDays(-daysAgo)),
            Status     = "Present"
        };
        db.WebAttendances.Add(att);
        await db.SaveChangesAsync();
        return att.Id;
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task EditAttendance_WithinWindow_NonPrivileged_Succeeds()
    {
        var (svc, db) = BuildWithDb(windowDays: 7);
        var attId = await SeedAttendanceAsync(db, "EMP001", 1, daysAgo: 3);

        var (ok, msg) = await svc.EditWebAttendanceAsync(attId, "Absent",
            "Correcting missed punch-in record", actorUserId: 10, actorCompanyId: 1,
            isPrivilegedUser: false);

        Assert.True(ok, msg);
    }

    [Fact]
    public async Task EditAttendance_OutsideWindow_NonPrivileged_Fails()
    {
        var (svc, db) = BuildWithDb(windowDays: 7);
        var attId = await SeedAttendanceAsync(db, "EMP001", 1, daysAgo: 10); // 10 days ago > 7-day window

        var (ok, msg) = await svc.EditWebAttendanceAsync(attId, "Absent",
            "Correcting old record", actorUserId: 10, actorCompanyId: 1,
            isPrivilegedUser: false);

        Assert.False(ok);
        Assert.Contains("7 days", msg);
    }

    [Fact]
    public async Task EditAttendance_OutsideWindow_PrivilegedUser_Succeeds()
    {
        var (svc, db) = BuildWithDb(windowDays: 7);
        var attId = await SeedAttendanceAsync(db, "EMP001", 1, daysAgo: 30); // 30 days ago

        // HR/Admin override bypasses the window check
        var (ok, msg) = await svc.EditWebAttendanceAsync(attId, "Leave",
            "HR correction for payroll month end", actorUserId: 1, actorCompanyId: 1,
            isPrivilegedUser: true);

        Assert.True(ok, msg);
    }

    [Fact]
    public async Task EditAttendance_PayrollLocked_Fails()
    {
        var (svc, db) = BuildWithDb(windowDays: 7, periodLocked: true);
        var attId = await SeedAttendanceAsync(db, "EMP001", 1, daysAgo: 2);

        var (ok, msg) = await svc.EditWebAttendanceAsync(attId, "Absent",
            "Correcting attendance record for payroll", actorUserId: 1, actorCompanyId: 1,
            isPrivilegedUser: true);

        Assert.False(ok);
        Assert.Contains("locked", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditAttendance_WrongCompany_ReturnsNotFound()
    {
        var (svc, db) = BuildWithDb(windowDays: 7);
        // Attendance record belongs to company 1, but we pass company 2
        var attId = await SeedAttendanceAsync(db, "EMP001", companyId: 1, daysAgo: 2);

        var (ok, msg) = await svc.EditWebAttendanceAsync(attId, "Absent",
            "Trying to edit another company's record", actorUserId: 99, actorCompanyId: 2,
            isPrivilegedUser: true);

        Assert.False(ok);
        Assert.Contains("not found", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditAttendance_RecordNotFound_ReturnsFalse()
    {
        var (svc, _) = BuildWithDb(windowDays: 7);

        var (ok, msg) = await svc.EditWebAttendanceAsync(99999, "Absent",
            "Editing a nonexistent record here", actorUserId: 1, actorCompanyId: 1,
            isPrivilegedUser: true);

        Assert.False(ok);
        Assert.Contains("not found", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditAttendance_SuperadminBypass_ZeroCompanyId_SkipsIdorCheck()
    {
        // Superadmin passes actorCompanyId = 0 (bypass flag)
        var (svc, db) = BuildWithDb(windowDays: 7);
        var att = new WebAttendance
        {
            EmployeeId = "EMP_ANY", AttDate = DateOnly.FromDateTime(DateTime.Today),
            Status = "Present"
        };
        db.WebAttendances.Add(att);
        await db.SaveChangesAsync();

        // Company 0 means IDOR check is skipped (superadmin path)
        var (ok, _) = await svc.EditWebAttendanceAsync(att.Id, "Absent",
            "Superadmin correction with full bypass", actorUserId: 1, actorCompanyId: 0,
            isPrivilegedUser: true);

        Assert.True(ok);
    }

    [Fact]
    public async Task EditAttendance_TodayRecord_Succeeds()
    {
        var (svc, db) = BuildWithDb(windowDays: 0); // zero window = today only for non-privileged
        var attId = await SeedAttendanceAsync(db, "EMP001", 1, daysAgo: 0); // today

        var (ok, msg) = await svc.EditWebAttendanceAsync(attId, "Half Day",
            "Correcting today's attendance status", actorUserId: 1, actorCompanyId: 1,
            isPrivilegedUser: false);

        Assert.True(ok, msg);
    }
}
