using HRMS.Application.DTOs.Recruitment;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Unit tests for RecruitmentService.
/// All tests use EF Core InMemory — no real database required.
/// </summary>
public class RecruitmentServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static RecruitmentService BuildService(HRMS.Infrastructure.Data.ApplicationDbContext db)
        => new(db, new MockLogger<RecruitmentService>());

    private const int CompanyId = 1;
    private const int UserId    = 42;

    private static CreateRequisitionDto SampleRequisitionDto() => new(
        Title:              "Software Engineer",
        DepartmentName:     "Engineering",
        Description:        "We need a great engineer.",
        OpeningsCount:      2,
        ExperienceRequired: "3-5 years",
        SkillsRequired:     "C#, .NET, SQL",
        JobType:            "Full-Time",
        MinSalary:          50_000m,
        MaxSalary:          80_000m,
        Location:           "Remote",
        ClosingDate:        DateTime.UtcNow.AddDays(30)
    );

    private static CreateCandidateDto SampleCandidateDto(
        string email = "jane.doe@example.com") => new CreateCandidateDto
    {
        JobRequisitionId    = null,
        FirstName           = "Jane",
        LastName            = "Doe",
        Email               = email,
        Phone               = "+1-555-0100",
        Address             = "123 Main St",
        CurrentDesignation  = "Junior Developer",
        CurrentCompany      = "Acme Corp",
        TotalExperience     = 2.5m,
        Skills              = "C#, SQL",
        QualificationSummary = "B.Sc. Computer Science",
        SourceChannel       = "LinkedIn",
        Notes               = "Strong candidate"
    };

    // ── Job Requisition Tests ────────────────────────────────────────────────

    [Fact]
    public async Task CreateRequisition_ValidInput_ReturnsPositiveId()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var result = await svc.CreateRequisitionAsync(SampleRequisitionDto(), CompanyId, UserId);

        Assert.True(result.Id > 0);
        Assert.Equal("Software Engineer", result.Title);
        Assert.Equal("Open", result.Status);
    }

    [Fact]
    public async Task ListRequisitions_FilterByStatus_ReturnsOnlyMatching()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        await svc.CreateRequisitionAsync(SampleRequisitionDto(), CompanyId, UserId);
        var req2 = await svc.CreateRequisitionAsync(
            SampleRequisitionDto() with { Title = "Designer" }, CompanyId, UserId);
        await svc.UpdateRequisitionStatusAsync(req2.Id, "Closed", CompanyId);

        var open   = await svc.ListRequisitionsAsync(CompanyId, "Open");
        var closed = await svc.ListRequisitionsAsync(CompanyId, "Closed");

        Assert.Single(open);
        Assert.Single(closed);
        Assert.Equal("Closed", closed[0].Status);
    }

    [Fact]
    public async Task ListRequisitionsPaged_AppliesDatabasePageAndTotal()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);

        await svc.CreateRequisitionAsync(SampleRequisitionDto() with { Title = "First" }, CompanyId, UserId);
        await svc.CreateRequisitionAsync(SampleRequisitionDto() with { Title = "Second" }, CompanyId, UserId);
        await svc.CreateRequisitionAsync(SampleRequisitionDto() with { Title = "Third" }, CompanyId, UserId);

        var result = await svc.ListRequisitionsPagedAsync(CompanyId, page: 2, pageSize: 1);

        Assert.Equal(3, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task GetRequisition_WrongCompany_ReturnsNull()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var result  = await svc.CreateRequisitionAsync(SampleRequisitionDto(), CompanyId, UserId);
        var fetched = await svc.GetRequisitionAsync(result.Id, companyId: 999);

        Assert.Null(fetched);
    }

    [Fact]
    public async Task DeleteRequisition_ExistingRecord_Succeeds()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var req = await svc.CreateRequisitionAsync(SampleRequisitionDto(), CompanyId, UserId);
        var ok  = await svc.DeleteRequisitionAsync(req.Id, CompanyId);

        Assert.True(ok);
        Assert.Empty(await svc.ListRequisitionsAsync(CompanyId));
    }

    [Fact]
    public async Task DeleteRequisition_NonExistent_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);
        Assert.False(await svc.DeleteRequisitionAsync(9999, CompanyId));
    }

    // ── Candidate Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCandidate_ValidInput_SetsDefaultStatusNew()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var result = await svc.CreateCandidateAsync(SampleCandidateDto(), null, CompanyId);

        Assert.True(result.Id > 0);
        Assert.Equal("New", result.Status);
        Assert.Equal("Jane", result.FirstName);
    }

    [Fact]
    public async Task UpdateCandidateStatus_ExistingCandidate_UpdatesSuccessfully()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var cand = await svc.CreateCandidateAsync(SampleCandidateDto(), null, CompanyId);
        var ok   = await svc.UpdateCandidateStatusAsync(cand.Id, "Shortlisted", "Strong profile", CompanyId);

        Assert.True(ok);
        var detail = await svc.GetCandidateAsync(cand.Id, CompanyId);
        Assert.NotNull(detail);
        Assert.Equal("Shortlisted", detail.Status);
        Assert.Contains("Strong profile", detail.Notes);
    }

    [Fact]
    public async Task UpdateCandidateStatus_WrongCompany_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var cand = await svc.CreateCandidateAsync(SampleCandidateDto(), null, CompanyId);
        var ok   = await svc.UpdateCandidateStatusAsync(cand.Id, "Shortlisted", "", companyId: 999);

        Assert.False(ok);
    }

    [Fact]
    public async Task ListCandidates_FilterByStatus_ReturnsOnlyMatching()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        await svc.CreateCandidateAsync(SampleCandidateDto(), null, CompanyId);
        var c2 = await svc.CreateCandidateAsync(SampleCandidateDto("second@example.com"), null, CompanyId);
        await svc.UpdateCandidateStatusAsync(c2.Id, "Interviewed", "", CompanyId);

        var newOnes = await svc.ListCandidatesAsync(CompanyId, status: "New");
        var intv    = await svc.ListCandidatesAsync(CompanyId, status: "Interviewed");

        Assert.Single(newOnes.Items);
        Assert.Single(intv.Items);
    }

    // ── Interview Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleInterview_ValidInput_ReturnsScheduledInterview()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var cand = await svc.CreateCandidateAsync(SampleCandidateDto(), null, CompanyId);
        var dto  = new ScheduleInterviewDto(
            CandidateId:      cand.Id,
            JobRequisitionId: null,
            ScheduledAt:      DateTime.UtcNow.AddDays(3),
            InterviewType:    "Technical",
            Venue:            "Zoom",
            InterviewerNames: "Alice, Bob"
        );

        var result = await svc.ScheduleInterviewAsync(dto, CompanyId, UserId);

        Assert.True(result.Id > 0);
        Assert.Equal("Technical", result.InterviewType);
        Assert.Equal("Scheduled", result.Status);
    }

    [Fact]
    public async Task SubmitInterviewFeedback_UpdatesStatusAndScore()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var cand = await svc.CreateCandidateAsync(SampleCandidateDto(), null, CompanyId);
        var iv   = await svc.ScheduleInterviewAsync(
            new ScheduleInterviewDto(cand.Id, null, DateTime.UtcNow.AddDays(1), "HR", "Office", "Charlie"),
            CompanyId, UserId);

        var ok = await svc.SubmitInterviewFeedbackAsync(
            iv.Id,
            new SubmitFeedbackDto(FeedbackScore: 4, FeedbackNotes: "Great communication.",
                                  Recommendation: "Proceed", Status: "Completed"),
            CompanyId);

        Assert.True(ok);
        var interviews = await svc.ListInterviewsAsync(CompanyId, cand.Id);
        Assert.Equal("Completed", interviews[0].Status);
        Assert.Equal(4, interviews[0].FeedbackScore);
    }

    [Fact]
    public async Task ListInterviewsPaged_AppliesDatabasePageAndTotal()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildService(db);
        var candidate = await svc.CreateCandidateAsync(SampleCandidateDto(), null, CompanyId);

        await svc.ScheduleInterviewAsync(
            new ScheduleInterviewDto(candidate.Id, null, DateTime.UtcNow.AddDays(1), "HR", "Office", "A"),
            CompanyId, UserId);
        await svc.ScheduleInterviewAsync(
            new ScheduleInterviewDto(candidate.Id, null, DateTime.UtcNow.AddDays(2), "Technical", "Office", "B"),
            CompanyId, UserId);

        var result = await svc.ListInterviewsPagedAsync(CompanyId, candidate.Id, page: 2, pageSize: 1);

        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    // ── Offer Letter Tests ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateOffer_SetsStatusPendingAndCandidateOfferExtended()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var cand  = await svc.CreateCandidateAsync(SampleCandidateDto(), null, CompanyId);
        var offer = await svc.CreateOfferAsync(new CreateOfferDto(
            CandidateId:        cand.Id,
            JobRequisitionId:   null,
            OfferedDesignation: "Senior Engineer",
            OfferedDepartment:  "Engineering",
            OfferedSalary:      75_000m,
            JoiningDate:        DateTime.UtcNow.AddDays(30),
            ExpiryDate:         DateTime.UtcNow.AddDays(10)
        ), CompanyId, UserId);

        Assert.True(offer.Id > 0);
        Assert.Equal("Pending Approval", offer.Status);

        // Candidate status should auto-update to "Offer Extended"
        var detail = await svc.GetCandidateAsync(cand.Id, CompanyId);
        Assert.Equal("Offer Extended", detail!.Status);
    }

    [Fact]
    public async Task UpdateOfferStatus_Accepted_SetsCandidateHired()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var cand  = await svc.CreateCandidateAsync(SampleCandidateDto(), null, CompanyId);
        var offer = await svc.CreateOfferAsync(new CreateOfferDto(
            cand.Id, null, "Engineer", "Engineering", 70_000m,
            DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(10)), CompanyId, UserId);

        Assert.True(await svc.UpdateOfferStatusAsync(offer.Id, "Accepted", CompanyId));

        var detail = await svc.GetCandidateAsync(cand.Id, CompanyId);
        Assert.Equal("Hired", detail!.Status);
    }

    [Fact]
    public async Task UpdateOfferStatus_Rejected_SetsCandidateRejected()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        var cand  = await svc.CreateCandidateAsync(SampleCandidateDto(), null, CompanyId);
        var offer = await svc.CreateOfferAsync(new CreateOfferDto(
            cand.Id, null, "Engineer", "Engineering", 70_000m,
            DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(10)), CompanyId, UserId);

        await svc.UpdateOfferStatusAsync(offer.Id, "Rejected", CompanyId);

        var detail = await svc.GetCandidateAsync(cand.Id, CompanyId);
        Assert.Equal("Rejected", detail!.Status);
    }

    // ── Dashboard Test ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_ReflectsCurrentCounts()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc      = BuildService(db);

        await svc.CreateRequisitionAsync(SampleRequisitionDto(), CompanyId, UserId);
        var c1 = await svc.CreateCandidateAsync(SampleCandidateDto(), null, CompanyId);
        await svc.CreateCandidateAsync(SampleCandidateDto("b@example.com"), null, CompanyId);
        await svc.UpdateCandidateStatusAsync(c1.Id, "Hired", "", CompanyId);

        var dash = await svc.GetRecruitmentDashboardAsync(CompanyId);
        // Anonymous types serialize with their original PascalCase property names
        var json = System.Text.Json.JsonSerializer.Serialize(dash);

        Assert.Contains("\"OpenRequisitions\":1", json);
        Assert.Contains("\"Hired\":1", json);
        Assert.Contains("\"TotalCandidates\":2", json);
    }
}
