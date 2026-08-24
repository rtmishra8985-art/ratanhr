using HRMS.Application.DTOs.Training;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Training;
using HRMS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HRMS.Tests;

public class TrainingServiceTests
{
    private static TrainingService Build(HRMS.Infrastructure.Data.ApplicationDbContext db)
    {
        var cache = new Mock<HRMS.Application.Interfaces.ICacheService>();
        // Pass-through cache (call factory immediately)
        cache.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<HRMS.Application.Common.PagedResult<TrainingDto>>>>(),
                It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns<string, Func<Task<HRMS.Application.Common.PagedResult<TrainingDto>>>, TimeSpan?, CancellationToken>(
                (_, factory, __, ___) => factory());
        cache.Setup(c => c.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new TrainingService(db, cache.Object, NullLogger<TrainingService>.Instance,
            new Mock<IAuditService>().Object);
    }

    [Fact]
    public async Task CreateAsync_Saves_And_Returns_Program()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = Build(db);

        var dto = new CreateTrainingDto
        {
            Title     = "Onboarding 101",
            StartDate = DateTime.UtcNow.AddDays(7),
            EndDate   = DateTime.UtcNow.AddDays(14),
            Trainer   = "Jane Doe",
            MaxSeats  = 20
        };

        var result = await svc.CreateAsync(companyId: 1, dto);

        Assert.Equal("Onboarding 101", result.Title);
        Assert.Equal(1, result.CompanyId);
        Assert.True(result.IsActive);
        Assert.Single(db.TrainingPrograms);
    }

    [Fact]
    public async Task EnrollAsync_Success_WhenSeatsAvailable()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        // TrainingService.EnrollAsync looks up the employee by EmployeeCode before enrolling.
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
            { EmployeeCode = "EMP001", FullName = "Test Employee", CompanyId = 1, IsActive = true });
        db.TrainingPrograms.Add(new TrainingProgram
        {
            Title     = "Test",
            StartDate = DateTime.UtcNow,
            EndDate   = DateTime.UtcNow.AddDays(1),
            MaxSeats  = 5,
            IsActive  = true,
            CompanyId = 1,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var svc = Build(db);
        var prog = db.TrainingPrograms.First();

        var (ok, msg, _) = await svc.EnrollAsync(prog.Id, "EMP001");

        Assert.True(ok);
        Assert.Single(db.TrainingEnrollments);
        Assert.Equal("Enrolled", db.TrainingEnrollments.First().Status);
    }

    [Fact]
    public async Task EnrollAsync_Fails_WhenAlreadyEnrolled()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        // Seed the employee so the service can find them before checking enrollment.
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
            { EmployeeCode = "EMP001", FullName = "Test Employee", CompanyId = 1, IsActive = true });
        db.TrainingPrograms.Add(new TrainingProgram
        {
            Title = "T", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1),
            IsActive = true, CompanyId = 1, CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var prog = db.TrainingPrograms.First();
        db.TrainingEnrollments.Add(new TrainingEnrollment
        {
            TrainingProgramId = prog.Id, EmployeeId = "EMP001",
            Status = "Enrolled", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var svc = Build(db);
        var (ok, msg, _) = await svc.EnrollAsync(prog.Id, "EMP001");

        Assert.False(ok);
        Assert.Contains("Already enrolled", msg);
    }

    [Fact]
    public async Task EnrollAsync_Fails_WhenProgramNotFound()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = Build(db);

        var (ok, msg, _) = await svc.EnrollAsync(999, "EMP001");

        Assert.False(ok);
        Assert.Contains("not found", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_Program()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.TrainingPrograms.Add(new TrainingProgram
        {
            Title = "T", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1),
            IsActive = true, CompanyId = 1, CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);
        var id = db.TrainingPrograms.First().Id;

        var result = await svc.DeleteAsync(id, companyId: 1);

        Assert.True(result);
        Assert.False(db.TrainingPrograms.First().IsActive);
    }

    [Fact]
    public async Task MarkCompleteAsync_UpdatesStatus()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.TrainingPrograms.Add(new TrainingProgram
        {
            Title = "T", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1),
            IsActive = true, CompanyId = 1, CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var prog = db.TrainingPrograms.First();
        db.TrainingEnrollments.Add(new TrainingEnrollment
        {
            TrainingProgramId = prog.Id, EmployeeId = "EMP002",
            Status = "Enrolled", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);
        var enrollment = db.TrainingEnrollments.First();

        var ok = await svc.MarkCompleteAsync(enrollment.Id, companyId: 1, new MarkCompleteDto());

        Assert.True(ok);
        Assert.Equal("Completed", db.TrainingEnrollments.First().Status);
    }
}
