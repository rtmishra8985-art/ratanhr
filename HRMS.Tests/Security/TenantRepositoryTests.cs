// Fix 1 + Fix 6: Tenant isolation tests for GenericRepository and ApplicationDbContext
// global query filters. Verifies that Company A cannot read Company B data.
using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Repositories;
using HRMS.Infrastructure.Services;
using Xunit;

namespace HRMS.Tests.Security;

/// <summary>
/// Verifies that the tenant isolation layer (EF Core global query filters + GenericRepository
/// ICompanyOwned check) prevents cross-company data access.
/// </summary>
public class TenantRepositoryTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateDb(ITenantContext? tenant = null)
        => TestHelpers.CreateInMemoryDb(tenant);

    private static Employee AddEmployee(ApplicationDbContext db, string empId, int companyId)
    {
        var emp = new Employee
        {
            EmployeeCode = empId,
            CompanyId    = companyId,
            FullName    = $"Employee {empId}",
            Designation = "Staff",
            Department  = "HR",
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow
        };
        db.Employees.Add(emp);
        db.SaveChanges();
        return emp;
    }

    // ── Fix 1a: GetAllAsync tenant isolation via global query filter ─────────

    [Fact]
    public async Task GetAllAsync_ReturnsTenantEmployeesOnly()
    {
        var tenantCtx = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantCtx);

        AddEmployee(db, "EMP-A1", companyId: 1); // same company
        AddEmployee(db, "EMP-A2", companyId: 1); // same company
        AddEmployee(db, "EMP-B1", companyId: 2); // different company

        var repo = new GenericRepository<Employee>(db, tenantCtx);
        var result = await repo.GetAllAsync();

        Assert.Equal(2, result.Count());
        Assert.All(result, e => Assert.Equal(1, e.CompanyId));
    }

    [Fact]
    public async Task GetAllAsync_SuperAdmin_ReturnsAllCompanies()
    {
        var tenantCtx = new TenantContext { CompanyId = null, IsSuperAdmin = true };
        using var db = CreateDb(tenantCtx);

        AddEmployee(db, "EMP-A1", companyId: 1);
        AddEmployee(db, "EMP-B1", companyId: 2);

        var repo = new GenericRepository<Employee>(db, tenantCtx);
        var result = await repo.GetAllAsync();

        Assert.Equal(2, result.Count()); // superadmin sees all
    }

    // ── Fix 1b: GetByIdAsync tenant check for ICompanyOwned entities ────────

    [Fact]
    public async Task GetByIdAsync_CrossTenantEmployee_ReturnsNull()
    {
        var tenantCtx = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantCtx);

        // Insert a Company-2 employee without going through the filter
        // (use a superadmin context to bypass the filter on write)
        var superCtx = new TenantContext { CompanyId = null, IsSuperAdmin = true };
        using var superDb = CreateDb(superCtx);
        var emp = AddEmployee(superDb, "EMP-B1", companyId: 2);

        // Now try to read Company-2 employee as Company-1 caller
        var repo = new GenericRepository<Employee>(db, tenantCtx);
        var result = await repo.GetByIdAsync(emp.Id);

        Assert.Null(result); // must be blocked
    }

    [Fact]
    public async Task GetByIdAsync_SameTenantEmployee_ReturnsEmployee()
    {
        var tenantCtx = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantCtx);
        var emp = AddEmployee(db, "EMP-A1", companyId: 1);

        var repo = new GenericRepository<Employee>(db, tenantCtx);
        var result = await repo.GetByIdAsync(emp.Id);

        Assert.NotNull(result);
        Assert.Equal("EMP-A1", result!.EmployeeCode);
    }

    [Fact]
    public async Task GetByIdAsync_SuperAdmin_ReturnsCrossTenantEmployee()
    {
        var superCtx = new TenantContext { CompanyId = null, IsSuperAdmin = true };
        using var db = CreateDb(superCtx);
        var emp = AddEmployee(db, "EMP-B1", companyId: 2);

        var repo = new GenericRepository<Employee>(db, superCtx);
        var result = await repo.GetByIdAsync(emp.Id);

        Assert.NotNull(result); // superadmin is unrestricted
    }

    // ── Fix 1c: GlobalQueryFilter on DbContext directly ──────────────────────

    [Fact]
    public void DbContext_GlobalFilter_PreventsCompanyLeakOnEmployeeSet()   // FIX 8: no await → not async
    {
        var tenantCtx = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        using var db = CreateDb(tenantCtx);

        AddEmployee(db, "EMP-A1", companyId: 1);
        AddEmployee(db, "EMP-B1", companyId: 2);

        // Query through the DbSet directly — global filter should scope it
        var employees = db.Employees.ToList();

        Assert.Single(employees);
        Assert.Equal(1, employees[0].CompanyId);
    }
}
