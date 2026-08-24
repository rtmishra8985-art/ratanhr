namespace HRMS.Domain.Entities.Onboarding;

public class OnboardingRecord
{
    public int Id { get; set; }

    /// <summary>Business-key string employee ID (e.g. "EMP-0042") — preserved for display and legacy import.</summary>
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>Integer FK to employees.id — populated by migration 20260725000006.</summary>
    public int? EmployeeFk { get; set; }

    public int TemplateId { get; set; }

    /// <summary>JSON array of completed step indexes</summary>
    public string CompletedSteps { get; set; } = "[]";

    public int? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public OnboardingTemplate? Template { get; set; }
}
