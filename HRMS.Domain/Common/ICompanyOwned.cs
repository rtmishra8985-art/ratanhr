namespace HRMS.Domain.Common;

/// <summary>
/// Marker interface for domain entities that are scoped to a single company (tenant).
/// Entities implementing this interface are automatically protected by:
///   1. EF Core global query filters in ApplicationDbContext (primary layer).
///   2. GenericRepository.GetByIdAsync tenant check (secondary layer).
/// </summary>
public interface ICompanyOwned
{
    /// <summary>
    /// The owning company ID. Null means the entity is global (visible to all companies).
    /// </summary>
    int? CompanyId { get; }
}
