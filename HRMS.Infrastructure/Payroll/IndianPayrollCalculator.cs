using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;

namespace HRMS.Infrastructure.Payroll;

/// <summary>
/// Computes all Indian statutory payroll deductions and standard allowances
/// from a single input of BasicPay.
///
/// Rules implemented (FY 2025-26):
///   HRA          — 50% of Basic (metro) or 40% (non-metro)
///   DA           — 0% for private sector (industry standard)
///   Conveyance   — ₹1,600/month (standard exempt)
///   Medical      — ₹1,250/month (standard)
///   PF (Employee)— 12% of Basic+DA, capped at 12% of ₹15,000 = ₹1,800/month
///   PF (Employer)— 12% of Basic+DA (same ceiling)
///   ESI (Employee)— 0.75% of gross (only when gross ≤ ₹21,000)
///   ESI (Employer)— 3.25% of gross (same condition)
///   PT           — Multi-state slabs (Maharashtra / Karnataka / West Bengal /
///                  Tamil Nadu / Telangana / Andhra Pradesh / Gujarat / Punjab / Other)
///   TDS (new)    — Finance Act 2025 new regime; standard deduction ₹75,000/yr; 4% cess
///   TDS (old)    — Pre-Budget regime; standard deduction ₹50,000/yr; 80C up to ₹1.5L;
///                  HRA exemption; Section 87A rebate ≤ ₹5L; 4% cess
/// </summary>
public sealed class IndianPayrollCalculator : IPayrollCalculator
{
    public string Jurisdiction => "India";

    private const decimal PfCeilingBasic     = 15_000m;
    private const decimal EsiGrossCeiling    = 21_000m;
    private const decimal StdDeductionNew    = 75_000m;   // new regime FY 2025-26
    private const decimal StdDeductionOld    = 50_000m;   // old regime (salaried)
    private const decimal Max80C             = 150_000m;  // Section 80C cap
    private const decimal OldRegime87ALimit  = 500_000m;  // 87A rebate ceiling (old regime)

    private static readonly HashSet<string> MetroCities =
        new(StringComparer.OrdinalIgnoreCase)
        { "Mumbai", "Delhi", "Kolkata", "Chennai", "Bengaluru", "Hyderabad" };

