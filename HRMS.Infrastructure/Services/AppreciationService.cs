using HRMS.Application.Common;
using HRMS.Application.DTOs.Appreciation;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;          // Appreciation entity
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Security;
using HRMS.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class AppreciationService : IAppreciationService
{
    private readonly ApplicationDbContext _db;
    private readonly FileStorageService   _storage;

    public AppreciationService(ApplicationDbContext db, FileStorageService storage)
    {
        _db      = db;
        _storage = storage;
    }

    public async Task<int> UploadAsync(string employeeId, string? message, IFormFile? file, int createdBy)
    {
        // Item 9: appreciation attachments are images only.
        var path = await _storage.SaveFileAsync(file, "appreciation", UploadProfile.Image);
        var appreciation = new Appreciation
        {
            EmployeeId = employeeId,
            Message    = message,
            FilePath   = path,
            CreatedBy  = createdBy,
            CreatedAt  = DateTime.UtcNow
        };
        _db.Appreciations.Add(appreciation);
        await _db.SaveChangesAsync();
        return appreciation.Id;
    }

    // FIX [2] IDOR — enforce company ownership.
    // callerCompanyId == null means the caller is a SuperAdmin (unrestricted scope).
    public async Task<AppreciationDto?> GetByIdAsync(int id, int? callerCompanyId)
    {
        // FIX IDOR: replace two-step FindAsync + secondary ownership check with a
        // single company-scoped JOIN query. FindAsync bypasses EF Core global query
        // filters; FirstOrDefaultAsync respects them. SuperAdmin (null) → unrestricted.
        Appreciation? a;
        if (callerCompanyId.HasValue)
        {
            a = await (from ap in _db.Appreciations
                       join e in _db.Employees on ap.EmployeeId equals e.EmployeeCode
                       where ap.Id == id && ap.DeletedAt == null && e.CompanyId == callerCompanyId
                       select ap).FirstOrDefaultAsync();
        }
        else
        {
            a = await _db.Appreciations.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        }
        if (a == null) return null;
        return MapDto(a);
    }

    public async Task<List<AppreciationDto>> GetByEmployeeAsync(string employeeId)
    {
        // Materialize first — EF Core cannot translate a static mapper method into SQL.
        var rows = await _db.Appreciations
            .Where(a => a.EmployeeId == employeeId && a.DeletedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return rows.Select(MapDto).ToList();
    }

    public async Task<List<AppreciationDto>> GetAllAsync(int? companyId = null)
    {
        if (companyId.HasValue)
        {
            var empIds = await _db.Employees
                .Where(e => e.CompanyId == companyId)
                .Select(e => e.EmployeeCode)
                .ToListAsync();

            var scoped = await _db.Appreciations
                .Where(a => empIds.Contains(a.EmployeeId) && a.DeletedAt == null)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
            return scoped.Select(MapDto).ToList();
        }

        var all = await _db.Appreciations
            .Where(a => a.DeletedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return all.Select(MapDto).ToList();
    }

    // FIX [2] IDOR — enforce company ownership before deleting.
    // callerCompanyId == null means the caller is a SuperAdmin (unrestricted scope).
    public async Task<bool> DeleteAsync(int id, int? callerCompanyId)
    {
        // FIX IDOR: single company-scoped JOIN query replaces FindAsync + secondary check.
        Appreciation? a;
        if (callerCompanyId.HasValue)
        {
            a = await (from ap in _db.Appreciations
                       join e in _db.Employees on ap.EmployeeId equals e.EmployeeCode
                       where ap.Id == id && ap.DeletedAt == null && e.CompanyId == callerCompanyId
                       select ap).FirstOrDefaultAsync();
        }
        else
        {
            a = await _db.Appreciations.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        }
        if (a == null) return false;
        // Soft-delete: preserve the record for audit history; mark deleted_at instead of removing.
        a.DeletedAt = DateTime.UtcNow;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private static AppreciationDto MapDto(Appreciation a) => new()
    {
        Id         = a.Id,
        EmployeeId = a.EmployeeId,
        Message    = a.Message,
        FilePath   = a.FilePath,
        CreatedBy  = a.CreatedBy,
        CreatedAt  = a.CreatedAt
    };

    public async Task<PagedResult<AppreciationDto>> GetAllPagedAsync(int? companyId, int page, int pageSize)
    {
        if (companyId.HasValue)
        {
            var empIds = await _db.Employees
                .Where(e => e.CompanyId == companyId)
                .Select(e => e.EmployeeCode)
                .ToListAsync();
            return await _db.Appreciations
                .Where(a => empIds.Contains(a.EmployeeId) && a.DeletedAt == null)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AppreciationDto { Id = a.Id, EmployeeId = a.EmployeeId,
                    Message = a.Message, FilePath = a.FilePath,
                    CreatedBy = a.CreatedBy, CreatedAt = a.CreatedAt })
                .ToPagedResultAsync(page, pageSize);
        }
        return await _db.Appreciations
            .Where(a => a.DeletedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AppreciationDto { Id = a.Id, EmployeeId = a.EmployeeId,
                Message = a.Message, FilePath = a.FilePath,
                CreatedBy = a.CreatedBy, CreatedAt = a.CreatedAt })
            .ToPagedResultAsync(page, pageSize);
    }
}
