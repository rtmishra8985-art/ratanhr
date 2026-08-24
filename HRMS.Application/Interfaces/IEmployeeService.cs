using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Domain.Entities.Employee;
using Microsoft.AspNetCore.Http;

namespace HRMS.Application.Interfaces;

public interface IEmployeeService
{
    // ── Legacy methods (file-upload based) ───────────────────────────────
    Task<(string employeeId, string tempPassword)> CreateAsync(CreateEmployeeDto dto, IFormFileCollection files);
    Task<bool> UpdateAsync(string employeeId, CreateEmployeeDto dto, IFormFileCollection files, int? companyId = null);
    Task<bool> UpdateStatusAsync(string employeeId, bool isActive, int? companyId = null);
    Task<EmployeeDetailDto?> GetByIdAsync(string employeeId, int? companyId = null);
    Task<EmployeePiiDto?> GetPiiAsync(string employeeId, int? companyId = null, bool includeRaw = false);
    Task<List<EmployeeListDto>> GetAllAsync(int? companyId = null);
    Task<PagedResult<EmployeeListDto>> GetAllPagedAsync(
        int?    companyId,
        int     page,
        int     pageSize,
        string? sortBy        = null,
        string? sortDirection = "asc",
        string? search        = null,
        string? status        = null,
        string? department    = null,
        string? designation   = null);
    Task<bool> DeleteAsync(string employeeId, int? companyId = null);

    // ── New test-aligned methods (int ID based) ───────────────────────────
    /// <summary>Get all employees for a company, with optional status filter and cancellation.</summary>
    Task<List<Employee>> GetAllEmployeesAsync(int companyId, string? status = null, CancellationToken ct = default);

    /// <summary>Get a single employee by int PK, scoped to company.</summary>
    Task<Employee?> GetEmployeeByIdAsync(int id, int companyId, CancellationToken ct = default);

    /// <summary>Create an employee using the simplified DTO; returns new int PK.</summary>
    Task<int> CreateEmployeeAsync(int companyId, CreateEmployeeDto dto, CancellationToken ct = default);

    /// <summary>Paged employee list for a company.</summary>
    Task<PagedResult<Employee>> GetEmployeesPagedAsync(int companyId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Update employee fields by int PK, scoped to company. Returns false if not found or cross-tenant.</summary>
    Task<bool> UpdateEmployeeAsync(int id, int companyId, UpdateEmployeeDto dto);

    /// <summary>Soft-delete an employee by int PK, scoped to company. Returns false if not found or cross-tenant.</summary>
    Task<bool> DeleteEmployeeAsync(int id, int companyId);
}
