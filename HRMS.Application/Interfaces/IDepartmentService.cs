using HRMS.Application.Common;
using HRMS.Application.DTOs.Department;

namespace HRMS.Application.Interfaces;

public interface IDepartmentService
{
    // Departments
    Task<List<DepartmentDto>> GetDepartmentsAsync(int? companyId);

    // FIX 5: Added sortBy / sortDirection for column-level sorting support.
    Task<PagedResult<DepartmentDto>> GetDepartmentsPagedAsync(
        int?    companyId,
        int     page,
        int     pageSize,
        string? sortBy        = null,
        string? sortDirection = "asc",
        string? search        = null);

    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check). Global records (CompanyId == null) are visible to all.</param>
    Task<DepartmentDto?> GetDepartmentByIdAsync(int id, int? callerCompanyId);
    Task<DepartmentDto> CreateDepartmentAsync(int? companyId, CreateDepartmentDto dto);
    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check). Global records may only be updated by SuperAdmin.</param>
    Task<bool> UpdateDepartmentAsync(int id, CreateDepartmentDto dto, int? callerCompanyId);
    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check). Global records may only be deleted by SuperAdmin.</param>
    Task<bool> DeleteDepartmentAsync(int id, int? callerCompanyId);

    // Designations
    Task<List<DesignationDto>> GetDesignationsAsync(int? companyId);

    // FIX 5: Added sortBy / sortDirection.
    Task<PagedResult<DesignationDto>> GetDesignationsPagedAsync(
        int?    companyId,
        int     page,
        int     pageSize,
        string? sortBy        = null,
        string? sortDirection = "asc",
        string? search        = null);

    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check). Global records (CompanyId == null) are visible to all.</param>
    Task<DesignationDto?> GetDesignationByIdAsync(int id, int? callerCompanyId);
    Task<DesignationDto> CreateDesignationAsync(int? companyId, CreateDesignationDto dto);
    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check).</param>
    Task<bool> UpdateDesignationAsync(int id, CreateDesignationDto dto, int? callerCompanyId);
    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check).</param>
    Task<bool> DeleteDesignationAsync(int id, int? callerCompanyId);
}
