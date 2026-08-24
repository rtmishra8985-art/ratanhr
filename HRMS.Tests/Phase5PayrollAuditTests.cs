using FluentValidation;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Validators;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Payroll;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Phase 5 Payroll &amp; Business Workflow Audit — 31 test cases (TC-1 through TC-31).
///
/// Covers the complete INPUT → CALCULATION → DB → PAYSLIP → API trace:
///   TC-01–13  IndianPayrollCalculator statutory rules (hand-calculated)
///   TC-14–20  PayrollService CRUD, upsert, lock, delete
///   TC-21–23  IDOR / tenant isolation (per-employee and per-tenant)
///   TC-24–27  PayrollLock lifecycle (lock, unlock, idempotency, cross-period)
///   TC-28–30  BulkGeneratePayslips (generate, skip, overwrite)
///   TC-31     FluentValidation — DaysPresent > WorkingDays
///
/// All expected values are hand-calculated from the production IndianPayrollCalculator
/// rules (FY 2025-26 / Finance Act 2025).
///
/// Gross formula (non-metro, full attendance, no extra allowances):
///   gross = basic + RoundTo2(basic × 0.40) + 1,600 + 1,250
/// PF: 12% of min(basic, 15,000).  ESI: 0.75% of gross when gross ≤ 21,000.
/// 87A rebate: taxable ≤ 12,00,000 → TDS = 0 (Finance Act 2025).
/// </summary>
public class Phase5PayrollAuditTests
{
    private static readonly IndianPayrollCalculator Calc = new();

    // ── Request builder ────────────────────────────────────────────────────────

    private static PayrollCalculationRequest Req(
        decimal basicPay,
        string state = "Maharashtra",
        bool isMetro = false,
        int month = 7,
        int workingDays = 26,
        int daysPresent = 26,
        decimal additionalAllowances = 0m)
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

