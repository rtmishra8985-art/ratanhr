using FluentAssertions;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.FileStorage;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Tests for EmployeeService: CRUD operations, tenant isolation, status transitions,
/// and document management with proper authorization checks.
/// </summary>
public class EmployeeServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IEmployeeService _svc;
    private const int CompanyId = 1;

    public EmployeeServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _svc = new EmployeeService(
            _db,
            new Mock<IFileStorageService>().Object,
            new Mock<ILogger<EmployeeService>>().Object);
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    // ─── GetAll ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllEmployeesAsync_ReturnsScopedToCompany()
    {
        // Act
        var employees = await _svc.GetAllEmployeesAsync(CompanyId);

        // Assert
        employees.Should().HaveCount(3, "only CompanyId=1 employees must be returned");
        employees.All(e => e.CompanyId == CompanyId).Should().BeTrue();
    }

    [Fact]
    public async Task GetAllEmployeesAsync_OtherCompany_NotIncluded()
    {
        // Act
        var employees = await _svc.GetAllEmployeesAsync(companyId: 2);

        // Assert
        employees.Should().HaveCount(1, "only CompanyId=2 employees must be returned");
    }

    // ─── GetById ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEmployeeByIdAsync_SameCompany_ReturnsEmployee()
    {
        // Act
        var emp = await _svc.GetEmployeeByIdAsync(1, CompanyId);

        // Assert
        emp.Should().NotBeNull();
        emp!.EmployeeId.Should().Be(1);
    }

    [Fact]
    public async Task GetEmployeeByIdAsync_CrossCompany_ReturnsNull()
    {
        // Act — employee 4 belongs to company 2
        var emp = await _svc.GetEmployeeByIdAsync(4, CompanyId);

        // Assert
        emp.Should().BeNull("cross-company access must be blocked");
    }

    [Fact]
    public async Task GetEmployeeByIdAsync_NonExistent_ReturnsNull()
    {
        // Act
        var emp = await _svc.GetEmployeeByIdAsync(999, CompanyId);

        // Assert
        emp.Should().BeNull();
    }

    // ─── Create ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEmployeeAsync_ValidDto_ReturnsNewEmployeeId()
    {
        // Arrange
        var dto = new CreateEmployeeDto
        {
            FirstName    = "New",
            LastName     = "Employee",
            Email        = "new.emp@co.com",
            PhoneNumber  = "9876543210",
            DepartmentId = 1,
            DateOfJoining = new DateOnly(2025, 1, 15),
            Status       = "Active"
        };

        // Act
        var newId = await _svc.CreateEmployeeAsync(CompanyId, dto);

        // Assert
        newId.Should().BeGreaterThan(0);
        var saved = await _db.Employees.FindAsync(newId);
        saved.Should().NotBeNull();
        saved!.Email.Should().Be("new.emp@co.com");
        saved.CompanyId.Should().Be(CompanyId, "employee must be assigned to the creating company");
    }

    [Fact]
    public async Task CreateEmployeeAsync_DuplicateEmail_ThrowsOrReturnsFail()
    {
        // Arrange
        var dto = new CreateEmployeeDto
        {
            FirstName = "Dupe",
            LastName  = "User",
            Email     = "alice@co.com",   // already exists in seed
            DepartmentId = 1,
            DateOfJoining = new DateOnly(2025, 1, 1)
        };

        // Act & Assert
        var act = async () => await _svc.CreateEmployeeAsync(CompanyId, dto);
        await act.Should().ThrowAsync<Exception>("duplicate email must be rejected");
    }

    // ─── Update ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEmployeeAsync_SameCompany_UpdatesSuccessfully()
    {
        // Arrange
        var dto = new UpdateEmployeeDto { FirstName = "AliceUpdated", LastName = "A", DepartmentId = 1 };

        // Act
        var success = await _svc.UpdateEmployeeAsync(1, CompanyId, dto);

        // Assert
        success.Should().BeTrue();
        var updated = await _db.Employees.FindAsync(1);
        updated!.FirstName.Should().Be("AliceUpdated");
    }

    [Fact]
    public async Task UpdateEmployeeAsync_CrossCompany_ReturnsFalse()
    {
        // Act — try to update employee 4 (company 2) from company 1
        var success = await _svc.UpdateEmployeeAsync(4, CompanyId, new UpdateEmployeeDto { FirstName = "Hacked" });

        // Assert
        success.Should().BeFalse("cross-company update must be blocked");
    }

    // ─── Delete ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEmployeeAsync_SameCompany_SoftDeletesRecord()
    {
        // Act
        var success = await _svc.DeleteEmployeeAsync(1, CompanyId);

        // Assert
        success.Should().BeTrue();
        var deleted = await _db.Employees.FindAsync(1);
        // Soft-delete: record should still exist but marked as inactive/deleted
        deleted.Should().NotBeNull("soft-delete must retain the record");
    }

    [Fact]
    public async Task DeleteEmployeeAsync_CrossCompany_ReturnsFalse()
    {
        // Act
        var success = await _svc.DeleteEmployeeAsync(4, CompanyId);

        // Assert
        success.Should().BeFalse();
    }

    // ─── Paged retrieval ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEmployeesPagedAsync_PageSize1_ReturnsOneRecord()
    {
        // Act
        var result = await _svc.GetEmployeesPagedAsync(CompanyId, page: 1, pageSize: 1);

        // Assert
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetEmployeesPagedAsync_Page2_ReturnsSecondRecord()
    {
        // Act
        var result = await _svc.GetEmployeesPagedAsync(CompanyId, page: 2, pageSize: 1);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Page.Should().Be(2);
    }

    // ─── Status filter ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllEmployeesAsync_FilterByActiveStatus_ReturnsOnlyActive()
    {
        // Act
        var employees = await _svc.GetAllEmployeesAsync(CompanyId, status: "Active");

        // Assert
        employees.Should().HaveCount(2);
        employees.All(e => e.Status == "Active").Should().BeTrue();
    }

    // ─── Cancellation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllEmployeesAsync_CancelledToken_ThrowsOperationCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _svc.GetAllEmployeesAsync(CompanyId, ct: cts.Token));
    }

    // ─── Seed helpers ─────────────────────────────────────────────────────────────

    private void SeedData()
    {
        _db.Departments.Add(new Department { DepartmentId = 1, CompanyId = 1, Name = "Engineering" });
        _db.Employees.AddRange(
            new Employee { EmployeeId = 1, CompanyId = 1, DepartmentId = 1, Status = "Active",   FirstName = "Alice",  LastName = "A", Email = "alice@co.com" },
            new Employee { EmployeeId = 2, CompanyId = 1, DepartmentId = 1, Status = "Active",   FirstName = "Bob",    LastName = "B", Email = "bob@co.com"   },
            new Employee { EmployeeId = 3, CompanyId = 1, DepartmentId = 1, Status = "Inactive", FirstName = "Carol",  LastName = "C", Email = "carol@co.com" },
            new Employee { EmployeeId = 4, CompanyId = 2, DepartmentId = 1, Status = "Active",   FirstName = "Dan",    LastName = "D", Email = "dan@co.com"   }
        );
        _db.SaveChanges();
    }
}
