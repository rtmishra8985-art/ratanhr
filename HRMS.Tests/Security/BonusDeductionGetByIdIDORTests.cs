using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Tests.Security;

/// <summary>
/// FIX TEST-02 (SEC-02): Regression tests verifying that
/// BonusDeductionService.GetBonusByIdAsync and GetDeductionByIdAsync are
/// company-scoped when callerCompanyId is provided.
///
/// Before the fix, both methods performed an unscoped FirstOrDefaultAsync
/// with no JOIN on Employee.CompanyId, allowing any caller who knew an
/// integer ID to retrieve bonus/deduction records from any tenant.
///
/// After the fix a JOIN on Employee.CompanyId is applied when callerCompanyId
/// is provided, so cross-tenant lookups return null (→ 404 at the controller).
/// </summary>
public class BonusDeductionGetByIdIDORTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly BonusDeductionService _svc;

    public BonusDeductionGetByIdIDORTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new ApplicationDbContext(options);
        _svc = new BonusDeductionService(_db);
        SeedAsync().GetAwaiter().GetResult();
    }

    // ── Seed ───────────────────────────────────────────────────────────────

    private async Task SeedAsync()
    {
        // Company 1 employee
        _db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            Id            = 1,
            EmployeeCode  = "EMP-A-001",
            CompanyId     = 1,
            FirstName     = "Alice",
            LastName      = "A",
            Email         = "alice@a.com",
        });

        // Company 2 employee
        _db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            Id            = 2,
            EmployeeCode  = "EMP-B-001",
            CompanyId     = 2,
            FirstName     = "Bob",
            LastName      = "B",
            Email         = "bob@b.com",
        });

        // Bonus for company-1 employee
        _db.Bonuses.Add(new Bonus
        {
            Id         = 10,
            EmployeeId = "EMP-A-001",
            BonusType  = "Performance",
            Amount     = 5000m,
            Month      = 7,
            Year       = 2026,
        });

        // Bonus for company-2 employee
        _db.Bonuses.Add(new Bonus
        {
            Id         = 20,
            EmployeeId = "EMP-B-001",
            BonusType  = "Festival",
            Amount     = 3000m,
            Month      = 7,
            Year       = 2026,
        });

        // Deduction for company-1 employee
        _db.Deductions.Add(new Deduction
        {
            Id            = 10,
            EmployeeId    = "EMP-A-001",
            DeductionType = "Loan",
            Amount        = 1000m,
            Month         = 7,
            Year          = 2026,
        });

        // Deduction for company-2 employee
        _db.Deductions.Add(new Deduction
        {
            Id            = 20,
            EmployeeId    = "EMP-B-001",
            DeductionType = "Insurance",
            Amount        = 500m,
            Month         = 7,
            Year          = 2026,
        });

        await _db.SaveChangesAsync();
    }

    // ── Bonus GetById IDOR Tests ───────────────────────────────────────────

    [Fact]
    public async Task GetBonusByIdAsync_SameTenant_ReturnsBonus()
    {
        var result = await _svc.GetBonusByIdAsync(id: 10, callerCompanyId: 1);
        Assert.NotNull(result);
        Assert.Equal(10, result!.Id);
    }

    [Fact]
    public async Task GetBonusByIdAsync_CrossTenant_ReturnsNull()
    {
        // Company-1 admin tries to access company-2 bonus (ID=20)
        var result = await _svc.GetBonusByIdAsync(id: 20, callerCompanyId: 1);
        Assert.Null(result); // Must be null → controller returns 404
    }

    [Fact]
    public async Task GetBonusByIdAsync_SuperAdmin_CrossTenantAllowed()
    {
        // SuperAdmin passes null callerCompanyId — unrestricted
        var result = await _svc.GetBonusByIdAsync(id: 20, callerCompanyId: null);
        Assert.NotNull(result);
        Assert.Equal(20, result!.Id);
    }

    [Fact]
    public async Task GetBonusByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _svc.GetBonusByIdAsync(id: 999, callerCompanyId: 1);
        Assert.Null(result);
    }

    // ── Deduction GetById IDOR Tests ──────────────────────────────────────

    [Fact]
    public async Task GetDeductionByIdAsync_SameTenant_ReturnsDeduction()
    {
        var result = await _svc.GetDeductionByIdAsync(id: 10, callerCompanyId: 1);
        Assert.NotNull(result);
        Assert.Equal(10, result!.Id);
    }

    [Fact]
    public async Task GetDeductionByIdAsync_CrossTenant_ReturnsNull()
    {
        // Company-1 admin tries to access company-2 deduction (ID=20)
        var result = await _svc.GetDeductionByIdAsync(id: 20, callerCompanyId: 1);
        Assert.Null(result); // Must be null → controller returns 404
    }

    [Fact]
    public async Task GetDeductionByIdAsync_SuperAdmin_CrossTenantAllowed()
    {
        var result = await _svc.GetDeductionByIdAsync(id: 20, callerCompanyId: null);
        Assert.NotNull(result);
        Assert.Equal(20, result!.Id);
    }

    // ── Bonus GetBonusesAsync IDOR Tests ──────────────────────────────────

    [Fact]
    public async Task GetBonusesAsync_SameTenant_ReturnsOnlyOwnBonuses()
    {
        var result = await _svc.GetBonusesAsync(
            employeeId: null, callerCompanyId: 1, month: null, year: null);
        Assert.All(result, b => Assert.Equal("EMP-A-001", b.EmployeeId));
        Assert.DoesNotContain(result, b => b.EmployeeId == "EMP-B-001");
    }

    [Fact]
    public async Task GetBonusesAsync_SuperAdmin_ReturnsAll()
    {
        var result = await _svc.GetBonusesAsync(
            employeeId: null, callerCompanyId: null, month: null, year: null);
        Assert.Equal(2, result.Count);
    }

    // ── IDisposable ────────────────────────────────────────────────────────

    public void Dispose() => _db.Dispose();
}
