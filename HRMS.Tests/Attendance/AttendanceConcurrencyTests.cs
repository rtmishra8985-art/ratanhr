using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HRMS.Tests.Attendance;

/// <summary>
/// Verifies that simultaneous web check-ins result in one attendance row.
/// SQLite is used in-process so the database unique constraint participates in
/// the race without requiring a running MySQL instance.
/// </summary>
public sealed class AttendanceConcurrencyTests
{
    [Fact]
    public async Task ConcurrentWebCheckIns_CreateExactlyOneAttendanceRecord()
    {
        const string connectionString =
            "Data Source=file:attendance_concurrency?mode=memory&cache=shared";

        // Keep the shared in-memory database alive while each context opens
        // its own connection, matching two independent application requests.
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();

        await using (var seedDb = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(anchor)
                .Options))
        {
            await seedDb.Database.EnsureCreatedAsync();
            seedDb.Employees.Add(new Employee
            {
                EmployeeId = 1,
                EmployeeCode = "EMP_CONCURRENT",
                FullName = "Concurrency Test",
                IsActive = true,
                CompanyId = 1
            });
            await seedDb.SaveChangesAsync();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;
        await using var db1 = new ApplicationDbContext(options);
        await using var db2 = new ApplicationDbContext(options);
        var service1 = BuildService(db1);
        var service2 = BuildService(db2);

        var t1 = Task.Run(() => service1.WebCheckInAsync("EMP_CONCURRENT"));
        var t2 = Task.Run(() => service2.WebCheckInAsync("EMP_CONCURRENT"));

        var results = await Task.WhenAll(t1, t2);

        await using var verifyDb = new ApplicationDbContext(options);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var records = await verifyDb.WebAttendances
            .Where(a => a.EmployeeId == "EMP_CONCURRENT" && a.AttDate == today)
            .ToListAsync();

        Assert.Single(records);
        Assert.All(results, id => Assert.Equal(records[0].Id, id));
    }

    private static AttendanceService BuildService(ApplicationDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Attendance:BackDateEditWindowDays"] = "7"
            })
            .Build();

        return new AttendanceService(
            db,
            new MockAuditService(),
            new MockPayrollLockGuard(),
            config,
            new MockLogger<AttendanceService>());
    }
}