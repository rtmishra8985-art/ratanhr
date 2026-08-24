namespace HRMS.Domain.Entities.Expense;

/// <summary>
/// Defines company expense policies and approval rules
/// </summary>
public class ExpensePolicy
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    /// <summary>Category: Travel, Meals, Transport, Accommodation, Office, Medical, Other</summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>Maximum amount per transaction in local currency</summary>
    public decimal? MaxAmountPerTransaction { get; set; }
    
    /// <summary>Maximum amount per month in local currency</summary>
    public decimal? MaxAmountPerMonth { get; set; }
    
    /// <summary>Whether this expense requires approval before reimbursement</summary>
    public bool RequiresApproval { get; set; } = true;
    
    /// <summary>Approval level: 1=Manager, 2=Director, 3=Finance, 4=CEO</summary>
    public int? ApproverLevel { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
