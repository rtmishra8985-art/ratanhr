using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HRMS.Application.DTOs.Helpdesk;
using HRMS.Application.Interfaces;
using HRMS.Application.Common;
using HRMS.Domain.Entities.Helpdesk;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Repositories;

namespace HRMS.Infrastructure.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IHelpdeskService"/> backed by EF Core.
    /// Core ticket read operations (paginated list, single-entity fetch) are delegated
    /// to <see cref="IHelpdeskRepository"/> so the repository layer is actually exercised
    /// at runtime.  Write operations and category/comment queries that have no dedicated
    /// repository method continue to use the DbContext directly.
    /// </summary>
    public class HelpdeskService : IHelpdeskService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHelpdeskRepository _repo;
        private readonly ILogger<HelpdeskService> _logger;

        public HelpdeskService(
            ApplicationDbContext db,
            IHelpdeskRepository repo,
            ILogger<HelpdeskService> logger)
        {
            _db     = db;
            _repo   = repo;
            _logger = logger;
        }

        // ── Tickets ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<PagedResult<TicketDto>> GetTicketsAsync(TicketQueryDto query, int companyId, CancellationToken ct = default)
        {
            // Delegate to repository — avoids bypassing the repository layer.
            var paged = await _repo.GetPagedByCompanyAsync(
                companyId,
                query.Search,
                query.Status,
                query.Priority,
                query.CategoryId,
                query.AssignedToId,
                query.SortBy,
                query.SortDirection,
                query.Page,
                query.PageSize,
                ct);

            return new PagedResult<TicketDto>
            {
                Items      = paged.Items.Select(MapToDto).ToList(),
                TotalCount = paged.TotalCount,
                Page       = paged.Page,
                PageSize   = paged.PageSize,
            };
        }

        /// <inheritdoc/>
        public async Task<TicketDto?> GetTicketByIdAsync(int id, int companyId, CancellationToken ct = default)
        {
            // Delegate to repository — includes Category, Comments, and History navigation properties.
            var ticket = await _repo.GetByIdWithDetailsAsync(id, companyId, ct);
            return ticket is null ? null : MapToDto(ticket);
        }

        /// <inheritdoc/>
        public async Task<TicketDto> CreateTicketAsync(CreateTicketDto dto, int companyId, string raisedByEmployeeId, CancellationToken ct = default)
        {
            var ticket = new HelpdeskTicket
            {
                Title                = dto.Title,
                Description          = dto.Description,
                Status               = "Open",
                Priority             = dto.Priority,
                CategoryId           = dto.CategoryId,
                RaisedByEmployeeId   = raisedByEmployeeId,
                CompanyId            = companyId,
            };

            _db.HelpdeskTickets.Add(ticket);
            AddHistory(ticket, "Created", null, null, raisedByEmployeeId);
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Helpdesk ticket #{Id} created.", ticket.Id);
            return MapToDto(ticket);
        }

        /// <inheritdoc/>
        public async Task<TicketDto?> UpdateTicketAsync(int id, UpdateTicketDto dto, int companyId, string updatedByUserId, CancellationToken ct = default)
        {
            var ticket = await _db.HelpdeskTickets
                .Include(t => t.Category)
                .Include(t => t.Comments)
                .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId && t.DeletedAt == null, ct);

            if (ticket is null) return null;

            var oldStatus = ticket.Status;
            if (dto.Title       is not null) ticket.Title       = dto.Title;
            if (dto.Description is not null) ticket.Description = dto.Description;
            if (dto.Priority    is not null) ticket.Priority    = dto.Priority;
            if (dto.CategoryId  is not null) ticket.CategoryId  = dto.CategoryId;
            if (dto.Status      is not null)
            {
                ticket.Status    = dto.Status;
                if (dto.Status is "Resolved" or "Closed")
                    ticket.ResolvedAt = DateTime.UtcNow;
            }

            ticket.UpdatedAt = DateTime.UtcNow;
            if (oldStatus != ticket.Status)
                AddHistory(ticket, "StatusChanged", oldStatus, ticket.Status, updatedByUserId);

            await _db.SaveChangesAsync(ct);
            return MapToDto(ticket);
        }

        /// <inheritdoc/>
        public async Task<TicketDto?> AssignTicketAsync(int id, AssignTicketDto dto, int companyId, string performedByUserId, CancellationToken ct = default)
        {
            var ticket = await _db.HelpdeskTickets
                .Include(t => t.Category)
                .Include(t => t.Comments)
                .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId && t.DeletedAt == null, ct);

            if (ticket is null) return null;

            ticket.AssignedToUserId = dto.AssignedToId;
            ticket.Status           = "In Progress";
            ticket.UpdatedAt        = DateTime.UtcNow;

            AddHistory(ticket, "Assigned", null, dto.AssignedToId, performedByUserId);
            await _db.SaveChangesAsync(ct);
            return MapToDto(ticket);
        }

        // ── Comments ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<IEnumerable<TicketCommentDto>> GetCommentsAsync(int ticketId, int companyId, bool includeInternal = false, CancellationToken ct = default)
        {
            // Use ExistsForCompanyAsync from repository for the tenant-scoped existence check.
            var exists = await _repo.ExistsForCompanyAsync(ticketId, companyId, ct);
            if (!exists) return Enumerable.Empty<TicketCommentDto>();

            // BUG FIX (confidentiality leak): this method previously returned every comment on
            // the ticket unconditionally, including IsInternal == true rows. CreateTicketCommentDto
            // explicitly documents "Internal notes are only visible to agents", but nothing here
            // (or in HelpdeskController.GetComments, which has no role restriction and is callable
            // by the Employee role per the class-level doc "Employees can create tickets and
            // comment") ever enforced that. Any employee viewing their own ticket's comments
            // received HR/Admin-only internal notes verbatim. Default to excluding internal notes;
            // the controller now passes includeInternal = true only for HR Admin/Admin/Support roles.
            var query = _db.HelpdeskComments.Where(c => c.TicketId == ticketId);
            if (!includeInternal)
                query = query.Where(c => !c.IsInternal);

            return await query
                .OrderBy(c => c.CreatedAt)
                .Select(c => new TicketCommentDto
                {
                    Id         = c.Id,
                    TicketId   = c.TicketId,
                    AuthorId   = c.AuthorId,
                    Message    = c.Message,
                    IsInternal = c.IsInternal,
                    CreatedAt  = c.CreatedAt,
                })
                .ToListAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<TicketCommentDto> AddCommentAsync(int ticketId, CreateTicketCommentDto dto, int companyId, string authorId, CancellationToken ct = default)
        {
            var ticket = await _db.HelpdeskTickets
                .FirstOrDefaultAsync(t => t.Id == ticketId && t.CompanyId == companyId && t.DeletedAt == null, ct)
                ?? throw new InvalidOperationException($"Ticket {ticketId} not found.");

            var comment = new HelpdeskComment
            {
                TicketId   = ticketId,
                AuthorId   = authorId,
                Message    = dto.Message,
                IsInternal = dto.IsInternal,
            };

            ticket.UpdatedAt = DateTime.UtcNow;
            _db.HelpdeskComments.Add(comment);
            await _db.SaveChangesAsync(ct);

            return new TicketCommentDto
            {
                Id         = comment.Id,
                TicketId   = comment.TicketId,
                AuthorId   = comment.AuthorId,
                Message    = comment.Message,
                IsInternal = comment.IsInternal,
                CreatedAt  = comment.CreatedAt,
            };
        }

        // ── Summary ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<HelpdeskSummaryDto> GetSummaryAsync(int companyId, CancellationToken ct = default)
        {
            var tickets = await _db.HelpdeskTickets
                .Where(t => t.CompanyId == companyId && t.DeletedAt == null)
                .ToListAsync(ct);

            var resolved = tickets.Where(t => t.Status is "Resolved" or "Closed" && t.ResolvedAt.HasValue).ToList();
            double? avg = resolved.Count > 0
                ? resolved.Average(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalHours)
                : null;

            return new HelpdeskSummaryDto
            {
                Open              = tickets.Count(t => t.Status == "Open"),
                InProgress        = tickets.Count(t => t.Status == "In Progress"),
                Resolved          = tickets.Count(t => t.Status == "Resolved"),
                Closed            = tickets.Count(t => t.Status == "Closed"),
                Critical          = tickets.Count(t => t.Priority == "Critical"),
                AvgResolutionHours = avg,
            };
        }

        // ── Categories ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<IEnumerable<TicketCategoryDto>> GetCategoriesAsync(int companyId, CancellationToken ct = default)
        {
            return await _db.HelpdeskCategories
                .Where(c => c.CompanyId == companyId)
                .Select(c => new TicketCategoryDto
                {
                    Id          = c.Id,
                    Name        = c.Name,
                    Description = c.Description,
                    TicketCount = c.Tickets.Count(t => t.CompanyId == companyId),
                })
                .ToListAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<TicketCategoryDto> CreateCategoryAsync(CreateTicketCategoryDto dto, int companyId, CancellationToken ct = default)
        {
            var category = new HelpdeskCategory
            {
                Name        = dto.Name,
                Description = dto.Description,
                CompanyId   = companyId,
            };
            _db.HelpdeskCategories.Add(category);
            await _db.SaveChangesAsync(ct);

            return new TicketCategoryDto { Id = category.Id, Name = category.Name, Description = category.Description, TicketCount = 0 };
        }

        // ── Delete ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<bool> DeleteTicketAsync(int id, int companyId, CancellationToken ct = default)
        {
            var ticket = await _db.HelpdeskTickets
                .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId && t.DeletedAt == null, ct);

            if (ticket is null) return false;

            // Soft-delete: preserve comments and history for audit purposes.
            // Set deleted_at timestamp instead of removing rows so the ticket can be
            // recovered and its full history remains intact for compliance queries.
            ticket.DeletedAt = DateTime.UtcNow;
            ticket.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Helpdesk ticket #{Id} (company {CompanyId}) soft-deleted.", id, companyId);
            return true;
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static TicketDto MapToDto(HelpdeskTicket t) => new()
        {
            Id                   = t.Id,
            Title                = t.Title,
            Description          = t.Description,
            Status               = t.Status,
            Priority             = t.Priority,
            CategoryId           = t.CategoryId,
            CategoryName         = t.Category?.Name,
            RaisedByEmployeeId   = t.RaisedByEmployeeId,
            AssignedToUserId     = t.AssignedToUserId,
            CreatedAt            = t.CreatedAt,
            UpdatedAt            = t.UpdatedAt,
            ResolvedAt           = t.ResolvedAt,
            CommentCount         = t.Comments.Count,
        };

        private static void AddHistory(HelpdeskTicket ticket, string action, string? oldVal, string? newVal, string? performedBy)
        {
            ticket.History.Add(new HelpdeskHistory
            {
                Action            = action,
                OldValue          = oldVal,
                NewValue          = newVal,
                PerformedByUserId = performedBy,
                Timestamp         = DateTime.UtcNow,
            });
        }
    }
}
