using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Data;
using Xunit;

namespace HRMS.Tests.Infrastructure;

/// <summary>
/// Tests to verify all DbSet properties are registered in ApplicationDbContext
/// </summary>
public class DbContextRegistrationTests
{
    [Fact]
    public void ApplicationDbContext_HasAllNewDbSets()
    {
        var contextType = typeof(ApplicationDbContext);
        var properties = contextType.GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var dbSetProperties = properties
            .Where(p => p.PropertyType.IsGenericType && 
                       p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.Name)
            .OrderBy(x => x)
            .ToList();

        // Required DbSets from new tables
        var requiredDbSets = new[]
        {
            "DocumentTemplates",
            "ComplianceChecklists",
            "ComplianceEvidences",
            "EmployeeSkills",
            "ProjectAssignments",
            "ExpensePolicies",
            "BankAccountDetails",
            "EmergencyContacts",
            "SalaryStructureComponents",
            "AwardRecognitions",
            "ApiAuditLogs",
            "SystemSettings"
        };

        foreach (var requiredDbSet in requiredDbSets)
        {
            Assert.Contains(requiredDbSet, dbSetProperties, StringComparer.OrdinalIgnoreCase);
        }

        // Log found DbSets for verification
        var output = $"Found {dbSetProperties.Count} DbSet properties:\n{string.Join("\n", dbSetProperties)}";
        System.Diagnostics.Debug.WriteLine(output);
    }

    [Fact]
    public void ApplicationDbContext_CanCreateDbContext()
    {
        // FIX: Previously skipped as "Constructor signature mismatch - not critical" with a
        // placeholder Assert.True(true) body that verified nothing even when unskipped.
        // ApplicationDbContext's real constructor takes (DbContextOptions<ApplicationDbContext>,
        // IConfiguration? = null, ITenantContext? = null) — both optional parameters default to
        // null, so a context can be constructed with just the required options, exactly as the
        // production DI registration and every other test fixture in this suite already do via
        // TestHelpers.CreateInMemoryDb(). This proves the constructor is callable and the context
        // initializes without throwing.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"dbcontext_ctor_test_{Guid.NewGuid()}")
            .Options;

        using var db = new ApplicationDbContext(options);

        Assert.NotNull(db);
        Assert.NotNull(db.Employees);
        Assert.NotNull(db.Companies);
    }

    [Fact]
    public void Migration_AddMissingTables_ShouldExist()
    {
        // FIX: Previously named after a stale migration timestamp
        // ("20260815100000") that does not exist in the Migrations/MySql folder — the test
        // was skipped with "tested via EF migrations" and a placeholder body that verified
        // nothing even when unskipped. The migration that actually creates the 12 new tables
        // (DocumentTemplates, ComplianceChecklists, etc. — the same set asserted by
        // ApplicationDbContext_HasAllNewDbSets above) is 20260819061842_AddMissingTables.
        // Verify it is present in the compiled model's migration assembly so a future rename
        // or accidental deletion of the migration file fails this test instead of the older
        // stale-named stub silently passing forever.
        var migrationIds = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseMySql("Server=localhost;Database=hrms_migration_probe;",
                        Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.4.11-mysql"))
                    .Options)
            .Database.GetMigrations()
            .ToList();

        Assert.Contains(
            migrationIds,
            id => id.Contains("AddMissingTables", StringComparison.OrdinalIgnoreCase));
    }
}
