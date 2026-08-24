namespace HRMS.Application.DTOs.Performance;

public record CreateGoalDto(string EmployeeId, int? PerformanceCycleId, string Title, string Description, string GoalType, string Category, decimal TargetValue, string Unit, DateTime DueDate, int Weight);

public record UpdateGoalDto(string Title, string Description, string GoalType, string Category, decimal TargetValue, string Unit, DateTime DueDate, int Weight, string Status);

public record UpdateGoalProgressDto(decimal AchievedValue);

public class GoalListDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string? CycleName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string GoalType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public decimal? AchievedValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Weight { get; set; }
    public decimal ProgressPercent => TargetValue > 0 && AchievedValue.HasValue
        ? Math.Min(100, Math.Round(AchievedValue.Value / TargetValue * 100, 1))
        : 0;
    public DateTime CreatedAt { get; set; }
}

public class PagedGoalsDto
{
    public List<GoalListDto> Items    { get; set; } = [];
    public int TotalCount             { get; set; }
    public int Page                   { get; set; }
    public int PageSize               { get; set; }
    public int TotalPages             => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage       => Page > 1;
    public bool HasNextPage           => Page < TotalPages;

    /// <summary>
    /// The column that was sorted on (echoed from the request).
    /// Null when the default sort was applied.
    /// </summary>
    public string? SortBy             { get; set; }

    /// <summary>
    /// The sort direction applied: "asc" or "desc" (echoed from the request).
    /// </summary>
    public string? SortDirection      { get; set; }
}
