namespace HRMS.Application.DTOs.Performance;

public record CreateReviewDto(
    string EmployeeId,
    int ReviewerId,
    int? PerformanceCycleId,
    string ReviewType
);

public record SubmitSelfReviewDto(
    decimal SelfRating,
    string SelfComments,
    string OverallComments
);

public record SubmitManagerReviewDto(
    decimal ManagerRating,
    string ManagerComments
);

public record FinalizeReviewDto(
    decimal FinalRating,
    string HrComments
);

public class ReviewListDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string? CycleName { get; set; }
    public string ReviewType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? SelfRating { get; set; }
    public decimal? ManagerRating { get; set; }
    public decimal? FinalRating { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReviewDetailDto : ReviewListDto
{
    public string SelfComments { get; set; } = string.Empty;
    public string ManagerComments { get; set; } = string.Empty;
    public string HrComments { get; set; } = string.Empty;
    public string OverallComments { get; set; } = string.Empty;
    public DateTime? AcknowledgedAt { get; set; }
}
