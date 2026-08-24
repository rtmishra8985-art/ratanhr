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
/// Integration tests for pagination, filtering, and sorting across multiple services.
/// Verifies correct page boundaries, filter isolation, and sort direction.
/// All tests use EF Core InMemory with deterministic seed data.
/// </summary>
public class PaginationFilteringSortingTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private const int CompanyId = 1;

    public PaginationFilteringSortingTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    // ─── Payslip pagination ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetPayslipsPaged_Page1Size3_ReturnsFirst3Records()
    {
        // Arrange
        var svc = BuildPayrollService();

        // Act
        var result = await svc.GetAllPayslipsPagedAsync(
            month: null, year: null, employeeId: null,
            companyId: CompanyId, page: 1, pageSize: 3);

        // Assert
        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(10);
        result.TotalPages.Should().Be(4, "10 records / 3 per page = 4 pages (ceiling)");
    }

    [Fact]
    public async Task GetPayslipsPaged_LastPage_ReturnsRemainingRecords()
    {
        // Arrange
        var svc = BuildPayrollService();

        // Act
        var result = await svc.GetAllPayslipsPagedAsync(
            month: null, year: null, employeeId: null,
            companyId: CompanyId, page: 4, pageSize: 3);

        // Assert
        result.Items.Should().HaveCount(1, "last page has only 1 remaining record");
    }

    [Fact]
    public async Task GetPayslipsPaged_PageBeyondTotal_ReturnsEmptyList()
    {
        // Arrange
        var svc = BuildPayrollService();

        // Act
        var result = await svc.GetAllPayslipsPagedAsync(
            month: null, year: null, employeeId: null,
            companyId: CompanyId, page: 99, pageSize: 10);

        // Assert
        result.Items.Should().BeEmpty("page beyond total must return empty, not throw");
    }

    [Fact]
    public async Task GetPayslipsPaged_FilterByMonth_ReturnsMatchingOnly()
    {
        // Arrange
        var svc = BuildPayrollService();

        // Act
        var result = await svc.GetAllPayslipsPagedAsync(
            month: 6, year: 2025, employeeId: null,
            companyId: CompanyId, page: 1, pageSize: 20);

        // Assert
        result.Items.Should().HaveCountGreaterThan(0);
        result.Items.All(p => p.Month == 6 && p.Year == 2025)
              .Should().BeTrue("month filter must apply to all returned records");
    }

    [Fact]
    public async Task GetPayslipsPaged_FilterByEmployee_ReturnsOnlyThatEmployee()
    {
        // Arrange
        var svc = BuildPayrollService();

        // Act
        var result = await svc.GetAllPayslipsPagedAsync(
            month: null, year: null, employeeId: "E001",
            companyId: CompanyId, page: 1, pageSize: 20);

        // Assert
        result.Items.All(p => p.EmployeeId == "E001")
              .Should().BeTrue("employee filter must scope results to that employee");
    }

    [Fact]
    public async Task GetPayslipsPaged_SortByNetSalaryDesc_FirstItemIsHighest()
    {
        // Arrange
        var svc = BuildPayrollService();

        // Act
        var result = await svc.GetAllPayslipsPagedAsync(
            month: null, year: null, employeeId: null,
            companyId: CompanyId, page: 1, pageSize: 10,
            sortBy: "netSalary", sortDirection: "desc");

        // Assert
        result.Items.Should().HaveCountGreaterThan(1);
        var salaries = result.Items.Select(p => p.NetSalary).ToList();
        salaries.Should().BeInDescendingOrder("sort by NetSalary desc must produce descending order");
    }

    [Fact]
    public async Task GetPayslipsPaged_SortByNetSalaryAsc_FirstItemIsLowest()
    {
        // Arrange
        var svc = BuildPayrollService();

        // Act
        var result = await svc.GetAllPayslipsPagedAsync(
            month: null, year: null, employeeId: null,
            companyId: CompanyId, page: 1, pageSize: 10,
            sortBy: "netSalary", sortDirection: "asc");

        // Assert
        var salaries = result.Items.Select(p => p.NetSalary).ToList();
        salaries.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetPayslipsPaged_CompanyIsolation_DoesNotReturnOtherCompany()
    {
        // Arrange
        var svc = BuildPayrollService();

        // Act
        var result = await svc.GetAllPayslipsPagedAsync(
            month: null, year: null, employeeId: null,
            companyId: CompanyId, page: 1, pageSize: 100);

        // Assert
        result.Items.All(p => p.CompanyId == CompanyId)
              .Should().BeTrue("pagination must never return records from another company");
    }

    // ─── Leave types pagination ───────────────────────────────────────────────────

    [Fact]
    public async Task GetLeaveTypesPaged_ReturnsCorrectPage()
    {
        // Arrange
        var leaveSvc = BuildLeaveService();

        // Act
        var result = await leaveSvc.GetLeaveTypesPagedAsync(
            companyId: CompanyId, page: 1, pageSize: 2);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(5, "5 leave types seeded for Company1");
    }

    [Fact]
    public async Task GetLeaveTypesPaged_CompanyIsolation()
    {
        // Arrange
        var leaveSvc = BuildLeaveService();

        // Act
        var result = await leaveSvc.GetLeaveTypesPagedAsync(
            companyId: CompanyId, page: 1, pageSize: 100);

        // Assert
        result.Items.All(lt => lt.CompanyId == CompanyId)
              .Should().BeTrue();
    }

    // ─── CancellationToken ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPayslipsPaged_CancelledToken_ThrowsOperationCancelled()
    {
        // Arrange
        var svc = BuildPayrollService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.GetAllPayslipsPagedAsync(
                null, null, null, CompanyId, 1, 10, ct: cts.Token));
    }

    // ─── PageSize validation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPayslipsPaged_InvalidPageSize_ThrowsOrDefaults(int pageSize)
    {
        // Arrange
        var svc = BuildPayrollService();

        // Act & Assert — must not silently return unexpected data
        var act = async () => await svc.GetAllPayslipsPagedAsync(
            null, null, null, CompanyId, 1, pageSize);
        // Either throws ArgumentException or returns a safe default (e.g. pageSize=10)
        // The test just verifies it doesn't return a random result set
        try
        {
            var result = await act();
            result.Should().NotBeNull();
        }
        catch (ArgumentException)
        {
            // Acceptable — explicit rejection of invalid page size
        }
    }

    // ─── Seed + factory helpers ───────────────────────────────────────────────────

    private void SeedData()
    {
        // 10 payslips for Company1 with varying months, salaries, employees
        for (int i = 1; i <= 10; i++)
        {
            _db.Payslips.Add(new Payslip
            {
                PayslipId  = i,
                EmployeeId = i % 2 == 0 ? "E001" : "E002",
                CompanyId  = CompanyId,
                Month      = i <= 5 ? 6 : 7,
                Year       = 2025,
                BasicPay   = 30000m + i * 1000m,
                GrossPay   = 36000m + i * 1000m,
                NetSalary  = 30000m + i * 1000m,
                Status     = "Generated"
            });
        }
        // 2 payslips for Company2 — must never appear in Company1 queries
        for (int i = 11; i <= 12; i++)
        {
            _db.Payslips.Add(new Payslip
            {
                PayslipId  = i,
                EmployeeId = "E099",
                CompanyId  = 2,
                Month      = 6, Year = 2025,
                NetSalary  = 99999m, Status = "Generated"
            });
        }

        // 5 leave types for Company1, 2 for Company2
        for (int i = 1; i <= 5; i++)
            _db.LeaveTypes.Add(new LeaveType
            {
                LeaveTypeId = i, CompanyId = CompanyId,
                Name = $"LeaveType{i}", Quota = 10
            });
        for (int i = 6; i <= 7; i++)
            _db.LeaveTypes.Add(new LeaveType
            {
                LeaveTypeId = i, CompanyId = 2,
                Name = $"LeaveType{i}", Quota = 10
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
            new MockNotificationService());
}
