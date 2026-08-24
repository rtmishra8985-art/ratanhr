namespace HRMS.Application.DTOs.Payroll;

/// <summary>Body for payroll period lock/unlock endpoints.</summary>
public class PayrollPeriodActionDto
{
    public int     CompanyId { get; set; }
    public int     Month     { get; set; }
    public int     Year      { get; set; }
    public string? Notes     { get; set; }
}
