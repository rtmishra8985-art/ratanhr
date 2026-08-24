using HRMS.Application.Common;
using HRMS.Application.DTOs.Expense;

namespace HRMS.Application.Interfaces;

public interface IExpenseService
{
    // ── Queries ────────────────────────────────────────────────────────────
    Task<PagedResult<ExpenseDto>> GetAllAsync(int? companyId, int page, int pageSize, string? status = null);
    Task<List<ExpenseDto>> GetMyClaimsAsync(string employeeId);
    Task<ExpenseDto?> GetByIdAsync(int id, int? companyId);
    Task<ExpenseDashboardDto> GetDashboardAsync(int? companyId);
    Task<PagedResult<ExpenseDto>> GetReportAsync(int? companyId, ExpenseReportFilterDto filter);

    // ── Employee actions ───────────────────────────────────────────────────
    Task<ExpenseDto> CreateDraftAsync(string employeeId, int? companyId, CreateExpenseClaimDto dto);
    Task<bool> SubmitAsync(int id, string employeeId);
    Task<bool> DeleteAsync(int id, string employeeId);

    // ── Approver actions ───────────────────────────────────────────────────
    Task<bool> DecideAsync(int id, int reviewerUserId, string reviewerName, int? companyId, ExpenseDecisionDto dto);

    // ── Legacy (backward-compat shim) ─────────────────────────────────────
    [Obsolete("Use CreateDraftAsync + SubmitAsync instead")]
    Task<ExpenseDto> SubmitLegacyAsync(string employeeId, int? companyId, CreateExpenseDto dto);
    [Obsolete("Use DecideAsync with ExpenseDecisionDto instead")]
    Task<bool> DecideLegacyAsync(int id, int reviewerUserId, int? companyId, ExpenseDecisionDto dto);

    // ── Legacy shim kept so existing callers don't break ──────────────────
    Task<ExpenseDto?> GetMyClaimByIdAsync(string employeeId, int id);
}
