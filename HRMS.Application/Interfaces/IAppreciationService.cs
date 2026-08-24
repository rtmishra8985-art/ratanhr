using HRMS.Application.Common;
using HRMS.Application.DTOs.Appreciation;
using Microsoft.AspNetCore.Http;

namespace HRMS.Application.Interfaces;

public interface IAppreciationService
{
    Task<int>                    UploadAsync(string employeeId, string? message, IFormFile? file, int createdBy);
    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check); otherwise the caller's company ID.</param>
    Task<AppreciationDto?>       GetByIdAsync(int id, int? callerCompanyId);
    Task<List<AppreciationDto>>  GetByEmployeeAsync(string employeeId);
    Task<List<AppreciationDto>>  GetAllAsync(int? companyId = null);
    Task<PagedResult<AppreciationDto>> GetAllPagedAsync(int? companyId, int page, int pageSize);
    /// <param name="callerCompanyId">Null for SuperAdmin (bypasses tenant check); otherwise the caller's company ID.</param>
    Task<bool>                   DeleteAsync(int id, int? callerCompanyId);
}
