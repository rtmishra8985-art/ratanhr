using HRMS.Application.DTOs.Travel;
using HRMS.Domain.Entities.Travel;
using HRMS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HRMS.Tests;

public class TravelServiceTests
{
    private static TravelService Build(HRMS.Infrastructure.Data.ApplicationDbContext db)
        => new TravelService(db, NullLogger<TravelService>.Instance);

    [Fact]
    public async Task CreateAsync_Creates_Draft_Request()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = Build(db);

        var dto = new CreateTravelDto
        {
            Purpose       = "Client visit",
            Destination   = "Mumbai",
            DepartureDate = DateTime.UtcNow.AddDays(3),
            ReturnDate    = DateTime.UtcNow.AddDays(5),
            EstimatedCost = 12000m
        };

        var result = await svc.CreateAsync("EMP001", companyId: 1, dto);

        Assert.Equal("Draft", result.Status);
        Assert.Equal("Mumbai", result.ToCity);
        Assert.Single(db.TravelRequests);
    }

    [Fact]
    public async Task SubmitAsync_Changes_Status_To_Submitted()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.TravelRequests.Add(new TravelRequest
        {
            EmployeeId = "EMP001", CompanyId = 1,
            Purpose = "Meeting", Destination = "Delhi",
            DepartureDate = DateTime.UtcNow.AddDays(2), ReturnDate = DateTime.UtcNow.AddDays(3),
            EstimatedCost = 5000m, Status = "Draft", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);
        var id = db.TravelRequests.First().Id;

        var ok = await svc.SubmitAsync(id, "EMP001");

        Assert.True(ok);
        Assert.Equal("Submitted", db.TravelRequests.First().Status);
    }

    [Fact]
    public async Task DecideAsync_Approve_ChangesStatus()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.TravelRequests.Add(new TravelRequest
        {
            EmployeeId = "EMP002", CompanyId = 1,
            Purpose = "Conf", Destination = "Bangalore",
            DepartureDate = DateTime.UtcNow.AddDays(5), ReturnDate = DateTime.UtcNow.AddDays(7),
            EstimatedCost = 20000m, Status = "Submitted", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);
        var id = db.TravelRequests.First().Id;

        var ok = await svc.DecideAsync(id, approverUserId: 1, approverName: "hr@company.com",
            companyId: 1, new TravelDecisionDto { Approve = true });

        Assert.True(ok);
        Assert.Equal("Approved", db.TravelRequests.First().Status);
        Assert.Equal(1, db.TravelRequests.First().ApprovedBy);
    }

    [Fact]
    public async Task DeleteAsync_Removes_DraftRequest()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.TravelRequests.Add(new TravelRequest
        {
            EmployeeId = "EMP003", CompanyId = 1,
            Purpose = "X", Destination = "Y",
            DepartureDate = DateTime.UtcNow, ReturnDate = DateTime.UtcNow.AddDays(1),
            EstimatedCost = 1000m, Status = "Draft", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);
        var id = db.TravelRequests.First().Id;

        var ok = await svc.DeleteAsync(id, "EMP003");

        Assert.True(ok);
        Assert.Empty(db.TravelRequests);
    }
}
