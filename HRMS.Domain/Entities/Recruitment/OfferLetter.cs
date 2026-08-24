using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Recruitment;

public class OfferLetter : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int CandidateId { get; set; }
    public int? JobRequisitionId { get; set; }
    public string OfferedDesignation { get; set; } = string.Empty;
    public string OfferedDepartment { get; set; } = string.Empty;
    public decimal OfferedSalary { get; set; }
    public DateTime JoiningDate { get; set; }
    public DateTime OfferIssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = "Pending Approval"; // Pending Approval, Approved, Issued, Accepted, Rejected, Expired
    public int? ApprovedByUserId { get; set; }
    public string ApprovalNotes { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
