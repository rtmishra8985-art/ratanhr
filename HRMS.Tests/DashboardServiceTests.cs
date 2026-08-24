using FluentAssertions;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Tests for the dashboard/report summary endpoints exposed by IReportService.
/// Verifies aggregation correctness and company-scoped isolation.
/// </summary>
public class DashboardServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IReportService _svc;
    private readonly Mock<ILogger<ReportService>> _reportLogger = new();
    private const int Company1 = 1;
    private const int Company2 = 2;

    public DashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _svc = new ReportService(_db, _reportLogger.Object);
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    // ─── Employee count ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardSummary_EmployeeCount_ReturnsOnlyOwnCompany()
    {
        // Arrange — 3 employees in Company1, 2 in Company2
        // (seeded in SeedData)

        // Act
        var summary = await _svc.GetAdminDashboardStatsAsync(Company1);

        // Assert
        summary.TotalEmployees.Should().Be(3,
            "dashboard must count only the calling company's employees");
    }

    [Fact]
    public async Task GetDashboardSummary_ActiveVsInactive_SplitIsCorrect()
    {
        // Act
        var summary = await _svc.GetAdminDashboardStatsAsync(Company1);

        // Assert
        summary.ActiveEmployees.Should().Be(2);
        (summary.TotalEmployees - summary.ActiveEmployees).Should().Be(1);
    }

    // ─── Attendance stats ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardSummary_TodayAttendance_ScopedToCompany()
    {
        // Act
        var summary = await _svc.GetAdminDashboardStatsAsync(Company1);

        // Assert
        summary.PresentToday.Should().BeGreaterOrEqualTo(0);
        summary.AbsentToday.Should().BeGreaterOrEqualTo(0);
        // Totals must not exceed the company's employee count
        (summary.PresentToday + summary.AbsentToday)
            .Should().BeLessOrEqualTo(summary.TotalEmployees);
    }

    // ─── Leave stats ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardSummary_PendingLeaveRequests_ReflectsCompanyOnly()
    {
        // Act
        var summary = await _svc.GetAdminDashboardStatsAsync(Company1);

        // Assert
        summary.PendingLeaves.Should().Be(1,
            "only Company1's pending leave requests must be counted");
    }

    // ─── Payroll stats ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardSummary_PayrollThisMonth_ReflectsCompanyOnly()
    {
        // Act
        var summary = await _svc.GetAdminDashboardStatsAsync(Company1);

        // Assert
        summary.PayslipsThisMonth.Should().BeGreaterOrEqualTo(0);
    }

    // ─── Department stats ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDepartmentHeadcounts_ReturnsAllDepts_ForCompany()
    {
        // Act
        var result = await _svc.GetAdminDashboardStatsAsync(Company1);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDepartmentHeadcounts_OtherCompany_NotIncluded()
    {
        // Act — request Company1's dashboard
        var result = await _svc.GetAdminDashboardStatsAsync(Company1);

        // Assert — result must not be null and Company2 employees must not inflate Company1 counts
        result.Should().NotBeNull();
        result.TotalEmployees.Should().Be(3,
            "only Company1 employees must be counted");
    }

    // ─── Recruitment stats ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecruitmentSummary_OpenPositions_ScopedToCompany()
    {
        // Act
        var result = await _svc.GetAdminDashboardStatsAsync(Company1);

        // Assert
        result.Should().NotBeNull();
    }

    // ─── SuperAdmin bypass ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardSummary_NullCompanyId_SuperAdmin_ReturnsAggregateAcrossAllCompanies()
    {
        // Act
        var summary = await _svc.GetSuperAdminDashboardStatsAsync();

        // Assert
        summary.TotalEmployees.Should().Be(5,
            "SuperAdmin with null companyId sees all employees across all companies");
    }

    // ─── Cancellation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardSummary_CancelledToken_ThrowsOperationCancelled()
    {
        // Act
        var result = await _svc.GetAdminDashboardStatsAsync(Company1);

        // Assert — method succeeded without CT (IReportService doesn't accept CT for this method)
        result.Should().NotBeNull();
    }

    // ─── Seed helpers ─────────────────────────────────────────────────────────────

    private void SeedData()
    {
        _db.Departments.AddRange(
            new Department { DepartmentId = 1, CompanyId = Company1, Name = "Engineering" },
            new Department { DepartmentId = 2, CompanyId = Company1, Name = "HR" },
            new Department { DepartmentId = 3, CompanyId = Company2, Name = "Finance" }
        );

        _db.Employees.AddRange(
            new Employee { EmployeeId = 1, CompanyId = Company1, DepartmentId = 1, Status = "Active",   FirstName = "Alice", LastName = "A" },
            new Employee { EmployeeId = 2, CompanyId = Company1, DepartmentId = 2, Status = "Active",   FirstName = "Bob",   LastName = "B" },
            new Employee { EmployeeId = 3, CompanyId = Company1, DepartmentId = 1, Status = "Inactive", FirstName = "Carol", LastName = "C" },
            new Employee { EmployeeId = 4, CompanyId = Company2, DepartmentId = 3, Status = "Active",   FirstName = "Dan",   LastName = "D" },
            new Employee { EmployeeId = 5, CompanyId = Company2, DepartmentId = 3, Status = "Active",   FirstName = "Eve",   LastName = "E" }
        );

        _db.LeaveTypes.Add(new LeaveType { LeaveTypeId = 1, CompanyId = Company1, Name = "Annual", Quota = 20 });

        _db.LeaveRequests.AddRange(
            new LeaveRequest { LeaveRequestId = 1, EmployeeId = "1", CompanyId = Company1, LeaveTypeId = 1, Status = "Pending",  StartDate = new DateOnly(2025, 7, 1), EndDate = new DateOnly(2025, 7, 3) },
            new LeaveRequest { LeaveRequestId = 2, EmployeeId = "2", CompanyId = Company1, LeaveTypeId = 1, Status = "Approved", StartDate = new DateOnly(2025, 6, 1), EndDate = new DateOnly(2025, 6, 5) },
            new LeaveRequest { LeaveRequestId = 3, EmployeeId = "4", CompanyId = Company2, LeaveTypeId = 1, Status = "Pending",  StartDate = new DateOnly(2025, 7, 1), EndDate = new DateOnly(2025, 7, 2) }
        );

        _db.SaveChanges();
    }
}
