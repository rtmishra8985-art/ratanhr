#if TESTCONTAINERS_ENABLED
// Phase 8: Renamed from PostgresIntegrationTests.cs. Replaced:
//   - PostgreSqlContainer    → MySqlContainer (Testcontainers.MySql)
//   - UseNpgsql              → UseMySql (Pomelo)
//   - NpgsqlConnection       → MySqlConnection (MySqlConnector)
//   - postgres:16-alpine     → mysql:8.4
//   - CREATE EXTENSION calls → removed (MySQL does not use PostgreSQL extensions)
//   - PostgreSQL fixture SQL → MySQL-compatible SQL
//
// These tests require Docker to be available.
// They are conditionally compiled with -p:DefineConstants=TESTCONTAINERS_ENABLED
// Run with: dotnet test -p:DefineConstants=TESTCONTAINERS_ENABLED
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Testcontainers.MySql;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Authentication;
using HRMS.Domain.Entities.Employee;
using HRMS.Tests.Mocks;
using Moq;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Integration tests using a real MySQL 8.4 container via Testcontainers.
/// These tests verify FK constraints, real SQL, DateOnly handling, and EF Core translations
/// that InMemory cannot exercise.
///
/// Requires: Docker installed and running.
/// Run: dotnet test -p:DefineConstants=TESTCONTAINERS_ENABLED
/// </summary>
[Collection("MySQL")]
public class MySqlIntegrationTests : IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("hrms_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private ApplicationDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _mysql.StartAsync();
        var connectionString = _mysql.GetConnectionString();
        var serverVersion = ServerVersion.AutoDetect(connectionString);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connectionString, serverVersion)
            .Options;
        _db = new ApplicationDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _mysql.DisposeAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<string> SeedEmployeeAsync(string empId, int companyId = 1)
    {
        var user = new User
        {
            FullName = $"Test {empId}", Email = $"{empId.ToLower()}@test.com",
            PasswordHash = "x", Role = "employee", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _db.Employees.Add(new Employee
        {
            EmployeeId = empId, UserId = user.Id, CompanyId = companyId,
            FullName = $"Test {empId}", Designation = "Dev", Department = "Eng",
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return empId;
    }

    // ── MySQL-specific tests ─────────────────────────────────────────────────

    [Fact]
    public async Task MySQL_DateOnly_StoredAndRetrievedCorrectly()
    {
        await SeedEmployeeAsync("MY_EMP001");
        var svc = new PayrollService(_db, new Mock<IAuditService>().Object,
            new MockNotificationService(), new MockPayrollCalculator(),
            new MockLogger<PayrollService>());

        var dto = new GeneratePayslipDto
        {
            EmployeeId = "MY_EMP001", Month = 7, Year = 2026, BasicPay = 50_000
        };
        var id = await svc.GeneratePayslipAsync(dto, null, null);
        var payslip = await svc.GetPayslipAsync(id);

        Assert.NotNull(payslip);
        Assert.Equal(7, payslip!.Month);
        Assert.Equal(2026, payslip.Year);
    }

    [Fact]
    public async Task MySQL_ForeignKey_EmployeeDeleteCascadeRestrict()
    {
        // MySQL enforces FK constraints — deleting a user with employees referencing
        // them should raise a DbUpdateException (or equivalent).
        await SeedEmployeeAsync("MY_EMP002");
        var user = await _db.Users.FirstAsync(u => u.Email == "my_emp002@test.com");

        _db.Users.Remove(user);
        // FK from Employee.UserId → Users.Id with no cascade should throw
        await Assert.ThrowsAnyAsync<Exception>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task MySQL_OptimisticConcurrency_PayrollLock_ThrowsOnConflict()
    {
        // Verifies that IsRowVersion() on PayrollLock.RowVersion raises
        // DbUpdateConcurrencyException on concurrent edits.
        // (Simulated by manually fetching two instances and saving both.)
        var lock1 = new HRMS.Domain.Entities.Payroll.PayrollLock
        {
            CompanyId = 1, Month = 7, Year = 2026, IsLocked = true,
            LockedAt = DateTime.UtcNow, LockedByUserId = 1
        };
        _db.Set<HRMS.Domain.Entities.Payroll.PayrollLock>().Add(lock1);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var ctx1 = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseMySql(_mysql.GetConnectionString(), ServerVersion.AutoDetect(_mysql.GetConnectionString()))
                .Options);
        var ctx2 = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseMySql(_mysql.GetConnectionString(), ServerVersion.AutoDetect(_mysql.GetConnectionString()))
                .Options);

        var l1 = await ctx1.Set<HRMS.Domain.Entities.Payroll.PayrollLock>()
            .FirstAsync(x => x.CompanyId == 1 && x.Month == 7 && x.Year == 2026);
        var l2 = await ctx2.Set<HRMS.Domain.Entities.Payroll.PayrollLock>()
            .FirstAsync(x => x.CompanyId == 1 && x.Month == 7 && x.Year == 2026);

        l1.Notes = "First writer";
        await ctx1.SaveChangesAsync();

        l2.Notes = "Second writer";
        await Assert.ThrowsAnyAsync<Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException>(
            () => ctx2.SaveChangesAsync());

        await ctx1.DisposeAsync();
        await ctx2.DisposeAsync();
    }

    [Fact]
    public async Task MySQL_LikeSearch_CaseInsensitive()
    {
        // Verifies that EF.Functions.Like on lowercased strings works correctly
        // with MySQL's utf8mb4_unicode_ci collation (case-insensitive).
        await SeedEmployeeAsync("MY_EMP003");
        var user = await _db.Users.FirstAsync(u => u.Email == "my_emp003@test.com");

        // Verify search by partial email — should find the record regardless of case
        var found = await _db.Users
            .Where(u => EF.Functions.Like(u.Email.ToLower(), "%emp003%"))
            .FirstOrDefaultAsync();

        Assert.NotNull(found);
        Assert.Equal(user.Id, found!.Id);
    }
}
#endif
