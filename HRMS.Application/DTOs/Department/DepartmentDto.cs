using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.DTOs.Department;

public class DepartmentDto
{
    public int Id { get; set; }

    /// <summary>Domain-prefixed PK alias — tests assert DepartmentId.</summary>
    public int DepartmentId { get => Id; set => Id = value; }

    public int? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDepartmentDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class DesignationDto
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDesignationDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
