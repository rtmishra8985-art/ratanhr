namespace HRMS.Application.DTOs.Sales;

public class CreateCustomerDto
{
    public string Gst { get; set; } = string.Empty;
    public string Pan { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string BillingAddress { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? AssignedSalesPersonId { get; set; }
    public int? SalesLeadId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateCustomerDto : CreateCustomerDto { }

public class CustomerListDto
{
    public int Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? AssignedSalesPersonId { get; set; }
    public string? SalesPersonName { get; set; }
    public int? SalesLeadId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerDetailDto : CustomerListDto
{
    public string Gst { get; set; } = string.Empty;
    public string Pan { get; set; } = string.Empty;
    public string BillingAddress { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
