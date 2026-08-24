namespace HRMS.Domain.Entities.Performance;

/// <summary>
/// Tracks employee awards, recognition, and performance appreciation
/// </summary>
public class AwardRecognition
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    
    public string AwardName { get; set; } = string.Empty;
    
    /// <summary>Award type: Performance, Innovation, Attendance, Culture, Safety, Excellence</summary>
    public string? AwardType { get; set; }
    
    public DateTime AwardDate { get; set; }
    
    /// <summary>User ID of person who awarded this</summary>
    public string? AwardedByUserId { get; set; }
    
    /// <summary>Monetary prize amount (if any)</summary>
    public decimal? PrizeAmount { get; set; }
    
    /// <summary>Path to award certificate file</summary>
    public string? CertificatePath { get; set; }
    
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
