namespace HRMS.Application.DTOs.Sales;

public class CreateMeetingDto
{
    public int? SalesLeadId { get; set; }
    public int? SalesCustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime MeetingDate { get; set; }
    public string MeetingTime { get; set; } = "09:00";
    public string Location { get; set; } = string.Empty;
    public string? GoogleMapUrl { get; set; }
    /// <summary>Online / Offline</summary>
    public string MeetingType { get; set; } = "Offline";
    public string Outcome { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    /// <summary>Scheduled / Completed / Cancelled</summary>
    public string Status { get; set; } = "Scheduled";
}

public class UpdateMeetingDto : CreateMeetingDto { }

public class MeetingListDto
{
    public int Id { get; set; }
    public int? SalesLeadId { get; set; }
    public string? LeadCompanyName { get; set; }
    public int? SalesCustomerId { get; set; }
    public string? CustomerCompanyName { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime MeetingDate { get; set; }
    public string MeetingTime { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string MeetingType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MeetingDetailDto : MeetingListDto
{
    public string? GoogleMapUrl { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
