using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Helpdesk;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Repositories
{
    /// <summary>
    /// EF Core-backed repository for the Helpdesk module.
    /// Inherits generic CRUD + tenant-guard from <see cref="GenericRepository{T}"/>.
    /// All query methods apply an explicit <paramref name="companyId"/> filter as a
    /// secondary defence-in-depth layer on top of the EF global query filter.
    /// </summary>
    public class HelpdeskRepository : GenericRepository<HelpdeskTicket>, IHelpdeskRepository
    {
        public HelpdeskRepository(ApplicationDbContext ctx, ITenantContext? tenant = null)
            : base(ctx, tenant) { }

        /// <inheritdoc/>
        public async Task<PagedResult<HelpdeskTicket>> GetPagedByCompanyAsync(
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
            CancellationToken ct = default)
        {
            var q = _ctx.HelpdeskTickets
                .Include(t => t.Category)
                .Include(t => t.Comments)
                .Where(t => t.CompanyId == companyId && t.DeletedAt == null)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(t => t.Title.Contains(search));

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(t => t.Status == status);

            if (!string.IsNullOrWhiteSpace(priority))
                q = q.Where(t => t.Priority == priority);

            if (categoryId.HasValue)
                q = q.Where(t => t.CategoryId == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(assignedToId))
                q = q.Where(t => t.AssignedToUserId == assignedToId);

            q = (sortBy?.ToLower(), sortDirection?.ToLower()) switch
            {
                ("priority",  "asc") => q.OrderBy(t => t.Priority),
                ("priority",  _)     => q.OrderByDescending(t => t.Priority),
                ("createdat", "asc") => q.OrderBy(t => t.CreatedAt),
                ("createdat", _)     => q.OrderByDescending(t => t.CreatedAt),
                ("status",    _)     => q.OrderBy(t => t.Status),
                _                    => q.OrderByDescending(t => t.CreatedAt),
            };

            return await q.ToPagedResultAsync(page, pageSize, ct: ct);
        }

        /// <inheritdoc/>
        public async Task<HelpdeskTicket?> GetByIdWithDetailsAsync(int id, int companyId, CancellationToken ct = default)
            => await _ctx.HelpdeskTickets
                .Include(t => t.Category)
                .Include(t => t.Comments)
                .Include(t => t.History)
                .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId && t.DeletedAt == null, ct);

        /// <inheritdoc/>
        public async Task<bool> ExistsForCompanyAsync(int ticketId, int companyId, CancellationToken ct = default)
            => await _ctx.HelpdeskTickets
                .AnyAsync(t => t.Id == ticketId && t.CompanyId == companyId && t.DeletedAt == null, ct);
    }
}
