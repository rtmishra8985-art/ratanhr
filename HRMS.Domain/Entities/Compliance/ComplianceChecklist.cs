namespace HRMS.Domain.Entities.Compliance;

/// <summary>
/// Tracks compliance requirements (GDPR, tax, labor laws, internal policies)
/// </summary>
public class ComplianceChecklist
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    /// <summary>JSON array of checklist items with status tracking</summary>
    public string ChecklistItems { get; set; } = "[]";
    
    /// <summary>Frequency: Monthly, Quarterly, Annually, OneTime</summary>
    public string? Frequency { get; set; }
    
    public DateTime? DueDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
