using HRMS.Application.Common;
using HRMS.Application.DTOs.Timesheet;

namespace HRMS.Application.Interfaces;

public interface ITimesheetService
{
    Task<PagedResult<TimesheetEntryDto>> GetByEmployeeAsync(string employeeId, int companyId, PaginationQuery q);
    Task<PagedResult<TimesheetEntryDto>> GetPendingApprovalsAsync(int companyId, PaginationQuery q);
    Task<TimesheetEntryDto> CreateAsync(CreateTimesheetDto dto, int companyId);
    Task<TimesheetEntryDto> UpdateAsync(int id, CreateTimesheetDto dto, string employeeId);
    Task SubmitAsync(int id, string employeeId);
    // FIX: companyId added so services scope approval/rejection to the caller's tenant
    Task ApproveAsync(int id, int approverUserId, int companyId, string? remarks);
    Task RejectAsync(int id, int approverUserId, int companyId, string remarks);
    Task DeleteAsync(int id, string employeeId);
}
