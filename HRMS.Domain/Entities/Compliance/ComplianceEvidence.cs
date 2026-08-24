namespace HRMS.Domain.Entities.Compliance;

/// <summary>
/// Tracks evidence and documentation for compliance checklist items
/// </summary>
public class ComplianceEvidence
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int ComplianceChecklistId { get; set; }
    
    /// <summary>Index of the item in checklist_items JSON array</summary>
    public int? ItemId { get; set; }
    
    /// <summary>Status: Pending, Completed, Failed, OnHold</summary>
    public string Status { get; set; } = "Pending";
    
    /// <summary>Path to uploaded evidence document</summary>
    public string? EvidenceDocumentPath { get; set; }
    
    /// <summary>User ID who verified the compliance</summary>
    public string? VerifiedByUserId { get; set; }
    
    public DateTime? VerifiedAt { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual ComplianceChecklist? Checklist { get; set; }
}
