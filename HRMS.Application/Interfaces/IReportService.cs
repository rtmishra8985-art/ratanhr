using HRMS.Application.DTOs.Report;

namespace HRMS.Application.Interfaces;

public interface IReportService
{
    // ── Existing ──────────────────────────────────────────────────────────
    Task<List<AttendanceReportItemDto>> GetAttendanceReportAsync(AttendanceReportFilterDto filter);
    Task<DashboardStatsDto>            GetAdminDashboardStatsAsync(int? companyId);
    Task<DashboardStatsDto>            GetSuperAdminDashboardStatsAsync();

    // ── Attendance Reports ────────────────────────────────────────────────
    Task<List<MonthlyAttendanceReportDto>> GetMonthlyAttendanceReportAsync(int? companyId, int month, int year);
    Task<List<DailyAttendanceReportDto>>   GetDailyAttendanceReportAsync(int? companyId, DateOnly from, DateOnly to);
    Task<byte[]>                           ExportAttendanceReportAsync(int? companyId, int month, int year);

    // ── Employee Reports ──────────────────────────────────────────────────
    Task<EmployeeSummaryReportDto> GetEmployeeSummaryReportAsync(int? companyId);
    Task<byte[]>                   ExportEmployeeReportAsync(int? companyId);

    // ── Payroll Reports ───────────────────────────────────────────────────
    Task<PayrollReportDto> GetPayrollReportAsync(int? companyId, int month, int year);
    Task<byte[]>           ExportPayrollReportAsync(int? companyId, int month, int year);

    // ── Salary Register ───────────────────────────────────────────────────
    Task<SalaryRegisterDto> GetSalaryRegisterAsync(int? companyId, int month, int year);
    Task<byte[]>            ExportSalaryRegisterAsync(int? companyId, int month, int year);

    // ── Leave Reports ─────────────────────────────────────────────────────
    Task<LeaveReportDto> GetLeaveReportAsync(int? companyId, int month, int year);
    Task<byte[]>         ExportLeaveReportAsync(int? companyId, int month, int year);

    // ── Dashboard KPIs ────────────────────────────────────────────────────
    Task<DashboardKpiDto>           GetDashboardKpisAsync(int? companyId);
    Task<EmployeeDashboardStatsDto> GetEmployeeDashboardStatsAsync(string employeeId, int? companyId);
}
