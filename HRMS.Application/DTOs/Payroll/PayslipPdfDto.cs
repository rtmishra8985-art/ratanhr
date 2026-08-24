namespace HRMS.Application.DTOs.Payroll;

public class PayslipPdfDto
{
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string PayPeriod { get; set; } = string.Empty;  // e.g. "June 2025"
    public decimal BasicPay { get; set; }
    public decimal HRA { get; set; }
    public decimal DA { get; set; }
    public decimal Conveyance { get; set; }
    public decimal MedicalAllowance { get; set; }
    public decimal OtherAllowances { get; set; }
    public decimal GrossPay { get; set; }
    public decimal PFDeduction { get; set; }
    public decimal ESIDeduction { get; set; }
    public decimal PTDeduction { get; set; }
    public decimal TDSDeduction { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public int WorkingDays { get; set; }
    public int DaysPresent { get; set; }
}
