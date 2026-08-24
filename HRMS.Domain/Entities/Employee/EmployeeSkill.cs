namespace HRMS.Domain.Entities.Employee;

/// <summary>
/// Maintains employee skill inventory for project allocation and capability planning
/// </summary>
public class EmployeeSkill
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int EmployeeId { get; set; }
    
    public string SkillName { get; set; } = string.Empty;
    
    /// <summary>Proficiency level: Beginner, Intermediate, Advanced, Expert</summary>
    public string? ProficiencyLevel { get; set; }
    
    /// <summary>Years of experience with this skill</summary>
    public decimal? YearsOfExperience { get; set; }
    
    /// <summary>Whether this skill has been verified by manager/HR</summary>
    public bool IsVerified { get; set; } = false;
    
    /// <summary>User ID of person who verified this skill</summary>
    public string? VerifiedByUserId { get; set; }
    
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
