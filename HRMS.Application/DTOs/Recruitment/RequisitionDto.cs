namespace HRMS.Application.DTOs.Recruitment;

public record CreateRequisitionDto(
    string Title,
    string DepartmentName,
    string Description,
    int OpeningsCount,
    string ExperienceRequired,
    string SkillsRequired,
    string JobType,
    decimal? MinSalary,
    decimal? MaxSalary,
    string Location,
    DateTime? ClosingDate
);

public record UpdateRequisitionDto(
    string Title,
    string DepartmentName,
    string Description,
    int OpeningsCount,
    string ExperienceRequired,
    string SkillsRequired,
    string JobType,
    decimal? MinSalary,
    decimal? MaxSalary,
    string Location,
    DateTime? ClosingDate
);

public record UpdateRequisitionStatusDto(string Status);

public class RequisitionListDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int OpeningsCount { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime? ClosingDate { get; set; }
    public int TotalCandidates { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RequisitionDetailDto : RequisitionListDto
{
    public string Description { get; set; } = string.Empty;
    public string ExperienceRequired { get; set; } = string.Empty;
    public string SkillsRequired { get; set; } = string.Empty;
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public int CreatedByUserId { get; set; }
    public List<CandidateListDto> Candidates { get; set; } = new();
}
