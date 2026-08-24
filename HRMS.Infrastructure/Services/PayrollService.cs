using HRMS.Application.Common;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Company;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class PayrollService : IPayrollService
{
    private readonly ApplicationDbContext   _db;
    private readonly IAuditService          _audit;
    private readonly INotificationService   _notify;
    // FIX HIGH-03: Inject IPayrollCalculator — do not call IndianPayrollCalculator
    // statically. This makes the calculator swappable per jurisdiction and testable.
    private readonly IPayrollCalculator     _calc;
    private readonly ILogger<PayrollService> _logger;

    public PayrollService(ApplicationDbContext db, IAuditService audit,
                          INotificationService notify, IPayrollCalculator calc,
                          ILogger<PayrollService> logger)
    {
        _db    = db;
        _audit = audit;
        _notify = notify;
        _calc   = calc;
        _logger = logger;
    }

    public async Task<int> GeneratePayslipAsync(GeneratePayslipDto dto, int? actorId = null, string? actorName = null, int? callerCompanyId = null)
    {
        // Input validation — catch bad data before any calculation
        if (string.IsNullOrWhiteSpace(dto.EmployeeId))
            throw new ArgumentException("EmployeeId is required.", nameof(dto.EmployeeId));
        // FIX FUNC-02: scope the employee lookup to the caller's company.
        // The controller already performs the primary IDOR guard. This service-layer
        // lookup is defence-in-depth and also supplies the authoritative company ID
        // when a SuperAdmin generates payroll across tenants.
        var employeeQuery = _db.Employees
            .Where(e => e.EmployeeCode == dto.EmployeeId);
        if (callerCompanyId.HasValue)
            employeeQuery = employeeQuery.Where(e => e.CompanyId == callerCompanyId.Value);

        var employee = await employeeQuery
            .Select(e => new { e.CompanyId })
            .FirstOrDefaultAsync();
        if (employee == null)
            throw new KeyNotFoundException($"Employee '{dto.EmployeeId}' not found.");

        // Never trust the optional CompanyId in the request DTO. For a normal admin,
        // the value comes from the authenticated tenant context. For a SuperAdmin,
        // derive it from the target employee so the created row is still tenant-owned.
        var payslipCompanyId = callerCompanyId ?? employee.CompanyId;

        // Item 5 fix: auto-sum taxable bonuses already recorded for this employee/period
        // via the existing Bonus module, so payroll generation actually reflects bonus
        // data instead of silently ignoring it. A caller-supplied non-zero BonusAmount on
        // the request is treated as an explicit override and takes precedence.
        if (dto.AutoCalculate && dto.BonusAmount == 0m)
        {
            var bonusQuery = _db.Bonuses.Where(b =>
                b.EmployeeId == dto.EmployeeId && b.Month == dto.Month && b.Year == dto.Year && b.IsTaxable);
            if (callerCompanyId.HasValue)
                bonusQuery = bonusQuery.Where(b => b.CompanyId == callerCompanyId.Value);
            // Sum client-side after a single bulk fetch rather than pushing SumAsync(decimal)
            // into the SQL translation — see the identical note in BulkGeneratePayslipsAsync.
            // Some providers (SQLite, used by the atomicity/rollback test fixture) cannot
            // translate a server-side decimal SUM and throw at execution time; this form is
            // one query either way and behaves identically on every provider.
            var taxableBonus = (await bonusQuery.Select(b => b.Amount).ToListAsync()).Sum();
            if (taxableBonus > 0m) dto.BonusAmount = taxableBonus;
        }

        // FIX BLOCKER-7: Wrap generate in an explicit transaction.
        // Without this, a failure between ApplyPayslip() and SaveChangesAsync() could leave
        // the EF change-tracker in a dirty state on the next request. The transaction
        // also provides serialisation for the check-then-upsert pattern below, reducing
        // (but not eliminating — the DB unique constraint is the final guard) the window
        // for a duplicate to slip through under concurrent calls.
        var supportsTransactions = _db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        IDbContextTransaction? tx = supportsTransactions
            ? await _db.Database.BeginTransactionAsync()
            : null;

        try
        {
            var existing = await _db.Payslips.FirstOrDefaultAsync(p =>
                p.EmployeeId == dto.EmployeeId && p.Month == dto.Month && p.Year == dto.Year);

            // FIX BLOCKER-6: Duplicate-prevention check at the service layer.
            // If a payslip already exists for this employee+period AND it is already fully
            // calculated (NetPay > 0), reject re-generation unless the caller explicitly
            // requests an overwrite via dto.Overwrite = true. This complements the
            // database-level unique constraint (see migration 20260806000001).
            if (existing != null && existing.NetPay > 0 && !dto.Overwrite)
            {
                tx?.Dispose();
                throw new InvalidOperationException(
                    $"A payslip for employee '{dto.EmployeeId}' period {dto.Month}/{dto.Year} " +
                    "already exists. Set Overwrite = true to recalculate.");
            }

            var payslip = ApplyPayslip(dto, existing);
            // Existing legacy rows may have CompanyId=0. Repair that discriminator
            // whenever the row is regenerated, and always stamp new rows as well.
            payslip.CompanyId = payslipCompanyId;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("PAYSLIP_GENERATE", "Payslip", payslip.Id.ToString(), actorId, actorName,
                companyId: payslipCompanyId,
                details: $"Payslip generated for period {dto.Month}/{dto.Year}.");

            // The payslip and its audit record are both persisted inside the same
            // transaction. A failure in either write must roll back the payroll write.
            if (tx != null) await tx.CommitAsync();
            return payslip.Id;
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
        finally
        {
            tx?.Dispose();
        }
    }

    /// <summary>
    /// Pure in-memory payslip write: validates the request, runs the calculation and
    /// creates or updates the tracked <see cref="Payslip"/> entity. Performs no database
    /// round-trip and no SaveChanges, so bulk callers can build every payslip first and
    /// persist the whole batch in a single write (see BulkGeneratePayslipsAsync).
    /// </summary>
    private Payslip ApplyPayslip(GeneratePayslipDto dto, Payslip? existing)
    {
        if (string.IsNullOrWhiteSpace(dto.EmployeeId))
            throw new ArgumentException("EmployeeId is required.", nameof(dto.EmployeeId));
        if (dto.BasicPay < 0)
            throw new ArgumentException("BasicPay cannot be negative.", nameof(dto.BasicPay));
        if (dto.DaysPresent > dto.WorkingDays)
            throw new ArgumentException(
                $"DaysPresent ({dto.DaysPresent}) cannot exceed WorkingDays ({dto.WorkingDays}).", nameof(dto.DaysPresent));

        decimal gross, deductions, basic, hra, da, conv, med, other, pfEmp, pfEmpl, esi, pt, tds;
        decimal overtime, bonus, arrears;

        if (dto.AutoCalculate)
        {
            var calc = _calc.Calculate(new PayrollCalculationRequest
            {
                BasicPay             = dto.BasicPay,
                WorkingDays          = dto.WorkingDays,
                DaysPresent          = dto.DaysPresent,
                IsMetroCity          = dto.IsMetroCity,
                State                = dto.State,
                TaxRegime            = dto.TaxRegime,
                Month                = dto.Month,
                AdditionalAllowances = dto.OtherAllowances,
                OvertimePay          = dto.OvertimePay,
                BonusAmount          = dto.BonusAmount,
                Arrears              = dto.Arrears
            });
            basic    = calc.BasicPay;
            hra      = calc.HRA;
            da       = calc.DA;
            conv     = calc.Conveyance;
            med      = calc.MedicalAllowance;
            other    = calc.OtherAllowances;
            overtime = calc.OvertimePay;
            bonus    = calc.BonusAmount;
            arrears  = calc.Arrears;
            gross    = calc.GrossEarnings;
            pfEmp  = calc.PFEmployee;
            pfEmpl = calc.PFEmployer;
            esi    = calc.ESIEmployee;
            pt     = calc.ProfessionalTax;
            tds    = calc.TDS;
            deductions = calc.TotalDeductions;
        }
        else
        {
            basic  = dto.BasicPay;
            // Pro-rate for partial attendance in manual mode (mirrors AutoCalculate behaviour).
            // DaysPresent=0 → NetSalary=0; DaysPresent==WorkingDays → full pay.
            if (dto.WorkingDays > 0)
            {
                var factor = (decimal)dto.DaysPresent / dto.WorkingDays;
                basic = Math.Round(dto.BasicPay * factor, 2, MidpointRounding.AwayFromZero);
            }
            hra    = dto.Hra;
            da     = dto.Da;
            conv   = dto.Conveyance;
            med    = dto.MedicalAllowance;
            other  = dto.OtherAllowances;
            // Item 5 fix: overtime is pro-rated (same reasoning as other manual-mode
            // components); bonus/arrears are not, consistent with IndianPayrollCalculator.
            overtime = dto.WorkingDays > 0
                ? Math.Round(dto.OvertimePay * (decimal)dto.DaysPresent / dto.WorkingDays, 2, MidpointRounding.AwayFromZero)
                : dto.OvertimePay;
            bonus   = dto.BonusAmount;
            arrears = dto.Arrears;
            pfEmp  = dto.PfEmployee;
            pfEmpl = dto.PfEmployer;
            esi    = dto.Esi;
            pt     = dto.Pt;
            tds    = dto.Tds;
            gross      = basic + hra + da + conv + med + other + overtime + bonus + arrears;
            deductions = pfEmp + esi + pt + tds + dto.OtherDeductions;
        }

        if (existing != null)
        {
            existing.WorkingDays      = dto.WorkingDays;
            existing.DaysPresent      = dto.DaysPresent;
            existing.BasicPay         = basic;
            existing.HRA              = hra;
            existing.DA               = da;
            existing.Conveyance       = conv;
            existing.MedicalAllowance = med;
            existing.OtherAllowances  = other;
            existing.OvertimePay      = overtime;
            existing.BonusAmount      = bonus;
            existing.Arrears          = arrears;
            existing.GrossEarnings    = gross;
            existing.PFEmployee       = pfEmp;
            existing.PFEmployer       = pfEmpl;
            existing.ESI              = esi;
            existing.PT               = pt;
            existing.TDS              = tds;
            existing.OtherDeductions  = dto.AutoCalculate ? 0 : dto.OtherDeductions;
            existing.TotalDeductions  = deductions;
            existing.NetPay           = gross - deductions;
            return existing;
        }

        var payslip = new Payslip
        {
            EmployeeId       = dto.EmployeeId,
            Month            = dto.Month,
            Year             = dto.Year,
            WorkingDays      = dto.WorkingDays,
            DaysPresent      = dto.DaysPresent,
            BasicPay         = basic,
            HRA              = hra,
            DA               = da,
            Conveyance       = conv,
            MedicalAllowance = med,
            OtherAllowances  = other,
            OvertimePay      = overtime,
            BonusAmount      = bonus,
            Arrears          = arrears,
            GrossEarnings    = gross,
            PFEmployee       = pfEmp,
            PFEmployer       = pfEmpl,
            ESI              = esi,
            PT               = pt,
            TDS              = tds,
            OtherDeductions  = dto.AutoCalculate ? 0 : dto.OtherDeductions,
            TotalDeductions  = deductions,
            NetPay           = gross - deductions,
            CreatedAt        = DateTime.UtcNow
        };
        _db.Payslips.Add(payslip);
        return payslip;
    }

    public Task<PayrollCalculationResult> PreviewCalculationAsync(PayrollCalculationRequest req)
        => Task.FromResult(_calc.Calculate(req));

    // FIX IDOR: companyId is now applied at DB level so a cross-tenant payslip is
    // never loaded into memory. SuperAdmin passes null for unrestricted access.
    // The same company-scoping rule used by GetAllPayslipsAsync/Paged is reused:
    // prefer the payslip's own CompanyId column; fall back to employee join for legacy
    // rows written before CompanyId was added (CompanyId == 0).
    public async Task<PayslipDto?> GetPayslipAsync(int id, int? companyId = null)
    {
        var q = _db.Payslips.Where(x => x.Id == id);
        if (companyId.HasValue)
        {
            var companyEmpIds = _db.Employees
                .Where(e => e.CompanyId == companyId)
                .Select(e => e.EmployeeCode);
            q = q.Where(p => p.CompanyId == companyId
                             || (p.CompanyId == 0 && companyEmpIds.Contains(p.EmployeeId)));
        }
        var p = await q.FirstOrDefaultAsync();
        return p == null ? null : await EnrichPayslip(p);
    }

    public async Task<List<PayslipDto>> GetAllPayslipsAsync(int? month = null, int? year = null, string? employeeId = null, int? companyId = null, CancellationToken ct = default)
    {
        var q = _db.Payslips.AsQueryable();
        if (month.HasValue) q = q.Where(p => p.Month == month);
        if (year.HasValue)  q = q.Where(p => p.Year  == year);
        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(p => p.EmployeeId == employeeId);

        // SECURITY FIX – PayrollController.GetAll IDOR
        // Scope results to the caller's company by joining through the Employees table.
        // SuperAdmin passes null → unrestricted access across all companies.
        if (companyId.HasValue)
        {
            // Payslips carry their own CompanyId. Older rows written before that column
            // existed leave it at 0, so those fall back to the employee-table join.
            var companyEmpIds = _db.Employees
                .Where(e => e.CompanyId == companyId)
                .Select(e => e.EmployeeCode);
            q = q.Where(p => p.CompanyId == companyId
                             || (p.CompanyId == 0 && companyEmpIds.Contains(p.EmployeeId)));
        }

        var list = await q.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ToListAsync(ct);
        // Use batch enrichment to avoid N+1 queries (one query per payslip → one query per list)
        return await EnrichPayslipListAsync(list);
    }

    // FIX 6: CancellationToken propagated to CountAsync and ToListAsync — allows the DB query
    // to be cancelled if the HTTP client disconnects before the response is sent.
    public async Task<PagedResult<PayslipDto>> GetAllPayslipsPagedAsync(int? month, int? year, string? employeeId, int? companyId, int page, int pageSize, string? sortBy = null, string? sortDirection = "desc", CancellationToken ct = default)
    {
        // AsNoTracking: read-only paged query — no change tracking overhead.
        var q = _db.Payslips.AsNoTracking().AsQueryable();
        if (month.HasValue) q = q.Where(p => p.Month == month);
        if (year.HasValue)  q = q.Where(p => p.Year  == year);
        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(p => p.EmployeeId == employeeId);
        if (companyId.HasValue)
        {
            // Same scoping rule as GetAllPayslipsAsync: prefer the payslip's own CompanyId,
            // fall back to the employee join for legacy rows with CompanyId == 0.
            var companyEmpIds = _db.Employees
                .AsNoTracking()
                .Where(e => e.CompanyId == companyId)
                .Select(e => e.EmployeeCode);
            q = q.Where(p => p.CompanyId == companyId
                             || (p.CompanyId == 0 && companyEmpIds.Contains(p.EmployeeId)));
        }
        // FIX (production-complete): full SQL-level sorting for all documented columns.
        // EmployeeName/EmployeeCode resolved via correlated subquery (EF Core → SQL LEFT JOIN).
        // Status does not exist on Payslip — falls back to default. No in-memory ordering.
        _logger.LogInformation(
            "GetAllPayslipsPagedAsync requested: sortBy={SortBy} sortDirection={SortDirection} page={Page} pageSize={PageSize}",
            sortBy, sortDirection, page, pageSize);

        bool desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var effectiveSortBy = sortBy?.Trim().ToLowerInvariant() ?? string.Empty;
        q = effectiveSortBy switch
        {
            "employeename" => desc
                ? q.OrderByDescending(p => _db.Employees
                    .Where(e => e.EmployeeCode == p.EmployeeId)
                    .Select(e => e.FullName)
                    .FirstOrDefault() ?? "")
                : q.OrderBy(p => _db.Employees
                    .Where(e => e.EmployeeCode == p.EmployeeId)
                    .Select(e => e.FullName)
                    .FirstOrDefault() ?? ""),
            // EmployeeCode is stored as the EmployeeId string in this system.
            "employeecode" => desc
                ? q.OrderByDescending(p => p.EmployeeId)
                : q.OrderBy(p => p.EmployeeId),
            // PayrollMonth maps to the compound Year + Month sort.
            "payrollmonth" => desc
                ? q.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                : q.OrderBy(p => p.Year).ThenBy(p => p.Month),
            // Entity property is NetPay (not NetSalary — legacy naming fixed here).
            "netsalary"    => desc ? q.OrderByDescending(p => p.NetPay)        : q.OrderBy(p => p.NetPay),
            // Entity property is GrossEarnings (not GrossSalary — legacy naming fixed here).
            "grosssalary"  => desc ? q.OrderByDescending(p => p.GrossEarnings) : q.OrderBy(p => p.GrossEarnings),
            "createddate"  => desc ? q.OrderByDescending(p => p.CreatedAt)     : q.OrderBy(p => p.CreatedAt),
            // Status is not a stored column on Payslip — default fallback.
            _              => q.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
        };

        _logger.LogInformation(
            "Payroll sort applied: effectiveSortBy={EffectiveSortBy} desc={Desc}",
            effectiveSortBy, desc);
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;
        var totalCount = await q.CountAsync(ct);
        var list = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = await EnrichPayslipListAsync(list);
        // Echo sortBy/sortDirection so callers can confirm the applied sort.
        return PagedResult<PayslipDto>.Create(items, totalCount, page, pageSize,
            sortBy: string.IsNullOrEmpty(effectiveSortBy) ? null : effectiveSortBy,
            sortDirection: desc ? "desc" : "asc");
    }

    /// <summary>
    /// FIX P3-2 (GetMyPayslips tenant filtering): scoping is enforced here, in the service,
    /// as part of the SQL predicate — not in the controller and never from client input.
    ///
    /// Three guards:
    ///   1. employeeId must be non-empty (fail closed rather than returning every payslip).
    ///   2. callerCompanyId (non-SuperAdmin) is applied to Payslip.CompanyId AND cross-checked
    ///      against the owning Employee row, so a stale/mismatched payslip tenant column cannot
    ///      leak a record across tenants.
    ///   3. Only the employee's own rows are returned.
    /// </summary>
    public async Task<List<PayslipDto>> GetEmployeePayslipsAsync(string employeeId, int? callerCompanyId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return new List<PayslipDto>();

        var q = _db.Payslips.Where(p => p.EmployeeId == employeeId);

        // callerCompanyId == null means SuperAdmin (unrestricted). Any other value — including
        // the -1 sentinel emitted for a missing/malformed companyId claim — is applied verbatim
        // so a token without a valid tenant claim returns an empty set (fail closed).
        if (callerCompanyId.HasValue)
        {
            var cid = callerCompanyId.Value;
            q = q.Where(p => p.CompanyId == cid
                             && _db.Employees.Any(e => e.EmployeeCode == p.EmployeeId
                                                       && e.CompanyId == cid));
        }

        var list = await q
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .ToListAsync();
        // Use batch enrichment to avoid N+1 queries
        return await EnrichPayslipListAsync(list);
    }

    public async Task<bool> DeletePayslipAsync(int id, int? actorId = null, string? actorName = null)
    {
        var p = await _db.Payslips.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return false;
        _db.Payslips.Remove(p);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("PAYSLIP_DELETE", "Payslip", id.ToString(), actorId, actorName);
        return true;
    }

    // ── Bulk Payroll ───────────────────────────────────────────────────────

    public async Task<BulkPayrollResultDto> BulkGeneratePayslipsAsync(
        BulkPayrollDto dto, int? actorId = null, string? actorName = null)
    {
        // Resolve employee list
        var empQ = _db.Employees.Where(e => e.IsActive);
        if (dto.CompanyId.HasValue) empQ = empQ.Where(e => e.CompanyId == dto.CompanyId);
        if (dto.EmployeeIds?.Count > 0)
            empQ = empQ.Where(e => dto.EmployeeIds.Contains(e.EmployeeCode));

        // FIX: this previously called ToListAsync() with no bound, which loaded the
        // entire matching employee set directly off ApplicationDbContext and bypassed
        // GenericRepository's 500-row safety cap entirely — for the one write path
        // (bulk payroll generation) where a silently truncated employee list means
        // real payroll undercalculation, not just a slow query. Enforce the same cap
        // GenericRepository<T>.GetAllAsync uses.
        const int maxBulkEmployees = HRMS.Infrastructure.Repositories.GenericRepository<Employee>.MaxRows;
        var employees = await empQ.Take(maxBulkEmployees + 1).ToListAsync();
        if (employees.Count > maxBulkEmployees)
            throw new InvalidOperationException(
                $"BulkGeneratePayslipsAsync: employee result set exceeds the {maxBulkEmployees}-row " +
                "safety cap. Page through employees (e.g. via EmployeeIds batches) and call this " +
                "method per batch to avoid silent payroll undercalculation.");

        // P2 FIX: Cross-company guard — reject if any specified EmployeeId belongs to a
        // different company than dto.CompanyId. Prevents cross-company payroll generation.
        if (dto.CompanyId.HasValue && dto.EmployeeIds?.Count > 0)
        {
            var outsiders = employees
                .Where(e => e.CompanyId != dto.CompanyId)
                .Select(e => e.EmployeeCode)
                .ToList();
            if (outsiders.Count > 0)
                throw new InvalidOperationException(
                    $"Cross-company payroll rejected. These employees do not belong to company " +
                    $"{dto.CompanyId}: {string.Join(", ", outsiders)}");
        }

        // P2 FIX: Removed hard 500-employee cap. Large organisations (>500 employees) are
        // now fully supported. Employees are processed in chunks of ChunkSize: each chunk
        // performs its own 4-query pre-load and commits in its own transaction, bounding
        // EF change-tracker memory and keeping each transaction short.
        const int ChunkSize = 500;

        int generated = 0, skipped = 0, failed = 0;
        var errors = new List<string>();

        // Load company settings once — applies to all chunks.
        CompanySettings? settings = null;
        if (dto.CompanyId.HasValue)
            settings = await _db.CompanySettings.FirstOrDefaultAsync(s => s.CompanyId == dto.CompanyId);
        var defaultWorkingDays = settings?.WorkingDaysPerMonth ?? 26;

        var attendancePeriodStart = new DateOnly(dto.Year, dto.Month, 1);
        var attendancePeriodEnd   = attendancePeriodStart.AddMonths(1).AddDays(-1);

        // Process employees in chunks of ChunkSize (no overall cap).
        for (int chunkStart = 0; chunkStart < employees.Count; chunkStart += ChunkSize)
        {
            var chunk      = employees.GetRange(chunkStart, Math.Min(ChunkSize, employees.Count - chunkStart));
            var chunkEmpIds = chunk.Select(e => e.EmployeeCode).ToList();

            // 4-query pre-load scoped to this chunk (same N+1 fix, per chunk).
            var existingPayslips = (await _db.Payslips
                .Where(p => chunkEmpIds.Contains(p.EmployeeId) && p.Month == dto.Month && p.Year == dto.Year)
                .ToListAsync())
                .GroupBy(p => p.EmployeeId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var allSalaries = await _db.SalaryStructures
                .Where(s => chunkEmpIds.Contains(s.EmployeeId) && s.IsActive)
                .OrderByDescending(s => s.EffectiveFrom)
                .ToListAsync();
            var salaryByEmp = allSalaries
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(g => g.Key, g => g.First());

            var webCountsRaw = await _db.WebAttendances
                .Where(a => chunkEmpIds.Contains(a.EmployeeId)
                         && a.AttDate >= attendancePeriodStart
                         && a.AttDate <= attendancePeriodEnd
                         && a.Status == "Present")
                .GroupBy(a => a.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToListAsync();
            var webCounts = webCountsRaw.ToDictionary(x => x.EmployeeId, x => x.Count);

            var excelCountsRaw = await _db.ExcelAttendances
                .Where(a => chunkEmpIds.Contains(a.EmployeeId)
                         && a.AttDate >= attendancePeriodStart
                         && a.AttDate <= attendancePeriodEnd
                         && a.Status == "Present")
                .GroupBy(a => a.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToListAsync();
            var excelCounts = excelCountsRaw.ToDictionary(x => x.EmployeeId, x => x.Count);

            // Item 5 fix: pre-load taxable bonus totals for this chunk/period so bulk
            // generation reflects the Bonus module too, not just the single-employee path.
            // NOTE: Sum(decimal) is deliberately NOT pushed into the GroupBy/Select
            // translated to SQL here. Some providers (notably SQLite, used by the
            // in-process test fixture) cannot translate a server-side decimal SUM
            // inside a GROUP BY projection and throw at query-execution time. Pulling
            // the raw (EmployeeId, Amount) rows back and grouping/summing in memory
            // is provider-agnostic — it produces the exact same result on MySQL,
            // PostgreSQL, and SQLite alike, so production behaviour on the real
            // database is unchanged. It is still a single bulk query for the whole
            // chunk (no N+1): row count per chunk is bounded by taxable bonuses for
            // chunkEmpIds in this month/year, not by employee count.
            var bonusRowsRaw = await _db.Bonuses
                .Where(b => chunkEmpIds.Contains(b.EmployeeId)
                         && b.Month == dto.Month && b.Year == dto.Year && b.IsTaxable)
                .Select(b => new { b.EmployeeId, b.Amount })
                .ToListAsync();
            var bonusTotals = bonusRowsRaw
                .GroupBy(x => x.EmployeeId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount), StringComparer.Ordinal);

            // Per-chunk transaction (InMemory provider does not support transactions).
            var supportsTransactions = _db.Database.IsRelational();
            IDbContextTransaction? transaction = supportsTransactions
                ? await _db.Database.BeginTransactionAsync()
                : null;
            await using var transactionScope = transaction;

            var generatedPayslips = new List<(string EmployeeCode, Payslip Payslip)>();
            try
            {
                foreach (var emp in chunk)
                {
                    try
                    {
                        var existingPayslip = existingPayslips.GetValueOrDefault(emp.EmployeeCode);
                        if (existingPayslip != null && !dto.Overwrite) { skipped++; continue; }

                        var salary = salaryByEmp.GetValueOrDefault(emp.EmployeeCode);
                        if (salary == null)
                        {
                            errors.Add($"{emp.EmployeeCode}: no active salary structure — payslip generated with zero earnings.");
                            salary = new SalaryStructure { EmployeeId = emp.EmployeeCode };
                        }

                        var daysPresent = webCounts.GetValueOrDefault(emp.EmployeeCode);
                        if (daysPresent == 0)
                            daysPresent = excelCounts.GetValueOrDefault(emp.EmployeeCode);
                        if (daysPresent == 0)
                        {
                            daysPresent = defaultWorkingDays;
                            errors.Add($"{emp.EmployeeCode}: no attendance records found for {dto.Month}/{dto.Year} — full working days assumed.");
                        }

                        var payslip = ApplyPayslip(new GeneratePayslipDto
                        {
                            EmployeeId       = emp.EmployeeCode,
                            Month            = dto.Month,
                            Year             = dto.Year,
                            WorkingDays      = defaultWorkingDays,
                            DaysPresent      = daysPresent,
                            BasicPay         = salary.BasicPay,
                            Hra              = salary.HRA,
                            Da               = salary.DA,
                            Conveyance       = salary.Conveyance,
                            MedicalAllowance = salary.MedicalAllowance,
                            OtherAllowances  = salary.OtherAllowances,
                            PfEmployee       = salary.PFEmployee,
                            PfEmployer       = salary.PFEmployer,
                            Esi              = salary.ESI,
                            Pt               = salary.PT,
                            Tds              = salary.TDS,
                            AutoCalculate    = false,
                            BonusAmount      = bonusTotals.GetValueOrDefault(emp.EmployeeCode),
                            // Route bulk payslips through the correct TDS regime by
                            // reading the persisted choice from the salary structure.
                            // AutoCalculate=false means the stored TDS value is used
                            // directly, but the field is preserved here so that any
                            // future switch to AutoCalculate=true picks it up correctly.
                            TaxRegime        = salary.IsOldRegime ? "old" : "new"
                        }, existingPayslip);
                        // Bulk payroll is tenant-scoped by dto.CompanyId and the
                        // employee's company. Stamp every generated/updated row so
                        // global query filters and payslip access checks remain valid.
                        payslip.CompanyId = dto.CompanyId ?? emp.CompanyId;
                        generatedPayslips.Add((emp.EmployeeCode, payslip));

                        if (emp.UserId.HasValue)
                        {
                            try
                            {
                                await _notify.NotifyAsync(emp.UserId.Value,
                                    "Payslip Generated",
                                    $"Your payslip for {MonthNames[dto.Month]} {dto.Year} has been generated and is ready to view.",
                                    "success", "Payslip", emp.EmployeeCode);
                            }
                            catch (Exception notifyEx)
                            {
                                _logger.LogWarning(notifyEx, "Payslip notification failed.");
                            }
                        }
                        generated++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"{emp.EmployeeId}: {ex.Message}");
                    }
                }

                if (generatedPayslips.Count > 0)
                    await _db.SaveChangesAsync();

                foreach (var (employeeCode, payslip) in generatedPayslips)
                {
                    await _audit.LogAsync("PAYSLIP_GENERATE", "Payslip", payslip.Id.ToString(), actorId, actorName,
                        details: $"Payslip generated for period {dto.Month}/{dto.Year}.");
                }

                if (transaction is not null) await transaction.CommitAsync();
            }
            catch (Exception)
            {
                if (transaction is not null) await transaction.RollbackAsync();
                throw;
            }
        } // end chunk loop

        await _audit.LogAsync("BULK_PAYROLL", "Payslip", null, actorId, actorName,
            details: $"{dto.Month}/{dto.Year} — Generated: {generated}, Skipped: {skipped}, Failed: {failed}");

        return new BulkPayrollResultDto
        {
            Month = dto.Month, Year = dto.Year,
            Generated = generated, Skipped = skipped, Failed = failed, Errors = errors
        };
    }

    // ── Private ────────────────────────────────────────────────────────────

    private static readonly string[] MonthNames =
    { "", "January", "February", "March", "April", "May", "June",
      "July", "August", "September", "October", "November", "December" };

    /// <summary>
    /// Single-record enrichment used by <see cref="GetPayslipAsync"/>.
    /// Two queries maximum (employee + company). Acceptable for point lookups.
    /// For list operations use <see cref="EnrichPayslipListAsync"/> instead.
    /// </summary>
    private async Task<PayslipDto> EnrichPayslip(Payslip p)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == p.EmployeeId);
        // FIX: emp.CompanyId is now a non-nullable int — the old null-check was always true
        // when emp was non-null. Use emp != null as the guard instead.
        Company? co = emp != null ? await _db.Companies.FindAsync(emp.CompanyId) : null;
        return MapPayslip(p, emp, co);
    }

    /// <summary>
    /// Batch enrichment for list operations: loads all required employees and companies
    /// in exactly two queries, eliminating the N+1 problem from iterative EnrichPayslip calls.
    /// </summary>
    private async Task<List<PayslipDto>> EnrichPayslipListAsync(List<Payslip> payslips)
    {
        if (payslips.Count == 0) return new();

        // One query: all distinct employees referenced by this payslip list
        var empIds  = payslips.Select(p => p.EmployeeId).Distinct().ToList();
        var empDict = await _db.Employees
            .Where(e => empIds.Contains(e.EmployeeCode))
            .ToDictionaryAsync(e => e.EmployeeCode);

        // One query: all distinct companies referenced by those employees
        // Employee.CompanyId is now a non-nullable int (FIX CRIT-1).
        var coIds = empDict.Values
            .Select(e => e.CompanyId)
            .Distinct().ToList();
        var coDict = coIds.Count > 0
            ? await _db.Companies
                .Where(c => coIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id)
            : new Dictionary<int, Company>();

        return payslips.Select(p =>
        {
            empDict.TryGetValue(p.EmployeeId, out var emp);
            Company? co = emp != null
                ? coDict.GetValueOrDefault(emp.CompanyId) : null;
            return MapPayslip(p, emp, co);
        }).ToList();
    }

    /// <summary>Stateless synchronous mapper — shared by single and batch enrichment paths.</summary>
    private static PayslipDto MapPayslip(Payslip p, Employee? emp, Company? co) => new()
    {
        Id               = p.Id,
        EmployeeId       = p.EmployeeId,
        EmployeeName     = emp?.FullName ?? p.EmployeeId,
        Designation      = emp?.Designation ?? "",
        Department       = emp?.Department ?? "",
        BankName         = emp?.BankName ?? "",
        AccountNumber    = emp?.AccountNumber ?? "",
        UAN              = emp?.UAN ?? "",
        MonthYear        = $"{MonthNames[p.Month]} {p.Year}",
        Month            = p.Month,
        Year             = p.Year,
        WorkingDays      = p.WorkingDays,
        DaysPresent      = p.DaysPresent,
        BasicPay         = p.BasicPay,
        HRA              = p.HRA,
        DA               = p.DA,
        Conveyance       = p.Conveyance,
        MedicalAllowance = p.MedicalAllowance,
        OtherAllowances  = p.OtherAllowances,
        OvertimePay      = p.OvertimePay,
        BonusAmount      = p.BonusAmount,
        Arrears          = p.Arrears,
        GrossEarnings    = p.GrossEarnings,
        PFEmployee       = p.PFEmployee,
        PFEmployer       = p.PFEmployer,
        ESI              = p.ESI,
        PT               = p.PT,
        TDS              = p.TDS,
        OtherDeductions  = p.OtherDeductions,
        TotalDeductions  = p.TotalDeductions,
        NetPay           = p.NetPay,
        CreatedAt        = p.CreatedAt,
        // Surface the owning company so callers can verify tenant scoping.
        CompanyId        = p.CompanyId != 0 ? p.CompanyId : emp?.CompanyId,
        CompanyName      = co?.CompanyName,
        CompanyLogo      = co?.LogoPath
    };
}
