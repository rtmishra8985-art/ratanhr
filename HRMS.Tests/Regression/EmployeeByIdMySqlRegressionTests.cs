using FluentAssertions;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.FileStorage;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;

namespace HRMS.Tests.Regression;

/// <summary>
/// Regression guard for the production bug where <c>GetEmployeeByIdAsync</c> filtered on
/// <c>Employee.EmployeeId</c> — a <c>[NotMapped]</c> alias that is also explicitly
/// <c>Ignore()</c>d in <see cref="ApplicationDbContext"/>. EF Core cannot translate an
/// unmapped member, so the query threw <see cref="InvalidOperationException"/> at runtime
/// against MySQL even though it silently "worked" with the InMemory provider.
///
/// These tests use the real Pomelo MySQL provider with a pinned server version, so the
/// full MySQL query pipeline (translation + SQL generation) runs without needing a live
/// database or Docker.
/// </summary>
public class EmployeeByIdMySqlRegressionTests
{
    private const int CompanyId = 1;

    /// <summary>MySQL-provider context — never connects; used for translation assertions.</summary>
    private static ApplicationDbContext CreateMySqlContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                "Server=localhost;Database=hrms_translation_only;User=root;Password=root;",
                ServerVersion.Create(new Version(8, 4, 0), ServerType.MySql))
            .Options;
        return new ApplicationDbContext(options);
    }

    // ── 1. The mapped Id filter must translate to MySQL SQL ────────────────────

    [Fact]
    public void GetEmployeeById_Predicate_TranslatesToMySql_UsingMappedIdColumn()
    {
        using var db = CreateMySqlContext();

        var id = 42;
        var sql = db.Employees
            .Where(e => e.Id == id && e.CompanyId == CompanyId)
            .ToQueryString();

        sql.Should().Contain("`employees`", "the query must target the employees table");
        sql.Should().Contain("`id`", "the filter must use the mapped id primary-key column");
        sql.Should().Contain("`company_id`", "tenant scoping must be part of the SQL");
        // employee_id is the string business key (EmployeeCode) — the int lookup must not use it.
        sql.Should().NotContain("`employee_id` =",
            "the int id lookup must never be translated against the string business-key column");
    }

    // ── 2. The old (buggy) predicate must still be untranslatable ──────────────

    [Fact]
    public void EmployeeIdAlias_IsUnmapped_AndCannotBeTranslatedByMySqlProvider()
    {
        using var db = CreateMySqlContext();

        var entity = db.Model.FindEntityType(typeof(Employee))!;
        entity.FindProperty(nameof(Employee.EmployeeId)).Should().BeNull(
            "EmployeeId is a [NotMapped] alias and is Ignore()d in ApplicationDbContext");

        var id = 42;
        var act = () => db.Employees
            .Where(e => e.EmployeeId == id && e.CompanyId == CompanyId)
            .ToQueryString();

        act.Should().Throw<InvalidOperationException>(
            "filtering on the unmapped alias is exactly the production bug this test guards");
    }

    // ── 3. Behavioural check of the service method itself ──────────────────────

    [Fact]
    public async Task GetEmployeeByIdAsync_ReturnsMatchingEmployee_AndIsTenantScoped()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new ApplicationDbContext(options);

        var mine = new Employee
        {
            EmployeeCode = "EMP001", CompanyId = CompanyId, FullName = "Alice Mine",
            Status = "Active", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var theirs = new Employee
        {
            EmployeeCode = "EMP002", CompanyId = 2, FullName = "Bob Other",
            Status = "Active", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.Employees.AddRange(mine, theirs);
        await db.SaveChangesAsync();

        IEmployeeService svc = new EmployeeService(
            db,
            new Mock<IFileStorageService>().Object,
            new Mock<ILogger<EmployeeService>>().Object);

        var found = await svc.GetEmployeeByIdAsync(mine.Id, CompanyId);
        found.Should().NotBeNull();
        found!.Id.Should().Be(mine.Id);
        found.EmployeeId.Should().Be(mine.Id, "the alias must mirror the mapped PK");

        var crossTenant = await svc.GetEmployeeByIdAsync(theirs.Id, CompanyId);
        crossTenant.Should().BeNull("employees of another company must never be returned");

        var missing = await svc.GetEmployeeByIdAsync(999_999, CompanyId);
        missing.Should().BeNull();
    }
}