    public PayrollCalculationResult Calculate(PayrollCalculationRequest req)
    {
        var basic = Math.Max(0, req.BasicPay);

        // No basic pay means there is no salary structure to build on: fixed
        // allowances (conveyance/medical) must not conjure earnings out of nothing.
        // Return an all-zero payslip rather than paying ₹2,850 of phantom gross.
        if (basic == 0m && req.AdditionalAllowances <= 0m && req.OvertimePay <= 0m
            && req.BonusAmount <= 0m && req.Arrears <= 0m)
        {
            return new PayrollCalculationResult
            {
                PFNote  = "₹0 — no basic pay",
                ESINote = "Not applicable (no earnings)",
                PTNote  = "₹0 — no earnings",
                TDSNote = "₹0 — no earnings"
            };
        }

        // ── Earnings ───────────────────────────────────────────────────────
        var isMetro = req.IsMetroCity || MetroCities.Contains(req.State ?? "");
        var hra     = RoundTo2(basic * (isMetro ? 0.50m : 0.40m));
        var da      = 0m;
        var conv    = 1_600m;
        var medical = 1_250m;
        var other   = Math.Max(0, req.AdditionalAllowances);
        var overtime = Math.Max(0, req.OvertimePay);

        // Pro-rate for attendance
        decimal factor = req.WorkingDays > 0
            ? Math.Min(1m, (decimal)req.DaysPresent / req.WorkingDays)
            : 1m;

        basic    = RoundTo2(basic    * factor);
        hra      = RoundTo2(hra      * factor);
        conv     = RoundTo2(conv     * factor);
        medical  = RoundTo2(medical  * factor);
        other    = RoundTo2(other    * factor);
        overtime = RoundTo2(overtime * factor);

        // Item 5 fix: bonus and arrears are NOT pro-rated by the current period's
        // attendance factor. A bonus is a discrete award, not an hourly wage component,
        // and arrears represent pay already earned in an earlier period — halving either
        // one because the employee took leave this month would be wrong.
        var bonus   = RoundTo2(Math.Max(0, req.BonusAmount));
        var arrears = RoundTo2(Math.Max(0, req.Arrears));

        var gross = basic + hra + da + conv + medical + other + overtime + bonus + arrears;

        // ── PF ────────────────────────────────────────────────────────────
        // FIX P1: the EPFO wage ceiling (Rs.15,000) is a FIXED monthly ceiling. Basic+DA is
        // already pro-rated for attendance above; pro-rating the ceiling as well double-counts
        // the LOP factor and understates statutory PF in any month with LOP.
        var pfBase = Math.Min(basic + da, PfCeilingBasic);
        var pfEmp  = RoundTo2(pfBase * 0.12m);
        var pfEmpl = RoundTo2(pfBase * 0.12m);
        var pfNote = (basic + da > PfCeilingBasic)
            ? $"12% of ₹{pfBase:N0} (capped at EPFO ceiling ₹{PfCeilingBasic:N0}/month)"
            : $"12% of Basic+DA (₹{pfBase:N0})";

        // ── ESI ───────────────────────────────────────────────────────────
        decimal esiEmp = 0, esiEmpl = 0;
        var esiNote = "Not applicable (gross > ₹21,000)";
        if (gross <= EsiGrossCeiling)
        {
            esiEmp  = RoundTo2(gross * 0.0075m);
            esiEmpl = RoundTo2(gross * 0.0325m);
            esiNote = $"0.75% employee + 3.25% employer of gross ₹{gross:N0}";
        }

        // ── Professional Tax (multi-state) ────────────────────────────────
        var (pt, ptNote) = ComputeProfessionalTax(gross, req.State ?? "Maharashtra", req.Month);

        // ── TDS ───────────────────────────────────────────────────────────
        decimal tds;
        string  tdsNote;

        if (string.Equals(req.TaxRegime, "old", StringComparison.OrdinalIgnoreCase))
        {
            (tds, tdsNote) = ComputeOldRegimeTds(
                basic, hra, gross, isMetro,
                req.Section80CDeduction,
                req.RentPaidMonthly);
        }
        else
        {
            (tds, tdsNote) = ComputeNewRegimeTds(gross);
        }

        // ── Totals ────────────────────────────────────────────────────────
        var totalDed = pfEmp + esiEmp + pt + tds;
        var netPay   = gross - totalDed;

        return new PayrollCalculationResult
        {
            BasicPay         = basic,
            HRA              = hra,
            DA               = da,
            Conveyance       = conv,
            MedicalAllowance = medical,
            OtherAllowances  = other,
            OvertimePay      = overtime,
            BonusAmount      = bonus,
            Arrears          = arrears,
            GrossEarnings    = gross,
            PFEmployee       = pfEmp,
            PFEmployer       = pfEmpl,
            ESIEmployee      = esiEmp,
            ESIEmployer      = esiEmpl,
            ProfessionalTax  = pt,
            TDS              = tds,
            TotalDeductions  = totalDed,
            NetPay           = netPay,
            PFNote           = pfNote,
            ESINote          = esiNote,
            PTNote           = ptNote,
            TDSNote          = tdsNote
        };
    }

    // ── New regime TDS — Finance Act 2025 (FY 2025-26 / AY 2026-27) ──────────
    // Standard deduction ₹75,000. Section 87A rebate ceiling raised to ₹12L.
    private static (decimal tds, string note) ComputeNewRegimeTds(decimal gross)
    {
        var annualGross   = gross * 12m;
        var taxableIncome = Math.Max(0, annualGross - StdDeductionNew);
        var annualTax     = ComputeNewRegimeSlabs(taxableIncome);
        // Finance Act 2025: 87A rebate raised from ₹7L to ₹12L
        if (taxableIncome <= 1_200_000m) annualTax = 0m;
        annualTax += RoundTo2(annualTax * 0.04m);   // 4% cess
        var tds   = decimal.Floor(annualTax / 12m);

        var note = taxableIncome <= 1_200_000m
            ? "₹0 — Section 87A rebate (taxable income ≤ ₹12L, Finance Act 2025)"
            : $"New regime FY25-26 on ₹{taxableIncome:N0}/yr; monthly TDS ₹{tds:N0} (incl. 4% cess)";

        return (tds, note);
    }

