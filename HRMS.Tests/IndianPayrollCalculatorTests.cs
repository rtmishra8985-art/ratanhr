using HRMS.Application.DTOs.Payroll;
using HRMS.Infrastructure.Payroll;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Unit tests for <see cref="IndianPayrollCalculator"/> covering statutory deduction
/// rules documented as missing in LOW-02 of the enterprise audit report:
///   - ESI threshold cut-off at ₹21,000 gross
///   - PT state-specific slabs (MH, KA, WB, TN, TS, GJ, and no-PT states)
///   - TDS new-regime slabs (FY 2025-26 / Finance Act 2025)
///   - Zero-gross edge case (no exception, no negative net pay)
///   - PF EPFO ceiling (12% capped at ₹15,000 basic)
///   - Net-pay invariant (gross − deductions = net)
///   - HRA metro vs non-metro
///   - Attendance pro-ration
///
/// All tests run directly against IndianPayrollCalculator — no database needed.
///
/// Gross formula (non-metro, no extra allowances, full attendance):
///   gross = basic + RoundTo2(basic × 0.40) + 1,600 (conv) + 1,250 (medical)
///         ≈ basic × 1.4 + 2,850
/// Basic values in each InlineData row are chosen to land in the stated PT slab.
/// </summary>
public class IndianPayrollCalculatorTests
{
    private static readonly IndianPayrollCalculator Calc = new();

    // ── Request builder ──────────────────────────────────────────────────────

    private static PayrollCalculationRequest Req(
        decimal basicPay,
        string  state    = "Maharashtra",
        bool    isMetro  = false,
        int     month    = 7,
        int     workingDays = 26,
        int     daysPresent = 26,
        decimal additionalAllowances = 0)
        => new()
        {
            BasicPay             = basicPay,
            State                = state,
            IsMetroCity          = isMetro,
            Month                = month,
            WorkingDays          = workingDays,
            DaysPresent          = daysPresent,
            AdditionalAllowances = additionalAllowances,
        };

    // ── Zero-gross edge case ──────────────────────────────────────────────────

    [Fact]
    public void Calculate_ZeroBasic_NoExceptionZeroNetPay()
    {
        // A payslip row with BasicPay = 0 must not throw and must not produce negative NetPay.
        var result = Calc.Calculate(Req(0, state: "Punjab")); // no PT in Punjab

        Assert.Equal(0m, result.BasicPay);
        // Gross may still be non-zero if conv/medical are always included — they are prorated
        // by attendance factor (0/0 guard in calculator yields factor=1), so they will be
        // included at full value even for zero basic. NetPay must be ≥ 0.
        Assert.True(result.NetPay >= 0m, $"NetPay was negative: {result.NetPay}");
        Assert.Equal(0m, result.PFEmployee);   // 12% of 0 = 0
        Assert.Equal(0m, result.PFEmployer);
        Assert.Equal(0m, result.TDS);          // annual income too low
    }

    // ── ESI threshold ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gross ≤ ₹21,000 → ESI employee = 0.75% of gross.
    /// BasicPay = ₹12,000; gross = 12,000 + 4,800 (HRA 40%) + 1,600 + 1,250 = 19,650 ≤ 21,000.
    /// </summary>
    [Fact]
    public void Calculate_GrossAtOrBelowEsiCeiling_EsiDeducted()
    {
        var result = Calc.Calculate(Req(12_000, state: "Punjab")); // no PT → isolate ESI

        Assert.True(result.GrossEarnings <= 21_000m,
            $"Expected gross ≤ 21,000 for ESI applicability; actual: {result.GrossEarnings}");
        var expectedEsi = Math.Round(result.GrossEarnings * 0.0075m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedEsi, result.ESIEmployee);
        Assert.True(result.ESIEmployee > 0m, "ESI should be > 0 when gross ≤ 21,000.");
    }

    /// <summary>
    /// Gross > ₹21,000 → ESI = 0.
    /// BasicPay = ₹16,000; gross = 16,000 + 6,400 + 1,600 + 1,250 = 25,250 > 21,000.
    /// </summary>
    [Fact]
    public void Calculate_GrossAboveEsiCeiling_EsiIsZero()
    {
        var result = Calc.Calculate(Req(16_000, state: "Punjab"));

        Assert.True(result.GrossEarnings > 21_000m,
            $"Expected gross > 21,000; actual: {result.GrossEarnings}");
        Assert.Equal(0m, result.ESIEmployee);
    }

