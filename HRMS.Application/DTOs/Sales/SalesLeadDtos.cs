namespace HRMS.Application.DTOs.Sales;

public class CreateLeadDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string LeadSource { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string? EmployeeOwnerId { get; set; }
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "New";
    public string Remarks { get; set; } = string.Empty;
    public decimal? ExpectedValue { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
}

public class UpdateLeadDto : CreateLeadDto { }

public record UpdateLeadStatusDto(string Status);

public class LeadListDto
{
    public int Id { get; set; }
    public string LeadNo { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string LeadSource { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string? EmployeeOwnerId { get; set; }
    public string? OwnerName { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? ExpectedValue { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LeadDetailDto : LeadListDto
{
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
