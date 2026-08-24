using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;

namespace HRMS.Application.Interfaces;

public interface IShiftService
{
    Task<List<ShiftDto>> GetShiftsAsync(int companyId);
    /// <param name="companyId">Null means unrestricted (SuperAdmin cross-tenant view).</param>
    Task<PagedResult<ShiftDto>> GetShiftsPagedAsync(int? companyId, int page, int pageSize);
    Task<int> CreateShiftAsync(CreateShiftDto dto);
    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check). Otherwise must match Shift.CompanyId.</param>
    Task<bool> UpdateShiftAsync(int id, CreateShiftDto dto, int? callerCompanyId);
    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check). Otherwise must match Shift.CompanyId.</param>
    Task<bool> DeleteShiftAsync(int id, int? callerCompanyId);
}
