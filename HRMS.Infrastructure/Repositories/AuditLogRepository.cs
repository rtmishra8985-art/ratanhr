using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HRMS.Application.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Repositories
{
    /// <summary>
    /// EF Core-backed repository for <see cref="AuditLog"/> read and write access.
    /// Does NOT extend <see cref="GenericRepository{T}"/> because audit logs are
    /// append-only (no Update / Delete) and writes must never propagate exceptions
    /// to the calling business transaction.
    /// </summary>
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AuditLogRepository> _logger;

        public AuditLogRepository(ApplicationDbContext db, ILogger<AuditLogRepository> logger)
        {
            _db     = db;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task LogAsync(
            string action,
            string entityType,
            string? entityId       = null,
            int?    performedBy    = null,
            string? performedByName = null,
            string? ipAddress      = null,
            string? details        = null,
            bool    success        = true,
            CancellationToken ct   = default)
        {
            try
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    Action           = action,
                    EntityType       = entityType,
                    EntityId         = entityId,
                    PerformedBy      = performedBy,
                    PerformedByName  = performedByName,
                    IpAddress        = ipAddress,
                    Details          = details,
                    Success          = success,
                    OccurredAt       = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Audit logging must never crash the main operation.
                // Log to structured logger so the failure is observable without
                // surfacing an exception to the caller.
                _logger.LogError(ex,
                    "AuditLogRepository.LogAsync failed — action={Action} entityType={EntityType} entityId={EntityId}",
                    action, entityType, entityId);
            }
        }

        /// <inheritdoc/>
        public async Task<PagedResult<AuditLog>> GetPagedAsync(
            int       page         = 1,
            int       pageSize     = 50,
            string?   action       = null,
            int?      performedBy  = null,
            string?   entityType   = null,
            string?   entityId     = null,
            DateTime? from         = null,
            DateTime? to           = null,
            CancellationToken ct   = default)
        {
            var q = _db.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(action))      q = q.Where(a => a.Action.Contains(action));
            if (performedBy.HasValue)                    q = q.Where(a => a.PerformedBy == performedBy);
            if (!string.IsNullOrWhiteSpace(entityType))  q = q.Where(a => a.EntityType == entityType);
            if (!string.IsNullOrWhiteSpace(entityId))    q = q.Where(a => a.EntityId == entityId);
            if (from.HasValue)                           q = q.Where(a => a.OccurredAt >= from.Value);
            if (to.HasValue)                             q = q.Where(a => a.OccurredAt <= to.Value);

            q = q.OrderByDescending(a => a.OccurredAt);

            return await q.ToPagedResultAsync(page, pageSize, ct: ct);
        }

        /// <inheritdoc/>
        public async Task<List<AuditLog>> GetRecentAsync(
            int     pageSize     = 50,
            string? action       = null,
            int?    performedBy  = null,
            CancellationToken ct = default)
        {
            var q = _db.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action.Contains(action));
            if (performedBy.HasValue)               q = q.Where(a => a.PerformedBy == performedBy);

            return await q
                .OrderByDescending(a => a.OccurredAt)
                .Take(pageSize)
                .ToListAsync(ct);
        }
    }
}
