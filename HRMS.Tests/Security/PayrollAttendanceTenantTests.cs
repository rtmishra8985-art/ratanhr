// Fix: Security tests proving tenant isolation for WebAttendance, Payslip,
// Bonus, and Deduction. Verifies that Company A cannot access Company B records
// through the EF Core global query filter layer (now that ICompanyOwned is
// implemented on these entities and HasQueryFilter is applied in ApplicationDbContext).
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Repositories;
using HRMS.Infrastructure.Services;
using Xunit;

namespace HRMS.Tests.Security;

/// <summary>
/// Proves that the tenant isolation fixes for payroll and attendance entities
/// (Blocker 2) correctly prevent cross-company data access.
///
/// Coverage:
///   • Company A cannot read Company B WebAttendance
///   • Company A cannot read Company B Payslip
///   • Company A cannot read Company B Bonus
///   • Company A cannot read Company B Deduction
///   • Company A cannot read Company B SalaryStructure
///   • SuperAdmin can read all companies' data (regression guard)
/// </summary>
public class PayrollAttendanceTenantTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateDb(ITenantContext? tenant = null)
        => TestHelpers.CreateInMemoryDb(tenant);

    // ── WebAttendance ─────────────────────────────────────────────────────────

    [Fact]
    public async Task WebAttendance_GetAll_CompanyA_CannotSeeCompanyB()
    {
        var tenantA = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantA);

        db.WebAttendances.Add(new WebAttendance { EmployeeId = "EMP-A1", CompanyId = 1, AttDate = new DateOnly(2026, 7, 1), Status = "Present", CreatedAt = DateTime.UtcNow });
        db.WebAttendances.Add(new WebAttendance { EmployeeId = "EMP-B1", CompanyId = 2, AttDate = new DateOnly(2026, 7, 1), Status = "Present", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var repo   = new GenericRepository<WebAttendance>(db, tenantA);
        var result = await repo.GetAllAsync();

        Assert.Single(result);
        Assert.All(result, r => Assert.Equal(1, r.CompanyId));
    }

    [Fact]
    public async Task WebAttendance_GetById_CompanyA_CannotReadCompanyBRecord()
    {
        var superCtx = new TenantContext { CompanyId = null, IsSuperAdmin = true };
        using var superDb = CreateDb(superCtx);
        var att = new WebAttendance { EmployeeId = "EMP-B1", CompanyId = 2, AttDate = new DateOnly(2026, 7, 1), Status = "Present", CreatedAt = DateTime.UtcNow };
        superDb.WebAttendances.Add(att);
        await superDb.SaveChangesAsync();

        var tenantA = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantA);
        var repo   = new GenericRepository<WebAttendance>(db, tenantA);
        var result = await repo.GetByIdAsync(att.Id);

        Assert.Null(result); // must be blocked by global query filter
    }

    [Fact]
    public async Task WebAttendance_SuperAdmin_CanSeeAllCompanies()
    {
        var superCtx = new TenantContext { CompanyId = null, IsSuperAdmin = true };
        using var db = CreateDb(superCtx);

        db.WebAttendances.Add(new WebAttendance { EmployeeId = "EMP-A1", CompanyId = 1, AttDate = new DateOnly(2026, 7, 1), Status = "Present", CreatedAt = DateTime.UtcNow });
        db.WebAttendances.Add(new WebAttendance { EmployeeId = "EMP-B1", CompanyId = 2, AttDate = new DateOnly(2026, 7, 1), Status = "Present", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var repo   = new GenericRepository<WebAttendance>(db, superCtx);
        var result = await repo.GetAllAsync();

        Assert.Equal(2, result.Count()); // superadmin sees all
    }

    // ── Payslip ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Payslip_GetAll_CompanyA_CannotSeeCompanyB()
    {
        var tenantA = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantA);

        db.Payslips.Add(new Payslip { EmployeeId = "EMP-A1", CompanyId = 1, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow });
        db.Payslips.Add(new Payslip { EmployeeId = "EMP-B1", CompanyId = 2, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var repo   = new GenericRepository<Payslip>(db, tenantA);
        var result = await repo.GetAllAsync();

        Assert.Single(result);
        Assert.All(result, p => Assert.Equal(1, p.CompanyId));
    }

    [Fact]
    public async Task Payslip_GetById_CompanyA_CannotReadCompanyBRecord()
    {
        var superCtx = new TenantContext { CompanyId = null, IsSuperAdmin = true };
        using var superDb = CreateDb(superCtx);
        var payslip = new Payslip { EmployeeId = "EMP-B1", CompanyId = 2, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow };
        superDb.Payslips.Add(payslip);
        await superDb.SaveChangesAsync();

        var tenantA = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantA);
        var repo   = new GenericRepository<Payslip>(db, tenantA);
        var result = await repo.GetByIdAsync(payslip.Id);

        Assert.Null(result); // must be blocked
    }

    [Fact]
    public async Task Payslip_SuperAdmin_CanSeeAllCompanies()
    {
        var superCtx = new TenantContext { CompanyId = null, IsSuperAdmin = true };
        using var db = CreateDb(superCtx);

        db.Payslips.Add(new Payslip { EmployeeId = "EMP-A1", CompanyId = 1, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow });
        db.Payslips.Add(new Payslip { EmployeeId = "EMP-B1", CompanyId = 2, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var repo   = new GenericRepository<Payslip>(db, superCtx);
        var result = await repo.GetAllAsync();

        Assert.Equal(2, result.Count());
    }

    // ── Bonus ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bonus_GetAll_CompanyA_CannotSeeCompanyBBonuses()
    {
        var tenantA = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantA);

        db.Bonuses.Add(new Bonus { EmployeeId = "EMP-A1", CompanyId = 1, BonusType = "Festival", Amount = 5000, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow });
        db.Bonuses.Add(new Bonus { EmployeeId = "EMP-B1", CompanyId = 2, BonusType = "Festival", Amount = 5000, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var repo   = new GenericRepository<Bonus>(db, tenantA);
        var result = await repo.GetAllAsync();

        Assert.Single(result);
        Assert.All(result, b => Assert.Equal(1, b.CompanyId));
    }

    [Fact]
    public async Task Bonus_GetById_CompanyA_CannotReadCompanyBBonus()
    {
        var superCtx = new TenantContext { CompanyId = null, IsSuperAdmin = true };
        using var superDb = CreateDb(superCtx);
        var bonus = new Bonus { EmployeeId = "EMP-B1", CompanyId = 2, BonusType = "Festival", Amount = 5000, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow };
        superDb.Bonuses.Add(bonus);
        await superDb.SaveChangesAsync();

        var tenantA = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantA);
        var repo   = new GenericRepository<Bonus>(db, tenantA);
        var result = await repo.GetByIdAsync(bonus.Id);

        Assert.Null(result); // must be blocked
    }

    // ── Deduction ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deduction_GetAll_CompanyA_CannotSeeCompanyBDeductions()
    {
        var tenantA = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantA);

        db.Deductions.Add(new Deduction { EmployeeId = "EMP-A1", CompanyId = 1, DeductionType = "Loan", Amount = 1000, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow });
        db.Deductions.Add(new Deduction { EmployeeId = "EMP-B1", CompanyId = 2, DeductionType = "Loan", Amount = 1000, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var repo   = new GenericRepository<Deduction>(db, tenantA);
        var result = await repo.GetAllAsync();

        Assert.Single(result);
        Assert.All(result, d => Assert.Equal(1, d.CompanyId));
    }

    [Fact]
    public async Task Deduction_GetById_CompanyA_CannotReadCompanyBDeduction()
    {
        var superCtx = new TenantContext { CompanyId = null, IsSuperAdmin = true };
        using var superDb = CreateDb(superCtx);
        var ded = new Deduction { EmployeeId = "EMP-B1", CompanyId = 2, DeductionType = "Loan", Amount = 1000, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow };
        superDb.Deductions.Add(ded);
        await superDb.SaveChangesAsync();

        var tenantA = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantA);
        var repo   = new GenericRepository<Deduction>(db, tenantA);
        var result = await repo.GetByIdAsync(ded.Id);

        Assert.Null(result); // must be blocked
    }

    [Fact]
    public async Task Deduction_SuperAdmin_CanSeeAllCompanies()
    {
        var superCtx = new TenantContext { CompanyId = null, IsSuperAdmin = true };
        using var db = CreateDb(superCtx);

        db.Deductions.Add(new Deduction { EmployeeId = "EMP-A1", CompanyId = 1, DeductionType = "Loan", Amount = 500, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow });
        db.Deductions.Add(new Deduction { EmployeeId = "EMP-B1", CompanyId = 2, DeductionType = "Loan", Amount = 500, Month = 7, Year = 2026, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var repo   = new GenericRepository<Deduction>(db, superCtx);
        var result = await repo.GetAllAsync();

        Assert.Equal(2, result.Count());
    }

    // ── SalaryStructure ───────────────────────────────────────────────────────

    [Fact]
    public async Task SalaryStructure_GetAll_CompanyA_CannotSeeCompanyBSalaries()
    {
        var tenantA = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantA);

        db.SalaryStructures.Add(new SalaryStructure { EmployeeId = "EMP-A1", CompanyId = 1, BasicPay = 30000, EffectiveFrom = new DateOnly(2026, 1, 1), IsActive = true, CreatedAt = DateTime.UtcNow });
        db.SalaryStructures.Add(new SalaryStructure { EmployeeId = "EMP-B1", CompanyId = 2, BasicPay = 40000, EffectiveFrom = new DateOnly(2026, 1, 1), IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var repo   = new GenericRepository<SalaryStructure>(db, tenantA);
        var result = await repo.GetAllAsync();

        Assert.Single(result);
        Assert.All(result, s => Assert.Equal(1, s.CompanyId));
    }
}
