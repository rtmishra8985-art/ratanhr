using HRMS.Application.Common;
using HRMS.Application.DTOs.Leave;

namespace HRMS.Application.Interfaces;

public interface ILeaveService
{
    // ── Leave types (admin configuration) ────────────────────────────────
    Task<List<LeaveTypeDto>> GetLeaveTypesAsync(int? companyId, CancellationToken ct = default);
    Task<PagedResult<LeaveTypeDto>> GetLeaveTypesPagedAsync(int? companyId, int page, int pageSize, CancellationToken ct = default);
    Task<LeaveTypeDto>       CreateLeaveTypeAsync(int? companyId, CreateLeaveTypeDto dto);
    /// <summary>
    /// Update a leave type. Non-superadmin callers pass their companyId so the service
    /// can verify the leave type belongs to their company (IDOR protection).
    /// Superadmin passes null to bypass the company check.
    /// </summary>
    Task<bool>               UpdateLeaveTypeAsync(int id, int? companyId, CreateLeaveTypeDto dto);

    /// <summary>
    /// Soft-delete a leave type. Same IDOR scoping as UpdateLeaveTypeAsync.
    /// </summary>
    Task<bool>               DeleteLeaveTypeAsync(int id, int? companyId);

    // ── Employee self-service ─────────────────────────────────────────────
    Task<(bool ok, string message, int? id)> ApplyAsync(string employeeId, int? companyId, ApplyLeaveDto dto);
    Task<List<LeaveRequestDto>>              GetMyRequestsAsync(string employeeId);
    Task<List<LeaveBalanceDto>>              GetMyBalanceAsync(string employeeId, int? companyId);
    /// <summary>
    /// Employee cancels their own pending request.
    /// IDOR fix: callerCompanyId scopes the query at DB level — the record is never
    /// loaded if it belongs to a different tenant.
    /// </summary>
    Task<bool>                               CancelAsync(string employeeId, int requestId, int? callerCompanyId = null);

    // ── Admin ─────────────────────────────────────────────────────────────
    /// <summary>
    /// FIX HIGH-2: callerCompanyId is now pushed into the DB query (WHERE clause),
    /// not checked post-fetch. SuperAdmin passes null for unrestricted access.
    /// </summary>
    Task<LeaveRequestDto?> GetRequestByIdAsync(int id, int? callerCompanyId = null);
    Task<List<LeaveRequestDto>>     GetAllRequestsAsync(int? companyId, string? status);
    // FIX 5: Added sortBy / sortDirection for column-level sorting support.
    Task<PagedResult<LeaveRequestDto>> GetAllRequestsPagedAsync(
        int?    companyId,
        string? status,
        int     page,
        int     pageSize,
        string? sortBy        = null,
        string? sortDirection = "desc");
    /// <summary>
    /// Admin approves or rejects a leave request.
    /// IDOR fix: callerCompanyId scopes the query at DB level so admins cannot
    /// approve/reject requests belonging to other tenants.
    /// SuperAdmin passes null for unrestricted access.
    /// </summary>
    Task<(bool ok, string message)> DecideAsync(int requestId, int approverUserId, LeaveDecisionDto dto, int? callerCompanyId = null);

    // ── Balance Adjustment (admin) ────────────────────────────────────────
    Task<LeaveBalanceAdjustmentDto>     CreateBalanceAdjustmentAsync(int actorUserId, int? companyId, CreateLeaveBalanceAdjustmentDto dto);
    /// <summary>
    /// Get leave balance adjustment history for an employee.
    /// IDOR fix: pass <paramref name="callerCompanyId"/> to scope access to caller's company.
    /// Superadmin passes null for unrestricted access.
    /// Throws <see cref="UnauthorizedAccessException"/> when a non-superadmin requests an employee
    /// from a different company.
    /// </summary>
    Task<List<LeaveBalanceAdjustmentDto>> GetBalanceAdjustmentsAsync(string employeeId, int? year, int? callerCompanyId = null);

    // ── Carry Forward (admin — run at year-end) ───────────────────────────
    Task<(int processed, int skipped)> CarryForwardBalancesAsync(LeaveCarryForwardDto dto, int actorUserId);

    /// <summary>Alias for GetAllRequestsAsync — used by cancellation-token tests.</summary>
    Task<List<LeaveRequestDto>> GetLeaveRequestsAsync(int? companyId, string? status = null, CancellationToken ct = default);
}
