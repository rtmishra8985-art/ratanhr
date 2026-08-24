namespace HRMS.Application.DTOs.Sales;

public class CreateFollowUpDto
{
    public int SalesLeadId { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime ReminderDate { get; set; }
    public string? ReminderTime { get; set; }
    /// <summary>Phone / WhatsApp / Email / Meeting</summary>
    public string Mode { get; set; } = "Phone";
    /// <summary>Pending / Completed</summary>
    public string Status { get; set; } = "Pending";
}

public class UpdateFollowUpDto : CreateFollowUpDto { }

public class FollowUpListDto
{
    public int Id { get; set; }
    public int SalesLeadId { get; set; }
    public string? LeadCompanyName { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime ReminderDate { get; set; }
    public string? ReminderTime { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
