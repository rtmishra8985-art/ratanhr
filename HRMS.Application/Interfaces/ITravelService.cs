using HRMS.Application.Common;
using HRMS.Application.DTOs.Travel;

namespace HRMS.Application.Interfaces;

public interface ITravelService
{
    // ── Queries ────────────────────────────────────────────────────────────
    Task<PagedResult<TravelDto>> GetAllAsync(int? companyId, int page, int pageSize, string? status = null);
    Task<List<TravelDto>> GetMyRequestsAsync(string employeeId);
    Task<TravelDto?> GetByIdAsync(int id, int? companyId);
    Task<TravelDashboardDto> GetDashboardAsync(int? companyId);
    Task<PagedResult<TravelDto>> GetReportAsync(int? companyId, TravelReportFilterDto filter);

    // ── Employee actions ───────────────────────────────────────────────────
    Task<TravelDto> CreateAsync(string employeeId, int? companyId, CreateTravelDto dto);
    Task<TravelDto?> UpdateAsync(int id, string employeeId, int? companyId, UpdateTravelDto dto);
    Task<bool> SubmitAsync(int id, string employeeId);
    Task<bool> CancelAsync(int id, string employeeId);
    Task<bool> DeleteAsync(int id, string employeeId);

    // ── Approver actions ───────────────────────────────────────────────────
    /// <summary>
    /// Multi-step approval: step is "Manager", "HR", or "Finance".
    /// </summary>
    Task<bool> DecideAsync(int id, int approverUserId, string approverName, int? companyId, TravelDecisionDto dto);
}