    // ── Old regime TDS — pre-Budget regime (FY 2025-26 / AY 2026-27) ─────────
    // Slabs:  ₹0–₹2.5L = 0%; ₹2.5L–₹5L = 5%; ₹5L–₹10L = 20%; >₹10L = 30%
    // Standard deduction: ₹50,000 (salaried employees)
    // Section 80C:        up to ₹1,50,000 (investments / ELSS / PPF / LIC etc.)
    // HRA exemption:      least of (actual annual HRA, 50%/40% × annual basic,
    //                     annual rent – 10% × annual basic); clamped to ≥ 0
    // Section 87A rebate: full tax waived when taxable income ≤ ₹5,00,000
    // Health & Education cess: 4% on tax
    private static (decimal tds, string note) ComputeOldRegimeTds(
        decimal monthlyBasic,
        decimal monthlyHra,
        decimal monthlyGross,
        bool    isMetro,
        decimal section80C,
        decimal rentPaidMonthly)
    {
        // Annualise inputs
        var annualGross = monthlyGross * 12m;
        var annualBasic = monthlyBasic * 12m;
        var annualHra   = monthlyHra   * 12m;

        // 1. Standard deduction
        var afterStd = Math.Max(0m, annualGross - StdDeductionOld);

        // 2. HRA exemption (Rule 2A of Income Tax Rules):
        //    Least of (a) actual HRA received, (b) 50%/40% of basic for metro/non-metro,
        //    (c) annual rent paid minus 10% of annual basic.
        //    When rentPaidMonthly = 0 the third leg is negative → exemption = 0.
        var annualRent   = rentPaidMonthly * 12m;
        var hraA         = annualHra;
        var hraB         = annualBasic * (isMetro ? 0.50m : 0.40m);
        var hraC         = Math.Max(0m, annualRent - annualBasic * 0.10m);
        var hraExemption = rentPaidMonthly > 0m ? Math.Min(hraA, Math.Min(hraB, hraC)) : 0m;
        hraExemption     = Math.Max(0m, hraExemption);

        // 3. Section 80C (capped at ₹1,50,000)
        var deduction80C = Math.Min(Math.Max(0m, section80C), Max80C);

        // 4. Taxable income
        var taxable = Math.Max(0m, afterStd - hraExemption - deduction80C);

        // 5. Old-regime slab tax
        var annualTax = ComputeOldRegimeSlabs(taxable);

        // 6. Section 87A rebate (old regime: taxable ≤ ₹5L → full rebate, max ₹12,500)
        var rebate = taxable <= OldRegime87ALimit
            ? Math.Min(annualTax, 12_500m)
            : 0m;
        annualTax = Math.Max(0m, annualTax - rebate);

        // 7. Health & Education cess: 4%
        annualTax += RoundTo2(annualTax * 0.04m);

        var tds = decimal.Floor(annualTax / 12m);

        // Build a concise audit note for the payslip
        var noteParts = new List<string>
        {
            $"Old regime FY25-26 on taxable ₹{taxable:N0}/yr",
            $"(gross ₹{annualGross:N0} − std.ded ₹{StdDeductionOld:N0}",
        };
        if (hraExemption > 0m) noteParts.Add($"− HRA exemption ₹{hraExemption:N0}");
        if (deduction80C  > 0m) noteParts.Add($"− 80C ₹{deduction80C:N0}");
        noteParts.Add(")");
        if (rebate > 0m) noteParts.Add($"87A rebate applied");
        noteParts.Add($"monthly TDS ₹{tds:N0} (incl. 4% cess)");

        var note = taxable <= OldRegime87ALimit && annualTax == 0m
            ? "₹0 — Section 87A rebate (taxable income ≤ ₹5L, old regime)"
            : string.Join("; ", noteParts);

        return (tds, note);
    }

