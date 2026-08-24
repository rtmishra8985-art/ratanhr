using HRMS.Application.Common;
using HRMS.Application.DTOs.Payroll;

namespace HRMS.Application.Interfaces;

public interface IBonusDeductionService
{
    // ── Bonus ──────────────────────────────────────────────────────────────
    /// <summary>
    /// FIX SEC-02: callerCompanyId scopes the lookup via JOIN on Employee.CompanyId.
    /// Passing null is the SuperAdmin path (unrestricted). Non-null callers receive
    /// null (→ 404) when the bonus belongs to a different tenant's employee.
    /// </summary>
    Task<BonusDto?>         GetBonusByIdAsync(int id, int? callerCompanyId = null);
    /// <summary>
    /// FIX FUNC-01: callerCompanyId scopes the list via JOIN on Employee.CompanyId.
    /// Passing null is the SuperAdmin path (unrestricted).
    /// </summary>
    Task<List<BonusDto>>    GetBonusesAsync(string? employeeId, int? callerCompanyId, int? month, int? year);
    Task<PagedResult<BonusDto>> GetBonusesPagedAsync(string? employeeId, int? month, int? year, int page, int pageSize);
    /// <summary>Company-scoped paged bonus list — FIX IDOR: always tenant-filtered for non-superadmin callers.</summary>
    Task<PagedResult<BonusDto>> GetBonusesPagedScopedAsync(string? employeeId, int? companyId, int? month, int? year, int page, int pageSize);
    Task<int>               AddBonusAsync(CreateBonusDto dto);
    /// <summary>FIX IDOR: callerCompanyId scopes the lookup; null = SuperAdmin (unrestricted).</summary>
    Task<bool>              UpdateBonusAsync(int id, CreateBonusDto dto, int? callerCompanyId = null);
    /// <summary>FIX IDOR: callerCompanyId scopes the lookup; null = SuperAdmin (unrestricted).</summary>
    Task<bool>              DeleteBonusAsync(int id, int? callerCompanyId = null);

    // ── Deduction ─────────────────────────────────────────────────────────
    /// <summary>
    /// FIX SEC-02: callerCompanyId scopes the lookup via JOIN on Employee.CompanyId.
    /// Passing null is the SuperAdmin path (unrestricted).
    /// </summary>
    Task<DeductionDto?>      GetDeductionByIdAsync(int id, int? callerCompanyId = null);
    /// <summary>
    /// FIX FUNC-01: callerCompanyId scopes the list via JOIN on Employee.CompanyId.
    /// Passing null is the SuperAdmin path (unrestricted).
    /// </summary>
    Task<List<DeductionDto>> GetDeductionsAsync(string? employeeId, int? callerCompanyId, int? month, int? year);
    Task<PagedResult<DeductionDto>> GetDeductionsPagedAsync(string? employeeId, int? month, int? year, int page, int pageSize);
    /// <summary>Company-scoped paged deduction list — FIX IDOR: always tenant-filtered for non-superadmin callers.</summary>
    Task<PagedResult<DeductionDto>> GetDeductionsPagedScopedAsync(string? employeeId, int? companyId, int? month, int? year, int page, int pageSize);
    Task<int>                AddDeductionAsync(CreateDeductionDto dto);
    /// <summary>FIX IDOR: callerCompanyId scopes the lookup; null = SuperAdmin (unrestricted).</summary>
    Task<bool>               UpdateDeductionAsync(int id, CreateDeductionDto dto, int? callerCompanyId = null);
    /// <summary>FIX IDOR: callerCompanyId scopes the lookup; null = SuperAdmin (unrestricted).</summary>
    Task<bool>               DeleteDeductionAsync(int id, int? callerCompanyId = null);
}
