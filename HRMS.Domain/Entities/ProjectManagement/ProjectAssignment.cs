namespace HRMS.Domain.Entities.ProjectManagement;

/// <summary>
/// Tracks employee assignment to projects and allocation percentage
/// </summary>
public class ProjectAssignment
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? ProjectCode { get; set; }
    
    /// <summary>Employee ID of person assigned to project</summary>
    public string AssignedEmployeeId { get; set; } = string.Empty;
    
    /// <summary>Role on the project: Developer, Manager, Tester, Designer, etc.</summary>
    public string? Role { get; set; }
    
    /// <summary>Allocation percentage (0-100)</summary>
    public int AllocationPercentage { get; set; } = 100;
    
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    /// <summary>Status: Assigned, InProgress, OnHold, Completed</summary>
    public string Status { get; set; } = "Assigned";
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
