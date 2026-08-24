namespace HRMS.Application.DTOs.Sales;

public class CreateSalesTaskDto
{
    public int? SalesLeadId { get; set; }
    public int? SalesCustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AssignedToEmployeeId { get; set; }
    /// <summary>Low / Medium / High / Critical</summary>
    public string Priority { get; set; } = "Medium";
    /// <summary>Pending / In Progress / Completed / Cancelled</summary>
    public string Status { get; set; } = "Pending";
    public DateTime? Deadline { get; set; }
    public DateTime? ReminderDate { get; set; }
}

public class UpdateSalesTaskDto : CreateSalesTaskDto { }

public record UpdateTaskStatusDto(string Status);

public class SalesTaskListDto
{
    public int Id { get; set; }
    public int? SalesLeadId { get; set; }
    public string? LeadCompanyName { get; set; }
    public int? SalesCustomerId { get; set; }
    public string? CustomerCompanyName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AssignedToEmployeeId { get; set; }
    public string? AssigneeName { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }
    public DateTime? ReminderDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
