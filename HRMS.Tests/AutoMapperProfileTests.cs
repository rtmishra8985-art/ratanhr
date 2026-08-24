using AutoMapper;
using FluentAssertions;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.DTOs.Department;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.DTOs.Leave;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Mapping;
using HRMS.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Validates every AutoMapper profile mapping in HrmsAutoMapperProfile.
/// A failing test here means a mapping regression was introduced — catch it before prod.
/// </summary>
public class AutoMapperProfileTests
{
    private readonly IMapper _mapper;

    public AutoMapperProfileTests()
    {
        var config = new MapperConfiguration(cfg =>
            cfg.AddProfile<HrmsAutoMapperProfile>(), NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid(); // fails fast on missing member mappings
        _mapper = config.CreateMapper();
    }

    // ─── Configuration ───────────────────────────────────────────────────────────

    [Fact]
    public void AutoMapper_Configuration_IsValid()
    {
        // Assert — MapperConfiguration.AssertConfigurationIsValid() already called
        // in ctor; if this test runs without exception, configuration is valid.
        _mapper.Should().NotBeNull();
    }

    // ─── Employee mappings ────────────────────────────────────────────────────────

    [Fact]
    public void Employee_To_EmployeeListDto_MapsCorrectly()
    {
        // Arrange
        var entity = new Employee
        {
            EmployeeId = 42,
            CompanyId  = 1,
            FirstName  = "Alice",
            LastName   = "Smith",
            Email      = "alice@co.com",
            Status     = "Active",
            DepartmentId = 10
        };

        // Act
        var dto = _mapper.Map<EmployeeListDto>(entity);

        // Assert
        dto.EmployeeId.Should().Be(42);
        dto.CompanyId.Should().Be(1);
        dto.FullName.Should().Contain("Alice");
        dto.Status.Should().Be("Active");
    }

    [Fact]
    public void Employee_To_EmployeeDetailDto_MapsCorrectly()
    {
        // Arrange
        var entity = new Employee
        {
            EmployeeId   = 55,
            CompanyId    = 2,
            FirstName    = "Bob",
            LastName     = "Jones",
            Email        = "bob@co.com",
            PhoneNumber  = "9876543210",
            DateOfBirth  = new DateOnly(1990, 3, 15),
            DateOfJoining = new DateOnly(2020, 1, 1),
            Status       = "Active"
        };

        // Act
        var dto = _mapper.Map<EmployeeDetailDto>(entity);

        // Assert
        dto.EmployeeId.Should().Be(55.ToString());
        dto.Email.Should().Be("bob@co.com");
        dto.DateOfBirth.Should().Be(new DateOnly(1990, 3, 15));
    }

    // ─── Department mappings ─────────────────────────────────────────────────────

    [Fact]
    public void Department_To_DepartmentDto_MapsCorrectly()
    {
        // Arrange
        var entity = new Department
        {
            DepartmentId = 7,
            CompanyId    = 1,
            Name         = "Engineering",
            Description  = "Software Engineering dept"
        };

        // Act
        var dto = _mapper.Map<DepartmentDto>(entity);

        // Assert
        dto.DepartmentId.Should().Be(7);
        dto.Name.Should().Be("Engineering");
    }

    // ─── Leave mappings ───────────────────────────────────────────────────────────

    [Fact]
    public void LeaveRequest_To_LeaveRequestDto_MapsCorrectly()
    {
        // Arrange
        var entity = new LeaveRequest
        {
            LeaveRequestId = 100,
            EmployeeId     = "E001",
            CompanyId      = 1,
            LeaveTypeId    = 3,
            StartDate      = new DateOnly(2025, 7, 1),
            EndDate        = new DateOnly(2025, 7, 5),
            Status         = "Pending",
            Reason         = "Annual leave"
        };

        // Act
        var dto = _mapper.Map<LeaveRequestDto>(entity);

        // Assert
        dto.LeaveRequestId.Should().Be(100);
        dto.Status.Should().Be("Pending");
        // StartDate/EndDate are mapped as "yyyy-MM-dd" strings by HrmsAutoMapperProfile
        dto.StartDate.Should().Be("2025-07-01");
        dto.EndDate.Should().Be("2025-07-05");
    }

    [Fact]
    public void LeaveType_To_LeaveTypeDto_MapsCorrectly()
    {
        // Arrange
        var entity = new LeaveType
        {
            LeaveTypeId = 5,
            CompanyId   = 1,
            Name        = "Sick Leave",
            Quota       = 12,
            IsPaid      = true
        };

        // Act
        var dto = _mapper.Map<LeaveTypeDto>(entity);

        // Assert
        dto.LeaveTypeId.Should().Be(5);
        dto.Name.Should().Be("Sick Leave");
        dto.Quota.Should().Be(12);
        dto.IsPaid.Should().BeTrue();
    }

    // ─── Payroll / Payslip mappings ───────────────────────────────────────────────

    [Fact]
    public void Payslip_To_PayslipDto_MapsCorrectly()
    {
        // Arrange
        var entity = new Payslip
        {
            PayslipId  = 200,
            EmployeeId = "E002",
            CompanyId  = 1,
            Month      = 6,
            Year       = 2025,
            BasicPay   = 50000m,
            GrossPay   = 60000m,
            NetSalary  = 55000m,
            Status     = "Generated"
        };

        // Act
        var dto = _mapper.Map<PayslipDto>(entity);

        // Assert
        dto.PayslipId.Should().Be(200);
        dto.BasicPay.Should().Be(50000m);
        dto.NetSalary.Should().Be(55000m);
        dto.MonthYear.Should().Contain("2025", "month-year must be formatted in the DTO");
    }

    // ─── Attendance mappings ──────────────────────────────────────────────────────

    [Fact]
    public void WebAttendance_To_AttendanceDto_MapsCorrectly()
    {
        // Arrange
        var entity = new WebAttendance
        {
            AttendanceId = 300,
            EmployeeId   = "E003",
            CompanyId    = 1,
            Date         = new DateOnly(2025, 6, 15),
            CheckIn      = new TimeOnly(9, 0),
            CheckOut     = new TimeOnly(18, 0),
            Status       = "Present"
        };

        // Act
        var dto = _mapper.Map<AttendanceDto>(entity);

        // Assert
        dto.AttendanceId.Should().Be(300);
        dto.EmployeeId.Should().Be("E003");
        dto.Status.Should().Be("Present");
    }

    // ─── Null safety ──────────────────────────────────────────────────────────────

    [Fact]
    public void Mapper_NullSource_ReturnsNull()
    {
        // Arrange
        Employee? nullEmployee = null;

        // Act
        var dto = _mapper.Map<EmployeeListDto?>(nullEmployee);

        // Assert
        dto.Should().BeNull("mapping null source must return null, not throw");
    }

    [Fact]
    public void Mapper_Collection_MapsAllItems()
    {
        // Arrange
        var entities = Enumerable.Range(1, 5).Select(i => new Employee
        {
            EmployeeId = i,
            CompanyId  = 1,
            FirstName  = $"User{i}",
            LastName   = "Test",
            Status     = "Active"
        }).ToList();

        // Act
        var dtos = _mapper.Map<List<EmployeeListDto>>(entities);

        // Assert
        dtos.Should().HaveCount(5);
        dtos.Select(d => d.EmployeeId).Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    // ─── Timesheet mapping ────────────────────────────────────────────────────────

    [Fact]
    public void Timesheet_To_TimesheetDto_MapsCorrectly()
    {
        // Arrange
        var entity = new Timesheet
        {
            TimesheetId = 400,
            EmployeeId  = "E004",
            CompanyId   = 1,
            WeekStartDate = new DateOnly(2025, 6, 9),
            WeekEndDate   = new DateOnly(2025, 6, 15),
            TotalHours  = 40.5m,
            Status      = "Submitted"
        };

        // Act
        var dto = _mapper.Map<TimesheetDto>(entity);

        // Assert
        dto.TimesheetId.Should().Be(400);
        dto.TotalHours.Should().Be(40.5m);
        dto.Status.Should().Be("Submitted");
    }
}
