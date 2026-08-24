using HRMS.Application.DTOs.Payroll;

namespace HRMS.Application.Interfaces;

public interface ISalaryStructureService
{
    Task<SalaryStructureDto?> GetActiveAsync(string employeeId);
    // FIX MEDIUM: Added pagination to prevent unbounded loads for long-tenured employees.
    Task<List<SalaryStructureDto>> GetHistoryAsync(string employeeId, int pageNumber = 1, int pageSize = 25);
    Task<int> UpsertAsync(CreateSalaryStructureDto dto);
}
