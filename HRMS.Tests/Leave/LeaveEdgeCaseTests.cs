using HRMS.Application.DTOs.Leave;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Xunit;
using HRMS.Tests;

namespace HRMS.Tests.Leave;

/// <summary>
/// Edge-case unit tests for LeaveService.
/// Covers zero-balance rejection, overlapping-leave rejection,
/// and public-holiday exclusion from deduction.
/// </summary>
public class LeaveEdgeCaseTests
{
    private static LeaveService BuildService(HRMS.Infrastructure.Data.ApplicationDbContext db)
        => new LeaveService(db, new MockAuditService(), new MockEmailService(),
                            new MockLogger<LeaveService>(), new MockNotificationService());

    // ── a) Zero balance → request rejected ────────────────────────────────

    [Fact]
    public async Task ApplyLeave_ZeroRemainingBalance_IsRejected()
    {
        using var db  = TestHelpers.CreateInMemoryDb();
        var svc       = BuildService(db);

        // FIX 4: seed Employee BEFORE LeaveType
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "EMP001", FullName = "Test User",
            IsActive = true, CompanyId = 1
        });
        await db.SaveChangesAsync();

        var type = new LeaveType
        {
            Name = "Annual Leave", AnnualQuotaDays = 0, // zero quota
            IsPaid = true, IsActive = true, CompanyId = 1
        };
        db.LeaveTypes.Add(type);
        await db.SaveChangesAsync();

        // FIX 4: seed LeaveBalance after LeaveType save
        db.LeaveBalances.Add(new HRMS.Domain.Entities.Leave.LeaveBalance
        {
            EmployeeId    = "EMP001",
            CompanyId     = 1,
            LeaveTypeId   = type.LeaveTypeId,
            Year          = 2026,
            TotalDays     = type.AnnualQuotaDays,
            AvailableDays = type.AnnualQuotaDays,
            UsedDays      = 0
        });
        await db.SaveChangesAsync();

        var (ok, msg, _) = await svc.ApplyAsync("EMP001", 1, new ApplyLeaveDto
        {
            LeaveTypeId = type.Id,
            StartDate   = "2026-09-01",
            EndDate     = "2026-09-01"
        });

        Assert.False(ok, "Leave application must be rejected when balance is zero.");
        Assert.False(string.IsNullOrWhiteSpace(msg), "A rejection reason must be provided.");
    }

    // ── b) Overlapping leave → second request rejected ─────────────────────

    [Fact]
    public async Task ApplyLeave_Overlapping_SecondRequestRejected()
    {
        using var db  = TestHelpers.CreateInMemoryDb();
        var svc       = BuildService(db);

        // FIX 4: seed Employee BEFORE LeaveType
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "EMP001", FullName = "Test User",
            IsActive = true, CompanyId = 1
        });
        await db.SaveChangesAsync();

        var type = new LeaveType
        {
            Name = "Sick Leave", AnnualQuotaDays = 10,
            IsPaid = true, IsActive = true, CompanyId = 1
        };
        db.LeaveTypes.Add(type);
        await db.SaveChangesAsync();

        // FIX 4: seed LeaveBalance after LeaveType save
        db.LeaveBalances.Add(new HRMS.Domain.Entities.Leave.LeaveBalance
        {
            EmployeeId    = "EMP001",
            CompanyId     = 1,
            LeaveTypeId   = type.LeaveTypeId,
            Year          = 2026,
            TotalDays     = type.AnnualQuotaDays,
            AvailableDays = type.AnnualQuotaDays,
            UsedDays      = 0
        });
        await db.SaveChangesAsync();

        // First request: Sep 1-3
        var (ok1, _, id1) = await svc.ApplyAsync("EMP001", 1, new ApplyLeaveDto
        {
            LeaveTypeId = type.Id,
            StartDate   = "2026-09-01",
            EndDate     = "2026-09-03"
        });
        Assert.True(ok1, "First leave application should succeed.");

        // Approve it so the dates are reserved
        if (id1.HasValue)
            await svc.DecideAsync(id1.Value, approverUserId: 1, new LeaveDecisionDto { Approve = true });

        // Second request overlapping Sep 2-4 for the same employee
        var (ok2, _, _) = await svc.ApplyAsync("EMP001", 1, new ApplyLeaveDto
        {
            LeaveTypeId = type.Id,
            StartDate   = "2026-09-02",
            EndDate     = "2026-09-04"
        });

        Assert.False(ok2, "Overlapping leave request must be rejected.");

        // Only one approved leave should exist for EMP001 in this range
        var approved = db.LeaveRequests.Where(r =>
            r.EmployeeId == "EMP001" && r.Status == "Approved").Count();
        Assert.Equal(1, approved);
    }

    // ── c) Public holiday in range → excluded from deduction ──────────────

    [Fact]
    public async Task ApplyLeave_SpanningPublicHoliday_HolidayExcludedFromDeduction()
    {
        using var db  = TestHelpers.CreateInMemoryDb();
        var svc       = BuildService(db);

        // FIX 4: seed Employee BEFORE LeaveType
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "EMP001", FullName = "Test User",
            IsActive = true, CompanyId = 1
        });
        await db.SaveChangesAsync();

        var type = new LeaveType
        {
            Name = "Casual Leave", AnnualQuotaDays = 10,
            IsPaid = true, IsActive = true, CompanyId = 1
        };
        db.LeaveTypes.Add(type);

        // Seed a public holiday on Sep 2 (Wednesday)
        db.HolidayCalendars.Add(new HolidayCalendar
        {
            Name      = "Independence Day Test",
            Date      = new DateOnly(2026, 9, 2),
            CompanyId = 1,
            IsActive  = true
        });
        await db.SaveChangesAsync();

        // FIX 4: seed LeaveBalance after LeaveType + Holiday save
        db.LeaveBalances.Add(new HRMS.Domain.Entities.Leave.LeaveBalance
        {
            EmployeeId    = "EMP001",
            CompanyId     = 1,
            LeaveTypeId   = type.LeaveTypeId,
            Year          = 2026,
            TotalDays     = type.AnnualQuotaDays,
            AvailableDays = type.AnnualQuotaDays,
            UsedDays      = 0
        });
        await db.SaveChangesAsync();

        // Apply for Sep 1–3 (3 calendar days; Sep 2 is a holiday → 2 working days deducted)
        var (ok, _, id) = await svc.ApplyAsync("EMP001", 1, new ApplyLeaveDto
        {
            LeaveTypeId = type.Id,
            StartDate   = "2026-09-01",
            EndDate     = "2026-09-03"
        });
        Assert.True(ok, "Leave application should succeed.");
        Assert.NotNull(id);

        await svc.DecideAsync(id!.Value, approverUserId: 1, new LeaveDecisionDto { Approve = true });

        var balance = await svc.GetMyBalanceAsync("EMP001", 1);
        var entry   = balance.FirstOrDefault(b => b.LeaveTypeId == type.Id);
        Assert.NotNull(entry);

        // Sep 2 (holiday) must NOT count — only Sep 1 and Sep 3 are deducted
        Assert.True(entry!.UsedDays <= 2,
            $"Used days should be ≤2 (holiday excluded), got {entry.UsedDays}.");
        Assert.True(entry.UsedDays > 0,
            "At least one working day must have been deducted.");
    }
}
