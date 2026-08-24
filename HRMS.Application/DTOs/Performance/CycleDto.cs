namespace HRMS.Application.DTOs.Performance;

public record CreateCycleDto(
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string ReviewType
);

public record UpdateCycleDto(
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string ReviewType,
    string Status
);

public class CycleListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ReviewType { get; set; } = string.Empty;
    public int TotalReviews { get; set; }
    public int TotalGoals { get; set; }
    public DateTime CreatedAt { get; set; }
}
