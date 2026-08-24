using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;

    public AuditService(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Updated signature: actorId / actorName / companyId match the IAuditService contract.
    /// The old positional params (performedBy, performedByName, ipAddress) are mapped to
    /// the renamed columns transparently — no DB schema change required.
    /// </summary>
    public async Task LogAsync(string action, string entityType, string? entityId = null,
                               int? actorId = null, string? actorName = null,
                               int? companyId = null,
                               string? details = null, bool success = true,
                               string? ipAddress = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action          = action,
            EntityType      = entityType,
            EntityId        = entityId,
            PerformedBy     = actorId,
            PerformedByName = actorName,
            CompanyId       = companyId,
            IpAddress       = ipAddress,
            Details         = details,
            Success         = success,
            OccurredAt      = DateTime.UtcNow
        });

        // Do not swallow persistence failures. Callers that combine a business
        // write with an audit write in one transaction must be able to roll the
        // business write back when the audit record cannot be persisted.
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<List<AuditLog>> GetLogsAsync(int companyId, string? entityType = null, int? actorId = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var query = _db.AuditLogs.AsQueryable();
        query = query.Where(a => a.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
        if (actorId.HasValue) query = query.Where(a => a.PerformedBy == actorId);
        return await query.OrderByDescending(a => a.OccurredAt).ToListAsync(ct);
    }

    public async Task<List<AuditLog>> GetRecentAsync(int page = 1, int pageSize = 50,
                                                      string? action = null, int? userId = null)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action.Contains(action));
        if (userId.HasValue) query = query.Where(a => a.PerformedBy == userId);

        return await query
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}
