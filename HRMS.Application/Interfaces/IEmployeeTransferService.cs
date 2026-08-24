using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;

namespace HRMS.Application.Interfaces;

public interface IEmployeeTransferService
{
    Task<List<EmployeeTransferDto>> GetTransfersAsync(string employeeId);
    Task<PagedResult<EmployeeTransferDto>> GetTransfersPagedAsync(string employeeId, int page, int pageSize);
    Task<int> CreateTransferAsync(CreateTransferDto dto);
    Task<bool> ApproveTransferAsync(int transferId, int approvedByUserId, int? companyId = null);
    Task<bool> RejectTransferAsync(int transferId, int? companyId = null);
}
