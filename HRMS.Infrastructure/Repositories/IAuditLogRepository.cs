using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HRMS.Application.Common;
using HRMS.Domain.Entities;

namespace HRMS.Infrastructure.Repositories
{
    /// <summary>
    /// Repository contract for <see cref="AuditLog"/> persistence and retrieval.
    /// Write operations must never throw — audit logging must be fire-and-forget
    /// relative to the business operation that triggered it.
    /// All read operations are scoped to a company via <paramref name="companyId"/>
    /// on the <c>performed_by</c> → user → company chain, or filtered by caller.
    /// </summary>
    public interface IAuditLogRepository
    {
        /// <summary>
        /// Writes a single audit log entry. Swallows all exceptions so that an audit
        /// failure never rolls back a legitimate business transaction.
        /// </summary>
        Task LogAsync(
            string action,
            string entityType,
            string? entityId      = null,
            int?    performedBy    = null,
            string? performedByName = null,
            string? ipAddress      = null,
            string? details        = null,
            bool    success        = true,
            CancellationToken ct   = default);

        /// <summary>
        /// Returns a paginated list of audit log entries, newest first.
        /// Optionally filtered by <paramref name="action"/> substring and/or
        /// <paramref name="performedBy"/> user ID.
        /// </summary>
        Task<PagedResult<AuditLog>> GetPagedAsync(
            int     page           = 1,
            int     pageSize       = 50,
            string? action         = null,
            int?    performedBy    = null,
            string? entityType     = null,
            string? entityId       = null,
            DateTime? from         = null,
            DateTime? to           = null,
            CancellationToken ct   = default);

        /// <summary>
        /// Returns the most-recent <paramref name="pageSize"/> entries for a quick
        /// dashboard feed. Equivalent to <c>GetPagedAsync(page: 1, pageSize)</c>
        /// but avoids the COUNT query for latency-sensitive callers.
        /// </summary>
        Task<List<AuditLog>> GetRecentAsync(
            int     pageSize       = 50,
            string? action         = null,
            int?    performedBy    = null,
            CancellationToken ct   = default);
    }
}
