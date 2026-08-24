using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Recruitment;

public class Candidate : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int? JobRequisitionId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string CurrentDesignation { get; set; } = string.Empty;
    public string CurrentCompany { get; set; } = string.Empty;
    public decimal TotalExperience { get; set; } = 0;
    public string Skills { get; set; } = string.Empty;
    public string QualificationSummary { get; set; } = string.Empty;
    public string? ResumeFilePath { get; set; }
    public string SourceChannel { get; set; } = "Portal"; // LinkedIn, Portal, Referral, Agency, Direct
    public string Status { get; set; } = "New"; // New, Shortlisted, Interviewed, Selected, Rejected, On Hold, Offer Extended, Hired, Withdrawn
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True when this candidate was created by the demo-mode seed service
    /// (<see cref="HRMS.Infrastructure.Services.Demo.DemoSeedService"/>). Used by
    /// CleanupAsync to delete only demo candidates and never touch real applicants.
    /// </summary>
    public bool IsDemo { get; set; } = false;
}
