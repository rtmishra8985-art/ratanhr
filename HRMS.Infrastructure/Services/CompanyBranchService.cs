using HRMS.Application.Common;
using HRMS.Application.DTOs.Company;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Company;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class CompanyBranchService : ICompanyBranchService
{
    private readonly ApplicationDbContext _ctx;
    private readonly IAuditService _audit;
    private readonly ILogger<CompanyBranchService> _logger;

    public CompanyBranchService(ApplicationDbContext ctx, IAuditService audit,
                                ILogger<CompanyBranchService> logger)
    {
        _ctx = ctx; _audit = audit; _logger = logger;
    }

    public async Task<PagedResult<CompanyBranchDto>> GetBranchesPagedAsync(int companyId, int page, int pageSize)
    {
        var q = _ctx.CompanyBranches
            .AsNoTracking()
            .Where(b => b.CompanyId == companyId)
            .OrderBy(b => b.BranchName);
        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total = await q.CountAsync();
        var rows  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<CompanyBranchDto>.Create(rows.Select(MapDto).ToList(), total, page, pageSize);
    }

    public async Task<List<CompanyBranchDto>> GetBranchesAsync(int companyId)
    {
        // AsNoTracking — read-only list; no write follows.
        var rows = await _ctx.CompanyBranches
            .AsNoTracking()
            .Where(b => b.CompanyId == companyId)
            .ToListAsync();
        return rows.Select(MapDto).ToList();
    }

    /// <summary>
    /// SEC-BRANCH-01 fix: validate that the branch belongs to <paramref name="callerCompanyId"/>
    /// before returning it. Returns null (→ 404) instead of 403 to avoid oracle attacks.
    /// </summary>
    public async Task<CompanyBranchDto?> GetBranchAsync(int branchId, int callerCompanyId)
    {
        // AsNoTracking — read-only.
        var b = await _ctx.CompanyBranches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == branchId && x.CompanyId == callerCompanyId);
        return b == null ? null : MapDto(b);
    }

    public async Task<int> CreateBranchAsync(CreateCompanyBranchDto dto)
    {
        var branch = new CompanyBranch {
            CompanyId = dto.CompanyId, BranchName = dto.BranchName,
            AddressLine1 = dto.AddressLine1, AddressLine2 = dto.AddressLine2,
            City = dto.City, StateProvince = dto.StateProvince, Country = dto.Country,
            PostalCode = dto.PostalCode, PhoneNumber = dto.PhoneNumber,
            Email = dto.Email, BranchManagerName = dto.BranchManagerName,
            IsHeadOffice = dto.IsHeadOffice, CreatedAt = DateTime.UtcNow
        };
        _ctx.CompanyBranches.Add(branch);
        await _ctx.SaveChangesAsync();
        return branch.Id;
    }

    /// <summary>
    /// SEC-BRANCH-01 fix: enforce that the branch belongs to the caller's company
    /// before allowing any update. Blocked attempts are audit-logged.
    /// </summary>
    public async Task<bool> UpdateBranchAsync(int branchId, int callerCompanyId, CreateCompanyBranchDto dto)
    {
        // FIX IDOR: FirstOrDefaultAsync respects EF Core global query filters that FindAsync bypasses.
        // Ownership enforcement and audit logging are preserved in the secondary check below.
        var b = await _ctx.CompanyBranches.FirstOrDefaultAsync(x => x.Id == branchId);
        if (b == null) return false;

        // Ownership check — restored from P1.
        if (b.CompanyId != callerCompanyId)
        {
            _logger.LogWarning(
                "IDOR attempt: company {Caller} tried to update branch {BranchId} belonging to company {Owner}.",
                callerCompanyId, branchId, b.CompanyId);
            await _audit.LogAsync(
                action: "IDOR_BRANCH_UPDATE_BLOCKED",
                entityType: "CompanyBranch",
                entityId: branchId.ToString(),
                details: $"Company {callerCompanyId} blocked from updating branch {branchId} (owner: {b.CompanyId}).");
            return false;
        }

        b.BranchName = dto.BranchName; b.AddressLine1 = dto.AddressLine1;
        b.AddressLine2 = dto.AddressLine2; b.City = dto.City;
        b.StateProvince = dto.StateProvince; b.Country = dto.Country;
        b.PostalCode = dto.PostalCode; b.PhoneNumber = dto.PhoneNumber;
        b.Email = dto.Email; b.BranchManagerName = dto.BranchManagerName;
        b.IsHeadOffice = dto.IsHeadOffice;
        await _ctx.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// SEC-BRANCH-01 fix: enforce that the branch belongs to the caller's company
    /// before allowing deletion.
    /// </summary>
    public async Task<bool> DeleteBranchAsync(int branchId, int callerCompanyId)
    {
        // FIX IDOR: FirstOrDefaultAsync respects EF Core global query filters that FindAsync bypasses.
        var b = await _ctx.CompanyBranches.FirstOrDefaultAsync(x => x.Id == branchId);
        if (b == null) return false;

        // Ownership check — restored from P1.
        if (b.CompanyId != callerCompanyId)
        {
            _logger.LogWarning(
                "IDOR attempt: company {Caller} tried to delete branch {BranchId} belonging to company {Owner}.",
                callerCompanyId, branchId, b.CompanyId);
            await _audit.LogAsync(
                action: "IDOR_BRANCH_DELETE_BLOCKED",
                entityType: "CompanyBranch",
                entityId: branchId.ToString(),
                details: $"Company {callerCompanyId} blocked from deleting branch {branchId} (owner: {b.CompanyId}).");
            return false;
        }

        _ctx.CompanyBranches.Remove(b);
        await _ctx.SaveChangesAsync();
        return true;
    }

    private static CompanyBranchDto MapDto(CompanyBranch b) => new() {
        Id = b.Id, CompanyId = b.CompanyId, BranchName = b.BranchName,
        AddressLine1 = b.AddressLine1, AddressLine2 = b.AddressLine2,
        City = b.City, StateProvince = b.StateProvince, Country = b.Country,
        PostalCode = b.PostalCode, PhoneNumber = b.PhoneNumber,
        Email = b.Email, BranchManagerName = b.BranchManagerName,
        IsHeadOffice = b.IsHeadOffice, IsActive = b.IsActive, CreatedAt = b.CreatedAt
    };
}
