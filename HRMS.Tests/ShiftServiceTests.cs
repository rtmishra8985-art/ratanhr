// Fix 6: Test coverage — Shift module (previously had zero test coverage).
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Services;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Unit tests for ShiftService — CRUD, tenant isolation, and validation.
/// </summary>
public class ShiftServiceTests
{
    private static IShiftService BuildService(HRMS.Infrastructure.Data.ApplicationDbContext db)
        => new ShiftService(db);

    private static async Task<int> CreateShift(IShiftService svc, int companyId = 1,
        string name = "Morning Shift")
        => await svc.CreateShiftAsync(new CreateShiftDto
        {
            CompanyId          = companyId,
            ShiftName          = name,
            StartTime          = "09:00",
            EndTime            = "18:00",
            IsNightShift       = false,
            GracePeriodMinutes = 15
        });

    // ── CRUD Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateShift_ValidData_ReturnsNewId()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);
        var id = await CreateShift(svc);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task GetShiftsAsync_ReturnsTenantShifts()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);
        await CreateShift(svc, companyId: 1, name: "Morning");
        await CreateShift(svc, companyId: 1, name: "Evening");
        await CreateShift(svc, companyId: 2, name: "Night");

        var result = await svc.GetShiftsAsync(companyId: 1);

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(1, s.CompanyId));
    }

    [Fact]
    public async Task UpdateShiftAsync_SameTenant_Succeeds()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);
        var id = await CreateShift(svc, companyId: 1, name: "Morning");

        var ok = await svc.UpdateShiftAsync(id, new CreateShiftDto
        {
            CompanyId  = 1, ShiftName = "Updated Morning",
            StartTime  = "08:00", EndTime = "17:00",
            IsNightShift = false, GracePeriodMinutes = 10
        }, callerCompanyId: 1);

        Assert.True(ok);

        var shifts = await svc.GetShiftsAsync(1);
        Assert.Equal("Updated Morning", shifts.First(s => s.Id == id).ShiftName);
    }

    [Fact]
    public async Task UpdateShiftAsync_CrossTenant_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);
        var id = await CreateShift(svc, companyId: 1, name: "Morning");

        // Caller from company 2 tries to update company 1's shift
        var ok = await svc.UpdateShiftAsync(id, new CreateShiftDto
        {
            CompanyId = 1, ShiftName = "Hacked Shift",
            StartTime = "08:00", EndTime = "17:00"
        }, callerCompanyId: 2);

        Assert.False(ok); // IDOR blocked
    }

    [Fact]
    public async Task DeleteShiftAsync_SameTenant_Succeeds()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);
        var id = await CreateShift(svc, companyId: 1);

        var ok = await svc.DeleteShiftAsync(id, callerCompanyId: 1);

        Assert.True(ok);
        var remaining = await svc.GetShiftsAsync(1);
        Assert.DoesNotContain(remaining, s => s.Id == id);
    }

    [Fact]
    public async Task DeleteShiftAsync_CrossTenant_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);
        var id = await CreateShift(svc, companyId: 1);

        var ok = await svc.DeleteShiftAsync(id, callerCompanyId: 2);

        Assert.False(ok); // IDOR blocked
    }

    // ── Tenant Isolation Tests ────────────────────────────────────────────────

    [Fact]
    public async Task GetShiftsPaged_IsolatesCompanies()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);
        await CreateShift(svc, companyId: 1, name: "A-Morning");
        await CreateShift(svc, companyId: 1, name: "A-Evening");
        await CreateShift(svc, companyId: 2, name: "B-Night");

        var page = await svc.GetShiftsPagedAsync(companyId: 1, page: 1, pageSize: 25);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, s => Assert.Equal(1, s.CompanyId));
    }

    // ── Validation Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetShiftsAsync_NoShifts_ReturnsEmpty()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);
        var result = await svc.GetShiftsAsync(companyId: 99);
        Assert.Empty(result);
    }

    [Fact]
    public async Task SuperAdmin_NullCallerCompanyId_CanUpdateAnyShift()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);
        var id = await CreateShift(svc, companyId: 1);

        // null callerCompanyId = superadmin bypass
        var ok = await svc.UpdateShiftAsync(id, new CreateShiftDto
        {
            CompanyId = 1, ShiftName = "SA Override", StartTime = "07:00", EndTime = "16:00"
        }, callerCompanyId: null);

        Assert.True(ok);
    }
}
