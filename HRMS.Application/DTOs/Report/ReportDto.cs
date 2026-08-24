using HRMS.Application.DTOs.Payroll;

namespace HRMS.Application.DTOs.Report;

public class AttendanceReportFilterDto
{
    public int? CompanyId { get; set; }
    public int? Month { get; set; }
    public int? Year { get; set; }
    public string? EmployeeId { get; set; }
    // Date range filter (yyyy-MM-dd strings)
    public string? From { get; set; }
    public string? To { get; set; }
    // Legacy fields kept for backward compat
    public string? Department { get; set; }
    public string? AttendanceType { get; set; }
}

public class AttendanceReportItemDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string? Department { get; set; }
    // Per-record fields (used in detailed/daily report mode)
    public string? Date { get; set; }
    public string? Status { get; set; }
    public string? Source { get; set; }
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
    public decimal? HoursWorked { get; set; }
    // Aggregate fields (used in summary mode)
    public int Present { get; set; }
    public int Absent { get; set; }
    public int WorkingDays { get; set; }
    // Legacy aliases
    public int TotalDays { get => WorkingDays; set => WorkingDays = value; }
    public int PresentDays { get => Present; set => Present = value; }
    public int AbsentDays { get => Absent; set => Absent = value; }
    public int HalfDays { get; set; }
    public decimal AttendancePercent { get; set; }
}

public class EmployeeReportItemDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public DateOnly? DateOfJoining { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Combined dashboard stats — covers both admin and superadmin dashboards.</summary>
public class DashboardStatsDto
{
    public int TotalEmployees { get; set; }
    public int TotalCompanies { get; set; }
    public int ActiveAdmins { get; set; }
    public int PendingLeaves { get; set; }
    // Legacy field aliases
    public int ActiveEmployees { get; set; }
    public int TodayPresent { get; set; }
    public int PresentToday { get => TodayPresent; set => TodayPresent = value; }
    public int TodayAbsent { get; set; }
    public int AbsentToday { get => TodayAbsent; set => TodayAbsent = value; }
    public int PayslipsThisMonth { get; set; }
    public decimal TotalPayrollThisMonth { get; set; }
    public decimal PayrollThisMonth { get => TotalPayrollThisMonth; set => TotalPayrollThisMonth = value; }
}

public class MonthlyAttendanceReportDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string? Department { get; set; }
    public int WorkingDays { get; set; }
    public int DaysPresent { get; set; }
    public int DaysAbsent { get; set; }
    public int LateCount { get; set; }
    public decimal AttendancePercent { get; set; }
    public decimal AttendancePct { get => AttendancePercent; set => AttendancePercent = value; }
}

public class DailyAttendanceReportDto
{
    public string Date { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
    public decimal? HoursWorked { get; set; }
}

public class GroupCount
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class EmployeeSummaryItemDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? DateOfJoining { get; set; }
    public bool IsActive { get; set; }
}

public class PayrollReportItemDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public decimal PFEmployee { get; set; }
    public decimal PFEmployer { get; set; }
    public decimal ESI { get; set; }
    public decimal PT { get; set; }
    public decimal TDS { get; set; }
}

public class EmployeeSummaryReportDto
{
    public int TotalEmployees { get; set; }
    public int ActiveEmployees { get; set; }
    public int InactiveEmployees { get; set; }
    public List<GroupCount> ByDepartment { get; set; } = new();
    public List<GroupCount> ByDesignation { get; set; } = new();
    public List<GroupCount> ByGender { get; set; } = new();
    public List<EmployeeSummaryItemDto> Details { get; set; } = new();
}

public class DepartmentCountDto
{
    public string Department { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class GenderCountDto
{
    public string Gender { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class PayrollReportDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int EmployeeCount { get; set; }
    public decimal TotalGrossEarnings { get; set; }
    public decimal TotalGross { get => TotalGrossEarnings; set => TotalGrossEarnings = value; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNetPay { get; set; }
    public decimal TotalPFEmployee { get; set; }
    public decimal TotalPFEmployer { get; set; }
    public decimal TotalPFContribution { get => TotalPFEmployee + TotalPFEmployer; }
    public decimal TotalESI { get; set; }
    public decimal TotalPT { get; set; }
    public decimal TotalTDS { get; set; }
    public List<PayrollReportItemDto> Items { get; set; } = new();

    /// <summary>
    /// BUG FIX: previously `Items.Select(i => new PayslipListDto())` discarded every field
    /// from the source item `i`, returning a list of blank/default PayslipListDto objects
    /// (EmployeeId = "", all amounts = 0) instead of the actual payroll data. Any caller of
    /// this legacy alias would have silently received zeroed-out records. Now maps the real
    /// fields from each PayrollReportItemDto (Month/Year come from the report header since
    /// PayrollReportItemDto itself doesn't carry them).
    /// </summary>
    public List<PayslipListDto> Details => Items.Select(i => new PayslipListDto
    {
        EmployeeId = i.EmployeeId,
        Month = Month,
        Year = Year,
        GrossEarnings = i.GrossEarnings,
        TotalDeductions = i.TotalDeductions,
        NetPay = i.NetPay,
    }).ToList();
}

public class DashboardKpiDto
{
    public int TotalEmployees { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int OnLeaveToday { get; set; }
    public int PendingLeaves { get; set; }
    public decimal PayrollThisMonth { get; set; }
    public int NewJoineesThisMonth { get; set; }
    public int TotalCompanies { get; set; }
    public int ActiveAdmins { get; set; }
}

public class EmployeeReportFilterDto
{
    public int? CompanyId { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? Gender { get; set; }
    public string? Status { get; set; } // "active" | "inactive"
}
