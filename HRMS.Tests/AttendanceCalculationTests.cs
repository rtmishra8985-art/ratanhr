using FluentAssertions;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Tests for AttendanceService calculations.
/// IMPORTANT: All test dates are fixed constants — never DateTime.UtcNow.
/// This prevents flaky failures near midnight UTC.
/// </summary>
public class AttendanceCalculationTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IAttendanceService _svc;
    private const int CompanyId = 1;

    // All tests use this fixed date/time — no DateTime.UtcNow anywhere.
    private static readonly DateOnly  FixedDate    = new(2025, 6, 15);
    private static readonly DateTime  FixedMorning = new(2025, 6, 15, 9, 0, 0, DateTimeKind.Utc);

    public AttendanceCalculationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _svc = new AttendanceService(_db, new MockAuditService());
        SeedEmployee();
    }

    public void Dispose() => _db.Dispose();

    // ─── Status derivation from hours worked ─────────────────────────────────────

    [Fact]
    public async Task CheckOut_EightOrMoreHours_StatusIsPresent()
    {
        // Arrange — check in at 09:00, check out at 17:30 = 8.5 hours
        var checkInTime  = FixedMorning;                           // 09:00
        var checkOutTime = FixedMorning.AddHours(8).AddMinutes(30); // 17:30

        var attendanceId = await _svc.CheckInAsync("E001", CompanyId,
            checkInTime, ipAddress: "127.0.0.1");

        // Act
        await _svc.CheckOutAsync(attendanceId, checkOutTime);

        // Assert
        var record = await _db.WebAttendances.FindAsync(attendanceId);
        record!.Status.Should().Be("Present");
    }

    [Fact]
    public async Task CheckOut_FiveHours_StatusIsHalfDay()
    {
        // Arrange — check in at 09:00, check out at 14:00 = 5 hours
        var checkInTime  = FixedMorning;
        var checkOutTime = FixedMorning.AddHours(5);

        var attendanceId = await _svc.CheckInAsync("E001", CompanyId,
            checkInTime, ipAddress: "127.0.0.1");

        // Act
        await _svc.CheckOutAsync(attendanceId, checkOutTime);

        // Assert
        var record = await _db.WebAttendances.FindAsync(attendanceId);
        record!.Status.Should().Be("Half Day");
    }

    [Fact]
    public async Task CheckOut_TwoHours_StatusIsAbsent()
    {
        // Arrange — check in at 09:00, check out at 11:00 = 2 hours
        var checkInTime  = FixedMorning;
        var checkOutTime = FixedMorning.AddHours(2);

        var attendanceId = await _svc.CheckInAsync("E001", CompanyId,
            checkInTime, ipAddress: "127.0.0.1");

        // Act
        await _svc.CheckOutAsync(attendanceId, checkOutTime);

        // Assert
        var record = await _db.WebAttendances.FindAsync(attendanceId);
        record!.Status.Should().Be("Absent");
    }

    // ─── Status boundary at exactly 4 and 8 hours ─────────────────────────────────

    [Theory]
    [InlineData(4,  "Half Day")]    // exactly 4 hours — half day threshold
    [InlineData(8,  "Present")]     // exactly 8 hours — present threshold
    [InlineData(3,  "Absent")]      // below half-day threshold
    [InlineData(12, "Present")]     // overtime — still present
    public async Task CheckOut_BoundaryHours_CorrectStatus(int hoursWorked, string expectedStatus)
    {
        // Arrange
        var checkInTime  = FixedMorning;
        var checkOutTime = FixedMorning.AddHours(hoursWorked);

        var attendanceId = await _svc.CheckInAsync("E001", CompanyId,
            checkInTime, ipAddress: "127.0.0.1");

        // Act
        await _svc.CheckOutAsync(attendanceId, checkOutTime);

        // Assert
        var record = await _db.WebAttendances.FindAsync(attendanceId);
        record!.Status.Should().Be(expectedStatus,
            $"{hoursWorked}h worked should produce status '{expectedStatus}'");
    }

    // ─── Check-in idempotency ─────────────────────────────────────────────────────

    [Fact]
    public async Task CheckIn_SecondCallSameDay_ReturnsExistingId()
    {
        // Arrange
        var firstId = await _svc.CheckInAsync("E001", CompanyId, FixedMorning, "127.0.0.1");

        // Act — second check-in on the same day
        var secondId = await _svc.CheckInAsync("E001", CompanyId,
            FixedMorning.AddHours(1), "127.0.0.1");

        // Assert — must not create a duplicate record
        secondId.Should().Be(firstId,
            "a second check-in on the same day must return the existing attendance record");
    }

    [Fact]
    public async Task CheckIn_DifferentDay_CreatesNewRecord()
    {
        // Arrange
        var day1 = new DateTime(2025, 6, 15, 9, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2025, 6, 16, 9, 0, 0, DateTimeKind.Utc);

        var id1 = await _svc.CheckInAsync("E001", CompanyId, day1, "127.0.0.1");

        // Act
        var id2 = await _svc.CheckInAsync("E001", CompanyId, day2, "127.0.0.1");

        // Assert
        id2.Should().NotBe(id1, "different calendar days must produce separate attendance records");
    }

    // ─── Company isolation ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWebAttendance_ScopedToCompany_ExcludesOtherCompany()
    {
        // Arrange — employee from company 1 and company 2 both check in on the same day
        _db.Employees.Add(new Employee
        {
            EmployeeId = 99, CompanyId = 2, FirstName = "Outsider", LastName = "X",
            EmployeeCode = "E099", Status = "Active"
        });
        await _db.SaveChangesAsync();

        await _svc.CheckInAsync("E001", CompanyId, FixedMorning, "127.0.0.1");
        await _svc.CheckInAsync("E099", 2, FixedMorning, "127.0.0.2");

        // Act
        var records = await _svc.GetAttendanceAsync(
            employeeId: null, companyId: CompanyId,
            startDate: FixedDate, endDate: FixedDate);

        // Assert
        records.All(r => r.CompanyId == CompanyId).Should().BeTrue(
            "attendance query must not leak other companies' records");
    }

    // ─── Edit attendance ──────────────────────────────────────────────────────────

    [Fact]
    public async Task EditAttendance_ReasonIsPersisted()
    {
        // Arrange
        var attendanceId = await _svc.CheckInAsync("E001", CompanyId, FixedMorning, "127.0.0.1");
        await _svc.CheckOutAsync(attendanceId, FixedMorning.AddHours(2)); // Mark absent

        // Act
        var editDto = new EditAttendanceDto
        {
            AttendanceId = attendanceId,
            Reason       = "Was on client site"
        };
        var success = await _svc.EditAttendanceAsync(editDto, actorId: 1, companyId: CompanyId);

        // Assert
        success.Should().BeTrue();
        var record = await _db.WebAttendances.FindAsync(attendanceId);
        record!.Reason.Should().Be("Was on client site");
    }

    [Fact]
    public async Task EditAttendance_NonExistentId_ReturnsFalse()
    {
        // Act
        var editDto = new EditAttendanceDto { AttendanceId = 999999, Reason = "test" };
        var success = await _svc.EditAttendanceAsync(editDto, actorId: 1, companyId: CompanyId);

        // Assert
        success.Should().BeFalse("editing a non-existent attendance record must return false");
    }

    // ─── Overtime calculation ─────────────────────────────────────────────────────

    [Fact]
    public async Task CheckOut_MoreThan9Hours_OvertimeIsCalculated()
    {
        // Arrange — 10 hours worked
        var checkInTime  = FixedMorning;
        var checkOutTime = FixedMorning.AddHours(10);
        var attendanceId = await _svc.CheckInAsync("E001", CompanyId, checkInTime, "127.0.0.1");

        // Act
        await _svc.CheckOutAsync(attendanceId, checkOutTime);

        // Assert
        var record = await _db.WebAttendances.FindAsync(attendanceId);
        record!.Status.Should().Be("Present");
        record.OvertimeMinutes.Should().BeGreaterThan(0,
            "working more than 9 hours must record overtime minutes");
    }

    // ─── Cancellation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttendance_CancelledToken_ThrowsOperationCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _svc.GetAttendanceAsync(
                employeeId: null, companyId: CompanyId,
                startDate: FixedDate, endDate: FixedDate,
                ct: cts.Token));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private void SeedEmployee()
    {
        _db.Employees.Add(new Employee
        {
            EmployeeId = 1, CompanyId = CompanyId,
            FirstName = "Alice", LastName = "A",
            EmployeeCode = "E001", Status = "Active"
        });
        _db.SaveChanges();
    }
}
