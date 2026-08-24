using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.DTOs.Company;

public class CreateCompanyDto
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Company name is required.")]
    [MaxLength(200, ErrorMessage = "Company name must not exceed 200 characters.")]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CompanyFounderName { get; set; }

    [MaxLength(20)]
    [Phone(ErrorMessage = "Phone number format is invalid.")]
    public string? PhoneNumber { get; set; }

    [MaxLength(254)]
    [EmailAddress(ErrorMessage = "Email address format is invalid.")]
    public string? EmailAddress { get; set; }

    [MaxLength(100)]
    public string? IndustryType { get; set; }

    [MaxLength(100)]
    public string? BusinessType { get; set; }

    /// <summary>Corporate Identification Number — 21-character alphanumeric.</summary>
    [MaxLength(21)]
    public string? CIN { get; set; }

    [MaxLength(20)]
    public string? TIN { get; set; }

    /// <summary>Permanent Account Number — 10-character alphanumeric.</summary>
    [MaxLength(10)]
    public string? PAN { get; set; }

    /// <summary>Tax Deduction and Collection Account Number — 10-character alphanumeric.</summary>
    [MaxLength(10)]
    public string? TAN { get; set; }

    [MaxLength(255)]
    public string? AddressLine1 { get; set; }

    [MaxLength(255)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? StateProvince { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; } = "India";

    [MaxLength(20)]
    public string? PostalCode { get; set; }
}

public class CompanyDto : CreateCompanyDto
{
    public int Id { get; set; }
    public string? LogoPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public int EmployeeCount { get; set; }
}
