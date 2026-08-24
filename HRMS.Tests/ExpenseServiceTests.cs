#pragma warning disable CS0618  // SubmitLegacyAsync / CreateExpenseDto are intentionally obsolete; tests cover the legacy path
using HRMS.Application.DTOs.Expense;
using HRMS.Domain.Entities.Expense;
using HRMS.Infrastructure.FileStorage;
using HRMS.Infrastructure.Security;
using HRMS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HRMS.Tests;

public class ExpenseServiceTests
{
    private static ExpenseService Build(HRMS.Infrastructure.Data.ApplicationDbContext db)
    {
        var storage = new FileStorageService(Path.GetTempPath(), Options.Create(new FileUploadOptions()));
        return new ExpenseService(db, storage, NullLogger<ExpenseService>.Instance);
    }

    [Fact]
    public async Task SubmitAsync_Creates_Claim_With_Submitted_Status()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = Build(db);

        var dto = new CreateExpenseDto
        {
            Title    = "Team Lunch",
            Amount   = 2500m,
            Currency = "INR",
            Category = "Meals"
        };

        var result = await svc.SubmitLegacyAsync("EMP001", companyId: 1, dto);

        Assert.Equal("Submitted", result.Status);
        Assert.Equal(2500m, result.TotalAmount);
        Assert.Equal("Meals", result.Items[0].Category);
        Assert.NotNull(result.SubmittedAt);
    }

    [Fact]
    public async Task DecideAsync_Approve_ChangesStatusToApproved()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.ExpenseClaims.Add(new ExpenseClaim
        {
            EmployeeId = "EMP001", CompanyId = 1,
            Title = "Flight", TotalAmount = 5000m, Currency = "INR",
            Status = "Submitted", SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var id = db.ExpenseClaims.First().Id;
        // Seed the pending approval step that DecideAsync requires
        db.ExpenseApprovals.Add(new ExpenseApproval
        {
            ExpenseClaimId = id, CompanyId = 1,
            Step = "Manager", Status = "Pending", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);

        var ok = await svc.DecideLegacyAsync(id, reviewerUserId: 99, companyId: 1,
            new ExpenseDecisionDto { Approve = true });

        Assert.True(ok);
        Assert.Equal("ManagerApproved", db.ExpenseClaims.First().Status);
        Assert.Equal(99, db.ExpenseApprovals.First(a => a.Step == "Manager").ApproverId);
    }

    [Fact]
    public async Task DecideAsync_Reject_ChangesStatusToRejected()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.ExpenseClaims.Add(new ExpenseClaim
        {
            EmployeeId = "EMP002", CompanyId = 1,
            Title = "Hotel", TotalAmount = 8000m, Currency = "INR",
            Status = "Submitted", SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var id = db.ExpenseClaims.First().Id;
        db.ExpenseApprovals.Add(new ExpenseApproval
        {
            ExpenseClaimId = id, CompanyId = 1,
            Step = "Manager", Status = "Pending", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);

        await svc.DecideLegacyAsync(id, reviewerUserId: 99, companyId: 1,
            new ExpenseDecisionDto { Approve = false, Comments = "Missing receipt" });

        Assert.Equal("Rejected", db.ExpenseClaims.First().Status);
    }

    [Fact]
    public async Task DecideAsync_Returns_False_For_NonSubmittedClaim()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.ExpenseClaims.Add(new ExpenseClaim
        {
            EmployeeId = "EMP003", CompanyId = 1,
            Title = "Taxi", TotalAmount = 200m, Currency = "INR",
            Status = "Approved", // already approved — no pending approval record
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);
        var id = db.ExpenseClaims.First().Id;

        var ok = await svc.DecideLegacyAsync(id, 99, 1, new ExpenseDecisionDto { Approve = true });

        Assert.False(ok);
    }

    [Fact]
    public async Task DeleteAsync_Removes_DraftClaim()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.ExpenseClaims.Add(new ExpenseClaim
        {
            EmployeeId = "EMP004", CompanyId = 1,
            Title = "Parking", TotalAmount = 50m, Currency = "INR",
            Status = "Draft", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);
        var id = db.ExpenseClaims.First().Id;

        var ok = await svc.DeleteAsync(id, "EMP004");

        Assert.True(ok);
        Assert.Empty(db.ExpenseClaims);
    }

    [Fact]
    public async Task DeleteAsync_Returns_False_For_SubmittedClaim()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.ExpenseClaims.Add(new ExpenseClaim
        {
            EmployeeId = "EMP005", CompanyId = 1,
            Title = "Meal", TotalAmount = 300m, Currency = "INR",
            Status = "Submitted", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);
        var id = db.ExpenseClaims.First().Id;

        var ok = await svc.DeleteAsync(id, "EMP005");

        Assert.False(ok);
        Assert.Single(db.ExpenseClaims);
    }
}
#pragma warning restore CS0618
