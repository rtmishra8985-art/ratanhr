using FluentAssertions;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Boundary and edge-case tests for numeric and date values.
/// FIXED: Uses business-cap constants instead of magic numbers.
/// Uses decimal.MaxValue where the true upper bound is intended.
/// </summary>
public class BoundaryTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IPayrollService _svc;
    private const int CompanyId = 1;

    // Business-defined caps — these are the authoritative bounds, not magic numbers.
    private const decimal MaxSupportedSalary     = 10_000_000m;  // ₹1 crore cap
    private const decimal MinSalary              = 0m;
    private const int     MaxPaginationPageSize  = 500;
    private const int     MinPaginationPageSize  = 1;

    public BoundaryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _svc = new PayrollService(_db, new MockAuditService(), new MockNotificationService(),
                                  new MockPayrollCalculator(), new MockLogger<PayrollService>());
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    // ─── Salary boundaries ────────────────────────────────────────────────────────

    [Fact]
    public async Task GeneratePayslip_MaxSupportedSalary_Succeeds()
    {
        // Arrange — test the business-defined upper cap (₹1 crore), not decimal.MaxValue
        // Using decimal.MaxValue would cause EF Core / Postgres overflow
        var dto = BuildDto("E001", basicPay: MaxSupportedSalary, daysPresent: 26, workingDays: 26);

        // Act
        var act = async () => await _svc.GeneratePayslipAsync(dto);

        // Assert — should not throw for a valid high-but-supportable salary
        await act.Should().NotThrowAsync("₹1 crore salary must be processable");
    }

    [Fact]
    public async Task GeneratePayslip_ZeroBasicPay_ReturnsZeroNetSalary()
    {
        // Arrange
        var dto = BuildDto("E001", basicPay: 0m, daysPresent: 26, workingDays: 26);

        // Act
        var id = await _svc.GeneratePayslipAsync(dto);
        var payslip = await _svc.GetPayslipAsync(id);

        // Assert
        payslip!.NetSalary.Should().Be(0m);
    }

    [Fact]
    public async Task GeneratePayslip_NegativeBasicPay_ThrowsOrReturnsFail()
    {
        // Arrange
        var dto = BuildDto("E001", basicPay: -1000m, daysPresent: 26, workingDays: 26);

        // Act & Assert — negative basic pay must never be accepted
        var act = async () => await _svc.GeneratePayslipAsync(dto);
        await act.Should().ThrowAsync<Exception>("negative basic pay must be rejected");
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(15000)]
    [InlineData(50000)]
    [InlineData(100000)]
    [InlineData(500000)]
    public async Task GeneratePayslip_VariousSalaryLevels_NetSalaryIsNonNegative(decimal basicPay)
    {
        // Act
        var dto = BuildDto("E001", basicPay, 26, 26);
        var id = await _svc.GeneratePayslipAsync(dto);
        var payslip = await _svc.GetPayslipAsync(id);

        // Assert
        payslip!.NetSalary.Should().BeGreaterOrEqualTo(0,
            $"Net salary must never be negative at any pay level (tested: {basicPay})");
    }

    // ─── Days present boundaries ──────────────────────────────────────────────────

    [Fact]
    public async Task GeneratePayslip_DaysPresentZero_NetPayIsZero()
    {
        // Arrange
        var dto = BuildDto("E001", 50000m, daysPresent: 0, workingDays: 26);

        // Act
        var id = await _svc.GeneratePayslipAsync(dto);
        var payslip = await _svc.GetPayslipAsync(id);

        // Assert
        payslip!.NetSalary.Should().Be(0m, "0 days present means no pay");
    }

    [Fact]
    public async Task GeneratePayslip_DaysPresentEqualsWorkingDays_FullPay()
    {
        // Arrange
        var dto = BuildDto("E001", 50000m, daysPresent: 26, workingDays: 26);

        // Act
        var id = await _svc.GeneratePayslipAsync(dto);
        var payslip = await _svc.GetPayslipAsync(id);

        // Assert — full attendance must produce full (un-pro-rated) basic pay
        payslip!.BasicPay.Should().Be(50000m);
    }

    [Fact]
    public async Task GeneratePayslip_DaysPresentExceedsWorkingDays_ThrowsOrCaps()
    {
        // Arrange
        var dto = BuildDto("E001", 50000m, daysPresent: 30, workingDays: 26);

        // Act & Assert — days present > working days is invalid
        var act = async () => await _svc.GeneratePayslipAsync(dto);
        await act.Should().ThrowAsync<Exception>("days present cannot exceed working days");
    }

    // ─── Pagination boundaries ────────────────────────────────────────────────────

    [Fact]
    public async Task GetPayslipsPaged_Page1_MinPageSize_Returns1Record()
    {
        // Arrange
        await _svc.GeneratePayslipAsync(BuildDto("E001", 50000m, 26, 26, month: 6));
        await _svc.GeneratePayslipAsync(BuildDto("E002", 40000m, 26, 26, month: 6));

        // Act
        var result = await _svc.GetAllPayslipsPagedAsync(
            6, 2025, null, CompanyId,
            page: 1, pageSize: MinPaginationPageSize);

        // Assert
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPayslipsPaged_MaxPageSize_DoesNotThrow()
    {
        // Act & Assert — large page size must not crash
        var act = async () => await _svc.GetAllPayslipsPagedAsync(
            null, null, null, CompanyId,
            page: 1, pageSize: MaxPaginationPageSize);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetPayslipsPaged_PageZero_ThrowsOrDefaultsToPage1()
    {
        // Act
        try
        {
            var result = await _svc.GetAllPayslipsPagedAsync(
                null, null, null, CompanyId, page: 0, pageSize: 10);
            // If it doesn't throw, it must behave as page 1
            result.Page.Should().BeGreaterOrEqualTo(1);
        }
        catch (ArgumentException)
        {
            // Explicit rejection is also acceptable
        }
    }

    // ─── Bonus boundaries ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Bonus_ZeroAmount_ThrowsOrReturnsFail()
    {
        // Arrange
        var dto = new CreateBonusDto { EmployeeId = "E001", CompanyId = CompanyId, Amount = 0m };

        // Act & Assert — zero amount bonus must be rejected (no-op transaction)
        var svc = new BonusDeductionService(_db);
        var act = async () => await svc.AddBonusAsync(dto);
        await act.Should().ThrowAsync<Exception>("zero-amount bonus is invalid");
    }

    [Fact]
    public async Task Bonus_NegativeAmount_ThrowsOrReturnsFail()
    {
        // Arrange
        var dto = new CreateBonusDto { EmployeeId = "E001", CompanyId = CompanyId, Amount = -100m };

        // Act & Assert
        var svc = new BonusDeductionService(_db);
        var act = async () => await svc.AddBonusAsync(dto);
        await act.Should().ThrowAsync<Exception>("negative bonus amount is invalid");
    }

    // ─── Date boundaries ──────────────────────────────────────────────────────────

    [Fact]
    public void DateOnlyParser_InvalidDateString_ReturnsNull()
    {
        // Act
        var (result, parsed) = DateOnlyParser.TryParse("not-a-date");

        // Assert
        result.Should().BeFalse();
        parsed.Should().Be(default(DateOnly));
    }

    [Fact]
    public void DateOnlyParser_ValidIsoDate_ParsesCorrectly()
    {
        // Act
        var (result, parsed) = DateOnlyParser.TryParse("2025-06-15");

        // Assert
        result.Should().BeTrue();
        parsed.Should().Be(new DateOnly(2025, 6, 15));
    }

    [Fact]
    public void DateOnlyParser_LeapDay_ParsesCorrectly()
    {
        // Act
        var (result, parsed) = DateOnlyParser.TryParse("2024-02-29");

        // Assert
        result.Should().BeTrue();
        parsed.Should().Be(new DateOnly(2024, 2, 29));
    }

    [Fact]
    public void DateOnlyParser_InvalidLeapDay_ReturnsFalse()
    {
        // Act — 2025 is not a leap year
        var (result, parsed) = DateOnlyParser.TryParse("2025-02-29");

        // Assert
        result.Should().BeFalse();
    }

    // ─── PagedResult ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,  10, 0)]
    [InlineData(5,  10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(100, 10, 10)]
    public void PagedResult_TotalPages_CalculatedCorrectly(
        int totalCount, int pageSize, int expectedPages)
    {
        // Act
        var paged = new PagedResult<string>
        {
            Items      = new List<string>(),
            TotalCount = totalCount,
            Page       = 1,
            PageSize   = pageSize
        };

        // Assert
        paged.TotalPages.Should().Be(expectedPages);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private GeneratePayslipDto BuildDto(
        string employeeId, decimal basicPay,
        int daysPresent, int workingDays,
        int month = 6, int year = 2025)
        => new()
        {
            EmployeeId = employeeId,
            BasicPay = basicPay, DaysPresent = daysPresent,
            WorkingDays = workingDays, Month = month, Year = year
        };

    private void SeedData()
    {
        _db.Employees.AddRange(
            new Employee { EmployeeId = 1, CompanyId = CompanyId, EmployeeCode = "E001", FirstName = "Alice", LastName = "A", Status = "Active" },
            new Employee { EmployeeId = 2, CompanyId = CompanyId, EmployeeCode = "E002", FirstName = "Bob",   LastName = "B", Status = "Active" }
        );
        _db.SaveChanges();
    }
}
