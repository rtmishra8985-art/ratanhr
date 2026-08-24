using HRMS.Application.DTOs.Analytics;

namespace HRMS.Application.Interfaces;

public interface IAnalyticsService
{
    Task<HeadcountAnalyticsDto> GetHeadcountAsync(int companyId, int year);
    Task<AttendanceAnalyticsDto> GetAttendanceSummaryAsync(int companyId, string period);
    Task<PayrollAnalyticsDto> GetPayrollSummaryAsync(int companyId, int year);
    Task<TurnoverAnalyticsDto> GetTurnoverAsync(int companyId, int year);
}
