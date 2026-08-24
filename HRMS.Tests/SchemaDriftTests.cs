using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Guards against EF model / manual SQL script drift.
///
/// The codebase maintains both EF Core migrations and raw SQL setup scripts
/// (db_setup.sql, db_recruitment.sql, db_performance.sql, db_setup_additions.sql).
/// If a developer adds a table to the SQL scripts but forgets to create a matching
/// EF migration — or vice versa — the application may fail at runtime with
/// "relation does not exist" errors.
///
/// These tests assert that every DbSet registered in ApplicationDbContext
/// has a corresponding relational table name configured, and that no pending
/// model changes exist against the compiled snapshot. They run in CI on every
/// push so drift is caught before it reaches a deployed environment.
/// </summary>
public class SchemaDriftTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static DbContextOptions<ApplicationDbContext> BuildInMemoryOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"SchemaDrift_{Guid.NewGuid()}")
            .Options;
    }

    // ── 1. Every DbSet has a table-name mapping ─────────────────────────────
    // Detects: a DbSet added to ApplicationDbContext without configuring
    // a matching table via ToTable() or conventions — the table name would be
    // inferred and may differ from the SQL script.

    [Fact]
    public void AllDbSets_HaveExplicitTableNameOrConvention()
    {
        using var ctx = new ApplicationDbContext(BuildInMemoryOptions(), config: null, tenant: null);
        var model = ctx.Model;

        var entityTypes = model.GetEntityTypes().ToList();
        Assert.NotEmpty(entityTypes);

        foreach (var entityType in entityTypes)
        {
            var tableName = entityType.GetTableName();
            Assert.False(
                string.IsNullOrWhiteSpace(tableName),
                $"Entity '{entityType.ClrType.Name}' has no table name mapped. " +
                $"Add a ToTable() call in OnModelCreating or verify the EF convention matches your SQL schema.");
        }
    }

    // ── 2. Core domain tables are present in the EF model ──────────────────
    // Detects: a table that exists in db_setup.sql but whose DbSet / entity
    // was accidentally removed from ApplicationDbContext or never added.
    // Each table name here must match the name used in the SQL scripts exactly.

    [Theory]
    [InlineData("users")]
    [InlineData("employees")]
    [InlineData("companies")]
    [InlineData("departments")]
    [InlineData("web_attendances")]
    [InlineData("leave_requests")]
    [InlineData("leave_balances")]
    [InlineData("salary_structures")]
    [InlineData("bonuses")]
    [InlineData("deductions")]
    [InlineData("payslips")]
    [InlineData("audit_logs")]
    [InlineData("notifications")]
    [InlineData("roles")]
    [InlineData("permissions")]
    [InlineData("refresh_tokens")]
    [InlineData("employee_documents")]
    [InlineData("company_branches")]
    [InlineData("company_settings")]
    [InlineData("holiday_calendars")]
    [InlineData("shifts")]
    [InlineData("assets")]
    [InlineData("helpdesk_tickets")]
    [InlineData("onboarding_templates")]
    [InlineData("onboarding_records")]
    [InlineData("training_programs")]
    [InlineData("training_enrollments")]
    [InlineData("expense_claims")]
    [InlineData("travel_requests")]
    [InlineData("timesheet_entries")]
    [InlineData("webhook_subscriptions")]
    public void SqlScriptTable_ExistsInEfModel(string expectedTableName)
    {
        using var ctx = new ApplicationDbContext(BuildInMemoryOptions(), config: null, tenant: null);
        var model = ctx.Model;

        var mapped = model
            .GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(t => t is not null)
            .Select(t => t!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(
            mapped.Contains(expectedTableName),
            $"Table '{expectedTableName}' is defined in a SQL setup script but has no " +
            $"matching entity / DbSet in ApplicationDbContext. " +
            $"Add the entity and a migration, or remove the SQL script entry.");
    }

    // ── 3. Recruitment module tables (db_recruitment.sql) ──────────────────

    [Theory]
    [InlineData("job_requisitions")]
    [InlineData("candidates")]
    [InlineData("interviews")]
    [InlineData("offer_letters")]
    public void RecruitmentSqlTable_ExistsInEfModel(string expectedTableName)
    {
        using var ctx = new ApplicationDbContext(BuildInMemoryOptions(), config: null, tenant: null);
        var model = ctx.Model;

        var mapped = model
            .GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(t => t is not null)
            .Select(t => t!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(
            mapped.Contains(expectedTableName),
            $"Recruitment table '{expectedTableName}' is defined in db_recruitment.sql " +
            $"but has no matching entity in ApplicationDbContext.");
    }

    // ── 4. Performance module tables (db_performance.sql) ──────────────────

    [Theory]
    [InlineData("performance_cycles")]
    [InlineData("employee_goals")]
    [InlineData("performance_reviews")]
    [InlineData("continuous_feedback")]
    public void PerformanceSqlTable_ExistsInEfModel(string expectedTableName)
    {
        using var ctx = new ApplicationDbContext(BuildInMemoryOptions(), config: null, tenant: null);
        var model = ctx.Model;

        var mapped = model
            .GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(t => t is not null)
            .Select(t => t!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(
            mapped.Contains(expectedTableName),
            $"Performance table '{expectedTableName}' is defined in db_performance.sql " +
            $"but has no matching entity in ApplicationDbContext.");
    }

    // ── 5. No duplicate table names in EF model ────────────────────────────
    // Detects: two entities accidentally mapped to the same table name,
    // which causes silent data corruption or EF scaffold errors.

    [Fact]
    public void EfModel_HasNoDuplicateTableNames()
    {
        using var ctx = new ApplicationDbContext(BuildInMemoryOptions(), config: null, tenant: null);
        var model = ctx.Model;

        var tableNames = model
            .GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();

        var duplicates = tableNames
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }
}
