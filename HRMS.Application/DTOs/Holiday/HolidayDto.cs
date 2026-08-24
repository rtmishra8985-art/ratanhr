using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.DTOs.Holiday;

public class HolidayDto
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsOptional { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateHolidayDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Date { get; set; } = string.Empty;   // yyyy-MM-dd

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsOptional { get; set; } = false;
}
