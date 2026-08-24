using HRMS.Domain.Entities;

namespace HRMS.Application.Interfaces;

public interface IAuditService
{
    /// <summary>
    /// Records one audit event. Parameter names updated to match domain language:
    /// actorId (formerly performedBy), actorName (formerly performedByName), companyId (new).
    /// ipAddress is now an optional trailing parameter for backward compatibility with
    /// existing callers that capture it at the HTTP layer.
    /// </summary>
    Task LogAsync(string action, string entityType, string? entityId = null,
                  int? actorId = null, string? actorName = null,
                  int? companyId = null,
                  string? details = null, bool success = true,
                  string? ipAddress = null);

    /// <summary>
    /// Retrieve logs scoped to a company, with optional filtering by entity type and actor.
    /// Used by compliance/audit report screens.
    /// </summary>
    Task<List<AuditLog>> GetLogsAsync(int companyId, string? entityType = null, int? actorId = null, CancellationToken ct = default);

    /// <summary>Legacy paged retrieval — kept for backward compatibility with existing controllers.</summary>
    Task<List<AuditLog>> GetRecentAsync(int page = 1, int pageSize = 50,
                                        string? action = null, int? userId = null);
}
