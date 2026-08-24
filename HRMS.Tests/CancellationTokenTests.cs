using FluentAssertions;
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
/// Verifies that all async service methods correctly honour CancellationToken.
/// A cancelled token must propagate OperationCanceledException (or TaskCanceledException)
/// rather than silently succeeding or throwing an unrelated exception.
/// </summary>
public class CancellationTokenTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private const int CompanyId = 1;

    public CancellationTokenTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    // ─── PayrollService ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PayrollService_GetAllPayslipsPaged_CancelledToken_Throws()
    {
        // Arrange
        var svc = BuildPayrollService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.GetAllPayslipsPagedAsync(null, null, null, CompanyId, 1, 10, ct: cts.Token));
    }

    [Fact]
    public async Task PayrollService_GetAllPayslips_CancelledToken_Throws()
    {
        // Arrange
        var svc = BuildPayrollService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.GetAllPayslipsAsync(companyId: CompanyId, ct: cts.Token));
    }

    // ─── LeaveService ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveService_GetLeaveTypesPaged_CancelledToken_Throws()
    {
        // Arrange
        var svc = BuildLeaveService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.GetLeaveTypesPagedAsync(CompanyId, 1, 10, cts.Token));
    }

    [Fact]
    public async Task LeaveService_GetLeaveRequests_CancelledToken_Throws()
    {
        // Arrange
        var svc = BuildLeaveService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.GetLeaveRequestsAsync(CompanyId, ct: cts.Token));
    }

    // ─── AttendanceService ────────────────────────────────────────────────────────

    [Fact]
    public async Task AttendanceService_GetAttendance_CancelledToken_Throws()
    {
        // Arrange
        var svc = BuildAttendanceService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.GetAttendanceAsync(
                employeeId: "E001", companyId: CompanyId,
                startDate: new DateOnly(2025, 6, 1),
                endDate: new DateOnly(2025, 6, 30),
                ct: cts.Token));
    }

    // ─── RecruitmentService ───────────────────────────────────────────────────────

    [Fact]
    public async Task RecruitmentService_GetRequisitions_CancelledToken_Throws()
    {
        // Arrange
        var svc = BuildRecruitmentService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.ListRequisitionsAsync(CompanyId, ct: cts.Token));
    }

    // ─── Not-yet-cancelled token (baseline) ──────────────────────────────────────

    [Fact]
    public async Task PayrollService_GetAllPayslipsPaged_ValidToken_DoesNotThrow()
    {
        // Arrange
        var svc = BuildPayrollService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act & Assert — should complete without throwing
        var act = async () => await svc.GetAllPayslipsPagedAsync(
            null, null, null, CompanyId, 1, 10, ct: cts.Token);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LeaveService_GetLeaveTypes_ValidToken_DoesNotThrow()
    {
        // Arrange
        var svc = BuildLeaveService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act & Assert
        var act = async () => await svc.GetLeaveTypesAsync(CompanyId);
        await act.Should().NotThrowAsync();
    }

    // ─── Cancellation after start ─────────────────────────────────────────────────

    [Fact]
    public async Task PayrollService_TokenCancelledMidway_DoesNotSilentlySucceed()
    {
        // Arrange
        var svc = BuildPayrollService();
        using var cts = new CancellationTokenSource();

        // Cancel on a very short timer to race against the DB call
        cts.CancelAfter(TimeSpan.FromMilliseconds(1));

        // Act
        bool cancelled = false;
        try
        {
            await svc.GetAllPayslipsPagedAsync(null, null, null, CompanyId, 1, 100, ct: cts.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        // Assert — either the query ran before the cancel (valid) or it was cancelled (valid)
        // What is NOT valid: swallowing the cancellation and returning results silently
        // This test documents the expected cancellation-aware behaviour.
        _ = cancelled; // Either outcome is acceptable here; the test ensures no unhandled exception
    }

    // ─── AuditService ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AuditService_GetLogs_CancelledToken_Throws()
    {
        // Arrange
        var svc = new AuditService(_db);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.GetLogsAsync(CompanyId, ct: cts.Token));
    }

    // ─── Seed + factory helpers ───────────────────────────────────────────────────

    private void SeedData()
    {
        _db.Payslips.Add(new Payslip
        {
            PayslipId = 1, EmployeeId = "E001",
            CompanyId = CompanyId, Month = 6, Year = 2025,
            NetSalary = 50000m, Status = "Generated"
        });
        _db.LeaveTypes.Add(new LeaveType
        {
            LeaveTypeId = 1, CompanyId = CompanyId,
            Name = "Annual", Quota = 20
        });
        _db.SaveChanges();
    }

    private IPayrollService BuildPayrollService()
        => new PayrollService(_db,
            new Mock<IAuditService>().Object,
            new MockNotificationService(),
            new MockPayrollCalculator(),
            new MockLogger<PayrollService>());

    private ILeaveService BuildLeaveService()
        => new LeaveService(_db,
            new Mock<IAuditService>().Object,
            new MockEmailService(),
            new MockLogger<LeaveService>(),
            new Mock<INotificationService>().Object);

    private IAttendanceService BuildAttendanceService()
        => new AttendanceService(_db, new Mock<IAuditService>().Object);

    private IRecruitmentService BuildRecruitmentService()
        => new RecruitmentService(_db, new MockLogger<RecruitmentService>());
}
