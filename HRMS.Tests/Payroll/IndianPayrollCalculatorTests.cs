using HRMS.Application.DTOs.Payroll;
using HRMS.Infrastructure.Payroll;
using Xunit;

namespace HRMS.Tests.Payroll;

/// <summary>
/// Phase 2 coverage for FY 2025-26 new-regime TDS.
/// The calculator accepts monthly basic pay, so each annual-gross case is
/// converted to the corresponding non-metro monthly gross (basic + 40% HRA
/// + ₹1,600 conveyance + ₹1,250 medical allowance).
/// </summary>
public sealed class IndianPayrollCalculatorTests
{
    private static readonly IndianPayrollCalculator Calculator = new();

    private static PayrollCalculationRequest RequestForAnnualGross(decimal annualGross)
    {
        var monthlyGross = annualGross / 12m;
        var monthlyBasic = (monthlyGross - 2_850m) / 1.4m;

        return new PayrollCalculationRequest
        {
            BasicPay = monthlyBasic,
            State = "Punjab",
            WorkingDays = 26,
            DaysPresent = 26
        };
    }

    [Theory]
    [InlineData(300_000, 0)]
    [InlineData(500_000, 0)]
    [InlineData(1_200_000, 0)]
    // Taxable = 12,10,000 - 75,000 = 11,35,000, which is still within
    // the ₹12,00,000 Section 87A rebate limit, so monthly TDS remains ₹0.
    [InlineData(1_210_000, 0)]
    // Taxable = 20,00,000 - 75,000 = 19,25,000.
    // Tax = 60,000 + 60,000 + (3,25,000 × 20%) = 1,85,000;
    // cess = 1,92,400; floor(1,92,400 / 12) = 16,033.
    [InlineData(2_000_000, 16_033)]
    // Monthly basic and HRA are rounded to paise before gross is annualized:
    // gross = ₹2,499,999.96, taxable = ₹2,424,999.96.
    // Tax = ₹3,07,499.99; cess = ₹3,19,799.99;
    // floor(₹3,19,799.99 / 12) = ₹26,649.
    [InlineData(2_500_000, 26_649)]
    public void Calculate_NewRegimeTds_Uses2025_26Slabs(
        decimal grossAnnual,
        decimal expectedMonthlyTds)
    {
        var result = Calculator.Calculate(RequestForAnnualGross(grossAnnual));

        Assert.Equal(expectedMonthlyTds, result.TDS);
    }

    [Fact]
    public void Calculate_ZeroIncome_ReturnsZeroMonthlyTds()
    {
        var result = Calculator.Calculate(new PayrollCalculationRequest
        {
            BasicPay = 0,
            State = "Punjab",
            WorkingDays = 26,
            DaysPresent = 26
        });

        Assert.Equal(0m, result.TDS);
    }
}