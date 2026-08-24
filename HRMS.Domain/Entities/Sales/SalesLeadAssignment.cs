using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Sales;

/// <summary>
/// Tracks the full assignment/reassignment history for a lead.
/// One row per assignment event.
/// </summary>
public class SalesLeadAssignment : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;

    /// <summary>The lead being assigned.</summary>
    public int SalesLeadId { get; set; }

    /// <summary>EmployeeId of the sales executive the lead is assigned TO.</summary>
    public string? AssignedToEmployeeId { get; set; }

    /// <summary>UserId of the manager/admin who performed the assignment.</summary>
    public int AssignedByUserId { get; set; }

    /// <summary>Optional EmployeeId of the previous owner (populated on reassignment).</summary>
    public string? ReassignedFromEmployeeId { get; set; }

    /// <summary>Action type: Assigned / Reassigned / Unassigned</summary>
    public string ActionType { get; set; } = "Assigned";

    /// <summary>Free-text remarks entered by the assigning manager.</summary>
    public string Remarks { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;

    // Navigation
    public SalesLead? Lead { get; set; }
}
