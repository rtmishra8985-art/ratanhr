namespace HRMS.Application.DTOs.Recruitment;

public record CreateOfferDto(
    int CandidateId,
    int? JobRequisitionId,
    string OfferedDesignation,
    string OfferedDepartment,
    decimal OfferedSalary,
    DateTime JoiningDate,
    DateTime ExpiryDate
);

public record ApproveOfferDto(string ApprovalNotes);
public record UpdateOfferStatusDto(string Status);

public class OfferListDto
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public int? JobRequisitionId { get; set; }
    public string? JobTitle { get; set; }
    public string OfferedDesignation { get; set; } = string.Empty;
    public string OfferedDepartment { get; set; } = string.Empty;
    public decimal OfferedSalary { get; set; }
    public DateTime JoiningDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