    /// <summary>
    /// Boundary: BasicPay = ₹12,964 yields gross ≈ ₹20,999.60 — just below ₹21,000 → ESI applies.
    /// </summary>
    [Fact]
    public void Calculate_GrossJustBelowEsiCeiling_EsiDeducted()
    {
        var result = Calc.Calculate(Req(12_964, state: "Punjab"));

        Assert.True(result.GrossEarnings <= 21_000m,
            $"Expected gross ≤ 21,000; actual: {result.GrossEarnings}");
        Assert.True(result.ESIEmployee > 0m,
            $"ESI should apply at gross = {result.GrossEarnings}.");
    }

    // ── PF EPFO ceiling ───────────────────────────────────────────────────────

    /// <summary>
    /// BasicPay = ₹80,000 (well above ₹15,000 ceiling).
    /// PF employee = 12% of ₹15,000 = ₹1,800/month.
    /// </summary>
    [Fact]
    public void Calculate_HighBasic_PfCappedAtEpfoCeiling()
    {
        var result = Calc.Calculate(Req(80_000));

        Assert.Equal(1_800m, result.PFEmployee);
        Assert.Equal(1_800m, result.PFEmployer);
    }

    /// <summary>
    /// BasicPay = ₹10,000 — below ceiling. PF = 12% × 10,000 = ₹1,200.
    /// </summary>
    [Fact]
    public void Calculate_BasicBelowCeiling_PfNotCapped()
    {
        var result = Calc.Calculate(Req(10_000));

        Assert.Equal(1_200m, result.PFEmployee);
        Assert.Equal(1_200m, result.PFEmployer);
    }

    // ── Professional Tax — Maharashtra ───────────────────────────────────────
    // Slabs: gross ≤ 7,500 → ₹0 | 7,501–10,000 → ₹175 | > 10,000 → ₹200 (₹300 in Feb)
    // Gross formula: basic × 1.4 + 2,850 (non-metro, full attendance, no extras)

    [Theory]
    // basic=3,000 → gross=7,050  (≤ 7,500)  → ₹0
    // basic=4,000 → gross=8,450  (7,501–10,000) → ₹175
    // basic=6,000 → gross=11,250 (> 10,000)  → ₹200
    [InlineData(3_000,  0,   7)]
    [InlineData(4_000,  175, 7)]
    [InlineData(6_000,  200, 7)]
    public void Calculate_PT_Maharashtra_CorrectSlab(decimal basic, decimal expectedPt, int month)
    {
        var result = Calc.Calculate(Req(basic, state: "Maharashtra", month: month));
        Assert.Equal(expectedPt, result.ProfessionalTax);
    }

    [Fact]
    public void Calculate_PT_Maharashtra_FebruaryCatchup()
    {
        // February: top slab becomes ₹300 instead of ₹200.
        // basic=6,000 → gross=11,250 (> 10,000) → Feb → ₹300
        var result = Calc.Calculate(Req(6_000, state: "Maharashtra", month: 2));
        Assert.Equal(300m, result.ProfessionalTax);
    }

    // ── Professional Tax — Karnataka ─────────────────────────────────────────
    // Slabs: ≤ 15,000 → ₹0 | 15,001–25,000 → ₹150 | 25,001–35,000 → ₹175 | > 35,000 → ₹200

    [Theory]
    // basic=8,000  → gross=14,050  (≤ 15,000)       → ₹0
    // basic=10,000 → gross=16,850  (15,001–25,000)   → ₹150
    // basic=17,000 → gross=26,650  (25,001–35,000)   → ₹175
    // basic=25,000 → gross=37,850  (> 35,000)        → ₹200
    [InlineData(8_000,    0)]
    [InlineData(10_000,  150)]
    [InlineData(17_000,  175)]
    [InlineData(25_000,  200)]
    public void Calculate_PT_Karnataka_CorrectSlab(decimal basic, decimal expectedPt)
    {
        var result = Calc.Calculate(Req(basic, state: "Karnataka"));
        Assert.Equal(expectedPt, result.ProfessionalTax);
    }

    // ── Professional Tax — West Bengal ───────────────────────────────────────
    // Slabs: ≤ 10,000 → ₹0 | 10,001–15,000 → ₹110 | 15,001–25,000 → ₹130
    //        25,001–40,000 → ₹150 | > 40,000 → ₹200

