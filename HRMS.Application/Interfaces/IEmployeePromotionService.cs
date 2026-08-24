using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;

namespace HRMS.Application.Interfaces;

public interface IEmployeePromotionService
{
    Task<List<EmployeePromotionDto>> GetPromotionsAsync(string employeeId);
    Task<PagedResult<EmployeePromotionDto>> GetPromotionsPagedAsync(string employeeId, int page, int pageSize);
    Task<int>                        CreatePromotionAsync(CreatePromotionDto dto);
    /// <summary>FIX IDOR: callerCompanyId scopes the lookup via Employee JOIN; null = SuperAdmin (unrestricted).</summary>
    Task<bool>                       DeletePromotionAsync(int id, int? callerCompanyId = null);
}
