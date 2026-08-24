namespace HRMS.Application.DTOs.Onboarding;

public class CreateOnboardingTemplateDto
{
    public string Name { get; set; } = string.Empty;
    /// <summary>JSON array of step objects: [{title, description}]</summary>
    public string Steps { get; set; } = "[]";
}

public class OnboardingTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Steps { get; set; } = "[]";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AssignOnboardingDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public int? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }
}

public class OnboardingRecordDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string CompletedSteps { get; set; } = "[]";
    public string Steps { get; set; } = "[]";
    public int? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MarkStepCompleteDto
{
    public int StepIndex { get; set; }
}
