namespace HRMS.Application.DTOs.Sales;

public class CreateQuotationDto
{
    public int? SalesLeadId { get; set; }
    public int? SalesCustomerId { get; set; }
    public decimal Amount { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    /// <summary>Draft / Sent / Accepted / Rejected</summary>
    public string Status { get; set; } = "Draft";
    public DateTime? ValidUntil { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class UpdateQuotationDto : CreateQuotationDto { }

public record UpdateQuotationStatusDto(string Status);

public class QuotationListDto
{
    public int Id { get; set; }
    public string QuotationNumber { get; set; } = string.Empty;
    public int? SalesLeadId { get; set; }
    public string? LeadCompanyName { get; set; }
    public int? SalesCustomerId { get; set; }
    public string? CustomerCompanyName { get; set; }
    public decimal Amount { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}
