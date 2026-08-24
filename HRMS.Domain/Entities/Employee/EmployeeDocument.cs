using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Employee;

public class EmployeeDocument : ICompanyOwned
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public int? CompanyId { get; set; }
    public string DocumentType { get; set; } = string.Empty; // Aadhaar, PAN, Passport, OfferLetter, etc.
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Notes { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
