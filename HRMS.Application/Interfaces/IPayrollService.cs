using HRMS.Application.Common;
using HRMS.Application.DTOs.Payroll;

namespace HRMS.Application.Interfaces;

public interface IPayrollService
{
    /// <summary>
    /// FIX FUNC-02: callerCompanyId scopes the employee existence check at the service layer.
    /// The controller already calls PayslipBelongsToCallerAsync before invoking this method
    /// (primary IDOR guard). This parameter adds defence-in-depth at the service layer so
    /// the service never generates a payslip for an employee outside the caller's tenant
    /// even if a future controller bypass is introduced.
    /// Passing null is the SuperAdmin path (unrestricted).
    /// </summary>
    Task<int>                    GeneratePayslipAsync(GeneratePayslipDto dto, int? actorId = null, string? actorName = null, int? callerCompanyId = null);
    Task<PayrollCalculationResult> PreviewCalculationAsync(PayrollCalculationRequest req);
    /// <summary>
    /// Get a single payslip by ID.
    /// IDOR fix: pass <paramref name="companyId"/> to scope the lookup to the caller's company at DB level.
    /// SuperAdmin passes null for unrestricted access.
    /// </summary>
    Task<PayslipDto?>            GetPayslipAsync(int id, int? companyId = null);
    /// <summary>
    /// Get all payslips with optional filters.
    /// IDOR fix: pass <paramref name="companyId"/> to scope results to the caller's company.
    /// SuperAdmin passes null for unrestricted access.
    /// </summary>
    Task<List<PayslipDto>>       GetAllPayslipsAsync(int? month = null, int? year = null, string? employeeId = null, int? companyId = null, CancellationToken ct = default);
    // FIX 6: CancellationToken added — allows the HTTP request's RequestAborted token to
    // propagate through to EF Core CountAsync/ToListAsync calls, cancelling in-flight
    // DB queries when the client disconnects before the response is sent.
    Task<PagedResult<PayslipDto>>  GetAllPayslipsPagedAsync(int? month, int? year, string? employeeId, int? companyId, int page, int pageSize, string? sortBy = null, string? sortDirection = "desc", CancellationToken ct = default);
    // FIX P3-2: employeeId comes from the caller's JWT claim only, and callerCompanyId is
    // the tenant scope resolved from the token (null => SuperAdmin). Both are applied as SQL
    // predicates so a client can never widen the result set by supplying its own IDs.
    Task<List<PayslipDto>>       GetEmployeePayslipsAsync(string employeeId, int? callerCompanyId);
    Task<bool>                   DeletePayslipAsync(int id, int? actorId = null, string? actorName = null);
    Task<BulkPayrollResultDto>   BulkGeneratePayslipsAsync(BulkPayrollDto dto, int? actorId = null, string? actorName = null);
}
