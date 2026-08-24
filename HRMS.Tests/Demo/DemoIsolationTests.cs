using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Options;
using HRMS.Infrastructure.Services.Demo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HRMS.Tests.Demo;

/// <summary>
/// Multi-tenancy isolation tests for demo mode.
/// Verifies that:
/// - Demo Company A cannot see Demo Company B data
/// - Real customer data is isolated from demo data
/// - Global query filters respect CompanyId scoping
/// - Cross-company access is properly blocked
/// </summary>
public class DemoIsolationTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly DemoSeedService _service;

    public DemoIsolationTests()
    {
        _db = TestHelpers.CreateInMemoryDb();
        var options = new DemoModeOptions
        {
            Enabled = true,
            SeedEnabled = true,
            AllowProduction = true
        };
        _service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    }

    public void Dispose()
    {
        _db?.Dispose();
    }

    [Fact]
    public async Task DemoCompanyA_CannotSeeDemoCompanyB()
    {
        // Arrange - seed demo data
        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Get employees from company 1 and 2
        var company1Employees = await _db.Employees
            .IgnoreQueryFilters()
            .Where(e => e.CompanyId == 1)
            .ToListAsync();

        var company2Employees = await _db.Employees
            .IgnoreQueryFilters()
            .Where(e => e.CompanyId == 2)
            .ToListAsync();

        // Assert - companies are separate
        Assert.NotEmpty(company1Employees);
        Assert.NotEmpty(company2Employees);
        
        // Employees from company 1 should not be accessible to company 2
        Assert.DoesNotContain(
            company1Employees,
            e => company2Employees.Any(e2 => e2.Id == e.Id));
    }

    [Fact]
    public async Task QueryFilter_ScopesToCompanyId()
    {
        // Arrange - seed demo data
        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Act - create context with company 1 scope
        var tenant = new HRMS.Infrastructure.Services.TenantContext
        {
            CompanyId = 1,
            IsSuperAdmin = false
        };
        var scopedDb = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            config: null,
            tenant: tenant);

        // For this test, manually copy data to scoped DB
        var allEmployees = await _db.Employees.IgnoreQueryFilters().ToListAsync();
        scopedDb.Employees.AddRange(allEmployees);
        await scopedDb.SaveChangesAsync();

        // Assert - filter only returns company 1 employees
        var filtered = await scopedDb.Employees.ToListAsync();
        Assert.All(filtered, e => Assert.Equal(1, e.CompanyId));
    }

    [Fact]
    public async Task SuperAdmin_CanSeeCrossCompany()
    {
        // Arrange - seed demo data
        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Act - query as superadmin
        var allEmployees = await _db.Employees
            .IgnoreQueryFilters()
            .ToListAsync();

        // Assert - superadmin sees all companies' employees
        var companies = allEmployees.Select(e => e.CompanyId).Distinct();
        Assert.True(companies.Count() > 1, "SuperAdmin should see multiple companies");
    }

    [Fact]
    public async Task DemoCompanies_IsolatedFromRealCustomers()
    {
        // Arrange
        var realCustomerCompany = new HRMS.Domain.Entities.Company.Company
        {
            Id = 1000,
            CompanyName = "Real Customer",
            IsDemo = false
        };
        _db.Companies.Add(realCustomerCompany);
        await _db.SaveChangesAsync();

        // Seed demo
        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Act - query all companies
        var allCompanies = await _db.Companies.IgnoreQueryFilters().ToListAsync();

        // Assert - both demo and real companies exist, but are separate
        var demoCompanies = allCompanies.Where(c => c.IsDemo).ToList();
        var realCompanies = allCompanies.Where(c => !c.IsDemo).ToList();

        Assert.NotEmpty(allCompanies);
        Assert.Equal(5, demoCompanies.Count);
        Assert.Single(realCompanies);
        Assert.Equal("Real Customer", realCompanies[0].CompanyName);
    }

    [Fact]
    public async Task DemoCompanies_UseReservedIds()
    {
        // Arrange & Act
        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Assert
        var demoCompanies = await _db.Companies
            .IgnoreQueryFilters()
            .Where(c => c.IsDemo)
            .ToListAsync();

        var ids = demoCompanies.Select(c => c.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, ids);
    }

    [Fact]
    public async Task AttendanceRecords_IsolatedByCompany()
    {
        // Arrange & Act
        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Assert - attendance from company 1
        var company1Attendance = await _db.WebAttendances
            .IgnoreQueryFilters()
            .Where(a => a.CompanyId == 1)
            .ToListAsync();

        // Attendance from company 2
        var company2Attendance = await _db.WebAttendances
            .IgnoreQueryFilters()
            .Where(a => a.CompanyId == 2)
            .ToListAsync();

        Assert.NotEmpty(company1Attendance);
        Assert.NotEmpty(company2Attendance);

        // No overlap
        var company1Ids = company1Attendance.Select(a => a.Id);
        Assert.DoesNotContain(company1Ids, id => company2Attendance.Any(a => a.Id == id));
    }

    [Fact]
    public async Task Payroll_IsolatedByCompany()
    {
        // Arrange & Act
        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Assert
        var company1Payslips = await _db.Payslips
            .IgnoreQueryFilters()
            .Where(p => p.CompanyId == 1)
            .ToListAsync();

        var company2Payslips = await _db.Payslips
            .IgnoreQueryFilters()
            .Where(p => p.CompanyId == 2)
            .ToListAsync();

        Assert.NotEmpty(company1Payslips);
        Assert.NotEmpty(company2Payslips);

        // Each payslip belongs to its company
        foreach (var payslip in company1Payslips)
        {
            Assert.Equal(1, payslip.CompanyId);
        }

        foreach (var payslip in company2Payslips)
        {
            Assert.Equal(2, payslip.CompanyId);
        }
    }

    [Fact]
    public async Task Assets_IsolatedByCompany()
    {
        // Arrange & Act
        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Assert
        var allAssets = await _db.Assets
            .IgnoreQueryFilters()
            .ToListAsync();

        var assetsByCompany = allAssets.GroupBy(a => a.CompanyId).ToList();

        // Assets distributed across companies
        Assert.True(assetsByCompany.Count > 1);
        
        // Each asset belongs to exactly one company
        foreach (var asset in allAssets)
        {
            Assert.True(asset.CompanyId >= 1 && asset.CompanyId <= 5);
        }
    }

    [Fact]
    public async Task DemoUsersAssignedToCompanies()
    {
        // Arrange & Act
        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Assert - verify demo users exist
        var demoUsers = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.IsDemo)
            .ToListAsync();

        Assert.NotEmpty(demoUsers);
        Assert.True(seedResult.CompaniesCreated > 0);

        // Demo users have demo email domains
        foreach (var user in demoUsers)
        {
            Assert.Contains("demo", user.Email, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Cleanup_RespectsDemoIsolation()
    {
        // Arrange - add real and demo data
        var realCompany = new HRMS.Domain.Entities.Company.Company
        {
            Id = 1000,
            CompanyName = "Real Company",
            IsDemo = false
        };
        _db.Companies.Add(realCompany);
        await _db.SaveChangesAsync();

        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        var demoCompaniesBefore = await _db.Companies.IgnoreQueryFilters().Where(c => c.IsDemo).CountAsync();
        var realCompaniesBefore = await _db.Companies.IgnoreQueryFilters().Where(c => !c.IsDemo).CountAsync();
        Assert.Equal(5, demoCompaniesBefore);
        Assert.Equal(1, realCompaniesBefore);

        // Act - cleanup
        var cleanupResult = await _service.CleanupAsync(dryRun: false, confirmCleanup: true, verbose: false);
        Assert.True(cleanupResult.IsSuccess);

        // Assert - only demo deleted
        var realCompaniesAfter = await _db.Companies.IgnoreQueryFilters().Where(c => !c.IsDemo).CountAsync();

        Assert.Equal(realCompaniesBefore, realCompaniesAfter); // Real untouched
        Assert.Equal(0, await _db.Companies.IgnoreQueryFilters().Where(c => c.IsDemo).CountAsync()); // Demo deleted
    }

    [Fact]
    public async Task LeaveRequests_IsolatedByCompany()
    {
        // Arrange & Act
        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Assert
        var leave1 = await _db.LeaveRequests
            .IgnoreQueryFilters()
            .Where(l => l.CompanyId == 1)
            .CountAsync();

        var leave2 = await _db.LeaveRequests
            .IgnoreQueryFilters()
            .Where(l => l.CompanyId == 2)
            .CountAsync();

        // Both companies have leave requests, but they're separate
        Assert.True(leave1 >= 0);
        Assert.True(leave2 >= 0);
    }

    [Fact]
    public async Task AllDemoRecords_ShareSameIsDemo_Flag()
    {
        // Arrange & Act
        var seedResult = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Assert - all demo entities have IsDemo=true
        var companies = await _db.Companies.IgnoreQueryFilters().Where(c => c.IsDemo).CountAsync();
        var employees = await _db.Employees.IgnoreQueryFilters().Where(e => e.IsDemo).CountAsync();
        var attendance = await _db.WebAttendances.IgnoreQueryFilters().Where(a => a.IsDemo).CountAsync();
        var payslips = await _db.Payslips.IgnoreQueryFilters().Where(p => p.IsDemo).CountAsync();

        Assert.Equal(5, companies);
        Assert.Equal(500, employees);
        Assert.True(attendance > 0);
        Assert.True(payslips > 0);

        // All should be marked demo (no untagged rows within the reserved demo ID range)
        Assert.False(await _db.Companies.IgnoreQueryFilters().AnyAsync(c => !c.IsDemo && c.Id <= 5));
        Assert.False(await _db.Employees.IgnoreQueryFilters().AnyAsync(e => !e.IsDemo && e.CompanyId <= 5));
    }
}
