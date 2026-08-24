namespace HRMS.Application.DTOs.Recruitment;

public class CreateCandidateDto
{
    public int? JobRequisitionId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string CurrentDesignation { get; set; } = string.Empty;
    public string CurrentCompany { get; set; } = string.Empty;
    public decimal TotalExperience { get; set; }
    public string Skills { get; set; } = string.Empty;
    public string QualificationSummary { get; set; } = string.Empty;
    public string SourceChannel { get; set; } = "Portal";
    public string Notes { get; set; } = string.Empty;
}

public class UpdateCandidateDto : CreateCandidateDto { }

public record UpdateCandidateStatusDto(string Status, string Notes = "");

public class CandidateListDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string CurrentDesignation { get; set; } = string.Empty;
    public decimal TotalExperience { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SourceChannel { get; set; } = string.Empty;
    public int? JobRequisitionId { get; set; }
    public string? JobTitle { get; set; }
    public bool HasResume { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CandidateDetailDto : CandidateListDto
{
    public string Address { get; set; } = string.Empty;
    public string CurrentCompany { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public string QualificationSummary { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string? ResumeFilePath { get; set; }
    public List<InterviewListDto> Interviews { get; set; } = new();
    public List<OfferListDto> Offers { get; set; } = new();
}
