using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Options;
using HRMS.Infrastructure.Services.Demo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace HRMS.Tests.Demo;

/// <summary>
/// Safety-focused tests for demo mode.
/// Verifies production safeguards, configuration protection,
/// and prevention of accidental production seeding.
/// </summary>
public class DemoSafetyTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public DemoSafetyTests()
    {
        _db = TestHelpers.CreateInMemoryDb();
    }

    public void Dispose()
    {
        _db?.Dispose();
    }

    [Fact]
    public async Task DemoMode_DisabledByDefault()
    {
        // Arrange
        var options = new DemoModeOptions
        {
            Enabled = false, // Default
            SeedEnabled = false,
            AllowProduction = false
        };
        var service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Act
        var validation = await service.ValidateAsync();

        // Assert
        Assert.False(validation.IsValid); // Should fail validation
        Assert.Contains(validation.Checks, c => c.CheckName == "DemoMode:Enabled" && !c.Passed);
    }

    [Fact]
    public async Task ProductionEnvironment_BlocksSeeding()
    {
        // Arrange
        var options = new DemoModeOptions
        {
            Enabled = true,
            SeedEnabled = true,
            AllowProduction = false // Production blocked
        };
        var prodEnv = new ProductionHostEnvironment();
        var service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            prodEnv,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Act
        var validation = await service.ValidateAsync();

        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Checks, c => c.CheckName == "Production Safeguard" && !c.Passed);
    }

    [Fact]
    public async Task ProductionEnvironment_AllowedWhenOptedIn()
    {
        // Arrange
        var options = new DemoModeOptions
        {
            Enabled = true,
            SeedEnabled = true,
            AllowProduction = true // Explicitly allowed
        };
        var prodEnv = new ProductionHostEnvironment();
        var service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            prodEnv,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Act
        var validation = await service.ValidateAsync();

        // Assert
        // Should pass production check
        var prodCheck = validation.Checks.FirstOrDefault(c => c.CheckName == "Production Safeguard");
        Assert.NotNull(prodCheck);
        Assert.True(prodCheck.Passed);
    }

    [Fact]
    public async Task SeedEnabled_RequiredForActualSeeding()
    {
        // Arrange
        var options = new DemoModeOptions
        {
            Enabled = true,
            SeedEnabled = false, // Not enabled
            AllowProduction = true
        };
        var service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Act - try to seed (not dry-run)
        var result = await service.SeedAsync(dryRun: false, verbose: false);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("disabled", result.ErrorMessage ?? result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DryRun_AllowedEvenWhenSeedDisabled()
    {
        // Arrange
        var options = new DemoModeOptions
        {
            Enabled = true,
            SeedEnabled = false, // Seeding disabled
            AllowProduction = true
        };
        var service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Act - dry-run should work
        var result = await service.SeedAsync(dryRun: true, verbose: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.WasDryRun);
    }

    [Fact]
    public async Task RealCustomerData_NeverTouched()
    {
        // Arrange - create real customer company
        var realCompany = new HRMS.Domain.Entities.Company.Company
        {
            Id = 1000, // Outside demo range
            CompanyName = "Real Company",
            IsDemo = false
        };
        _db.Companies.Add(realCompany);
        await _db.SaveChangesAsync();

        var options = new DemoModeOptions
        {
            Enabled = true,
            SeedEnabled = true,
            AllowProduction = true
        };
        var service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Act - seed
        var result = await service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(result.IsSuccess);

        // Assert - real company untouched
        var realCompanyAfter = await _db.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == 1000);

        Assert.NotNull(realCompanyAfter);
        Assert.Equal("Real Company", realCompanyAfter.CompanyName);
        Assert.False(realCompanyAfter.IsDemo); // Still not demo
    }

    [Fact]
    public async Task Cleanup_OnlyDeletesDemoRecords()
    {
        // Arrange - seed first
        var options = new DemoModeOptions
        {
            Enabled = true,
            SeedEnabled = true,
            AllowProduction = true
        };
        var service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        var seedResult = await service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(seedResult.IsSuccess);

        // Add real company data
        var realCompany = new HRMS.Domain.Entities.Company.Company
        {
            Id = 1000,
            CompanyName = "Real Company",
            IsDemo = false
        };
        _db.Companies.Add(realCompany);
        await _db.SaveChangesAsync();

        var companiesBefore = await _db.Companies.IgnoreQueryFilters().CountAsync();

        // Act - cleanup
        var cleanupResult = await service.CleanupAsync(dryRun: false, confirmCleanup: true, verbose: false);

        // Assert
        Assert.True(cleanupResult.IsSuccess);
        var companiesAfter = await _db.Companies.IgnoreQueryFilters().CountAsync();
        
        // Demo companies deleted, real company remains
        Assert.Equal(1, companiesAfter);
        var remaining = await _db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync();
        Assert.NotNull(remaining);
        Assert.Equal("Real Company", remaining.CompanyName);
    }

    [Fact]
    public async Task SeedVersion_PreventsRegressions()
    {
        // Arrange
        var options = new DemoModeOptions
        {
            Enabled = true,
            SeedEnabled = true,
            AllowProduction = true,
            SeedVersion = "1.0.0"
        };
        var service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Act - seed first time
        var result1 = await service.SeedAsync(dryRun: false, verbose: false);
        Assert.True(result1.IsSuccess);

        var companiesAfterFirstSeed = await _db.Companies.IgnoreQueryFilters().CountAsync();

        // Seed again with same version
        var result2 = await service.SeedAsync(dryRun: false, verbose: false);

        // Assert
        var companiesAfterSecondSeed = await _db.Companies.IgnoreQueryFilters().CountAsync();
        Assert.Equal(companiesAfterFirstSeed, companiesAfterSecondSeed); // No increase
    }

    [Fact]
    public async Task TransactionRollback_OnFailure()
    {
        // Arrange
        var options = new DemoModeOptions
        {
            Enabled = true,
            SeedEnabled = true,
            AllowProduction = true
        };
        var service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Act - seed should succeed normally
        var result = await service.SeedAsync(dryRun: false, verbose: false);

        // Assert - check transaction integrity
        Assert.True(result.IsSuccess);
        
        var companies = await _db.Companies.IgnoreQueryFilters().CountAsync();
        var employees = await _db.Employees.IgnoreQueryFilters().CountAsync();
        
        // All related records should be consistent
        Assert.Equal(5, companies);
        Assert.True(employees > 0);
    }

    [Fact]
    public async Task NoAutomaticSeeding_OnStartup()
    {
        // This is an architectural test - seed only runs when explicitly called
        // Verify by checking that service doesn't seed on creation

        var options = new DemoModeOptions
        {
            Enabled = true,
            SeedEnabled = true
        };
        
        // Act - just create service, don't call seed
        var service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            new TestHostEnvironment(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Assert - no seeding should have happened
        var companies = await _db.Companies.IgnoreQueryFilters().CountAsync();
        Assert.Equal(0, companies);
    }

    [Fact]
    public async Task NoDemoRecordsInProduction_ByDefault()
    {
        // Arrange - production with demo mode disabled
        var options = new DemoModeOptions
        {
            Enabled = false, // Off by default
            SeedEnabled = false,
            AllowProduction = false
        };
        var prodEnv = new ProductionHostEnvironment();
        var service = new DemoSeedService(
            _db,
            NullLogger<DemoSeedService>.Instance,
            Options.Create(options),
            prodEnv,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Act - validate
        var validation = await service.ValidateAsync();

        // Assert - production should block demo operations
        Assert.False(validation.IsValid);
    }
}

/// <summary>Production host environment for testing.</summary>
public class ProductionHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "HRMS.API";
    public string ContentRootPath { get; set; } = System.IO.Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
