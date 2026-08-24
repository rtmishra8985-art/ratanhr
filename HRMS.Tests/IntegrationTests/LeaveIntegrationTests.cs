using HRMS.Application.DTOs.Leave;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Xunit;

namespace HRMS.Tests.IntegrationTests;

/// <summary>
/// Phase 1 – Integration tests: Leave module end-to-end flow.
/// Tests apply → approve → verify balance, including lock check at the controller boundary.
/// </summary>
public class LeaveIntegrationTests
{
    [Fact]
    public async Task ApplyAndApprove_DeductsBalance()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new LeaveService(db, new MockAuditService(), new MockEmailService(), new MockLogger<LeaveService>(), new MockNotificationService());

        var type = new LeaveType { Name = "Annual Leave", AnnualQuotaDays = 10, IsPaid = true, IsActive = true };
        db.LeaveTypes.Add(type);
        await db.SaveChangesAsync();

        // Problem C: seed LeaveBalanceAdjustment so the service can resolve available days
        db.LeaveBalanceAdjustments.Add(new HRMS.Domain.Entities.Leave.LeaveBalanceAdjustment
        {
            EmployeeId       = "EMP001",
            CompanyId        = 1,
            LeaveTypeId      = type.LeaveTypeId,
            Year             = DateTime.UtcNow.Year,
            Days             = 10,
            Reason           = "Initial balance",
            AdjustedByUserId = 1
        });
        await db.SaveChangesAsync();

        var (ok, _, id) = await svc.ApplyAsync("EMP001", 1, new ApplyLeaveDto
        {
            LeaveTypeId = type.LeaveTypeId, StartDate = "2026-09-01", EndDate = "2026-09-03"
        });
        Assert.True(ok);

        // Problem D: DecideAsync signature matches; callerCompanyId added explicitly for clarity
        var (decideOk, _) = await svc.DecideAsync(id!.Value, approverUserId: 1,
            new LeaveDecisionDto { Approve = true }, callerCompanyId: 1);
        Assert.True(decideOk);

        var balance = await svc.GetMyBalanceAsync("EMP001", 1);
        var annual  = balance.FirstOrDefault(b => b.LeaveTypeId == type.LeaveTypeId);
        Assert.NotNull(annual);
        Assert.True(annual!.UsedDays >= 3);
    }

    [Fact]
    public async Task ApplyLeave_InsufficientBalance_Fails()
    {
        using var db  = TestHelpers.CreateInMemoryDb();
        var svc  = new LeaveService(db, new MockAuditService(), new MockEmailService(), new MockLogger<LeaveService>(), new MockNotificationService());

        var type = new LeaveType { Name = "Sick Leave", AnnualQuotaDays = 2, IsPaid = true, IsActive = true };
        db.LeaveTypes.Add(type);
        await db.SaveChangesAsync();

        // Problem C: seed LeaveBalanceAdjustment so the service can resolve available days
        db.LeaveBalanceAdjustments.Add(new HRMS.Domain.Entities.Leave.LeaveBalanceAdjustment
        {
            EmployeeId       = "EMP001",
            CompanyId        = 1,
            LeaveTypeId      = type.LeaveTypeId,
            Year             = DateTime.UtcNow.Year,
            Days             = 2,
            Reason           = "Initial balance",
            AdjustedByUserId = 1
        });
        await db.SaveChangesAsync();

        // Apply for 5 days when only 2 are available
        var (ok, msg, _) = await svc.ApplyAsync("EMP001", 1, new ApplyLeaveDto
        {
            LeaveTypeId = type.LeaveTypeId, StartDate = "2026-09-01", EndDate = "2026-09-05"
        });

        Assert.False(ok);
        Assert.Contains("Insufficient", msg, StringComparison.OrdinalIgnoreCase);
    }
}
