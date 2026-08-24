namespace HRMS.Application.DTOs.Sales;

public class CheckInDto
{
    public int? SalesLeadId { get; set; }
    public int? SalesCustomerId { get; set; }
    public string VisitedEmployeeId { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
}

public class CheckOutDto
{
    public decimal? DistanceKm { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class VisitListDto
{
    public int Id { get; set; }
    public int? SalesLeadId { get; set; }
    public string? LeadCompanyName { get; set; }
    public int? SalesCustomerId { get; set; }
    public string? CustomerCompanyName { get; set; }
    public string VisitedEmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string CheckInAddress { get; set; } = string.Empty;
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal? DistanceKm { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
