namespace HRMS.Application.DTOs.Analytics;

public class HeadcountAnalyticsDto
{
    public int TotalEmployees { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }
    public Dictionary<string, int> ByDepartment { get; set; } = new();
}

public class AttendanceAnalyticsDto
{
    public string Period { get; set; } = string.Empty;
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int LeaveDays { get; set; }
    public decimal AttendancePercent { get; set; }
}

public class MonthlyPayrollSummary
{
    public string Month { get; set; } = string.Empty;
    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public int EmployeeCount { get; set; }
}

public class PayrollAnalyticsDto
{
    public int Year { get; set; }
    public List<MonthlyPayrollSummary> Monthly { get; set; } = new();
}

public class TurnoverAnalyticsDto
{
    public int Year { get; set; }
    public int JoinedCount { get; set; }
    public int ExitedCount { get; set; }
    public decimal TurnoverRate { get; set; }
}
