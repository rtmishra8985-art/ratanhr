using HRMS.Application.Common;
using HRMS.Application.DTOs.Training;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Training;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class TrainingService : ITrainingService
{
    private readonly ApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<TrainingService> _logger;
    private readonly IAuditService _audit;
    private const string CachePrefix = "training:";

    public TrainingService(ApplicationDbContext db, ICacheService cache,
                           ILogger<TrainingService> logger, IAuditService audit)
    {
        _db = db; _cache = cache; _logger = logger; _audit = audit;
    }

    public async Task<PagedResult<TrainingDto>> GetAllAsync(int? companyId, int page, int pageSize)
    {
        var cacheKey = $"{CachePrefix}list:{companyId}:{page}:{pageSize}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            if (page < 1) page = 1;
            if (pageSize is < 1 or > 200) pageSize = 25;

            var q = _db.TrainingPrograms
                .AsNoTracking()
                .Where(t => t.IsActive && t.DeletedAt == null &&
                            (companyId == null || t.CompanyId == null || t.CompanyId == companyId))
                .OrderByDescending(t => t.StartDate);

            var total = await q.CountAsync();
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            // Batch enrollment counts — single query, no N+1.
            var ids = items.Select(i => i.Id).ToList();
            var enrollCounts = await _db.TrainingEnrollments
                .AsNoTracking()
                .Where(e => ids.Contains(e.TrainingProgramId) && e.Status == "Enrolled")
                .GroupBy(e => e.TrainingProgramId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            return PagedResult<TrainingDto>.Create(
                items.Select(t => ToDto(t, enrollCounts.GetValueOrDefault(t.Id))).ToList(),
                total, page, pageSize);
        }, TimeSpan.FromMinutes(5));
    }

    public async Task<TrainingDto?> GetByIdAsync(int id, int? companyId)
    {
        // AsNoTracking — read-only path, no write follows.
        var t = await _db.TrainingPrograms
            .AsNoTracking()
            .FirstOrDefaultAsync(tp => tp.Id == id && tp.DeletedAt == null &&
                                       (companyId == null || tp.CompanyId == null || tp.CompanyId == companyId));
        if (t == null || !t.IsActive) return null;
        var count = await _db.TrainingEnrollments.CountAsync(e => e.TrainingProgramId == id && e.Status == "Enrolled");
        return ToDto(t, count);
    }

    public async Task<TrainingDto> CreateAsync(int? companyId, CreateTrainingDto dto)
    {
        var program = new TrainingProgram
        {
            CompanyId   = companyId,
            Title       = dto.Title,
            Description = dto.Description,
            StartDate   = dto.StartDate,
            EndDate     = dto.EndDate,
            Trainer     = dto.Trainer,
            MaxSeats    = dto.MaxSeats,
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow
        };
        _db.TrainingPrograms.Add(program);
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync(CachePrefix);
        return ToDto(program, 0);
    }

    public async Task<bool> UpdateAsync(int id, int? companyId, CreateTrainingDto dto)
    {
        // FIX IDOR: fold the company-scope guard into a single FirstOrDefaultAsync query.
        // FindAsync bypasses EF Core global query filters; FirstOrDefaultAsync respects them.
        // Global programs (CompanyId == null) remain editable by any company-scoped admin.
        // SuperAdmin (companyId == null) receives unrestricted access.
        var t = companyId.HasValue
            ? await _db.TrainingPrograms.FirstOrDefaultAsync(x =>
                  x.Id == id && x.IsActive &&
                  (!x.CompanyId.HasValue || x.CompanyId == companyId))
            : await _db.TrainingPrograms.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (t == null) return false;
        t.Title = dto.Title; t.Description = dto.Description;
        t.StartDate = dto.StartDate; t.EndDate = dto.EndDate;
        t.Trainer = dto.Trainer; t.MaxSeats = dto.MaxSeats;
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync(CachePrefix);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, int? companyId)
    {
        // FIX IDOR: same single-query approach as UpdateAsync.
        var t = companyId.HasValue
            ? await _db.TrainingPrograms.FirstOrDefaultAsync(x =>
                  x.Id == id && x.DeletedAt == null &&
                  (!x.CompanyId.HasValue || x.CompanyId == companyId))
            : await _db.TrainingPrograms.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (t == null) return false;
        // Soft-delete: set both IsActive=false (query guard) and DeletedAt (canonical marker).
        t.IsActive  = false;
        t.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync(CachePrefix);
        return true;
    }

    /// <summary>
    /// Enroll an employee in a training program.
    ///
    /// SEC-TRAINING-01 fix: cross-tenant enrollment was possible because P2 dropped the
    /// employee-to-training company check. This version:
    ///   1. Verifies the training program belongs to the employee's company.
    ///   2. Returns a specific cross-tenant error (mapped to 403 at the controller layer).
    ///   3. Writes an audit log entry for any blocked attempt.
    /// </summary>
    public async Task<(bool ok, string message, bool isCrossTenant)> EnrollAsync(
        int programId, string employeeId)
    {
        // FIX IDOR: FirstOrDefaultAsync respects EF Core global query filters that FindAsync bypasses.
        var program = await _db.TrainingPrograms.FirstOrDefaultAsync(x => x.Id == programId);
        if (program == null || !program.IsActive)
            return (false, "Training program not found or inactive.", false);

        // ── IDOR / Tenant Validation ───────────────────────────────────────
        // Fetch only the employee's CompanyId — avoid loading the full entity.
        var empCompanyId = await _db.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeCode == employeeId)
            .Select(e => (int?)e.CompanyId)
            .FirstOrDefaultAsync();

        if (empCompanyId == null)
            return (false, "Employee not found.", false);

        // If the training is company-scoped, enforce same-tenant rule.
        if (program.CompanyId.HasValue && empCompanyId != program.CompanyId)
        {
            _logger.LogWarning(
                "IDOR attempt: Employee {EmployeeId} (company {EmpCompany}) tried to enroll in " +
                "training {ProgramId} belonging to company {ProgramCompany}.",
                employeeId, empCompanyId, programId, program.CompanyId);

            await _audit.LogAsync(
                action: "IDOR_CROSS_TENANT_ENROLL_BLOCKED",
                entityType: "TrainingEnrollment",
                entityId: programId.ToString(),
                details: $"Employee {employeeId} (company {empCompanyId}) blocked from enrolling " +
                             $"in training {programId} (company {program.CompanyId}).");

            return (false, "Cross-tenant enrollment denied.", true);
        }
        // ── End Tenant Validation ──────────────────────────────────────────

        var existing = await _db.TrainingEnrollments.FirstOrDefaultAsync(
            e => e.TrainingProgramId == programId && e.EmployeeId == employeeId && e.Status == "Enrolled");
        if (existing != null)
            return (false, "Already enrolled in this program.", false);

        if (program.MaxSeats > 0)
        {
            var count = await _db.TrainingEnrollments.CountAsync(
                e => e.TrainingProgramId == programId && e.Status == "Enrolled");
            if (count >= program.MaxSeats)
                return (false, "No seats available.", false);
        }

        _db.TrainingEnrollments.Add(new TrainingEnrollment
        {
            TrainingProgramId = programId,
            EmployeeId        = employeeId,
            Status            = "Enrolled",
            CreatedAt         = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync(CachePrefix);
        return (true, "Enrolled successfully.", false);
    }

    public async Task<List<EnrollmentDto>> GetEnrollmentsByEmployeeAsync(string employeeId)
    {
        var list = await _db.TrainingEnrollments
            .AsNoTracking()
            .Include(e => e.TrainingProgram)
            .Where(e => e.EmployeeId == employeeId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        return list.Select(e => new EnrollmentDto
        {
            Id                = e.Id,
            TrainingProgramId = e.TrainingProgramId,
            TrainingTitle     = e.TrainingProgram?.Title ?? "",
            EmployeeId        = e.EmployeeId,
            Status            = e.Status,
            CompletionDate    = e.CompletionDate,
            CertificatePath   = e.CertificatePath,
            CreatedAt         = e.CreatedAt
        }).ToList();
    }

    public async Task<bool> MarkCompleteAsync(int enrollmentId, int? companyId, MarkCompleteDto dto)
    {
        var enrollment = await _db.TrainingEnrollments
            .Include(e => e.TrainingProgram)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId);
        if (enrollment == null) return false;
        if (companyId.HasValue && enrollment.TrainingProgram?.CompanyId.HasValue == true
            && enrollment.TrainingProgram.CompanyId != companyId) return false;
        enrollment.Status         = "Completed";
        enrollment.CompletionDate = dto.CompletionDate ?? DateTime.UtcNow;
        enrollment.CertificatePath = dto.CertificatePath;
        await _db.SaveChangesAsync();
        return true;
    }

    private static TrainingDto ToDto(TrainingProgram t, int enrolledCount) => new()
    {
        Id            = t.Id,
        CompanyId     = t.CompanyId,
        Title         = t.Title,
        Description   = t.Description,
        StartDate     = t.StartDate,
        EndDate       = t.EndDate,
        Trainer       = t.Trainer,
        MaxSeats      = t.MaxSeats,
        IsActive      = t.IsActive,
        EnrolledCount = enrolledCount,
        CreatedAt     = t.CreatedAt
    };
}
