// Fix 7: N+1 regression tests.
// Uses SQLite in-process provider so QueryCounterInterceptor can count real SQL statements.
// Tests fail if service methods exceed their expected query-count thresholds.
using HRMS.Application.DTOs.Leave;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Authentication;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using HRMS.Tests.Mocks;

namespace HRMS.Tests;

/// <summary>
/// N+1 regression tests — assert that bulk operations stay within fixed query-count
/// budgets regardless of dataset size. Uses SQLite so EF Core actually executes SQL
/// commands that the <see cref="QueryCounterInterceptor"/> can count.
/// </summary>
public class N1RegressionTests : IDisposable
{
    private readonly QueryCounterInterceptor _counter = new();
    private readonly HRMS.Infrastructure.Data.ApplicationDbContext _db;
    private readonly SqliteConnection _conn;

    public N1RegressionTests()
    {
        (_db, _conn) = TestHelpers.CreateSqliteDb(_counter);
        // payslips.company_id now carries a real FK to companies(id), so the tenant
        // row must exist before any payslip is written.
        SeedCompany(1);
    }

    private void SeedCompany(int companyId)
    {
        if (_db.Companies.IgnoreQueryFilters().Any(c => c.Id == companyId)) return;
        _db.Companies.Add(new HRMS.Domain.Entities.Company.Company
        {
            CompanyId = companyId,
            Name      = $"Test Company {companyId}",
            IsActive  = true
        });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Employee SeedEmployee(string empId, int companyId = 1)
    {
        var emp = new Employee
        {
            EmployeeCode = empId,
            CompanyId    = companyId,
            FullName    = $"Emp {empId}",
            Designation = "Staff",
            Department  = "HR",
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow
        };
        _db.Employees.Add(emp);

        _db.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeId   = empId,
            BasicPay     = 30_000m,
            HRA          = 10_000m,
            IsActive     = true,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            CreatedAt    = DateTime.UtcNow
        });

        // Add attendance for June 2026
        for (int d = 1; d <= 20; d++)
        {
            _db.WebAttendances.Add(new WebAttendance
            {
                EmployeeId = empId,
                AttDate    = new DateOnly(2026, 6, d),
                Status     = "Present",
                CompanyId  = companyId,
                CreatedAt  = DateTime.UtcNow
            });
        }

        _db.SaveChanges();
        return emp;
    }

    private LeaveType SeedLeaveType(string name = "Casual Leave")
    {
        var lt = new LeaveType { Name = name, AnnualQuotaDays = 12, IsPaid = true, IsActive = true };
        _db.LeaveTypes.Add(lt);
        _db.SaveChanges();
        return lt;
    }

    // ── Payroll N+1 Regression ────────────────────────────────────────────────

    [Fact]
    public async Task BulkGeneratePayroll_10Employees_MaxFewQueries()
    {
        // Seed 10 employees
        for (int i = 1; i <= 10; i++)
            SeedEmployee($"EMP{i:D4}");

        var svc = new PayrollService(_db, new Mock<IAuditService>().Object,
                                     new MockNotificationService(),
                                     new MockPayrollCalculator(), new MockLogger<PayrollService>());

        _counter.Reset();
        var dto = new BulkPayrollDto
        {
            CompanyId = 1,
            Month     = 6,
            Year      = 2026,
            Overwrite = false
        };
        await svc.BulkGeneratePayslipsAsync(dto);

        // After the N+1 fix: 4 pre-load queries + 1 settings query + 1 employee fetch
        // + 1 transaction overhead + 1 SaveChanges batch = well under 20.
        // Previously: 3–4 queries PER employee → ~40 for 10 employees.
        Assert.True(_counter.ReadQueryCount <= 20,
            $"Expected ≤20 read queries for 10-employee bulk payroll, got {_counter.ReadQueryCount} " +
            $"(total statements incl. writes: {_counter.QueryCount}). Possible N+1 regression.");
    }

    [Fact]
    public async Task BulkGeneratePayroll_QueryCountDoesNotScaleWithEmployeeCount()
    {
        // Seed 1 employee and record query count
        SeedEmployee("EMP0001");
        var svc = new PayrollService(_db, new Mock<IAuditService>().Object,
                                     new MockNotificationService(),
                                     new MockPayrollCalculator(), new MockLogger<PayrollService>());

        _counter.Reset();
        var dto = new BulkPayrollDto { CompanyId = 1, Month = 6, Year = 2026 };
        await svc.BulkGeneratePayslipsAsync(dto);
        int queriesFor1 = _counter.ReadQueryCount;

        // Seed 9 more employees
        for (int i = 2; i <= 10; i++)
            SeedEmployee($"EMP{i:D4}");

        _counter.Reset();
        await svc.BulkGeneratePayslipsAsync(new BulkPayrollDto { CompanyId = 1, Month = 6, Year = 2026, Overwrite = true });
        int queriesFor10 = _counter.ReadQueryCount;

        // The delta should be small (ideally ≤ 5) — SaveChanges batches scale, not reads.
        // Before the fix, queriesFor10 - queriesFor1 would be ~27 (3 extra per employee).
        Assert.True(queriesFor10 - queriesFor1 <= 10,
            $"Read query count scaled from {queriesFor1} to {queriesFor10} — possible N+1 regression.");
    }

    // ── Leave Carry-Forward N+1 Regression ───────────────────────────────────

    [Fact]
    public async Task CarryForwardBalances_5Employees3Types_MaxFewQueries()
    {
        // Seed 3 leave types
        for (int t = 1; t <= 3; t++)
            SeedLeaveType($"Type{t}");

        // Seed 5 employees with approved leave requests
        for (int i = 1; i <= 5; i++)
        {
            SeedEmployee($"EMP{i:D4}");
            foreach (var lt in _db.LeaveTypes.ToList())
            {
                _db.LeaveRequests.Add(new LeaveRequest
                {
                    EmployeeId  = $"EMP{i:D4}",
                    CompanyId   = 1,
                    LeaveTypeId = lt.Id,
                    StartDate   = new DateOnly(2025, 8, 1),
                    EndDate     = new DateOnly(2025, 8, 3),
                    TotalDays   = 3,
                    Status      = "Approved",
                    Reason      = "Test",
                    CreatedAt   = DateTime.UtcNow
                });
            }
        }
        _db.SaveChanges();

        var svc = new LeaveService(_db, new Mock<IAuditService>().Object,
                                   new Mock<IEmailService>().Object,
                                   NullLogger<LeaveService>.Instance,
                                   new MockNotificationService());

        _counter.Reset();
        var (processed, _) = await svc.CarryForwardBalancesAsync(new LeaveCarryForwardDto
        {
            CompanyId = 1,
            FromYear  = 2025,
            ToYear    = 2026,
            MaxDays   = 10
        }, actorUserId: 1);

        // After N+1 fix: 2 bulk queries (LeaveRequests + Adjustments) + employee/type fetches
        // + SaveChanges = well under 15 total.
        // Previously: 5 employees × 3 types × 2 queries = 30 queries just for the lookups.
        Assert.True(_counter.ReadQueryCount <= 15,
            $"Expected ≤15 read queries for carry-forward (5 employees, 3 types), got {_counter.ReadQueryCount} " +
            $"(total statements incl. writes: {_counter.QueryCount}). Possible N+1 regression.");

        Assert.True(processed > 0, "Expected at least some carry-forward records processed.");
    }
}
