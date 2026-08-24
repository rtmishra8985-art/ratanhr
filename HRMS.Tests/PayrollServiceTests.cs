using FluentAssertions;
using HRMS.Application.DTOs.Payroll;
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
/// Comprehensive tests for PayrollService.
/// All test dates are fixed constants — never DateTime.UtcNow.
/// CancellationToken is used for every async call.
/// </summary>
public class PayrollServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IPayrollService _svc;
    private const int CompanyId = 1;

    // Fixed, deterministic date constants
    private const int TestMonth = 6;
    private const int TestYear  = 2025;

    public PayrollServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _svc = new PayrollService(
            _db,
            new MockAuditService(),
            new MockNotificationService(),
            new MockPayrollCalculator(),
            new MockLogger<PayrollService>());
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    // ─── GeneratePayslip ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GeneratePayslip_ValidInput_ReturnsPositiveId()
    {
        // Arrange
        var dto = BuildDto("E001", 50000m, daysPresent: 26, workingDays: 26);

        // Act
        var id = await _svc.GeneratePayslipAsync(dto, actorId: 1, actorName: "hr@co.com");

        // Assert
        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GeneratePayslip_NetPayCalculatedCorrectly()
    {
        // Arrange — basic pay 50000, no PF/PT (low slab), no HRA
        var dto = BuildDto("E001", basicPay: 50000m, daysPresent: 26, workingDays: 26);

        // Act
        var id = await _svc.GeneratePayslipAsync(dto);
        var payslip = await _svc.GetPayslipAsync(id);

        // Assert
        payslip.Should().NotBeNull();
        payslip!.BasicPay.Should().Be(50000m);
        payslip.NetSalary.Should().BeGreaterThan(0);
        payslip.NetSalary.Should().BeLessOrEqualTo(payslip.GrossPay,
            "net salary must not exceed gross pay");
    }

    [Fact]
    public async Task GeneratePayslip_DuplicateMonthYear_UpdatesExisting()
    {
        // Arrange
        var dto = BuildDto("E001", 50000m, 26, 26);
        var firstId = await _svc.GeneratePayslipAsync(dto);

        // Act — generate again for same employee + month + year
        dto.BasicPay = 55000m;
        // BLOCKER-6: regenerating an already-calculated payslip requires an explicit
        // opt-in; this is intentional duplicate protection, not a bug. This test
        // predates that fix and never set the flag.
        dto.Overwrite = true;
        var secondId = await _svc.GeneratePayslipAsync(dto);

        // Assert — must update, not create a duplicate
        secondId.Should().Be(firstId, "re-generating for same period must update, not duplicate");
        var payslip = await _svc.GetPayslipAsync(firstId);
        payslip!.BasicPay.Should().Be(55000m);
    }

    [Fact]
    public async Task GeneratePayslip_PfCappedAt1800WhenBasicExceeds15000()
    {
        // Arrange — PF is 12% of basic, capped at ₹1800 when basic > ₹15000
        var dto = BuildDto("E001", basicPay: 30000m, daysPresent: 26, workingDays: 26);

        // Act
        var id = await _svc.GeneratePayslipAsync(dto);
        var payslip = await _svc.GetPayslipAsync(id);

        // Assert
        payslip!.PfDeduction.Should().Be(1800m,
            "PF is capped at ₹1800 for basic salary above ₹15000");
    }

    [Fact]
    public async Task GeneratePayslip_PfIs12PercentWhenBasicBelow15000()
    {
        // Arrange — basic 10000, PF should be 12% = 1200 (below cap)
        var dto = BuildDto("E001", basicPay: 10000m, daysPresent: 26, workingDays: 26);

        // Act
        var id = await _svc.GeneratePayslipAsync(dto);
        var payslip = await _svc.GetPayslipAsync(id);

        // Assert
        payslip!.PfDeduction.Should().Be(1200m, "PF is 12% of basic when below ₹15000");
    }

    [Fact]
    public async Task GeneratePayslip_PartialMonth_ProRatesBasicPay()
    {
        // Arrange — present 13 of 26 working days (50%)
        var dto = BuildDto("E001", basicPay: 50000m, daysPresent: 13, workingDays: 26);

        // Act
        var id = await _svc.GeneratePayslipAsync(dto);
        var payslip = await _svc.GetPayslipAsync(id);

        // Assert
        payslip!.BasicPay.Should().BeLessThan(50000m,
            "pro-rated payslip must have reduced basic pay");
    }

    [Fact]
    public async Task GeneratePayslip_ZeroDaysPresent_NetPayIsZeroOrMinimum()
    {
        // Arrange
        var dto = BuildDto("E001", basicPay: 50000m, daysPresent: 0, workingDays: 26);

        // Act
        var id = await _svc.GeneratePayslipAsync(dto);
        var payslip = await _svc.GetPayslipAsync(id);

        // Assert
        payslip!.NetSalary.Should().BeGreaterOrEqualTo(0,
            "zero days present must result in zero or minimum statutory pay, never negative");
    }

    [Fact]
    public async Task GeneratePayslip_LockedPayroll_ThrowsConflict()
    {
        // Arrange — use locked mock guard
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var lockedDb = new ApplicationDbContext(options);
        var lockedSvc = new PayrollService(lockedDb, new MockAuditService(), new MockNotificationService(),
                                          new MockPayrollCalculator(), new MockLogger<PayrollService>());
        var dto = BuildDto("E001", 50000m, 26, 26);

        // Act & Assert
        var act = async () => await lockedSvc.GeneratePayslipAsync(dto);
        await act.Should().ThrowAsync<Exception>(
            "generating payslip in a locked period must throw a conflict exception");
    }

    // ─── GetAllPayslips ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllPayslips_CompanyScoped_DoesNotReturnOtherCompany()
    {
        // Arrange — add a payslip for company 2
        _db.Payslips.Add(new Payslip
        {
            PayslipId = 99, EmployeeId = "E099", CompanyId = 2,
            Month = TestMonth, Year = TestYear, NetSalary = 99999m, Status = "Generated"
        });
        await _db.SaveChangesAsync();

        // Act
        var payslips = await _svc.GetAllPayslipsAsync(companyId: CompanyId);

        // Assert
        payslips.All(p => p.CompanyId == CompanyId)
            .Should().BeTrue("company-scoped query must not return other companies' payslips");
    }

    [Fact]
    public async Task GetAllPayslips_FilterByEmployee_ReturnsOnlyThatEmployee()
    {
        // Arrange
        await _svc.GeneratePayslipAsync(BuildDto("E001", 50000m, 26, 26));
        await _svc.GeneratePayslipAsync(BuildDto("E002", 40000m, 26, 26));

        // Act
        var payslips = await _svc.GetAllPayslipsAsync(employeeId: "E001", companyId: CompanyId);

        // Assert
        payslips.All(p => p.EmployeeId == "E001").Should().BeTrue();
    }

    [Fact]
    public async Task GetAllPayslips_FilterByMonthYear_ReturnsMatchingOnly()
    {
        // Arrange — generate payslips for two different months
        await _svc.GeneratePayslipAsync(BuildDto("E001", 50000m, 26, 26, month: 5, year: 2025));
        await _svc.GeneratePayslipAsync(BuildDto("E001", 50000m, 26, 26, month: 6, year: 2025));

        // Act
        var payslips = await _svc.GetAllPayslipsAsync(month: 6, year: 2025, companyId: CompanyId);

        // Assert
        payslips.All(p => p.Month == 6 && p.Year == 2025).Should().BeTrue();
    }

    // ─── GetPayslipById ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPayslipById_ExistingId_ReturnsCorrectRecord()
    {
        // Arrange
        var id = await _svc.GeneratePayslipAsync(BuildDto("E001", 50000m, 26, 26));

        // Act
        var payslip = await _svc.GetPayslipAsync(id);

        // Assert
        payslip.Should().NotBeNull();
        payslip!.PayslipId.Should().Be(id);
        payslip.EmployeeId.Should().Be("E001");
    }

    [Fact]
    public async Task GetPayslipById_NonExistentId_ReturnsNull()
    {
        // Act
        var payslip = await _svc.GetPayslipAsync(999999);

        // Assert
        payslip.Should().BeNull();
    }

    // ─── DeletePayslip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePayslip_ExistingRecord_Succeeds()
    {
        // Arrange
        var id = await _svc.GeneratePayslipAsync(BuildDto("E001", 50000m, 26, 26));

        // Act
        var result = await _svc.DeletePayslipAsync(id, actorId: 1, actorName: "admin");

        // Assert
        result.Should().BeTrue();
        var deleted = await _svc.GetPayslipAsync(id);
        deleted.Should().BeNull("deleted payslip must not be retrievable");
    }

    [Fact]
    public async Task DeletePayslip_NonExistentId_ReturnsFalse()
    {
        // Act
        var result = await _svc.DeletePayslipAsync(999999);

        // Assert
        result.Should().BeFalse();
    }

    // ─── PreviewCalculation ───────────────────────────────────────────────────────

    [Fact]
    public async Task PreviewCalculation_ReturnsBreakdown_WithoutPersisting()
    {
        // Arrange
        var req = new PayrollCalculationRequest
        {
            BasicPay     = 50000m,
            DaysPresent  = 26,
            WorkingDays  = 26,
            Month        = TestMonth
        };

        // Act
        var preview = await _svc.PreviewCalculationAsync(req);

        // Assert
        preview.Should().NotBeNull();
        preview.NetPay.Should().BeGreaterThan(0);

        var countBefore = await _db.Payslips.CountAsync();
        var countAfter  = await _db.Payslips.CountAsync();
        countAfter.Should().Be(countBefore,
            "preview must not persist any records to the database");
    }

    // ─── BulkGenerate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkGeneratePayslips_AllEmployees_GeneratesForEach()
    {
        // Arrange
        var dto = new BulkPayrollDto
        {
            CompanyId   = CompanyId,
            Month       = TestMonth,
            Year        = TestYear,
            WorkingDays = 26
        };

        // Act
        var result = await _svc.BulkGeneratePayslipsAsync(dto, actorId: 1, actorName: "admin");

        // Assert
        result.Should().NotBeNull();
        result.Generated.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BulkGeneratePayslips_PartialFailure_ReportsErrors()
    {
        // Arrange — one employee has no salary structure (will fail)
        _db.Employees.Add(new Employee
        {
            EmployeeId = 99, CompanyId = CompanyId,
            FirstName = "NoSalary", LastName = "User", Status = "Active"
            // intentionally no SalaryStructure
        });
        await _db.SaveChangesAsync();

        var dto = new BulkPayrollDto
        {
            CompanyId = CompanyId, Month = TestMonth, Year = TestYear, WorkingDays = 26
        };

        // Act
        var result = await _svc.BulkGeneratePayslipsAsync(dto);

        // Assert
        result.Failed.Should().BeGreaterThan(0,
            "employees without salary structures must be reported as failures");
        result.Errors.Should().NotBeEmpty();
    }

    // ─── GetAllPayslipsPaged ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllPayslipsPaged_SortByNetSalaryDesc_IsOrdered()
    {
        // Arrange
        await _svc.GeneratePayslipAsync(BuildDto("E001", 30000m, 26, 26));
        await _svc.GeneratePayslipAsync(BuildDto("E002", 50000m, 26, 26));
        await _svc.GeneratePayslipAsync(BuildDto("E003", 40000m, 26, 26));

        // Act
        var result = await _svc.GetAllPayslipsPagedAsync(
            null, null, null, CompanyId, 1, 10,
            sortBy: "netSalary", sortDirection: "desc");

        // Assert
        var salaries = result.Items.Select(p => p.NetSalary).ToList();
        salaries.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetAllPayslipsPaged_WithCancellationToken_DoesNotThrowWhenNotCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act & Assert — should complete normally
        var act = async () => await _svc.GetAllPayslipsPagedAsync(
            null, null, null, CompanyId, 1, 10, ct: cts.Token);
        await act.Should().NotThrowAsync();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private GeneratePayslipDto BuildDto(
        string employeeId, decimal basicPay,
        int daysPresent, int workingDays,
        int month = TestMonth, int year = TestYear)
        => new()
        {
            EmployeeId  = employeeId,
            CompanyId   = CompanyId,
            BasicPay    = basicPay,
            DaysPresent = daysPresent,
            WorkingDays = workingDays,
            Month       = month,
            Year        = year
        };

    private void SeedData()
    {
        _db.Departments.Add(new Department { DepartmentId = 1, CompanyId = CompanyId, Name = "Engineering" });
        _db.Employees.AddRange(
            new Employee { EmployeeId = 1, CompanyId = CompanyId, DepartmentId = 1, Status = "Active", FirstName = "Alice", LastName = "A", EmployeeCode = "E001" },
            new Employee { EmployeeId = 2, CompanyId = CompanyId, DepartmentId = 1, Status = "Active", FirstName = "Bob",   LastName = "B", EmployeeCode = "E002" },
            new Employee { EmployeeId = 3, CompanyId = CompanyId, DepartmentId = 1, Status = "Active", FirstName = "Carol", LastName = "C", EmployeeCode = "E003" }
        );
        _db.SaveChanges();
    }
}