    [Theory]
    // basic=5,000  → gross=9,850   (≤ 10,000)        → ₹0
    // basic=7,000  → gross=12,650  (10,001–15,000)   → ₹110
    // basic=10,000 → gross=16,850  (15,001–25,000)   → ₹130
    // basic=18,000 → gross=28,050  (25,001–40,000)   → ₹150
    // basic=30,000 → gross=44,850  (> 40,000)        → ₹200
    [InlineData(5_000,    0)]
    [InlineData(7_000,  110)]
    [InlineData(10_000, 130)]
    [InlineData(18_000, 150)]
    [InlineData(30_000, 200)]
    public void Calculate_PT_WestBengal_CorrectSlab(decimal basic, decimal expectedPt)
    {
        var result = Calc.Calculate(Req(basic, state: "West Bengal"));
        Assert.Equal(expectedPt, result.ProfessionalTax);
    }

    // ── Professional Tax — Tamil Nadu ────────────────────────────────────────
    // Slabs: < 3,500 → ₹0 | 3,500–4,999 → ₹60 | 5,000–7,499 → ₹80
    //        7,500–9,999 → ₹100 | 10,000–12,499 → ₹150 | ≥ 12,500 → ₹208

    [Theory]
    // basic=300   → gross=3,270   (< 3,500)          → ₹0
    // basic=1,000 → gross=4,250   (3,500–4,999)      → ₹60
    // basic=2,000 → gross=5,650   (5,000–7,499)      → ₹80
    // basic=4,000 → gross=8,450   (7,500–9,999)      → ₹100
    // basic=6,000 → gross=11,250  (10,000–12,499)    → ₹150
    // basic=7,500 → gross=13,350  (≥ 12,500)         → ₹208
    [InlineData(300,      0)]
    [InlineData(1_000,   60)]
    [InlineData(2_000,   80)]
    [InlineData(4_000,  100)]
    [InlineData(6_000,  150)]
    [InlineData(7_500,  208)]
    public void Calculate_PT_TamilNadu_CorrectSlab(decimal basic, decimal expectedPt)
    {
        var result = Calc.Calculate(Req(basic, state: "Tamil Nadu"));
        Assert.Equal(expectedPt, result.ProfessionalTax);
    }

    // ── Professional Tax — Telangana ─────────────────────────────────────────
    // Slabs: ≤ 15,000 → ₹0 | 15,001–20,000 → ₹150 | > 20,000 → ₹200

    [Theory]
    // basic=8,000  → gross=14,050  (≤ 15,000)        → ₹0
    // basic=10,000 → gross=16,850  (15,001–20,000)   → ₹150
    // basic=13,000 → gross=21,050  (> 20,000)        → ₹200
    [InlineData(8_000,    0)]
    [InlineData(10_000,  150)]
    [InlineData(13_000,  200)]
    public void Calculate_PT_Telangana_CorrectSlab(decimal basic, decimal expectedPt)
    {
        var result = Calc.Calculate(Req(basic, state: "Telangana"));
        Assert.Equal(expectedPt, result.ProfessionalTax);
    }

    // ── Professional Tax — Gujarat ───────────────────────────────────────────
    // Slabs: < 6,000 → ₹0 | 6,000–8,999 → ₹80 | 9,000–11,999 → ₹150 | ≥ 12,000 → ₹200

    [Theory]
    // basic=2,000 → gross=5,650   (< 6,000)          → ₹0
    // basic=3,000 → gross=7,050   (6,000–8,999)      → ₹80
    // basic=5,000 → gross=9,850   (9,000–11,999)     → ₹150
    // basic=7,000 → gross=12,650  (≥ 12,000)         → ₹200
    [InlineData(2_000,   0)]
    [InlineData(3_000,  80)]
    [InlineData(5_000, 150)]
    [InlineData(7_000, 200)]
    public void Calculate_PT_Gujarat_CorrectSlab(decimal basic, decimal expectedPt)
    {
        var result = Calc.Calculate(Req(basic, state: "Gujarat"));
        Assert.Equal(expectedPt, result.ProfessionalTax);
    }

    // ── Professional Tax — no-PT states ─────────────────────────────────────

    /// <summary>
    /// States with no PT obligation must always return ₹0 regardless of salary.
    /// </summary>
    [Theory]
    [InlineData("Punjab")]
    [InlineData("Delhi")]
    [InlineData("Haryana")]
    [InlineData("Rajasthan")]
    [InlineData("Uttar Pradesh")]
    public void Calculate_PT_NoPTStates_AlwaysZero(string state)
    {
        // High basic ensures we would hit the top slab if PT were mis-applied.
        var result = Calc.Calculate(Req(50_000, state: state));
        Assert.Equal(0m, result.ProfessionalTax);
    }

    // ── TDS — New regime FY 2025-26 (Finance Act 2025) ───────────────────────
    // Slabs: 0–4L → 0% | 4L–8L → 5% | 8L–12L → 10% | 12L–16L → 15% | 16L–20L → 20%
    //        20L–24L → 25% | > 24L → 30%
    // Section 87A rebate: taxable ≤ ₹12,00,000 → full rebate (tax = 0)
    // Standard deduction: ₹75,000/year
    // 4% cess applied on annual tax before monthly division.

