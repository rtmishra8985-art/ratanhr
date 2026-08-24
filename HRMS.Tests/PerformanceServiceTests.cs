using HRMS.Application.DTOs.Performance;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Unit tests for PerformanceService.
/// All tests use EF Core InMemory — no real database required.
/// </summary>
public class PerformanceServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static PerformanceService BuildService(HRMS.Infrastructure.Data.ApplicationDbContext db)
        => new(db, new MockLogger<PerformanceService>());

    private const int    CompanyId = 1;
    private const int    UserId    = 42;
    private const string EmpId     = "EMP9001";

    private static CreateCycleDto SampleCycleDto() => new(
        Name:       "Q1 2026 Annual Review",
        StartDate:  new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate:    new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc),
        ReviewType: "Quarterly"
    );

    private static CreateGoalDto SampleGoalDto(int? cycleId = null, string title = "Increase sales by 20%") => new(
        EmployeeId:         EmpId,
        PerformanceCycleId: cycleId,
        Title:              title,
        Description:        "Close more deals.",
        GoalType:           "Individual",
        Category:           "KPI",
        TargetValue:        100m,
        Unit:               "%",
        DueDate:            new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc),
        Weight:             50
    );

    // ── Performance Cycle Tests ──────────────────────────────────────────────

    [Fact]
    public async Task CreateCycle_ValidInput_ReturnsPositiveId()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var result = await svc.CreateCycleAsync(SampleCycleDto(), CompanyId, UserId);

        Assert.True(result.Id > 0);
        Assert.Equal("Q1 2026 Annual Review", result.Name);
        Assert.Equal("Draft", result.Status);
    }

    [Fact]
    public async Task ListCycles_IsolatedByCompany_ReturnsOnlyOwnCompany()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        await svc.CreateCycleAsync(SampleCycleDto(), companyId: 1, UserId);
        await svc.CreateCycleAsync(SampleCycleDto(), companyId: 2, UserId);

        Assert.Single((await svc.ListCyclesAsync(companyId: 1)).Items);
        Assert.Single((await svc.ListCyclesAsync(companyId: 2)).Items);
    }

    [Fact]
    public async Task UpdateCycle_ChangesStatus()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var cycle   = await svc.CreateCycleAsync(SampleCycleDto(), CompanyId, UserId);
        var updated = await svc.UpdateCycleAsync(cycle.Id, new UpdateCycleDto(
            Name:       cycle.Name,
            StartDate:  cycle.StartDate,
            EndDate:    cycle.EndDate,
            ReviewType: cycle.ReviewType,
            Status:     "Active"
        ), CompanyId);

        Assert.Equal("Active", updated.Status);
    }

    [Fact]
    public async Task DeleteCycle_ExistingRecord_ReturnsTrue()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var cycle = await svc.CreateCycleAsync(SampleCycleDto(), CompanyId, UserId);
        Assert.True(await svc.DeleteCycleAsync(cycle.Id, CompanyId));
        Assert.Empty((await svc.ListCyclesAsync(CompanyId)).Items);
    }

    [Fact]
    public async Task DeleteCycle_NonExistent_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);
        Assert.False(await svc.DeleteCycleAsync(9999, CompanyId));
    }

    // ── Employee Goal Tests ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateGoal_ValidInput_SetsDefaultStatusNotStarted()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var result = await svc.CreateGoalAsync(SampleGoalDto(), CompanyId, UserId);

        Assert.True(result.Id > 0);
        Assert.Equal("Not Started", result.Status);
        Assert.Equal(100m, result.TargetValue);
        Assert.Equal(0m, result.ProgressPercent);
    }

    [Fact]
    public async Task ListGoals_Paginated_ReturnsCorrectPage()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        for (var i = 1; i <= 5; i++)
            await svc.CreateGoalAsync(SampleGoalDto(title: $"Goal {i}"), CompanyId, UserId);

        var page1 = await svc.ListGoalsAsync(CompanyId, page: 1, pageSize: 3);
        var page2 = await svc.ListGoalsAsync(CompanyId, page: 2, pageSize: 3);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(2, page1.TotalPages);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);
        Assert.False(page2.HasNextPage);
        Assert.True(page2.HasPreviousPage);
    }

    [Fact]
    public async Task ListGoals_FilterByEmployee_ReturnsOnlyThatEmployee()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        await svc.CreateGoalAsync(SampleGoalDto() with { EmployeeId = "EMP9001" }, CompanyId, UserId);
        await svc.CreateGoalAsync(SampleGoalDto() with { EmployeeId = "EMP9002" }, CompanyId, UserId);

        var result = await svc.ListGoalsAsync(CompanyId, employeeId: "EMP9001");

        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, g => Assert.Equal("EMP9001", g.EmployeeId));
    }

    [Fact]
    public async Task UpdateGoalProgress_BelowTarget_SetsInProgress()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var goal = await svc.CreateGoalAsync(SampleGoalDto(), CompanyId, UserId);
        Assert.True(await svc.UpdateGoalProgressAsync(goal.Id, achievedValue: 40m, CompanyId));

        var updated = (await svc.ListGoalsAsync(CompanyId, EmpId)).Items.First(g => g.Id == goal.Id);
        Assert.Equal("In Progress", updated.Status);
        Assert.Equal(40m, updated.ProgressPercent);
    }

    [Fact]
    public async Task UpdateGoalProgress_MeetsTarget_SetsCompleted()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var goal = await svc.CreateGoalAsync(SampleGoalDto(), CompanyId, UserId);
        await svc.UpdateGoalProgressAsync(goal.Id, achievedValue: 100m, CompanyId);

        var updated = (await svc.ListGoalsAsync(CompanyId, EmpId)).Items.First(g => g.Id == goal.Id);
        Assert.Equal("Completed", updated.Status);
        Assert.Equal(100m, updated.ProgressPercent);
    }

    [Fact]
    public async Task UpdateGoalProgress_ExceedsTarget_ClampsProgressAtHundred()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var goal = await svc.CreateGoalAsync(SampleGoalDto(), CompanyId, UserId);
        await svc.UpdateGoalProgressAsync(goal.Id, achievedValue: 150m, CompanyId);

        var updated = (await svc.ListGoalsAsync(CompanyId, EmpId)).Items.First(g => g.Id == goal.Id);
        Assert.Equal("Completed", updated.Status);
        Assert.Equal(100m, updated.ProgressPercent); // clamped at 100 by GoalListDto.ProgressPercent
    }

    [Fact]
    public async Task UpdateGoalProgress_WrongCompany_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var goal = await svc.CreateGoalAsync(SampleGoalDto(), CompanyId, UserId);
        Assert.False(await svc.UpdateGoalProgressAsync(goal.Id, 50m, companyId: 999));
    }

    // ── Performance Review Tests ─────────────────────────────────────────────

    [Fact]
    public async Task CreateReview_ValidInput_SetsStatusPending()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var result = await svc.CreateReviewAsync(
            new CreateReviewDto(EmpId, ReviewerId: UserId, PerformanceCycleId: null, ReviewType: "Annual"),
            CompanyId);

        Assert.True(result.Id > 0);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(EmpId, result.EmployeeId);
    }

    [Fact]
    public async Task SubmitSelfReview_CorrectEmployee_UpdatesStatusInProgress()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var review = await svc.CreateReviewAsync(
            new CreateReviewDto(EmpId, UserId, null, "Annual"), CompanyId);

        var ok = await svc.SubmitSelfReviewAsync(
            review.Id,
            new SubmitSelfReviewDto(SelfRating: 4m, SelfComments: "Performed well.", OverallComments: "On track."),
            CompanyId, employeeId: EmpId);

        Assert.True(ok);
        var detail = await svc.GetReviewAsync(review.Id, CompanyId);
        Assert.NotNull(detail);
        Assert.Equal("In Progress", detail.Status);
        Assert.Equal(4m, detail.SelfRating);
    }

    [Fact]
    public async Task SubmitSelfReview_WrongEmployee_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var review = await svc.CreateReviewAsync(
            new CreateReviewDto(EmpId, UserId, null, "Annual"), CompanyId);

        // A different employee trying to fill in this review — must be rejected
        var ok = await svc.SubmitSelfReviewAsync(
            review.Id,
            new SubmitSelfReviewDto(3m, "OK", "OK"),
            CompanyId, employeeId: "EMP_OTHER");

        Assert.False(ok);
    }

    [Fact]
    public async Task SubmitManagerReview_SetsStatusSubmitted()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var review = await svc.CreateReviewAsync(
            new CreateReviewDto(EmpId, UserId, null, "Annual"), CompanyId);

        var ok = await svc.SubmitManagerReviewAsync(
            review.Id,
            new SubmitManagerReviewDto(ManagerRating: 4.5m, ManagerComments: "Excellent work."),
            CompanyId);

        Assert.True(ok);
        var detail = await svc.GetReviewAsync(review.Id, CompanyId);
        Assert.Equal("Submitted", detail!.Status);
        Assert.Equal(4.5m, detail.ManagerRating);
    }

    [Fact]
    public async Task FinalizeReview_SetsStatusAcknowledgedWithFinalRating()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var review = await svc.CreateReviewAsync(
            new CreateReviewDto(EmpId, UserId, null, "Annual"), CompanyId);

        var ok = await svc.FinalizeReviewAsync(
            review.Id,
            new FinalizeReviewDto(FinalRating: 4.2m, HrComments: "Consistent performer."),
            CompanyId);

        Assert.True(ok);
        var detail = await svc.GetReviewAsync(review.Id, CompanyId);
        Assert.Equal("Acknowledged", detail!.Status);
        Assert.Equal(4.2m, detail.FinalRating);
        Assert.NotNull(detail.AcknowledgedAt);
    }

    [Fact]
    public async Task GetReview_WrongCompany_ReturnsNull()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var review = await svc.CreateReviewAsync(
            new CreateReviewDto(EmpId, UserId, null, "Annual"), CompanyId);

        Assert.Null(await svc.GetReviewAsync(review.Id, companyId: 999));
    }

    // ── Continuous Feedback Tests ────────────────────────────────────────────

    [Fact]
    public async Task SubmitFeedback_NonAnonymous_PreservesFromEmployeeId()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var result = await svc.SubmitFeedbackAsync(
            new CreateFeedbackDto(
                ToEmployeeId: "EMP9002",
                FeedbackText: "Great collaboration!",
                FeedbackType: "Praise",
                IsAnonymous:  false),
            CompanyId, fromEmployeeId: EmpId);

        Assert.Equal(EmpId, result.FromEmployeeId);
        Assert.Equal("Praise", result.FeedbackType);
        Assert.False(result.IsAnonymous);
    }

    [Fact]
    public async Task SubmitFeedback_Anonymous_MasksFromEmployeeId()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var result = await svc.SubmitFeedbackAsync(
            new CreateFeedbackDto(
                ToEmployeeId: "EMP9002",
                FeedbackText: "Needs to improve time management.",
                FeedbackType: "Concern",
                IsAnonymous:  true),
            CompanyId, fromEmployeeId: EmpId);

        Assert.Equal("Anonymous", result.FromEmployeeId);
        Assert.True(result.IsAnonymous);
    }

    [Fact]
    public async Task ListFeedback_FilterByRecipient_ReturnsOnlyThatEmployee()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        await svc.SubmitFeedbackAsync(
            new CreateFeedbackDto("EMP9002", "Great!", "Praise", false), CompanyId, EmpId);
        await svc.SubmitFeedbackAsync(
            new CreateFeedbackDto("EMP9003", "Keep it up!", "Praise", false), CompanyId, EmpId);

        var emp9002Feedback = await svc.ListFeedbackAsync(CompanyId, toEmployeeId: "EMP9002");
        Assert.Single(emp9002Feedback.Items);
        Assert.Equal("EMP9002", emp9002Feedback.Items[0].ToEmployeeId);
    }

    // ── Dashboard Test ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_ReflectsGoalAndReviewCounts()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        await svc.CreateGoalAsync(SampleGoalDto(), CompanyId, UserId);
        var g2 = await svc.CreateGoalAsync(SampleGoalDto(title: "Goal 2"), CompanyId, UserId);
        await svc.UpdateGoalProgressAsync(g2.Id, 100m, CompanyId);

        await svc.CreateReviewAsync(new CreateReviewDto(EmpId, UserId, null, "Annual"), CompanyId);

        var dash = await svc.GetPerformanceDashboardAsync(CompanyId);
        // Anonymous types from the service serialize with their original PascalCase property names
        var json = System.Text.Json.JsonSerializer.Serialize(dash);

        Assert.Contains("\"TotalGoals\":2", json);
        Assert.Contains("\"CompletedGoals\":1", json);
        Assert.Contains("\"PendingReviews\":1", json);
    }
}
