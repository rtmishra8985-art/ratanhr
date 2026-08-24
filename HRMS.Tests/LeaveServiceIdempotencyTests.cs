using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Tests duplicate-approval idempotency against the current domain entities.
/// </summary>
public class LeaveServiceIdempotencyTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly LeaveService _svc;

    public LeaveServiceIdempotencyTests()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(opts);

        _db.LeaveBalances.Add(new LeaveBalance
        {
            BalanceId = 1,
            EmployeeId = "emp-1",
            LeaveTypeId = 1,
            Year = DateTime.UtcNow.Year,
            TotalDays = 20,
            AvailableDays = 10,
            UsedDays = 0,
            PendingDays = 3,
        });
        _db.LeaveRequests.Add(new LeaveRequest
        {
            Id = 1,
            EmployeeId = "emp-1",
            LeaveTypeId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)),
            TotalDays = 0,
            Status = "Pending",
        });
        _db.SaveChanges();

        _svc = new LeaveService(
            _db,
            new MockAuditService(),
            new MockEmailService(),
            NullLogger<LeaveService>.Instance,
            new MockNotificationService());
    }

    [Fact]
    public async Task Approve_FirstCall_DeductsBalanceOnce()
    {
        var result = await _svc.ApproveLeaveAsync(1, 1);

        Assert.True(result.IsSuccess);
        var balance = await _db.LeaveBalances.FindAsync(1);
        Assert.Equal(7, balance!.AvailableDays);
        Assert.Equal(3, balance.UsedDays);
    }

    [Fact]
    public async Task Approve_SecondCallOnAlreadyApproved_ReturnsSuccessIdempotently()
    {
        await _svc.ApproveLeaveAsync(1, 1);
        var balanceAfterFirst = (await _db.LeaveBalances.FindAsync(1))!.AvailableDays;

        var result = await _svc.ApproveLeaveAsync(1, 1);

        Assert.True(result.IsSuccess);
        var balanceAfterSecond = (await _db.LeaveBalances.FindAsync(1))!.AvailableDays;
        Assert.Equal(balanceAfterFirst, balanceAfterSecond);
    }

    [Fact]
    public async Task Approve_ThenApproveAgain_BalanceChangedExactlyOnce()
    {
        var initialBalance = (await _db.LeaveBalances.FindAsync(1))!.AvailableDays;
        await _svc.ApproveLeaveAsync(1, 1);
        var afterFirst = (await _db.LeaveBalances.FindAsync(1))!.AvailableDays;

        await _svc.ApproveLeaveAsync(1, 1);
        var afterSecond = (await _db.LeaveBalances.FindAsync(1))!.AvailableDays;

        Assert.Equal(10, initialBalance);
        Assert.Equal(7, afterFirst);
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public async Task Reject_DoesNotDeductBalance()
    {
        var result = await _svc.RejectLeaveAsync(1, 1, "Business needs.");

        Assert.True(result.IsSuccess);
        var balance = await _db.LeaveBalances.FindAsync(1);
        Assert.Equal(10, balance!.AvailableDays);
        Assert.Equal(0, balance.UsedDays);

        var request = await _db.LeaveRequests.FindAsync(1);
        Assert.Equal("Rejected", request!.Status);
    }

    [Fact]
    public async Task Approve_AfterRejection_Fails()
    {
        await _svc.RejectLeaveAsync(1, 1, "Declined.");

        var result = await _svc.ApproveLeaveAsync(1, 1);

        Assert.False(result.IsSuccess);
        Assert.Contains("Rejected", result.Error);
    }

    [Fact]
    public async Task Reject_AfterApproval_Fails()
    {
        await _svc.ApproveLeaveAsync(1, 1);

        var result = await _svc.RejectLeaveAsync(1, 1, "Changed mind.");

        Assert.False(result.IsSuccess);
        Assert.Contains("Approved", result.Error);
    }

    public void Dispose() => _db.Dispose();
}