using System.Threading;
using System.Threading.Tasks;
using HRMS.Application.Common;
using HRMS.Domain.Entities.Helpdesk;

namespace HRMS.Infrastructure.Repositories
{
    /// <summary>
    /// Repository contract for the Helpdesk module.
    /// Extends <see cref="IGenericRepository{T}"/> with ticket-specific query methods.
    /// All operations are implicitly tenant-scoped via EF Core global query filters.
    /// </summary>
    public interface IHelpdeskRepository : IGenericRepository<HelpdeskTicket>
    {
        /// <summary>
        /// Returns a paginated, filtered list of tickets for the given company,
        /// including Category and Comment-count projections.
        /// </summary>
        Task<PagedResult<HelpdeskTicket>> GetPagedByCompanyAsync(
            int companyId,
            string? search,
            string? status,
            string? priority,
            int? categoryId,
            string? assignedToId,
            string? sortBy,
            string? sortDirection,
            int page,
            int pageSize,
            CancellationToken ct = default);

        /// <summary>
        /// Returns a single ticket with its Category and Comments collections,
        /// scoped to the tenant.
        /// </summary>
        Task<HelpdeskTicket?> GetByIdWithDetailsAsync(int id, int companyId, CancellationToken ct = default);

        /// <summary>Returns true when the ticket belongs to the specified company.</summary>
        Task<bool> ExistsForCompanyAsync(int ticketId, int companyId, CancellationToken ct = default);
    }
}
