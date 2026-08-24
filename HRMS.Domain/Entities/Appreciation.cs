using HRMS.Domain.Common;
namespace HRMS.Domain.Entities;

public class Appreciation : ICompanyOwned
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public int? CompanyId { get; set; }
    public string? AwardTitle { get; set; }
    public string? Description { get; set; }
    public string? Message { get; set; }
    public string? FilePath { get; set; }
    public string? CertificatePath { get; set; }
    public int? AwardedByUserId { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
