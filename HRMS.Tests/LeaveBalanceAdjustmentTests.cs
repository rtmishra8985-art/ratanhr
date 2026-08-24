using HRMS.Application.DTOs.Leave;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Xunit;

namespace HRMS.Tests;

public class LeaveBalanceAdjustmentTests
{
    private static LeaveType SeedLeaveType(HRMS.Infrastructure.Data.ApplicationDbContext db, int quota = 5)
    {
        var t = new LeaveType { Name = "Sick Leave", AnnualQuotaDays = quota, IsPaid = true, IsActive = true };
        db.LeaveTypes.Add(t);
        db.SaveChanges();
        return t;
    }

    [Fact]
    public async Task AdjustBalance_CreditIncreasesAvailableDays()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc  = new LeaveService(db, new MockAuditService(), new MockEmailService(), new MockLogger<LeaveService>(), new MockNotificationService());
        var type = SeedLeaveType(db, quota: 5);

        // Apply a 3-day leave (should succeed)
        var (ok1, _, _) = await svc.ApplyAsync("EMP001", 1, new ApplyLeaveDto {
            LeaveTypeId = type.Id, StartDate = "2026-08-01", EndDate = "2026-08-03"
        });
        Assert.True(ok1);

        // Try a 4-day leave — should fail (only 2 days left)
        var (ok2, msg2, _) = await svc.ApplyAsync("EMP001", 1, new ApplyLeaveDto {
            LeaveTypeId = type.Id, StartDate = "2026-08-05", EndDate = "2026-08-08"
        });
        Assert.False(ok2);
        Assert.Contains("Insufficient balance", msg2);

        // Admin credits 3 extra days
        await svc.CreateBalanceAdjustmentAsync(actorUserId: 1, companyId: 1,
            new CreateLeaveBalanceAdjustmentDto {
                EmployeeId = "EMP001", LeaveTypeId = type.Id,
                Year = 2026, Days = 3, Reason = "Special credit"
            });

        // Now the 4-day leave should succeed (2 + 3 = 5 available)
        var (ok3, _, _) = await svc.ApplyAsync("EMP001", 1, new ApplyLeaveDto {
            LeaveTypeId = type.Id, StartDate = "2026-08-05", EndDate = "2026-08-08"
        });
        Assert.True(ok3);
    }

    [Fact]
    public async Task CarryForward_CreatesAdjustmentsForNextYear()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc  = new LeaveService(db, new MockAuditService(), new MockEmailService(), new MockLogger<LeaveService>(), new MockNotificationService());
        var type = SeedLeaveType(db, quota: 10);

        // Employee has 10 quota, used 3
        var emp = new HRMS.Domain.Entities.Employee.Employee {
            EmployeeCode = "EMP002", FullName = "Jane Doe", IsActive = true, CompanyId = 1
        };
        db.Employees.Add(emp);
        db.SaveChanges();

        await svc.ApplyAsync("EMP002", 1, new ApplyLeaveDto {
            LeaveTypeId = type.Id, StartDate = "2026-07-01", EndDate = "2026-07-03"
        });
        // Approve
        var myReqs = await svc.GetMyRequestsAsync("EMP002");
        await svc.DecideAsync(myReqs[0].Id, approverUserId: 1, new LeaveDecisionDto { Approve = true });

        // Carry forward 7 remaining days to 2027
        var (processed, skipped) = await svc.CarryForwardBalancesAsync(
            new LeaveCarryForwardDto { FromYear = 2026, ToYear = 2027, MaxDays = 0, CompanyId = 1 },
            actorUserId: 1);

        Assert.True(processed >= 1);
        var adjs = await svc.GetBalanceAdjustmentsAsync("EMP002", 2027);
        Assert.NotEmpty(adjs);
        Assert.Contains(adjs, a => a.Days == 7 && a.Year == 2027);
    }
}