    // ── New regime slabs — Finance Act 2025 ───────────────────────────────────
    // 0–₹4L = 0%; ₹4L–₹8L = 5%; ₹8L–₹12L = 10%; ₹12L–₹16L = 15%;
    // ₹16L–₹20L = 20%; ₹20L–₹24L = 25%; >₹24L = 30%
    private static decimal ComputeNewRegimeSlabs(decimal taxable)
    {
        if (taxable <= 400_000m)   return 0m;
        if (taxable <= 800_000m)   return (taxable - 400_000m) * 0.05m;
        if (taxable <= 1_200_000m) return 20_000m  + (taxable - 800_000m)   * 0.10m;
        if (taxable <= 1_600_000m) return 60_000m  + (taxable - 1_200_000m) * 0.15m;
        if (taxable <= 2_000_000m) return 120_000m + (taxable - 1_600_000m) * 0.20m;
        if (taxable <= 2_400_000m) return 200_000m + (taxable - 2_000_000m) * 0.25m;
        return                            300_000m + (taxable - 2_400_000m) * 0.30m;
    }

    // ── Old regime slabs ──────────────────────────────────────────────────────
    // ₹0–₹2.5L = 0%; ₹2.5L–₹5L = 5%; ₹5L–₹10L = 20%; >₹10L = 30%
    private static decimal ComputeOldRegimeSlabs(decimal taxable)
    {
        if (taxable <= 250_000m)   return 0m;
        if (taxable <= 500_000m)   return (taxable - 250_000m) * 0.05m;
        if (taxable <= 1_000_000m) return 12_500m  + (taxable - 500_000m)   * 0.20m;
        return                            112_500m + (taxable - 1_000_000m) * 0.30m;
    }

