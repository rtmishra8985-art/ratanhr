namespace HRMS.Domain.Entities.Employee;

/// <summary>
/// Stores emergency contact information for employees
/// </summary>
public class EmergencyContact
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    
    public string ContactName { get; set; } = string.Empty;
    
    /// <summary>Relationship: Spouse, Parent, Child, Sibling, Friend, Other</summary>
    public string? Relationship { get; set; }
    
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    
    /// <summary>Priority order: 1=Primary, 2=Secondary, 3=Tertiary</summary>
    public int Priority { get; set; } = 1;
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
