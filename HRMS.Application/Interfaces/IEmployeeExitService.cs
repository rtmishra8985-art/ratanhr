using HRMS.Application.DTOs.Employee;

namespace HRMS.Application.Interfaces;

public interface IEmployeeExitService
{
    Task<EmployeeExitDto?> GetExitAsync(string employeeId);
    Task<int> InitiateExitAsync(InitiateExitDto dto);
    Task<bool> CompleteExitAsync(int exitId, CompleteExitDto dto, int? companyId = null);
}
