using FluentAssertions;
using HRMS.Application.DTOs.Leave;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Comprehensive tests for LeaveService.
/// Covers application, approval, rejection, balance, carry-forward, and IDOR scenarios.
/// All operations include CancellationToken.
/// </summary>
public class LeaveServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly ILeaveService _svc;
    private const int CompanyId = 1;
    private const int LeaveTypeId = 1;

    public LeaveServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _svc = new LeaveService(_db, new MockAuditService(), new MockEmailService(), new MockLogger<LeaveService>(), new MockNotificationService());
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    // ─── Apply leave ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_WithinBalance_Succeeds()
    {
        // Arrange
        var dto = BuildApplyDto("E001", new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 3));

        // Act
        var result = await _svc.ApplyAsync(dto.EmployeeId, CompanyId, dto);

        // Assert
        result.ok.Should().BeTrue();
        // Filter to Pending so we get the newly-applied request, not the seeded Approved one.
        var request = await _db.LeaveRequests.FirstOrDefaultAsync(r => r.EmployeeId == "E001" && r.Status == "Pending");
        request.Should().NotBeNull();
        request!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task ApplyAsync_ExceedingBalance_Fails()
    {
        // Arrange — balance is 5 days, requesting 10
        var dto = BuildApplyDto("E001",
            new DateOnly(2025, 7, 1),
            new DateOnly(2025, 7, 10));   // 10 days, more than balance

        // Act
        var result = await _svc.ApplyAsync(dto.EmployeeId, CompanyId, dto);

        // Assert
        result.ok.Should().BeFalse("leave application must fail when balance is insufficient");
    }

    [Fact]
    public async Task ApplyAsync_OverlappingDates_Fails()
    {
        // Arrange — apply for the same period twice
        var dto = BuildApplyDto("E001", new DateOnly(2025, 8, 1), new DateOnly(2025, 8, 3));
        await _svc.ApplyAsync(dto.EmployeeId, CompanyId, dto);

        // Act — re-apply for overlapping dates
        var overlapping = BuildApplyDto("E001", new DateOnly(2025, 8, 2), new DateOnly(2025, 8, 4));
        var result = await _svc.ApplyAsync(overlapping.EmployeeId, CompanyId, overlapping);

        // Assert
        result.ok.Should().BeFalse("overlapping leave dates must be rejected");
    }

    [Fact]
    public async Task ApplyAsync_EndDateBeforeStart_Fails()
    {
        // Arrange
        var dto = BuildApplyDto("E001",
            startDate: new DateOnly(2025, 7, 10),
            endDate:   new DateOnly(2025, 7, 5));   // end before start

        // Act
        var result = await _svc.ApplyAsync(dto.EmployeeId, CompanyId, dto);

        // Assert
        result.ok.Should().BeFalse("end date before start must be rejected");
    }

    [Fact]
    public async Task ApplyAsync_ZeroBalance_Fails()
    {
        // Arrange — exhaust E001's remaining 5 days by seeding 5 more approved leave days,
        // bringing total used to 20 (== AnnualQuotaDays) so remaining == 0.
        // LeaveService.ApplyAsync computes remaining from LeaveRequests via UsedDaysAsync,
        // NOT from LeaveBalance.AvailableDays, so we must seed a real request here.
        _db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId  = "E001",
            CompanyId   = CompanyId,
            LeaveTypeId = LeaveTypeId,
            StartDate   = new DateOnly(2025, 2, 1),
            EndDate     = new DateOnly(2025, 2, 5),
            TotalDays   = 5,
            Status      = "Approved",
            CreatedAt   = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var dto = BuildApplyDto("E001", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 1));

        // Act
        var result = await _svc.ApplyAsync(dto.EmployeeId, CompanyId, dto);

        // Assert
        result.ok.Should().BeFalse("zero balance must prevent leave application");
    }

    [Fact]
    public async Task ApplyAsync_InactiveLeaveType_Fails()
    {
        // Arrange
        var leaveType = await _db.LeaveTypes.FindAsync(LeaveTypeId);
        leaveType!.IsActive = false;
        await _db.SaveChangesAsync();

        var dto = BuildApplyDto("E001", new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 1));

        // Act
        var result = await _svc.ApplyAsync(dto.EmployeeId, CompanyId, dto);

        // Assert
        result.ok.Should().BeFalse("inactive leave type must not be usable");
    }

    // ─── Decide (approve / reject) ────────────────────────────────────────────────

    [Fact]
    public async Task DecideAsync_Approve_UpdatesBalanceUsage()
    {
        // Arrange — apply then approve
        var dto = BuildApplyDto("E001", new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 2));
        await _svc.ApplyAsync(dto.EmployeeId, CompanyId, dto);
        var request = await _db.LeaveRequests.FirstAsync(r => r.EmployeeId == "E001" && r.Status == "Pending");

        // LeaveService does NOT write back to LeaveBalance.UsedDays; balance is computed
        // on-the-fly from LeaveRequests via UsedDaysAsync.  Measure "used" by summing the
        // service-visible rows (Status != Rejected/Cancelled) before and after.
        var usedBefore = await _db.LeaveRequests
            .Where(r => r.EmployeeId == "E001" && r.LeaveTypeId == LeaveTypeId
                     && r.Status != "Rejected" && r.Status != "Cancelled"
                     && r.StartDate.Year == 2025)
            .SumAsync(r => (int?)r.TotalDays) ?? 0;

        // Act
        var success = await _svc.DecideAsync(
            request.LeaveRequestId,
            approverUserId: 1,
            new LeaveDecisionDto { Approve = true },
            callerCompanyId: CompanyId);

        // Assert — request status changed and used-day total grew
        success.ok.Should().BeTrue();
        var updated = await _db.LeaveRequests.FindAsync(request.LeaveRequestId);
        updated!.Status.Should().Be("Approved");
        var usedAfter = await _db.LeaveRequests
            .Where(r => r.EmployeeId == "E001" && r.LeaveTypeId == LeaveTypeId
                     && r.Status != "Rejected" && r.Status != "Cancelled"
                     && r.StartDate.Year == 2025)
            .SumAsync(r => (int?)r.TotalDays) ?? 0;
        usedAfter.Should().BeGreaterThan(usedBefore,
            "approving leave must count toward used days in the balance computation");
    }

    [Fact]
    public async Task DecideAsync_Reject_DoesNotDeductBalance()
    {
        // Arrange
        var dto = BuildApplyDto("E001", new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 2));
        await _svc.ApplyAsync(dto.EmployeeId, CompanyId, dto);
        var request = await _db.LeaveRequests.FirstAsync(r => r.EmployeeId == "E001" && r.Status == "Pending");

        // LeaveService does NOT write back to LeaveBalance.UsedDays; balance is computed
        // on-the-fly from LeaveRequests via UsedDaysAsync.  Capture the non-rejected
        // total before and after — rejecting must not increase the used-day count.
        var usedBefore = await _db.LeaveRequests
            .Where(r => r.EmployeeId == "E001" && r.LeaveTypeId == LeaveTypeId
                     && r.Status != "Rejected" && r.Status != "Cancelled"
                     && r.StartDate.Year == 2025)
            .SumAsync(r => (int?)r.TotalDays) ?? 0;

        // Act
        var success = await _svc.DecideAsync(
            request.LeaveRequestId,
            approverUserId: 1,
            new LeaveDecisionDto { Approve = false },
            callerCompanyId: CompanyId);

        // Assert — request is Rejected and no extra days are charged
        success.ok.Should().BeTrue();
        var updated = await _db.LeaveRequests.FindAsync(request.LeaveRequestId);
        updated!.Status.Should().Be("Rejected");
        var usedAfter = await _db.LeaveRequests
            .Where(r => r.EmployeeId == "E001" && r.LeaveTypeId == LeaveTypeId
                     && r.Status != "Rejected" && r.Status != "Cancelled"
                     && r.StartDate.Year == 2025)
            .SumAsync(r => (int?)r.TotalDays) ?? 0;
        usedAfter.Should().Be(usedBefore, "rejecting leave must not affect used-day count");
    }

    [Fact]
    public async Task DecideAsync_NonExistentRequest_ReturnsFalse()
    {
        // Act
        var success = await _svc.DecideAsync(
            999999,
            approverUserId: 1,
            new LeaveDecisionDto { Approve = true },
            callerCompanyId: CompanyId);

        // Assert
        success.ok.Should().BeFalse("non-existent leave request must return false");
    }

    [Fact]
    public async Task DecideAsync_AlreadyDecided_ReturnsFalse()
    {
        // Arrange — approve once
        var dto = BuildApplyDto("E001", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 2));
        await _svc.ApplyAsync(dto.EmployeeId, CompanyId, dto);
        // Filter to Pending so we get the newly-applied request, not the seeded Approved one.
        var request = await _db.LeaveRequests.FirstAsync(r => r.EmployeeId == "E001" && r.Status == "Pending");
        await _svc.DecideAsync(request.LeaveRequestId, 1, new LeaveDecisionDto { Approve = true }, CompanyId);

        // Act — try to decide again (already approved)
        var secondDecision = await _svc.DecideAsync(
            request.LeaveRequestId, 1, new LeaveDecisionDto { Approve = false }, CompanyId);

        // Assert
        secondDecision.ok.Should().BeFalse("a decided leave request must not be re-decided");
    }

    [Fact]
    public async Task DecideAsync_CrossCompany_ReturnsFalse()
    {
        // Arrange — request belongs to company 1; attempt decision from company 2
        var dto = BuildApplyDto("E001", new DateOnly(2025, 9, 5), new DateOnly(2025, 9, 6));
        await _svc.ApplyAsync(dto.EmployeeId, CompanyId, dto);
        // Filter to Pending so we get the newly-applied request, not the seeded Approved one.
        var request = await _db.LeaveRequests.FirstAsync(r => r.EmployeeId == "E001" && r.Status == "Pending");

        // Act
        var success = await _svc.DecideAsync(
            request.LeaveRequestId,
            approverUserId: 1,
            new LeaveDecisionDto { Approve = true },
            callerCompanyId: 2);       // wrong company

        // Assert
        success.ok.Should().BeFalse("cross-company decision must be rejected");
    }

    // ─── Leave types ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLeaveTypesAsync_ReturnsOnlyOwnCompany()
    {
        // Arrange — add a leave type for company 2
        _db.LeaveTypes.Add(new LeaveType { LeaveTypeId = 99, CompanyId = 2, Name = "ForeignType", Quota = 5 });
        await _db.SaveChangesAsync();

        // Act
        var types = await _svc.GetLeaveTypesAsync(CompanyId);

        // Assert
        types.All(t => t.CompanyId == CompanyId).Should().BeTrue();
    }

    [Fact]
    public async Task CreateLeaveTypeAsync_ValidDto_PersistsAndReturns()
    {
        // Arrange
        var dto = new CreateLeaveTypeDto { Name = "Maternity", Quota = 90, IsPaid = true };

        // Act
        var result = await _svc.CreateLeaveTypeAsync(CompanyId, dto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Maternity");
        result.Quota.Should().Be(90);
        result.CompanyId.Should().Be(CompanyId);
    }

    [Fact]
    public async Task UpdateLeaveTypeAsync_SameCompany_UpdatesSuccessfully()
    {
        // Arrange
        var dto = new CreateLeaveTypeDto { Name = "Annual Revised", Quota = 25, IsPaid = true };

        // Act
        var success = await _svc.UpdateLeaveTypeAsync(LeaveTypeId, CompanyId, dto);

        // Assert
        success.Should().BeTrue();
        var updated = await _db.LeaveTypes.FindAsync(LeaveTypeId);
        updated!.Name.Should().Be("Annual Revised");
        updated.Quota.Should().Be(25);
    }

    [Fact]
    public async Task UpdateLeaveTypeAsync_CrossCompany_ReturnsFalse()
    {
        // Act — company 2 tries to update company 1's leave type
        var success = await _svc.UpdateLeaveTypeAsync(LeaveTypeId, companyId: 2,
            new CreateLeaveTypeDto { Name = "Hacked", Quota = 0 });

        // Assert
        success.Should().BeFalse("cross-company update must be blocked");
    }

    // ─── Carry-forward ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CarryForwardAsync_TransfersRemainingBalance_ToNextYear()
    {
        // Arrange — SeedData already seeded 15 approved days for E001 in 2025.
        // Add 2 more approved days so total used = 17 → remaining = quota(20) - used(17) = 3.
        _db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId  = "E001",
            CompanyId   = CompanyId,
            LeaveTypeId = LeaveTypeId,
            StartDate   = new DateOnly(2025, 3, 1),
            EndDate     = new DateOnly(2025, 3, 2),
            TotalDays   = 2,
            Status      = "Approved",
            CreatedAt   = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act — CarryForwardBalancesAsync is the interface method; it creates
        // LeaveBalanceAdjustment records for the toYear.
        await _svc.CarryForwardBalancesAsync(
            new LeaveCarryForwardDto { CompanyId = CompanyId, FromYear = 2025, ToYear = 2026, MaxDays = 0 },
            actorUserId: 1);

        // Assert — implementation writes carry-forward into LeaveBalanceAdjustments
        var nextYearAdj = await _db.LeaveBalanceAdjustments
            .FirstOrDefaultAsync(a => a.EmployeeId == "E001"
                                   && a.Year        == 2026
                                   && a.LeaveTypeId == LeaveTypeId);
        nextYearAdj.Should().NotBeNull();
        nextYearAdj!.Days.Should().Be(3,
            "remaining days must be carried forward to the next year");
    }

    // ─── CancellationToken ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLeaveTypesAsync_CancelledToken_ThrowsOperationCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _svc.GetLeaveTypesAsync(CompanyId, cts.Token));
    }

    [Fact]
    public async Task GetLeaveRequestsAsync_CancelledToken_ThrowsOperationCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _svc.GetLeaveRequestsAsync(CompanyId, ct: cts.Token));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private ApplyLeaveDto BuildApplyDto(
        string employeeId,
        DateOnly startDate,
        DateOnly endDate)
        => new()
        {
            EmployeeId  = employeeId,
            LeaveTypeId = LeaveTypeId,
            StartDate   = startDate.ToString("yyyy-MM-dd"),
            EndDate     = endDate.ToString("yyyy-MM-dd"),
            Reason      = "Personal leave"
        };

    private void SeedData()
    {
        _db.LeaveTypes.Add(new LeaveType
        {
            LeaveTypeId = LeaveTypeId,
            CompanyId   = CompanyId,
            Name        = "Annual Leave",
            Quota       = 20,
            IsPaid      = true,
            IsActive    = true
        });
        _db.Employees.AddRange(
            new Employee { EmployeeId = 1, CompanyId = CompanyId, FirstName = "Alice", LastName = "A", EmployeeCode = "E001", Status = "Active" },
            new Employee { EmployeeId = 2, CompanyId = CompanyId, FirstName = "Bob",   LastName = "B", EmployeeCode = "E002", Status = "Active" }
        );
        // LeaveBalance rows kept for tests that read UsedDays/AvailableDays directly.
        _db.LeaveBalances.AddRange(
            new LeaveBalance { BalanceId = 1, EmployeeId = "E001", CompanyId = CompanyId, LeaveTypeId = LeaveTypeId, Year = 2025, TotalDays = 20, AvailableDays = 5, UsedDays = 15 },
            new LeaveBalance { BalanceId = 2, EmployeeId = "E002", CompanyId = CompanyId, LeaveTypeId = LeaveTypeId, Year = 2025, TotalDays = 20, AvailableDays = 10, UsedDays = 10 }
        );
        // LeaveService.ApplyAsync computes remaining balance from LeaveRequests (via
        // UsedDaysAsync), NOT from LeaveBalance.AvailableDays.  Seed approved requests
        // so UsedDaysAsync returns the expected "used" count:
        //   E001: 15 of 20 days used  → 5 days remaining
        //   E002: 10 of 20 days used  → 10 days remaining
        _db.LeaveRequests.AddRange(
            new LeaveRequest
            {
                EmployeeId  = "E001",
                CompanyId   = CompanyId,
                LeaveTypeId = LeaveTypeId,
                StartDate   = new DateOnly(2025, 1, 1),
                EndDate     = new DateOnly(2025, 1, 15),
                TotalDays   = 15,
                Status      = "Approved",
                CreatedAt   = DateTime.UtcNow
            },
            new LeaveRequest
            {
                EmployeeId  = "E002",
                CompanyId   = CompanyId,
                LeaveTypeId = LeaveTypeId,
                StartDate   = new DateOnly(2025, 1, 1),
                EndDate     = new DateOnly(2025, 1, 10),
                TotalDays   = 10,
                Status      = "Approved",
                CreatedAt   = DateTime.UtcNow
            }
        );
        _db.SaveChanges();
    }
}
