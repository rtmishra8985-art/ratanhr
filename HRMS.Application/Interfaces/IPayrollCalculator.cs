using HRMS.Application.DTOs.Payroll;

namespace HRMS.Application.Interfaces;

/// <summary>
/// Vendor-agnostic payroll calculation contract.
/// Implement per jurisdiction: <c>IndianPayrollCalculator</c>, future
/// <c>UkPayrollCalculator</c>, <c>UsPayrollCalculator</c>, etc.
/// Register the correct implementation in DI based on company jurisdiction.
/// </summary>
public interface IPayrollCalculator
{
    /// <summary>Jurisdiction identifier used for DI resolution (e.g. "India", "UK").</summary>
    string Jurisdiction { get; }

    /// <summary>
    /// Computes all earnings, deductions, and net pay from the supplied request.
    /// Must not throw for valid non-negative input — return zero deductions instead.
    /// </summary>
    PayrollCalculationResult Calculate(PayrollCalculationRequest request);
}
