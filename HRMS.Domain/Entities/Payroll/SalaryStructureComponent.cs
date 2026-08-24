namespace HRMS.Domain.Entities.Payroll;

/// <summary>
/// Breaks down salary structure into individual components (Basic, HRA, DA, etc.)
/// </summary>
public class SalaryStructureComponent
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int SalaryStructureId { get; set; }
    
    /// <summary>Component type: Basic, HRA, DA, Conveyance, Allowance, Bonus, Deduction, Tax, etc.</summary>
    public string ComponentType { get; set; } = string.Empty;
    
    public string ComponentName { get; set; } = string.Empty;
    
    /// <summary>Fixed component value</summary>
    public decimal? ComponentValue { get; set; }
    
    /// <summary>Value type: Fixed, Percentage, Formula</summary>
    public string ValueType { get; set; } = "Fixed";
    
    /// <summary>Formula expression for calculated components (e.g., "BasicSalary * 0.5")</summary>
    public string? FormulaExpression { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    /// <summary>Order of display/calculation</summary>
    public int? DisplayOrder { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
