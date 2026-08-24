using HRMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Domain.Entities.Sales;

public class SalesCustomer : ICompanyOwned
{
    public int Id { get; set; }
    /// <summary>Domain-prefixed PK alias — maps to Id.</summary>
    [NotMapped] public int CustomerId { get => Id; set => Id = value; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int? BranchId { get; set; }

    /// <summary>Auto-generated customer code, e.g. CUST-0001.</summary>
    public string CustomerCode { get; set; } = string.Empty;

    public string Gst { get; set; } = string.Empty;
    public string Pan { get; set; } = string.Empty;

    /// <summary>Customer company/organisation name.</summary>
    public string CompanyName { get; set; } = string.Empty;

    public string BillingAddress { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;

    public string ContactPerson { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>Employee assigned as account manager / sales person.</summary>
    public string? AssignedSalesPersonId { get; set; }

    /// <summary>Source lead that was converted to create this customer.</summary>
    public int? SalesLeadId { get; set; }

    public bool IsActive { get; set; } = true;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}
