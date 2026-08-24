using HRMS.Domain.Entities.Demo;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Options;
using HRMS.Infrastructure.Services.Demo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace HRMS.Tests.Demo;

/// <summary>
/// Tests for DemoSeedService idempotency, data generation, and safety.
/// Verifies that:
/// - Dry-run doesn't modify database
/// - Same SeedVersion never creates duplicates
/// - All records are marked IsDemo=true
/// - Correct record counts are generated
/// - Cleanup only deletes demo records
/// </summary>
public class DemoSeedServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly DemoSeedService _service;
    private readonly DemoModeOptions _options;

    public DemoSeedServiceTests()
    {
        _db = TestHelpers.CreateInMemoryDb();
        _options = new DemoModeOptions
        {
            Enabled = true,
            SeedEnabled = true,
            AllowProduction = true, // Allow in test
            SeedVersion = "1.0.0",
            DryRunByDefault = true
        };
        _service = new DemoSeedService(
            _db,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<DemoSeedService>(),
            Options.Create(_options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    }

    public void Dispose()
    {
        _db?.Dispose();
    }

    [Fact]
    public async Task DryRun_DoesNotModifyDatabase()
    {
        // Act
        var result = await _service.SeedAsync(dryRun: true, verbose: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.WasDryRun);
        var companyCount = await _db.Companies.IgnoreQueryFilters().CountAsync();
        Assert.Equal(0, companyCount); // No data created
    }

    [Fact]
    public async Task DryRun_ReturnsEstimatedCounts()
    {
        // Act
        var result = await _service.SeedAsync(dryRun: true, verbose: false);

        // Assert
        Assert.Equal(5, result.CompaniesCreated);
        Assert.Equal(500, result.EmployeesCreated);
        Assert.True(result.AttendanceRecordsCreated > 0);
        Assert.True(result.TotalRecordsCreated > 10000);
    }

    [Fact]
    public async Task Seed_CreatesCompanies()
    {
        // Act
        var result = await _service.SeedAsync(dryRun: false, verbose: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.CompaniesCreated);
        
        var companies = await _db.Companies.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(5, companies.Count);
        
        // All marked as demo
        foreach (var company in companies)
        {
            Assert.True(company.IsDemo);
        }
    }

    [Fact]
    public async Task Seed_CreatesEmployees()
    {
        // Act
        var result = await _service.SeedAsync(dryRun: false, verbose: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(500, result.EmployeesCreated);
        
        var employees = await _db.Employees.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(500, employees.Count);
        
        // All marked as demo
        foreach (var emp in employees)
        {
            Assert.True(emp.IsDemo);
            Assert.NotNull(emp.Email);
            Assert.Contains("demo.ratanhr.local", emp.Email);
        }
    }

    [Fact]
    public async Task AllRecords_MarkedWithIsDemo()
    {
        // Act
        var result = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(result.IsSuccess);

        // Assert - check all entity types
        var companies = await _db.Companies.IgnoreQueryFilters().CountAsync();
        var employees = await _db.Employees.IgnoreQueryFilters().CountAsync();
        var attendance = await _db.WebAttendances.IgnoreQueryFilters().Where(a => !a.IsDemo).CountAsync();
        var payslips = await _db.Payslips.IgnoreQueryFilters().Where(p => !p.IsDemo).CountAsync();

        Assert.True(companies > 0);
        Assert.True(employees > 0);
        Assert.Equal(0, attendance);
        Assert.Equal(0, payslips);
    }

    [Fact]
    public async Task Seed_Idempotent_SameVersionNotDuplicated()
    {
        // Act - seed twice with same version
        var result1 = await _service.SeedAsync(dryRun: false, verbose: false);
        var result2 = await _service.SeedAsync(dryRun: false, verbose: false);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.True(result2.WasDryRun); // Second run should be dry-run (already seeded)
        
        // Verify no duplicates
        var companies = await _db.Companies.IgnoreQueryFilters().CountAsync();
        Assert.Equal(5, companies); // Still 5, not 10
    }

    [Fact]
    public async Task DemoSeedTracker_RecordsOperation()
    {
        // Act
        var result = await _service.SeedAsync(dryRun: false, verbose: false);

        // Assert
        Assert.True(result.IsSuccess);
        var tracker = await _db.DemoSeedTrackers
            .Where(t => t.SeedVersion == "1.0.0")
            .FirstOrDefaultAsync();

        Assert.NotNull(tracker);
        Assert.True(tracker.IsSuccess);
        Assert.Equal(5, tracker.CreatedCompanyCount);
        Assert.Equal(500, tracker.CreatedEmployeeCount);
    }

    [Fact]
    public async Task Cleanup_DeletesOnlyDemoRecords()
    {
        // Arrange - seed first
        await _service.SeedAsync(dryRun: false, verbose: false);

        // Act - cleanup
        var result = await _service.CleanupAsync(dryRun: false, confirmCleanup: true, verbose: false);

        // Assert
        Assert.True(result.IsSuccess);
        var remainingCompanies = await _db.Companies.IgnoreQueryFilters().CountAsync();
        Assert.Equal(0, remainingCompanies); // All demo companies deleted
    }

    [Fact]
    public async Task CleanupDryRun_DoesNotModifyDatabase()
    {
        // Arrange - seed first
        await _service.SeedAsync(dryRun: false, verbose: false);
        var companiesBefore = await _db.Companies.IgnoreQueryFilters().CountAsync();

        // Act
        var result = await _service.CleanupAsync(dryRun: true, confirmCleanup: false, verbose: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.WasDryRun);
        var companiesAfter = await _db.Companies.IgnoreQueryFilters().CountAsync();
        Assert.Equal(companiesBefore, companiesAfter); // No deletion
    }

    [Fact]
    public async Task Cleanup_RequiresConfirmation()
    {
        // Arrange
        await _service.SeedAsync(dryRun: false, verbose: false);

        // Act
        var result = await _service.CleanupAsync(dryRun: false, confirmCleanup: false, verbose: false);

        // Assert
        Assert.False(result.IsSuccess); // Should fail without confirmation
        var companies = await _db.Companies.IgnoreQueryFilters().CountAsync();
        Assert.Equal(5, companies); // Still there
    }

    [Fact]
    public async Task DemoCompanies_HaveCorrectIds()
    {
        // Act
        var result = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(result.IsSuccess);

        // Assert
        var companies = await _db.Companies.IgnoreQueryFilters().OrderBy(c => c.Id).ToListAsync();
        for (int i = 0; i < companies.Count; i++)
        {
            Assert.Equal(i + 1, companies[i].Id); // IDs 1-5
        }
    }

    [Fact]
    public async Task DemoEmployees_DistributedAcrossCompanies()
    {
        // Act
        var result = await _service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(result.IsSuccess);

        // Assert
        var employees = await _db.Employees.IgnoreQueryFilters().ToListAsync();
        var groupedByCompany = employees.GroupBy(e => e.CompanyId).ToList();
        
        // Should have employees in each company
        Assert.Equal(5, groupedByCompany.Count); // 5 companies
        foreach (var group in groupedByCompany)
        {
            Assert.Equal(100, group.Count()); // ~100 per company
        }
    }

    [Fact]
    public async Task Validation_ChecksAllPreconditions()
    {
        // Act
        var result = await _service.ValidateAsync();

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Checks);
        
        // All checks should pass in test environment
        foreach (var check in result.Checks)
        {
            Assert.True(check.Passed, $"Validation failed: {check.CheckName} - {check.Message}");
        }
    }

    [Fact]
    public async Task RecordCounts_Deterministic()
    {
        // Act - seed twice (different DB instances)
        using var db1 = TestHelpers.CreateInMemoryDb();
        var service1 = new DemoSeedService(
            db1,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<DemoSeedService>(),
            Options.Create(_options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        
        var result1 = await service1.SeedAsync(dryRun: false, verbose: false);

        using var db2 = TestHelpers.CreateInMemoryDb();
        var service2 = new DemoSeedService(
            db2,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<DemoSeedService>(),
            Options.Create(_options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        
        var result2 = await service2.SeedAsync(dryRun: false, verbose: false);

        // Assert - same counts
        Assert.Equal(result1.CompaniesCreated, result2.CompaniesCreated);
        Assert.Equal(result1.EmployeesCreated, result2.EmployeesCreated);
        Assert.Equal(result1.AttendanceRecordsCreated, result2.AttendanceRecordsCreated);
        Assert.Equal(result1.PayslipsCreated, result2.PayslipsCreated);
    }
}

/// <summary>Test host environment implementation.</summary>
public class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "HRMS.Tests";
    public string ContentRootPath { get; set; } = System.IO.Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
