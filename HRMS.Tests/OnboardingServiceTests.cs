using HRMS.Application.DTOs.Onboarding;
using HRMS.Domain.Entities.Onboarding;
using HRMS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HRMS.Tests;

public class OnboardingServiceTests
{
    private static OnboardingService Build(HRMS.Infrastructure.Data.ApplicationDbContext db)
        => new OnboardingService(db, NullLogger<OnboardingService>.Instance);

    [Fact]
    public async Task CreateTemplateAsync_Saves_Template()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = Build(db);

        var dto = new CreateOnboardingTemplateDto
        {
            Name  = "Engineering Onboarding",
            Steps = """[{"title":"Setup laptop"},{"title":"Read handbook"}]"""
        };

        var result = await svc.CreateTemplateAsync(companyId: 1, dto);

        Assert.Equal("Engineering Onboarding", result.Name);
        Assert.True(result.IsActive);
        Assert.Single(db.OnboardingTemplates);
    }

    [Fact]
    public async Task AssignAsync_Creates_Record()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.OnboardingTemplates.Add(new OnboardingTemplate
        {
            CompanyId = 1, Name = "T",
            Steps = """[{"title":"Step 1"}]""",
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        // Remediation AUDIT-07S-01: AssignAsync now validates that the target employee
        // exists and belongs to the caller's tenant, so the employee must be seeded.
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "EMP001", CompanyId = 1, FullName = "Test Employee"
        });
        db.SaveChanges();
        var svc = Build(db);
        var tplId = db.OnboardingTemplates.First().Id;

        var result = await svc.AssignAsync(companyId: 1, new AssignOnboardingDto
        {
            EmployeeId = "EMP001",
            TemplateId = tplId,
            DueDate    = DateTime.UtcNow.AddDays(30)
        });

        Assert.Equal("EMP001", result.EmployeeId);
        Assert.Single(db.OnboardingRecords);
    }

    [Fact]
    public async Task MarkStepCompleteAsync_AddStep_To_CompletedList()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.OnboardingTemplates.Add(new OnboardingTemplate
        {
            CompanyId = 1, Name = "T",
            Steps = """[{"title":"A"},{"title":"B"}]""",
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var tpl = db.OnboardingTemplates.First();
        db.OnboardingRecords.Add(new OnboardingRecord
        {
            EmployeeId = "EMP002", TemplateId = tpl.Id,
            CompletedSteps = "[]", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);
        var rec = db.OnboardingRecords.First();

        var ok = await svc.MarkStepCompleteAsync(rec.Id, "EMP002", new MarkStepCompleteDto { StepIndex = 0 });

        Assert.True(ok);
        var updatedRec = db.OnboardingRecords.First();
        Assert.Contains("0", updatedRec.CompletedSteps);
    }

    [Fact]
    public async Task MarkStepCompleteAsync_AllStepsDone_SetsCompletedAt()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.OnboardingTemplates.Add(new OnboardingTemplate
        {
            CompanyId = 1, Name = "T2",
            Steps = """[{"title":"Only step"}]""",
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var tpl = db.OnboardingTemplates.First();
        db.OnboardingRecords.Add(new OnboardingRecord
        {
            EmployeeId = "EMP003", TemplateId = tpl.Id,
            CompletedSteps = "[]", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);
        var rec = db.OnboardingRecords.First();

        await svc.MarkStepCompleteAsync(rec.Id, "EMP003", new MarkStepCompleteDto { StepIndex = 0 });

        var updated = db.OnboardingRecords.First();
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task DeleteTemplateAsync_SoftDeletes()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.OnboardingTemplates.Add(new OnboardingTemplate
        {
            CompanyId = 1, Name = "TDel",
            Steps = "[]", IsActive = true, CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        var svc = Build(db);
        var id = db.OnboardingTemplates.First().Id;

        var ok = await svc.DeleteTemplateAsync(id, companyId: 1);

        Assert.True(ok);
        Assert.False(db.OnboardingTemplates.First().IsActive);
    }
}
