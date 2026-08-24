using HRMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;
namespace HRMS.Domain.Entities;

/// <summary>Department master record, scoped to a company.</summary>
public class Department : ICompanyOwned
{
    public int Id { get; set; }
    /// <summary>Domain-prefixed PK alias — maps to Id.</summary>
    [NotMapped] public int DepartmentId { get => Id; set => Id = value; }
    public int? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Designation (job title) master, scoped to a company.</summary>
public class Designation : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
