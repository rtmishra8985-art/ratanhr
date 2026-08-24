using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Domain.Entities.Company;

public class Company
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    /// <summary>Alias for CompanyName — tests use Name.</summary>
    [NotMapped] public string Name { get => CompanyName; set => CompanyName = value; }
    public string? CompanyFounderName { get; set; }
    public string? PhoneNumber { get; set; }
    /// <summary>Alias for PhoneNumber — tests use Phone.</summary>
    [NotMapped] public string? Phone { get => PhoneNumber; set => PhoneNumber = value; }
    public string? EmailAddress { get; set; }
    /// <summary>Alias for EmailAddress — tests use Email.</summary>
    [NotMapped] public string? Email { get => EmailAddress; set => EmailAddress = value; }
    public string? IndustryType { get; set; }
    public string? BusinessType { get; set; }
    public string? CIN { get; set; }
    public string? TIN { get; set; }
    public string? PAN { get; set; }
    public string? TAN { get; set; }
    public string? AddressLine1 { get; set; }
    /// <summary>Alias for AddressLine1 — tests use Address.</summary>
    [NotMapped] public string? Address { get => AddressLine1; set => AddressLine1 = value; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? LogoPath { get; set; }
    /// <summary>Maximum number of employees allowed. Tests use MaxEmployees.</summary>
    public int? MaxEmployees { get; set; }
    /// <summary>Soft-delete / active flag.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>Demo company marker — true indicates this is a test/demo company for seed operations.</summary>
    public bool IsDemo { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CompanyBranch> Branches { get; set; } = new List<CompanyBranch>();
    /// <summary>Domain-prefixed PK alias — maps to Id.</summary>
    [NotMapped] public int CompanyId { get => Id; set => Id = value; }
}
