using HRMS.Application.Common;
using HRMS.Application.DTOs.Holiday;

namespace HRMS.Application.Interfaces;

public interface IHolidayService
{
    Task<List<HolidayDto>> GetAllAsync(int? companyId, int? year);
    Task<PagedResult<HolidayDto>> GetAllPagedAsync(
        int? companyId,
        int? year,
        int page,
        int pageSize,
        string? search = null,
        bool? isOptional = null,
        string? sortBy = null,
        string? sortDirection = "asc");
    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check). Global holidays are always visible.</param>
    Task<HolidayDto?> GetByIdAsync(int id, int? callerCompanyId);
    Task<HolidayDto> CreateAsync(int? companyId, CreateHolidayDto dto);
    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check).</param>
    /// <param name="isSuperAdmin">True when the caller has the superadmin role. Global records may only be modified by SuperAdmin.</param>
    Task<bool> UpdateAsync(int id, CreateHolidayDto dto, int? callerCompanyId, bool isSuperAdmin);
    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check).</param>
    /// <param name="isSuperAdmin">True when the caller has the superadmin role. Global records may only be deleted by SuperAdmin.</param>
    Task<bool> DeleteAsync(int id, int? callerCompanyId, bool isSuperAdmin);
}
