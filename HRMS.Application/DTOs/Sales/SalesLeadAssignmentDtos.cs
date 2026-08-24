namespace HRMS.Application.DTOs.Sales;

// ── Assign a lead ──────────────────────────────────────────────────────────
public class AssignLeadDto
{
    /// <summary>EmployeeId of the sales executive to assign to.</summary>
    public string AssignedToEmployeeId { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}

// ── Reassign a lead ────────────────────────────────────────────────────────
public class ReassignLeadDto
{
    /// <summary>New EmployeeId to take over the lead.</summary>
    public string NewAssignedToEmployeeId { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}

// ── Bulk assign multiple leads ─────────────────────────────────────────────
public class BulkAssignLeadsDto
{
    public List<int> LeadIds { get; set; } = new();
    public string AssignedToEmployeeId { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}

// ── Assignment history row (read-only) ─────────────────────────────────────
public class LeadAssignmentHistoryDto
{
    public int Id { get; set; }
    public int SalesLeadId { get; set; }
    public string LeadNo { get; set; } = string.Empty;
    public string? AssignedToEmployeeId { get; set; }
    public string? AssigneeName { get; set; }
    public int AssignedByUserId { get; set; }
    public string? AssignedByName { get; set; }
    public string? ReassignedFromEmployeeId { get; set; }
    public string? ReassignedFromName { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}