    // ── Multi-state Professional Tax slabs (FY 2025-26) ───────────────────────
    // Sources: respective state government gazette notifications.
    // Add new states here; all unlisted states return ₹0 (no PT obligation).
    private static (decimal pt, string note) ComputeProfessionalTax(
        decimal gross, string state, int month)
    {
        var s = state.Trim().ToLowerInvariant()
                     .Replace(" ", "").Replace("-", "");
        bool isFeb = month == 2;

        return s switch
        {
            // ── Maharashtra ────────────────────────────────────────────────
            // Slabs: ≤7,500 → ₹0 | 7,501–10,000 → ₹175 | >10,000 → ₹200 (₹300 Feb)
            "maharashtra" => gross switch
            {
                <= 7_500m  => (0m,   "₹0 (MH: gross ≤ ₹7,500)"),
                <= 10_000m => (175m, "₹175/month (MH: ₹7,501–₹10,000 slab)"),
                _          => isFeb
                              ? (300m, "₹300 (MH: February catch-up)")
                              : (200m, "₹200/month (MH: gross > ₹10,000)")
            },

            // ── Karnataka ──────────────────────────────────────────────────
            // Slabs: ≤15,000 → ₹0 | 15,001–25,000 → ₹150 | 25,001–35,000 → ₹175 | >35,000 → ₹200
            "karnataka" => gross switch
            {
                <= 15_000m => (0m,   "₹0 (KA: gross ≤ ₹15,000)"),
                <= 25_000m => (150m, "₹150/month (KA: ₹15,001–₹25,000)"),
                <= 35_000m => (175m, "₹175/month (KA: ₹25,001–₹35,000)"),
                _          => (200m, "₹200/month (KA: gross > ₹35,000)")
            },

            // ── West Bengal ────────────────────────────────────────────────
            // Slabs: ≤10,000 → ₹0 | 10,001–15,000 → ₹110 | 15,001–25,000 → ₹130
            //        25,001–40,000 → ₹150 | >40,000 → ₹200
            "westbengal" or "wb" => gross switch
            {
                <= 10_000m => (0m,   "₹0 (WB: gross ≤ ₹10,000)"),
                <= 15_000m => (110m, "₹110/month (WB: ₹10,001–₹15,000)"),
                <= 25_000m => (130m, "₹130/month (WB: ₹15,001–₹25,000)"),
                <= 40_000m => (150m, "₹150/month (WB: ₹25,001–₹40,000)"),
                _          => (200m, "₹200/month (WB: gross > ₹40,000)")
            },

            // ── Tamil Nadu ─────────────────────────────────────────────────
            // Slabs: ≤3,499 → ₹0 | 3,500–4,999 → ₹60 | 5,000–7,499 → ₹80
            //        7,500–9,999 → ₹100 | 10,000–12,499 → ₹150 | ≥12,500 → ₹208
            "tamilnadu" or "tn" => gross switch
            {
                <  3_500m  => (0m,   "₹0 (TN: gross < ₹3,500)"),
                <  5_000m  => (60m,  "₹60/month (TN: ₹3,500–₹4,999)"),
                <  7_500m  => (80m,  "₹80/month (TN: ₹5,000–₹7,499)"),
                < 10_000m  => (100m, "₹100/month (TN: ₹7,500–₹9,999)"),
                < 12_500m  => (150m, "₹150/month (TN: ₹10,000–₹12,499)"),
                _          => (208m, "₹208/month (TN: gross ≥ ₹12,500)")
            },

            // ── Telangana ──────────────────────────────────────────────────
            // Slabs: ≤15,000 → ₹0 | 15,001–20,000 → ₹150 | >20,000 → ₹200
            "telangana" or "ts" => gross switch
            {
                <= 15_000m => (0m,   "₹0 (TS: gross ≤ ₹15,000)"),
                <= 20_000m => (150m, "₹150/month (TS: ₹15,001–₹20,000)"),
                _          => (200m, "₹200/month (TS: gross > ₹20,000)")
            },

            // ── Andhra Pradesh ─────────────────────────────────────────────
            // Slabs: ≤15,000 → ₹0 | 15,001–20,000 → ₹150 | >20,000 → ₹200
            "andhrapradesh" or "ap" => gross switch
            {
                <= 15_000m => (0m,   "₹0 (AP: gross ≤ ₹15,000)"),
                <= 20_000m => (150m, "₹150/month (AP: ₹15,001–₹20,000)"),
                _          => (200m, "₹200/month (AP: gross > ₹20,000)")
            },

            // ── Gujarat ────────────────────────────────────────────────────
            // Slabs: ≤5,999 → ₹0 | 6,000–8,999 → ₹80 | 9,000–11,999 → ₹150 | ≥12,000 → ₹200
            "gujarat" or "gj" => gross switch
            {
                <  6_000m  => (0m,   "₹0 (GJ: gross < ₹6,000)"),
                <  9_000m  => (80m,  "₹80/month (GJ: ₹6,000–₹8,999)"),
                < 12_000m  => (150m, "₹150/month (GJ: ₹9,000–₹11,999)"),
                _          => (200m, "₹200/month (GJ: gross ≥ ₹12,000)")
            },

            // ── Madhya Pradesh ─────────────────────────────────────────────
            // Slabs: ≤18,750 → ₹0 | 18,751–25,000 → ₹125 | 25,001–33,333 → ₹167 | >33,333 → ₹208
            "madhyapradesh" or "mp" => gross switch
            {
                <= 18_750m => (0m,   "₹0 (MP: gross ≤ ₹18,750)"),
                <= 25_000m => (125m, "₹125/month (MP: ₹18,751–₹25,000)"),
                <= 33_333m => (167m, "₹167/month (MP: ₹25,001–₹33,333)"),
                _          => (208m, "₹208/month (MP: gross > ₹33,333)")
            },

            // ── States with no PT obligation ──────────────────────────────
            _ => (0m, $"₹0 (No Professional Tax applicable in {state})")
        };
    }

    private static decimal RoundTo2(decimal v)
        => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
