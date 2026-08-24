using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Sales;

public class SalesQuotation : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int? BranchId { get; set; }

    /// <summary>Auto-generated quotation number, e.g. QT-0001.</summary>
    public string QuotationNumber { get; set; } = string.Empty;

    public int? SalesLeadId { get; set; }
    public int? SalesCustomerId { get; set; }

    public decimal Amount { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Draft / Sent / Accepted / Rejected</summary>
    public string Status { get; set; } = "Draft";

    public DateTime? ValidUntil { get; set; }
    public string Notes { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}
