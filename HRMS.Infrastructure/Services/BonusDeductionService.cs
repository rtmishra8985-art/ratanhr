using HRMS.Application.Common;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class BonusDeductionService : IBonusDeductionService
{
    private readonly ApplicationDbContext _ctx;
    public BonusDeductionService(ApplicationDbContext ctx) => _ctx = ctx;

    // ── Bonus ──────────────────────────────────────────────────────────────

    // FIX SEC-02: company-scoped GetById — returns null for cross-tenant IDs when
    // callerCompanyId is provided, so callers receive a clean 404 without exposing
    // that the record exists in another tenant.
    public async Task<BonusDto?> GetBonusByIdAsync(int id, int? callerCompanyId = null)
    {
        Bonus? b;
        if (callerCompanyId.HasValue)
        {
            // JOIN-scope: only returns the bonus when its employee belongs to the caller's company.
            b = await (from bon in _ctx.Bonuses
                       join e in _ctx.Employees on bon.EmployeeId equals e.EmployeeCode
                       where bon.Id == id && e.CompanyId == callerCompanyId
                       select bon).FirstOrDefaultAsync();
        }
        else
        {
            // SuperAdmin path — unrestricted.
            b = await _ctx.Bonuses.FirstOrDefaultAsync(x => x.Id == id);
        }
        return b == null ? null : MapBonus(b);
    }

    // FIX FUNC-01: company-scoped list — when callerCompanyId is provided the query
    // is constrained via JOIN on Employee.CompanyId so the list never leaks cross-tenant rows.
    public async Task<List<BonusDto>> GetBonusesAsync(string? employeeId, int? callerCompanyId, int? month, int? year)
    {
        IQueryable<Bonus> q;
        if (callerCompanyId.HasValue)
        {
            q = from b in _ctx.Bonuses
                join e in _ctx.Employees on b.EmployeeId equals e.EmployeeCode
                where e.CompanyId == callerCompanyId
                select b;
        }
        else
        {
            q = _ctx.Bonuses.AsQueryable();
        }
        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(b => b.EmployeeId == employeeId);
        if (month.HasValue) q = q.Where(b => b.Month == month);
        if (year.HasValue)  q = q.Where(b => b.Year  == year);
        var rows = await q.OrderByDescending(b => b.CreatedAt).ToListAsync();
        return rows.Select(MapBonus).ToList();
    }

    public async Task<PagedResult<BonusDto>> GetBonusesPagedAsync(string? employeeId, int? month, int? year, int page, int pageSize)
    {
        var q = _ctx.Bonuses.AsQueryable();
        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(b => b.EmployeeId == employeeId);
        if (month.HasValue) q = q.Where(b => b.Month == month);
        if (year.HasValue)  q = q.Where(b => b.Year  == year);
        q = q.OrderByDescending(b => b.CreatedAt);
        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total = await q.CountAsync();
        var rows  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<BonusDto>.Create(rows.Select(MapBonus).ToList(), total, page, pageSize);
    }

    /// <summary>
    /// FIX IDOR: company-scoped paged bonus list used by the controller when
    /// the caller is a non-superadmin. When <paramref name="companyId"/> is provided
    /// the query is constrained to employees of that company via a JOIN, so a company
    /// admin can never enumerate bonuses belonging to a different tenant even if they
    /// omit the employeeId filter.
    /// </summary>
    public async Task<PagedResult<BonusDto>> GetBonusesPagedScopedAsync(
        string? employeeId, int? companyId, int? month, int? year, int page, int pageSize)
    {
        IQueryable<HRMS.Domain.Entities.Payroll.Bonus> q;

        if (companyId.HasValue)
        {
            // Scope to caller's company via JOIN — prevents cross-tenant listing
            q = from b in _ctx.Bonuses
                join e in _ctx.Employees on b.EmployeeId equals e.EmployeeCode
                where e.CompanyId == companyId
                select b;
        }
        else
        {
            // Superadmin — unrestricted
            q = _ctx.Bonuses.AsQueryable();
        }

        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(b => b.EmployeeId == employeeId);
        if (month.HasValue) q = q.Where(b => b.Month == month);
        if (year.HasValue)  q = q.Where(b => b.Year  == year);
        q = q.OrderByDescending(b => b.CreatedAt);
        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total = await q.CountAsync();
        var rows  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<BonusDto>.Create(rows.Select(MapBonus).ToList(), total, page, pageSize);
    }

    public async Task<int> AddBonusAsync(CreateBonusDto dto)
    {
        if (dto.Amount <= 0)
            throw new ArgumentException("Bonus amount must be greater than zero.", nameof(dto.Amount));

        var b = new Bonus
        {
            EmployeeId      = dto.EmployeeId,
            BonusType       = dto.BonusType,
            Amount          = dto.Amount,
            Month           = dto.Month,
            Year            = dto.Year,
            Remarks         = dto.Remarks,
            IsTaxable       = dto.IsTaxable,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt       = DateTime.UtcNow
        };
        _ctx.Bonuses.Add(b);
        await _ctx.SaveChangesAsync();
        return b.Id;
    }

    public async Task<bool> UpdateBonusAsync(int id, CreateBonusDto dto, int? callerCompanyId = null)
    {
        // FIX IDOR: company-scoped JOIN query replaces FindAsync (which bypasses
        // EF Core global query filters). A Company A admin supplying a Company B
        // bonus ID now receives null → false rather than silently mutating the record.
        // SuperAdmin (callerCompanyId == null) retains unrestricted access.
        Bonus? b;
        if (callerCompanyId.HasValue)
        {
            b = await (from bon in _ctx.Bonuses
                       join e in _ctx.Employees on bon.EmployeeId equals e.EmployeeCode
                       where bon.Id == id && e.CompanyId == callerCompanyId
                       select bon).FirstOrDefaultAsync();
        }
        else
        {
            b = await _ctx.Bonuses.FirstOrDefaultAsync(x => x.Id == id);
        }
        if (b == null) return false;
        b.BonusType = dto.BonusType;
        b.Amount    = dto.Amount;
        b.Month     = dto.Month;
        b.Year      = dto.Year;
        b.Remarks   = dto.Remarks;
        b.IsTaxable = dto.IsTaxable;
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteBonusAsync(int id, int? callerCompanyId = null)
    {
        // FIX IDOR: same company-scoped JOIN lookup as UpdateBonusAsync.
        Bonus? b;
        if (callerCompanyId.HasValue)
        {
            b = await (from bon in _ctx.Bonuses
                       join e in _ctx.Employees on bon.EmployeeId equals e.EmployeeCode
                       where bon.Id == id && e.CompanyId == callerCompanyId
                       select bon).FirstOrDefaultAsync();
        }
        else
        {
            b = await _ctx.Bonuses.FirstOrDefaultAsync(x => x.Id == id);
        }
        if (b == null) return false;
        _ctx.Bonuses.Remove(b);
        await _ctx.SaveChangesAsync();
        return true;
    }

    // ── Deduction ─────────────────────────────────────────────────────────

    // FIX SEC-02: company-scoped GetById — mirrors the Bonus fix above.
    public async Task<DeductionDto?> GetDeductionByIdAsync(int id, int? callerCompanyId = null)
    {
        Deduction? d;
        if (callerCompanyId.HasValue)
        {
            d = await (from ded in _ctx.Deductions
                       join e in _ctx.Employees on ded.EmployeeId equals e.EmployeeCode
                       where ded.Id == id && e.CompanyId == callerCompanyId
                       select ded).FirstOrDefaultAsync();
        }
        else
        {
            d = await _ctx.Deductions.FirstOrDefaultAsync(x => x.Id == id);
        }
        return d == null ? null : MapDeduction(d);
    }

    public async Task<PagedResult<DeductionDto>> GetDeductionsPagedAsync(string? employeeId, int? month, int? year, int page, int pageSize)
    {
        var q = _ctx.Deductions.AsQueryable();
        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(d => d.EmployeeId == employeeId);
        if (month.HasValue) q = q.Where(d => d.Month == month);
        if (year.HasValue)  q = q.Where(d => d.Year  == year);
        q = q.OrderByDescending(d => d.CreatedAt);
        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total = await q.CountAsync();
        var rows  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<DeductionDto>.Create(rows.Select(MapDeduction).ToList(), total, page, pageSize);
    }

    /// <summary>
    /// FIX IDOR: company-scoped paged deduction list. When <paramref name="companyId"/>
    /// is provided the query is constrained via a JOIN to prevent cross-tenant listing.
    /// </summary>
    public async Task<PagedResult<DeductionDto>> GetDeductionsPagedScopedAsync(
        string? employeeId, int? companyId, int? month, int? year, int page, int pageSize)
    {
        IQueryable<HRMS.Domain.Entities.Payroll.Deduction> q;

        if (companyId.HasValue)
        {
            q = from d in _ctx.Deductions
                join e in _ctx.Employees on d.EmployeeId equals e.EmployeeCode
                where e.CompanyId == companyId
                select d;
        }
        else
        {
            q = _ctx.Deductions.AsQueryable();
        }

        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(d => d.EmployeeId == employeeId);
        if (month.HasValue) q = q.Where(d => d.Month == month);
        if (year.HasValue)  q = q.Where(d => d.Year  == year);
        q = q.OrderByDescending(d => d.CreatedAt);
        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total = await q.CountAsync();
        var rows  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<DeductionDto>.Create(rows.Select(MapDeduction).ToList(), total, page, pageSize);
    }

    // FIX FUNC-01: company-scoped list — mirrors the Bonus fix above.
    public async Task<List<DeductionDto>> GetDeductionsAsync(string? employeeId, int? callerCompanyId, int? month, int? year)
    {
        IQueryable<Deduction> q;
        if (callerCompanyId.HasValue)
        {
            q = from d in _ctx.Deductions
                join e in _ctx.Employees on d.EmployeeId equals e.EmployeeCode
                where e.CompanyId == callerCompanyId
                select d;
        }
        else
        {
            q = _ctx.Deductions.AsQueryable();
        }
        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(d => d.EmployeeId == employeeId);
        if (month.HasValue) q = q.Where(d => d.Month == month);
        if (year.HasValue)  q = q.Where(d => d.Year  == year);
        var rows = await q.OrderByDescending(d => d.CreatedAt).ToListAsync();
        return rows.Select(MapDeduction).ToList();
    }

    public async Task<int> AddDeductionAsync(CreateDeductionDto dto)
    {
        var d = new Deduction
        {
            EmployeeId      = dto.EmployeeId,
            DeductionType   = dto.DeductionType,
            Amount          = dto.Amount,
            Month           = dto.Month,
            Year            = dto.Year,
            Remarks         = dto.Remarks,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt       = DateTime.UtcNow
        };
        _ctx.Deductions.Add(d);
        await _ctx.SaveChangesAsync();
        return d.Id;
    }

    public async Task<bool> UpdateDeductionAsync(int id, CreateDeductionDto dto, int? callerCompanyId = null)
    {
        // FIX IDOR: company-scoped JOIN query prevents cross-tenant deduction mutation.
        Deduction? d;
        if (callerCompanyId.HasValue)
        {
            d = await (from ded in _ctx.Deductions
                       join e in _ctx.Employees on ded.EmployeeId equals e.EmployeeCode
                       where ded.Id == id && e.CompanyId == callerCompanyId
                       select ded).FirstOrDefaultAsync();
        }
        else
        {
            d = await _ctx.Deductions.FirstOrDefaultAsync(x => x.Id == id);
        }
        if (d == null) return false;
        d.DeductionType = dto.DeductionType;
        d.Amount        = dto.Amount;
        d.Month         = dto.Month;
        d.Year          = dto.Year;
        d.Remarks       = dto.Remarks;
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteDeductionAsync(int id, int? callerCompanyId = null)
    {
        // FIX IDOR: same company-scoped JOIN lookup as UpdateDeductionAsync.
        Deduction? d;
        if (callerCompanyId.HasValue)
        {
            d = await (from ded in _ctx.Deductions
                       join e in _ctx.Employees on ded.EmployeeId equals e.EmployeeCode
                       where ded.Id == id && e.CompanyId == callerCompanyId
                       select ded).FirstOrDefaultAsync();
        }
        else
        {
            d = await _ctx.Deductions.FirstOrDefaultAsync(x => x.Id == id);
        }
        if (d == null) return false;
        _ctx.Deductions.Remove(d);
        await _ctx.SaveChangesAsync();
        return true;
    }

    // ── Mappers ───────────────────────────────────────────────────────────

    private static BonusDto MapBonus(Bonus b) => new()
    {
        Id         = b.Id,
        EmployeeId = b.EmployeeId,
        BonusType  = b.BonusType,
        Amount     = b.Amount,
        Month      = b.Month,
        Year       = b.Year,
        Remarks    = b.Remarks,
        IsTaxable  = b.IsTaxable,
        CreatedAt  = b.CreatedAt
    };

    private static DeductionDto MapDeduction(Deduction d) => new()
    {
        Id            = d.Id,
        EmployeeId    = d.EmployeeId,
        DeductionType = d.DeductionType,
        Amount        = d.Amount,
        Month         = d.Month,
        Year          = d.Year,
        Remarks       = d.Remarks,
        CreatedAt     = d.CreatedAt
    };
}
