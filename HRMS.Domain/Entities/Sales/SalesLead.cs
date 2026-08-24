using HRMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Domain.Entities.Sales;

public class SalesLead : ICompanyOwned
{
    public int Id { get; set; }
    /// <summary>Domain-prefixed PK alias — maps to Id.</summary>
    [NotMapped] public int LeadId { get => Id; set => Id = value; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int? BranchId { get; set; }

    /// <summary>Auto-generated lead number, e.g. LEAD-0001.</summary>
    public string LeadNo { get; set; } = string.Empty;

    /// <summary>Short title / subject for the lead. Tests use Title.</summary>
    public string Title { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    /// <summary>Alias for ContactPerson — tests use ContactName.</summary>
    [NotMapped] public string ContactName { get => ContactPerson; set => ContactPerson = value; }
    public string Mobile { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Alias for Email — tests use ContactEmail.</summary>
    [NotMapped] public string ContactEmail { get => Email; set => Email = value; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    /// <summary>Source: Website / Referral / Cold Call / Exhibition / Social Media / Other</summary>
    public string LeadSource { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;

    /// <summary>Employee who owns this lead.</summary>
    public string? EmployeeOwnerId { get; set; }
    /// <summary>Alias for EmployeeOwnerId — tests use AssignedToEmployeeId.</summary>
    [NotMapped] public string? AssignedToEmployeeId { get => EmployeeOwnerId; set => EmployeeOwnerId = value; }

    /// <summary>Low / Medium / High / Critical</summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>New / Contacted / Qualified / Proposal Sent / Negotiation / Won / Lost</summary>
    public string Status { get; set; } = "New";

    public string Remarks { get; set; } = string.Empty;
    public decimal? ExpectedValue { get; set; }
    /// <summary>Alias for ExpectedValue — tests use Value.</summary>
    [NotMapped] public decimal? Value { get => ExpectedValue; set => ExpectedValue = value; }
    public DateTime? NextFollowUpDate { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}
