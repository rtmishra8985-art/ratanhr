namespace HRMS.Application.DTOs.Report;

public class LeaveReportDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int? CompanyId { get; set; }
    public int TotalRequests { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Pending { get; set; }
    public int TotalDaysApproved { get; set; }
    public List<LeaveReportItemDto> Details { get; set; } = new();
}

public class LeaveReportItemDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string? LeaveTypeName { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public int TotalDays { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class SalaryRegisterDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int EmployeeCount { get; set; }
    public decimal TotalCTC { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalPFEmployee { get; set; }
    public decimal TotalPFEmployer { get; set; }
    public decimal TotalESI { get; set; }
    public decimal TotalPT { get; set; }
    public decimal TotalTDS { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNetPay { get; set; }
    public List<SalaryRegisterItemDto> Rows { get; set; } = new();
}

public class SalaryRegisterItemDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? UAN { get; set; }
    public int DaysPresent { get; set; }
    public int WorkingDays { get; set; }
    public decimal BasicPay { get; set; }
    public decimal HRA { get; set; }
    public decimal DA { get; set; }
    public decimal Conveyance { get; set; }
    public decimal MedicalAllowance { get; set; }
    public decimal OtherAllowances { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal PFEmployee { get; set; }
    public decimal PFEmployer { get; set; }
    public decimal ESI { get; set; }
    public decimal PT { get; set; }
    public decimal TDS { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
}

public class EmployeeDashboardStatsDto
{
    public string? EmployeeId { get; set; }
    public string? FullName { get; set; }
    public int PendingLeaves { get; set; }
    public int ApprovedLeavesThisMonth { get; set; }
    public int TotalLeavesUsedThisYear { get; set; }
    public bool CheckedInToday { get; set; }
    public string? TodayCheckInTime { get; set; }
    public string? TodayCheckOutTime { get; set; }
    public decimal? HoursWorkedToday { get; set; }
    public int AttendanceDaysThisMonth { get; set; }
    public int WorkingDaysThisMonth { get; set; }
    public decimal? LastNetPay { get; set; }
    public string? LastPayMonth { get; set; }
    public int UpcomingHolidays { get; set; }
}