    // ════════════════════════════════════════════════════════════════════════════
    // TC-01 — CALCULATOR: full-month gross for a standard Maharashtra employee
    //
    //   BasicPay = ₹30,000 | non-metro | 26/26 days | July
    //   HRA  = 30,000 × 0.40 = 12,000
    //   Conv = 1,600 | Medical = 1,250 | DA = 0
    //   Gross = 30,000 + 12,000 + 1,600 + 1,250 = 44,850
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC01_Calculator_StandardEmployee_GrossIsCorrect()
    {
        var r = Calc.Calculate(Req(30_000m));

        Assert.Equal(30_000m, r.BasicPay);
        Assert.Equal(12_000m, r.HRA);
        Assert.Equal(0m,      r.DA);
        Assert.Equal(1_600m,  r.Conveyance);
        Assert.Equal(1_250m,  r.MedicalAllowance);
        Assert.Equal(44_850m, r.GrossEarnings);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-02 — CALCULATOR: PF ceiling — basic > ₹15,000 → PF capped at ₹1,800
    //
    //   PfBase = min(30,000, 15,000) = 15,000
    //   PFEmployee = PFEmployer = 12% × 15,000 = ₹1,800
    //   FIX P1: ceiling is NOT pro-rated — it is always the FIXED ₹15,000 limit.
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC02_Calculator_PfCeiling_BasicAbove15000_PfCappedAt1800()
    {
        var r = Calc.Calculate(Req(30_000m));

        Assert.Equal(1_800m, r.PFEmployee);
        Assert.Equal(1_800m, r.PFEmployer);
        Assert.Contains("capped", r.PFNote, StringComparison.OrdinalIgnoreCase);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-03 — CALCULATOR: PF below ceiling — basic ≤ ₹15,000 → PF = 12% of basic
    //
    //   BasicPay = ₹10,000
    //   PFEmployee = PFEmployer = 12% × 10,000 = ₹1,200
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC03_Calculator_PfBelowCeiling_BasicBelow15000_PfIs12Percent()
    {
        var r = Calc.Calculate(Req(10_000m, state: "Punjab"));

        Assert.Equal(1_200m, r.PFEmployee);
        Assert.Equal(1_200m, r.PFEmployer);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-04 — CALCULATOR: ESI applies when gross ≤ ₹21,000
    //
    //   BasicPay = ₹12,000 | Punjab (no PT to isolate ESI)
    //   HRA = 4,800 | Conv = 1,600 | Medical = 1,250
    //   Gross = 12,000 + 4,800 + 1,600 + 1,250 = 19,650 ≤ 21,000
    //   ESI = round(19,650 × 0.0075, 2) = round(147.375, 2) = 147.38
    //   (MidpointRounding.AwayFromZero used by RoundTo2)
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC04_Calculator_Esi_GrossAtOrBelowCeiling_EsiDeducted()
    {
        var r = Calc.Calculate(Req(12_000m, state: "Punjab"));

        Assert.Equal(19_650m, r.GrossEarnings);
        Assert.Equal(147.38m, r.ESIEmployee);
        Assert.True(r.ESIEmployee > 0m);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-05 — CALCULATOR: ESI does NOT apply when gross > ₹21,000
    //
    //   BasicPay = ₹16,000 | Punjab (no PT)
    //   Gross = 16,000 + 6,400 + 1,600 + 1,250 = 25,250 > 21,000
    //   ESI = 0
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC05_Calculator_Esi_GrossAboveCeiling_EsiIsZero()
    {
        var r = Calc.Calculate(Req(16_000m, state: "Punjab"));

        Assert.True(r.GrossEarnings > 21_000m);
        Assert.Equal(0m, r.ESIEmployee);
        Assert.Equal(0m, r.ESIEmployer);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-06 — CALCULATOR: Section 87A rebate — taxable ≤ ₹12L → TDS = 0
    //
    //   BasicPay = ₹50,000 | Punjab | full month
    //   Gross ≈ 72,850/month → Annual ≈ 874,200
    //   Taxable = 874,200 − 75,000 = 799,200 ≤ 12,00,000
    //   Finance Act 2025: 87A rebate → annual tax = 0 → TDS = ₹0
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC06_Calculator_TDS_Section87ARebate_TaxableBelow12L_TdsIsZero()
    {
        var r = Calc.Calculate(Req(50_000m, state: "Punjab"));

        Assert.Equal(0m, r.TDS);
        Assert.Contains("87A", r.TDSNote, StringComparison.OrdinalIgnoreCase);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-07 — CALCULATOR: TDS positive — taxable > ₹12L
    //
    //   BasicPay = ₹1,50,000 | Punjab | full month
    //   Gross = 1,50,000 + 60,000 + 1,600 + 1,250 = 2,12,850/month
    //   Annual gross = 25,54,200 | Taxable = 24,79,200 > 12,00,000 → no rebate
    //   New-regime tax on ₹24,79,200:
    //     0→4L=0 | 4→8L=20,000 | 8→12L=40,000 | 12→16L=60,000 | 16→20L=80,000
    //     20→24L=1,00,000 | 24L→24,79,200: 79,200×0.30=23,760 → total=3,23,760
    //   + 4% cess: round(3,23,760×1.04,2)=3,36,710.40
    //   Monthly TDS = floor(3,36,710.40/12) = floor(28,059.20) = 28,059
    //   (Monthly TDS is floored to a whole rupee, consistent with
    //   IndianPayrollCalculator and OldRegimeTdsTests — not rounded to 2dp.)
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC07_Calculator_TDS_HighIncome_TdsIs28059()
    {
        var r = Calc.Calculate(Req(1_50_000m, state: "Punjab"));

        Assert.Equal(28_059m, r.TDS);
        Assert.True(r.TDS > 0m);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-08 — CALCULATOR: Attendance pro-ration (13 of 26 days present)
    //
    //   BasicPay = ₹30,000 | factor = 13/26 = 0.50
    //   Basic after pro-rate  = round(30,000 × 0.5, 2) = 15,000
    //   HRA after pro-rate    = round(12,000 × 0.5, 2) = 6,000
    //   Conv after pro-rate   = round(1,600  × 0.5, 2) = 800
    //   Medical after pro-rate = round(1,250  × 0.5, 2) = 625
    //   Gross = 15,000 + 6,000 + 800 + 625 = 22,425
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC08_Calculator_AttendanceProRation_HalfMonth_GrossIsHalved()
    {
        var r = Calc.Calculate(Req(30_000m, workingDays: 26, daysPresent: 13));

        Assert.Equal(15_000m, r.BasicPay);
        Assert.Equal(6_000m,  r.HRA);
        Assert.Equal(800m,    r.Conveyance);
        Assert.Equal(625m,    r.MedicalAllowance);
        Assert.Equal(22_425m, r.GrossEarnings);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-09 — CALCULATOR: HRA metro city → 50% of basic
    //
    //   BasicPay = ₹40,000 | isMetro=true
    //   HRA = 40,000 × 0.50 = 20,000
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC09_Calculator_HRA_Metro_Is50Percent()
    {
        var r = Calc.Calculate(Req(40_000m, isMetro: true));

        Assert.Equal(20_000m, r.HRA);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-10 — CALCULATOR: HRA non-metro → 40% of basic
    //
    //   BasicPay = ₹40,000 | isMetro=false
    //   HRA = 40,000 × 0.40 = 16,000
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC10_Calculator_HRA_NonMetro_Is40Percent()
    {
        var r = Calc.Calculate(Req(40_000m, isMetro: false, state: "Pune"));

        Assert.Equal(16_000m, r.HRA);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-11 — CALCULATOR: Maharashtra PT slab ₹175 (7,501–10,000 gross band)
    //
    //   BasicPay = ₹4,000 | MH | non-metro | full attendance
    //   HRA = 4,000 × 0.40 = 1,600
    //   Gross = 4,000 + 1,600 + 1,600 + 1,250 = 8,450 → MH slab ₹175
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC11_Calculator_PT_Maharashtra_Slab175()
    {
        var r = Calc.Calculate(Req(4_000m, state: "Maharashtra", month: 7));

        Assert.Equal(8_450m, r.GrossEarnings);
        Assert.Equal(175m,   r.ProfessionalTax);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-12 — CALCULATOR: Maharashtra PT February catch-up ₹300
    //
    //   BasicPay = ₹6,000 | MH | February | non-metro | full attendance
    //   HRA = 6,000 × 0.40 = 2,400
    //   Gross = 6,000 + 2,400 + 1,600 + 1,250 = 11,250 > 10,000 → Feb → ₹300
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC12_Calculator_PT_Maharashtra_FebruaryCatchup_300()
    {
        var r = Calc.Calculate(Req(6_000m, state: "Maharashtra", month: 2));

        Assert.Equal(11_250m, r.GrossEarnings);
        Assert.Equal(300m,    r.ProfessionalTax);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-13 — CALCULATOR: zero basic + zero allowances → all-zero payslip
    //
    //   BasicPay = 0, AdditionalAllowances = 0 → early-exit branch
    //   No phantom earnings from conv/medical.
    //   NetPay = 0, PFEmployee = 0, TDS = 0.
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC13_Calculator_ZeroBasicAndAllowances_AllZeroPayslip()
    {
        var r = Calc.Calculate(Req(0m, state: "Punjab"));

        Assert.Equal(0m, r.GrossEarnings);
        Assert.Equal(0m, r.PFEmployee);
        Assert.Equal(0m, r.ESIEmployee);
        Assert.Equal(0m, r.TDS);
        Assert.Equal(0m, r.NetPay);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-14 — SERVICE: GeneratePayslipAsync valid input → positive ID persisted
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC14_Service_GeneratePayslip_ValidInput_ReturnsPositiveId()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "TC14_EMP", FullName = "Test14", IsActive = true, CompanyId = 1
        });
        await db.SaveChangesAsync();

        var svc = BuildSvc(db);
        var id = await svc.GeneratePayslipAsync(new GeneratePayslipDto
        {
            EmployeeId  = "TC14_EMP", Month = 7, Year = 2026,
            CompanyId   = 1,  BasicPay = 30_000m,
            WorkingDays = 26, DaysPresent = 26, AutoCalculate = true
        });

        Assert.True(id > 0);

        var saved = await db.Payslips.FindAsync(id);
        Assert.NotNull(saved);
        Assert.Equal("TC14_EMP", saved!.EmployeeId);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-15 — SERVICE: Duplicate generate (same employee + period) → upsert
    //
    //   Calling GeneratePayslipAsync twice for TC15_EMP / 7 / 2026 must
    //   return the SAME payslip ID (update, not insert).
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC15_Service_GeneratePayslip_SamePeriodTwice_UpsertNotDuplicate()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "TC15_EMP", FullName = "Test15", IsActive = true, CompanyId = 1
        });
        await db.SaveChangesAsync();

        var svc = BuildSvc(db);
        var firstId = await svc.GeneratePayslipAsync(new GeneratePayslipDto
        {
            EmployeeId  = "TC15_EMP", Month = 7, Year = 2026, CompanyId = 1,
            BasicPay    = 30_000m, WorkingDays = 26, DaysPresent = 26, AutoCalculate = true
        });

        var secondId = await svc.GeneratePayslipAsync(new GeneratePayslipDto
        {
            EmployeeId  = "TC15_EMP", Month = 7, Year = 2026, CompanyId = 1,
            BasicPay    = 35_000m, WorkingDays = 26, DaysPresent = 26, AutoCalculate = true,
            // BLOCKER-6: regenerating a period that already has a calculated payslip
            // (NetPay > 0) requires an explicit opt-in; this is intentional duplicate
            // protection, not a bug. This test predates that fix.
            Overwrite = true,
        });

        Assert.Equal(firstId, secondId);
        Assert.Single(db.Payslips.Where(p => p.EmployeeId == "TC15_EMP" && p.Month == 7 && p.Year == 2026).ToList());
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-16 — SERVICE: Unknown employee → KeyNotFoundException
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC16_Service_GeneratePayslip_UnknownEmployee_ThrowsKeyNotFound()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildSvc(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GeneratePayslipAsync(new GeneratePayslipDto
            {
                EmployeeId  = "GHOST_EMP", Month = 7, Year = 2026, CompanyId = 1,
                BasicPay    = 30_000m, WorkingDays = 26, DaysPresent = 26
            }));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-17 — SERVICE: DaysPresent > WorkingDays → ArgumentException
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC17_Service_GeneratePayslip_DaysPresentExceedsWorkingDays_ThrowsArgument()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "TC17_EMP", FullName = "Test17", IsActive = true, CompanyId = 1
        });
        await db.SaveChangesAsync();

        var svc = BuildSvc(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.GeneratePayslipAsync(new GeneratePayslipDto
            {
                EmployeeId  = "TC17_EMP", Month = 7, Year = 2026, CompanyId = 1,
                BasicPay    = 30_000m, WorkingDays = 26, DaysPresent = 27  // 27 > 26
            }));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-18 — SERVICE: DeletePayslipAsync existing → returns true, not retrievable
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC18_Service_DeletePayslip_Existing_ReturnsTrueAndIsGone()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "TC18_EMP", FullName = "Test18", IsActive = true, CompanyId = 1
        });
        await db.SaveChangesAsync();

        var svc = BuildSvc(db);
        var id = await svc.GeneratePayslipAsync(new GeneratePayslipDto
        {
            EmployeeId = "TC18_EMP", Month = 7, Year = 2026, CompanyId = 1,
            BasicPay = 30_000m, WorkingDays = 26, DaysPresent = 26, AutoCalculate = true
        });

        var deleted = await svc.DeletePayslipAsync(id, actorId: 1, actorName: "hr");

        Assert.True(deleted);
        var fetched = await svc.GetPayslipAsync(id);
        Assert.Null(fetched);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-19 — SERVICE: DeletePayslipAsync non-existent → returns false
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC19_Service_DeletePayslip_NonExistent_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = BuildSvc(db);

        var result = await svc.DeletePayslipAsync(999_999);

        Assert.False(result);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-20 — SERVICE: GetAllPayslipsPagedAsync filters by month/year
    //
    //   Seed payslips for month 6 and month 7. Paged query with month=7 must
    //   return only the July payslip.
    //   Signature: (int? month, int? year, string? employeeId, int? companyId,
    //               int page, int pageSize, ...)
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC20_Service_GetAllPayslipsPaged_MonthYearFilter_ReturnsMatchingOnly()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "TC20_EMP", FullName = "Test20", IsActive = true, CompanyId = 1
        });
        await db.SaveChangesAsync();

        var svc = BuildSvc(db);
        await svc.GeneratePayslipAsync(new GeneratePayslipDto
        {
            EmployeeId = "TC20_EMP", Month = 6, Year = 2026, CompanyId = 1,
            BasicPay = 30_000m, WorkingDays = 26, DaysPresent = 26, AutoCalculate = true
        });
        await svc.GeneratePayslipAsync(new GeneratePayslipDto
        {
            EmployeeId = "TC20_EMP", Month = 7, Year = 2026, CompanyId = 1,
            BasicPay = 30_000m, WorkingDays = 26, DaysPresent = 26, AutoCalculate = true
        });

        var paged = await svc.GetAllPayslipsPagedAsync(
            month: 7, year: 2026, employeeId: null, companyId: null,
            page: 1, pageSize: 20);

        Assert.Single(paged.Items);
        Assert.Equal(7, paged.Items[0].Month);
        Assert.Equal(2026, paged.Items[0].Year);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-21 — IDOR: company-scoped query returns own-company payslip
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC21_Idor_GetAllPayslips_CompanyScoped_ReturnsOwnPayslip()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        await SeedPayslipAsync(db, "TC21_EMP_C1", companyId: 1);

        var svc = BuildSvc(db);
        var result = await svc.GetAllPayslipsAsync(companyId: 1);

        Assert.Single(result);
        Assert.Equal("TC21_EMP_C1", result[0].EmployeeId);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-22 — IDOR: company-scoped query does NOT return cross-company payslips
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC22_Idor_GetAllPayslips_CompanyScoped_ExcludesOtherCompany()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        await SeedPayslipAsync(db, "TC22_EMP_C1", companyId: 1);

        var svc = BuildSvc(db);
        // Company-2 admin requests — must see nothing from company-1
        var result = await svc.GetAllPayslipsAsync(companyId: 2);

        Assert.Empty(result);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-23 — IDOR: SuperAdmin (null companyId) returns payslips from all companies
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC23_Idor_GetAllPayslips_SuperAdmin_ReturnsAllCompanies()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        await SeedPayslipAsync(db, "TC23_EMP_C1", companyId: 1, month: 7);
        await SeedPayslipAsync(db, "TC23_EMP_C2", companyId: 2, month: 8);

        var svc = BuildSvc(db);
        var result = await svc.GetAllPayslipsAsync(companyId: null);

        Assert.Equal(2, result.Count);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-24 — LOCK: LockAsync creates a locked row with non-default LockedAt
    //
    //   LockedAt is DateTime (non-nullable) — must not equal default(DateTime).
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC24_Lock_LockAsync_SetsIsLockedAndLockedAt()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(companyId: 1, month: 7, year: 2026, lockedByUserId: 99);

        Assert.True(await guard.IsLockedAsync(1, 7, 2026));

        var lockRow = db.PayrollLocks
            .First(l => l.CompanyId == 1 && l.Month == 7 && l.Year == 2026);
        Assert.NotEqual(default(DateTime), lockRow.LockedAt);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-25 — LOCK: UnlockAsync clears IsLocked and sets non-null UnlockedAt
    //
    //   UnlockedAt is DateTime? (nullable) — Assert.NotNull is correct.
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC25_Lock_UnlockAsync_SetsIsLockedFalseAndUnlockedAt()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(1, 7, 2026, 99);
        await guard.UnlockAsync(1, 7, 2026, unlockedByUserId: 1);

        Assert.False(await guard.IsLockedAsync(1, 7, 2026));

        var lockRow = db.PayrollLocks
            .First(l => l.CompanyId == 1 && l.Month == 7 && l.Year == 2026);
        Assert.NotNull(lockRow.UnlockedAt);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-26 — LOCK: Idempotent double-lock → exactly one row in PayrollLocks
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC26_Lock_DoubleLock_Idempotent_OnlyOneRow()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(1, 7, 2026, 99);
        await guard.LockAsync(1, 7, 2026, 99);  // idempotent — must not throw or duplicate

        var locks = await guard.GetLocksAsync(companyId: 1);
        Assert.Single(locks);
        Assert.True(locks[0].IsLocked);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-27 — LOCK: Lock on company/month/year does NOT affect a different month
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC27_Lock_CrossPeriod_DifferentMonthIsNotLocked()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(companyId: 1, month: 7, year: 2026, lockedByUserId: 99);

        Assert.False(await guard.IsLockedAsync(companyId: 1, month: 8, year: 2026));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-28 — BULK: BulkGeneratePayslipsAsync → Generated=3, Skipped=0, Failed=0
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC28_Bulk_Generate_AllEmployeesWithSalary_GeneratedThree()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        for (int i = 1; i <= 3; i++)
        {
            var code = $"TC28_{i:D3}";
            db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
            {
                EmployeeCode = code, FullName = $"Emp{i}", IsActive = true, CompanyId = 1
            });
            db.SalaryStructures.Add(new SalaryStructure
            {
                EmployeeId    = code, BasicPay = 25_000, HRA = 10_000,
                IsActive      = true,
                EffectiveFrom = DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
                CreatedAt     = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();

        var svc = BuildSvc(db);
        var result = await svc.BulkGeneratePayslipsAsync(new BulkPayrollDto
        {
            Month = 7, Year = 2026, CompanyId = 1
        });

        Assert.Equal(3, result.Generated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-29 — BULK: Overwrite=false with existing payslip → Skipped=1, Generated=0
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC29_Bulk_Overwrite_False_ExistingPayslip_Skipped()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "TC29_EMP", FullName = "Test29", IsActive = true, CompanyId = 1
        });
        db.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeId    = "TC29_EMP", BasicPay = 30_000, IsActive = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
            CreatedAt     = DateTime.UtcNow
        });
        db.Payslips.Add(new Payslip
        {
            EmployeeId = "TC29_EMP", Month = 7, Year = 2026,
            BasicPay = 30_000, GrossEarnings = 44_850, NetPay = 42_850,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = BuildSvc(db);
        var result = await svc.BulkGeneratePayslipsAsync(new BulkPayrollDto
        {
            Month = 7, Year = 2026, CompanyId = 1, Overwrite = false
        });

        Assert.Equal(0, result.Generated);
        Assert.Equal(1, result.Skipped);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-30 — BULK: Overwrite=true with existing payslip → Generated=1, Skipped=0
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC30_Bulk_Overwrite_True_ExistingPayslip_Regenerated()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "TC30_EMP", FullName = "Test30", IsActive = true, CompanyId = 1
        });
        db.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeId    = "TC30_EMP", BasicPay = 30_000, IsActive = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
            CreatedAt     = DateTime.UtcNow
        });
        db.Payslips.Add(new Payslip
        {
            EmployeeId = "TC30_EMP", Month = 7, Year = 2026,
            BasicPay = 30_000, GrossEarnings = 44_850, NetPay = 42_850,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = BuildSvc(db);
        var result = await svc.BulkGeneratePayslipsAsync(new BulkPayrollDto
        {
            Month = 7, Year = 2026, CompanyId = 1, Overwrite = true
        });

        Assert.Equal(1, result.Generated);
        Assert.Equal(0, result.Skipped);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC-31 — VALIDATOR: FluentValidation rejects DaysPresent > WorkingDays
    //
    //   GeneratePayslipDtoValidator must surface at least one validation failure
    //   when DaysPresent (27) exceeds WorkingDays (26).
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC31_Validator_DaysPresentExceedsWorkingDays_FailsValidation()
    {
        var validator = new GeneratePayslipDtoValidator();
        var dto = new GeneratePayslipDto
        {
            EmployeeId  = "VALID_EMP",
            Month       = 7,
            Year        = 2026,
            WorkingDays = 26,
            DaysPresent = 27,       // invalid: 27 > 26
            BasicPay    = 30_000m
        };

        var result = validator.Validate(dto);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static PayrollService BuildSvc(HRMS.Infrastructure.Data.ApplicationDbContext db)
        => new PayrollService(db, new MockAuditService(), new MockNotificationService(),
                              new MockPayrollCalculator(), new MockLogger<PayrollService>());

    /// <summary>Seeds an employee + payslip in the given company and returns the payslip id.</summary>
    private static async Task<int> SeedPayslipAsync(
        HRMS.Infrastructure.Data.ApplicationDbContext db,
        string employeeId, int companyId, int month = 7, int year = 2026)
    {
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = employeeId, FullName = $"Emp {employeeId}",
            IsActive = true, CompanyId = companyId,
            CreatedAt = DateTime.UtcNow
        });
        var payslip = new Payslip
        {
            EmployeeId    = employeeId, CompanyId  = companyId,
            Month         = month,      Year       = year,
            BasicPay      = 50_000,     GrossEarnings = 55_000, NetPay = 50_000,
            CreatedAt     = DateTime.UtcNow
        };
        db.Payslips.Add(payslip);
        await db.SaveChangesAsync();
        return payslip.Id;
    }
}
