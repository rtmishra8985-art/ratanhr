namespace HRMS.Application.Interfaces;

/// <summary>
/// FIX HIGH-PS5: Service layer for payslip access control and metadata retrieval.
/// PayslipController previously injected ApplicationDbContext directly, bypassing the
/// service layer. All DB access now lives here so the controller is testable and
/// the access-control logic is centralised.
/// </summary>
public interface IPayslipService
{
    /// <summary>
    /// Returns true when the caller is allowed to access the given payslip.
    ///
    /// Rules:
    ///   - Employee  → may only access their own payslips.
    ///   - Admin     → scoped to their own company; checks the payslip owner's company.
    ///   - Superadmin → unrestricted.
    ///   - Non-existent payslip → returns false (controller maps to 404).
    /// </summary>
    Task<bool> CanAccessPayslipAsync(
        int     payslipId,
        string? callerRole,
        string? callerEmployeeId,
        int?    callerCompanyId);

    /// <summary>
    /// Returns identifying metadata for the payslip — used to build a human-friendly
    /// download filename without exposing the full payslip entity to the controller.
    /// Returns null when the payslip does not exist.
    /// </summary>
    Task<(string EmployeeId, int Month, int Year)?> GetPayslipMetaAsync(int payslipId);
}
