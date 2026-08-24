using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HRMS.Tests.IntegrationTests;

/// <summary>
/// Phase 1 – Integration tests: Attendance service full flow.
/// Tests check-in, check-out, and back-dated edit in a realistic sequence.
/// </summary>
public class AttendanceIntegrationTests
{
    private static AttendanceService BuildService(bool periodLocked = false, int windowDays = 7)
    {
        var db     = TestHelpers.CreateInMemoryDb();
        var audit  = new MockAuditService();
        var guard  = periodLocked ? (IPayrollLockGuard)new MockLockedPayrollLockGuard() : new MockPayrollLockGuard();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["Attendance:BackDateEditWindowDays"] = windowDays.ToString() })
            .Build();
        return new AttendanceService(db, audit, guard, config,
            new HRMS.Tests.Mocks.MockLogger<AttendanceService>());
    }

    [Fact]
    public async Task CheckIn_Then_CheckOut_StatusDerived()
    {
        var db     = TestHelpers.CreateInMemoryDb();
        var audit  = new MockAuditService();
        var guard  = new MockPayrollLockGuard();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["Attendance:BackDateEditWindowDays"] = "7" })
            .Build();
        var svc    = new AttendanceService(db, audit, guard, config,
            new HRMS.Tests.Mocks.MockLogger<AttendanceService>());

        // Seed employee
        db.Employees.Add(new Employee { EmployeeId = 1, EmployeeCode = "EMP001", FullName = "Integration", IsActive = true, CompanyId = 1 });
        await db.SaveChangesAsync();

        var attId = await svc.WebCheckInAsync("EMP001");
        Assert.True(attId > 0);

        // Simulate 9 hours passing by directly updating CheckIn
        var att = await db.WebAttendances.FindAsync(attId);
        att!.CheckIn = (TimeOnly?)TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-9));
        await db.SaveChangesAsync();

        var ok = await svc.WebCheckOutAsync(attId);
        Assert.True(ok);

        var updated = await db.WebAttendances.FindAsync(attId);
        Assert.Equal("Present", updated!.Status);
        Assert.NotNull(updated.CheckOut);
    }

    [Fact]
    public async Task FullFlow_LockPreventsEdit()
    {
        var db    = TestHelpers.CreateInMemoryDb();
        var audit = new MockAuditService();
        var guard = new PayrollLockGuard(db);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["Attendance:BackDateEditWindowDays"] = "30" })
            .Build();
        var svc = new AttendanceService(db, audit, guard, config,
            new HRMS.Tests.Mocks.MockLogger<AttendanceService>());

        db.Employees.Add(new Employee { EmployeeId = 1, EmployeeCode = "EMP001", FullName = "Test", IsActive = true, CompanyId = 1 });
        var att = new WebAttendance
        {
            EmployeeId = "EMP001",
            AttDate    = new DateOnly(2026, 7, 15),
            Status     = "Present"
        };
        db.WebAttendances.Add(att);
        await db.SaveChangesAsync();

        // Lock July 2026 for company 1
        await guard.LockAsync(1, 7, 2026, 99);

        // Try to edit — must be blocked
        var (ok, msg) = await svc.EditWebAttendanceAsync(
            att.Id, "Absent", "Correcting attendance after payroll run",
            actorUserId: 1, actorCompanyId: 1, isPrivilegedUser: true);

        Assert.False(ok);
        Assert.Contains("locked", msg, StringComparison.OrdinalIgnoreCase);
    }
}
