// Fix 6: Test coverage — Biometric module (previously had zero test coverage).
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Enums;
using HRMS.Infrastructure.Services;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Unit tests for BiometricService — CRUD, tenant scoping, and validation.
/// </summary>
public class BiometricServiceTests
{
    private static IBiometricService BuildService(HRMS.Infrastructure.Data.ApplicationDbContext db)
        => new BiometricService(db);

    private static Employee SeedEmployee(HRMS.Infrastructure.Data.ApplicationDbContext db,
        string empId = "EMP9001", int companyId = 1)
    {
        var emp = new Employee
        {
            // EmployeeId is an int PK — omit to use auto-generated value.
            // UserId on BiometricLog is the string employee code; no FK enforced in InMemory.
            CompanyId = companyId, FullName = "Bio Employee",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.Employees.Add(emp);
        db.SaveChanges();
        return emp;
    }

    // ── CRUD Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBiometricLogs_ByEmployeeId_ReturnsMatchingLogs()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        SeedEmployee(db);
        db.BiometricLogs.Add(new BiometricLog
        {
            UserId = "EMP9001", CompanyId = 1, BiometricDeviceId = 1,
            PunchedAt = DateTime.UtcNow, Direction = PunchDirection.CheckIn, CreatedAt = DateTime.UtcNow
        });
        db.BiometricLogs.Add(new BiometricLog
        {
            UserId = "EMP9002", CompanyId = 1, BiometricDeviceId = 1,
            PunchedAt = DateTime.UtcNow, Direction = PunchDirection.CheckIn, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.GetLogsAsync(companyId: 1, employeeId: "EMP9001",
            from: null, to: null, page: 1, pageSize: 25);

        Assert.Single(result.Items);
        Assert.Equal("EMP9001", result.Items[0].UserId);
    }

    [Fact]
    public async Task GetBiometricLogs_DateRangeFilter_ReturnsOnlyMatchingDates()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        SeedEmployee(db);
        db.BiometricLogs.Add(new BiometricLog
        {
            UserId = "EMP9001", CompanyId = 1, BiometricDeviceId = 1,
            PunchedAt  = new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc),
            Direction  = PunchDirection.CheckIn, CreatedAt = DateTime.UtcNow
        });
        db.BiometricLogs.Add(new BiometricLog
        {
            UserId = "EMP9001", CompanyId = 1, BiometricDeviceId = 1,
            PunchedAt  = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            Direction  = PunchDirection.CheckIn, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.GetLogsAsync(
            companyId: 1, employeeId: null,
            from: new DateTime(2026, 6, 1), to: new DateTime(2026, 6, 30),
            page: 1, pageSize: 25);

        Assert.Single(result.Items);
        Assert.Equal(new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc),
            result.Items[0].PunchedAt);
    }

    [Fact]
    public async Task GetBiometricLogs_PaginationWorks()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        SeedEmployee(db);
        for (int i = 1; i <= 10; i++)
            db.BiometricLogs.Add(new BiometricLog
            {
                UserId = "EMP9001", CompanyId = 1, BiometricDeviceId = 1,
                PunchedAt  = DateTime.UtcNow.AddHours(-i),
                Direction  = PunchDirection.CheckIn, CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var page1 = await svc.GetLogsAsync(1, null, null, null, page: 1, pageSize: 5);
        var page2 = await svc.GetLogsAsync(1, null, null, null, page: 2, pageSize: 5);

        Assert.Equal(5, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);
        Assert.Equal(10, page1.TotalCount);
    }

    // ── Tenant / Authorization Tests ─────────────────────────────────────────

    [Fact]
    public async Task GetBiometricLogs_CompanyIsolation_DoesNotReturnOtherCompanyLogs()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        SeedEmployee(db, "EMP9001", companyId: 1);
        db.BiometricLogs.Add(new BiometricLog
        {
            UserId = "EMP9001", CompanyId = 1, BiometricDeviceId = 1,
            PunchedAt = DateTime.UtcNow, Direction = PunchDirection.CheckIn, CreatedAt = DateTime.UtcNow
        });
        db.BiometricLogs.Add(new BiometricLog
        {
            UserId = "EMP9002", CompanyId = 2, BiometricDeviceId = 2,
            PunchedAt = DateTime.UtcNow, Direction = PunchDirection.CheckIn, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.GetLogsAsync(companyId: 1, null, null, null, 1, 25);

        Assert.All(result.Items, log => Assert.Equal(1, log.CompanyId));
    }

    // ── Validation Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetBiometricLogs_EmptyCompany_ReturnsEmptyPage()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);
        var result = await svc.GetLogsAsync(companyId: 99, null, null, null, 1, 25);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }
}
