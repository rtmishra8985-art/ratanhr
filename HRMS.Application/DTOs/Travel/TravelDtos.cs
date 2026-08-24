namespace HRMS.Application.DTOs.Travel;

// ── Create / Update ────────────────────────────────────────────────────────────

public class CreateTravelDto
{
    /// <summary>Local | Domestic | International</summary>
    public string TravelType { get; set; } = "Domestic";
    public string Purpose { get; set; } = string.Empty;
    public string FromCity { get; set; } = string.Empty;
    public string ToCity { get; set; } = string.Empty;

    /// <summary>Alias for ToCity — tests and the TravelRequest entity use Destination.</summary>
    public string Destination { get => ToCity; set => ToCity = value; }

    public DateTime StartDate { get; set; }

    /// <summary>Alias for StartDate — tests use DepartureDate.</summary>
    public DateTime DepartureDate { get => StartDate; set => StartDate = value; }

    public DateTime EndDate { get; set; }

    /// <summary>Alias for EndDate — tests use ReturnDate.</summary>
    public DateTime ReturnDate { get => EndDate; set => EndDate = value; }

    /// <summary>Flight | Train | Bus | Car | Cab | Ship | Other</summary>
    public string ModeOfTravel { get; set; } = "Flight";
    public bool AdvanceRequired { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }
}

public class UpdateTravelDto : CreateTravelDto { }

// ── Read ───────────────────────────────────────────────────────────────────────

public class TravelDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string TravelType { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string FromCity { get; set; } = string.Empty;
    public string ToCity { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ModeOfTravel { get; set; } = string.Empty;
    public bool AdvanceRequired { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal EstimatedCost { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<TravelApprovalDto> Approvals { get; set; } = new();
    public List<TravelHistoryDto> History { get; set; } = new();
}

public class TravelApprovalDto
{
    public int Id { get; set; }
    public string Step { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ApproverName { get; set; }
    public string? Comments { get; set; }
    public DateTime? ActionAt { get; set; }
}

public class TravelHistoryDto
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? PerformedByName { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Actions ────────────────────────────────────────────────────────────────────

public class TravelDecisionDto
{
    /// <summary>Manager | HR | Finance</summary>
    public string Step { get; set; } = "Manager";
    public bool Approve { get; set; }
    public bool SendBack { get; set; }
    public string? Comments { get; set; }
}

// ── Dashboard ──────────────────────────────────────────────────────────────────

public class TravelDashboardDto
{
    public int TotalRequests { get; set; }
    public int PendingApproval { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Completed { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public decimal CurrentMonthCost { get; set; }
    public List<TravelMonthlyStatDto> MonthlyTrend { get; set; } = new();
    public List<TravelTypeStatDto> ByType { get; set; } = new();
}

public class TravelMonthlyStatDto
{
    public string Month { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal EstimatedCost { get; set; }
}

public class TravelTypeStatDto
{
    public string TravelType { get; set; } = string.Empty;
    public int Count { get; set; }
}

// ── Reports ───────────────────────────────────────────────────────────────────

public class TravelReportFilterDto
{
    public string? Status { get; set; }
    public string? TravelType { get; set; }
    public string? EmployeeId { get; set; }
    public int? DepartmentId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
