namespace HRMS.Domain.Entities.Employee;

/// <summary>
/// Stores multiple bank account details for employee (salary, reimbursement, etc.)
/// </summary>
public class BankAccountDetail
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    
    public string AccountHolderName { get; set; } = string.Empty;
    
    /// <summary>Bank account number (encrypted)</summary>
    public string AccountNumber { get; set; } = string.Empty;
    
    /// <summary>IFSC code (encrypted)</summary>
    public string IFSCCode { get; set; } = string.Empty;
    
    /// <summary>Account type: Salary, Personal, Joint</summary>
    public string? AccountType { get; set; }
    
    /// <summary>Primary account for salary transfer</summary>
    public bool IsPrimary { get; set; } = true;
    
    /// <summary>Bank verified this account</summary>
    public bool IsVerified { get; set; } = false;
    
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
