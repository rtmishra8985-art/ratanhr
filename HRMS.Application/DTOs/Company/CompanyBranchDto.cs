namespace HRMS.Application.DTOs.Company;

public class CompanyBranchDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? BranchManagerName { get; set; }
    public bool IsHeadOffice { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCompanyBranchDto
{
    public int CompanyId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? BranchManagerName { get; set; }
    public bool IsHeadOffice { get; set; }
}

public class CompanySettingsDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int WorkingDaysPerMonth { get; set; }
    public decimal PFPercentage { get; set; }
    public decimal ESIPercentage { get; set; }
    public decimal PTAmount { get; set; }
    public string? PayslipFooterNote { get; set; }
    public string? TimeZone { get; set; }
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }
    public int? OvertimeThresholdMinutes { get; set; }
}

public class UpsertCompanySettingsDto
{
    public int CompanyId { get; set; }
    public int WorkingDaysPerMonth { get; set; } = 26;
    public decimal PFPercentage { get; set; } = 12m;
    public decimal ESIPercentage { get; set; } = 0.75m;
    public decimal PTAmount { get; set; } = 200m;
    public string? PayslipFooterNote { get; set; }
    public string? TimeZone { get; set; }
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }
    public int? OvertimeThresholdMinutes { get; set; }
}