    /// <summary>
    /// Annual gross ≤ ₹4,75,000 (annual taxable ≤ ₹4,00,000) → TDS = 0.
    /// BasicPay = ₹16,000 → gross ≈ ₹25,250/month → annual ≈ ₹3,03,000 → taxable = ₹2,28,000.
    /// </summary>
    [Fact]
    public void Calculate_TDS_LowIncome_Zero()
    {
        var result = Calc.Calculate(Req(16_000, state: "Punjab"));

        Assert.Equal(0m, result.TDS);
    }

    /// <summary>
    /// Section 87A rebate: taxable ≤ ₹12,00,000 → full rebate → TDS = 0.
    /// BasicPay = ₹50,000 → gross ≈ ₹72,850/month → annual ≈ ₹8,74,200
    /// → taxable = ₹7,99,200 ≤ ₹12,00,000 → rebate applies → TDS = ₹0.
    /// </summary>
    [Fact]
    public void Calculate_TDS_IncomeWithin87ARebate_Zero()
    {
        var result = Calc.Calculate(Req(50_000, state: "Punjab"));

        Assert.Equal(0m, result.TDS);
    }

    /// <summary>
    /// High income: taxable > ₹12,00,000 → no rebate → TDS > 0.
    /// BasicPay = ₹1,50,000 → annual gross ≈ ₹27.3L → taxable ≈ ₹26.55L > ₹12L.
    /// </summary>
    [Fact]
    public void Calculate_TDS_HighIncome_TdsPositive()
    {
        var result = Calc.Calculate(Req(150_000, state: "Punjab"));

        Assert.True(result.TDS > 0m,
            $"Expected positive TDS for high income; got {result.TDS}.");
    }

    /// <summary>TDS must be non-negative for any basic pay level.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5_000)]
    [InlineData(12_000)]
    [InlineData(25_000)]
    [InlineData(100_000)]
    [InlineData(500_000)]
    public void Calculate_TDS_NeverNegative(decimal basicPay)
    {
        var result = Calc.Calculate(Req(basicPay, state: "Punjab"));
        Assert.True(result.TDS >= 0m,
            $"TDS was negative ({result.TDS}) for BasicPay={basicPay}.");
    }

    // ── HRA metro vs non-metro ────────────────────────────────────────────────

    [Fact]
    public void Calculate_HRA_MetroCity_Is50PercentOfBasic()
    {
        var result = Calc.Calculate(Req(40_000, isMetro: true));
        Assert.Equal(20_000m, result.HRA);   // 50% × 40,000
    }

    [Fact]
    public void Calculate_HRA_NonMetroCity_Is40PercentOfBasic()
    {
        var result = Calc.Calculate(Req(40_000, isMetro: false, state: "Ahmedabad"));
        Assert.Equal(16_000m, result.HRA);   // 40% × 40,000
    }

    // ── Attendance pro-ration ─────────────────────────────────────────────────

    [Fact]
    public void Calculate_HalfMonthAttendance_GrossIsHalved()
    {
        var full = Calc.Calculate(Req(30_000, workingDays: 26, daysPresent: 26));
        var half = Calc.Calculate(Req(30_000, workingDays: 26, daysPresent: 13));

        // Allow a ₹1 rounding tolerance.
        var diff = Math.Abs(full.GrossEarnings / 2m - half.GrossEarnings);
        Assert.True(diff <= 1m,
            $"Expected half gross ≈ {full.GrossEarnings / 2m}; got {half.GrossEarnings}.");
    }

    // ── Net pay invariant ─────────────────────────────────────────────────────

    /// <summary>
    /// For any BasicPay, NetPay must equal GrossEarnings minus all employee deductions,
    /// and must never be negative.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(8_000)]
    [InlineData(15_000)]
    [InlineData(30_000)]
    [InlineData(80_000)]
    [InlineData(200_000)]
    public void Calculate_NetPay_EqualsGrossMinusDeductions(decimal basicPay)
    {
        var r = Calc.Calculate(Req(basicPay));

        var employeeDeductions = r.PFEmployee + r.ESIEmployee + r.ProfessionalTax + r.TDS;
        var expectedNet        = r.GrossEarnings - employeeDeductions;

        Assert.Equal(expectedNet, r.NetPay);
        Assert.True(r.NetPay >= 0m,
            $"NetPay is negative ({r.NetPay}) for BasicPay={basicPay}.");
    }
}
