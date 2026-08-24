using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HRMS.Application.DTOs.Helpdesk;
using HRMS.Application.Common;

namespace HRMS.Application.Interfaces
{
    /// <summary>
    /// Service contract for the Helpdesk module.
    /// All operations are tenant-scoped via <paramref name="companyId"/>.
    /// </summary>
    public interface IHelpdeskService
    {
        /// <summary>Returns a paginated, filtered list of tickets.</summary>
        Task<PagedResult<TicketDto>> GetTicketsAsync(TicketQueryDto query, int companyId, CancellationToken ct = default);

        /// <summary>Returns full detail for a single ticket including comment count.</summary>
        Task<TicketDto?> GetTicketByIdAsync(int id, int companyId, CancellationToken ct = default);

        /// <summary>Creates a new support ticket on behalf of the authenticated employee.</summary>
        Task<TicketDto> CreateTicketAsync(CreateTicketDto dto, int companyId, string raisedByEmployeeId, CancellationToken ct = default);

        /// <summary>Updates a ticket's fields (status, priority, category, title, description).</summary>
        Task<TicketDto?> UpdateTicketAsync(int id, UpdateTicketDto dto, int companyId, string updatedByUserId, CancellationToken ct = default);

        /// <summary>Assigns a ticket to a support agent.</summary>
        Task<TicketDto?> AssignTicketAsync(int id, AssignTicketDto dto, int companyId, string performedByUserId, CancellationToken ct = default);

        /// <summary>Returns all comments for a ticket.</summary>
        /// <summary>Returns all comments for a ticket.</summary>
        /// <param name="ticketId">The ticket ID.</param>
        /// <param name="companyId">Tenant scope.</param>
        /// <param name="includeInternal">
        /// When false (the default), internal-only notes (<see cref="TicketCommentDto.IsInternal"/> == true)
        /// are excluded from the result. Callers must explicitly opt in only when the caller is an
        /// HR Admin / Admin / Support Agent — employees must never receive internal notes.
        /// </param>
        Task<IEnumerable<TicketCommentDto>> GetCommentsAsync(int ticketId, int companyId, bool includeInternal = false, CancellationToken ct = default);

        /// <summary>Adds a comment or internal note to a ticket.</summary>
        Task<TicketCommentDto> AddCommentAsync(int ticketId, CreateTicketCommentDto dto, int companyId, string authorId, CancellationToken ct = default);

        /// <summary>Returns aggregate statistics for the helpdesk dashboard.</summary>
        Task<HelpdeskSummaryDto> GetSummaryAsync(int companyId, CancellationToken ct = default);

        // ── Categories ────────────────────────────────────────────────────

        Task<IEnumerable<TicketCategoryDto>> GetCategoriesAsync(int companyId, CancellationToken ct = default);

        Task<TicketCategoryDto> CreateCategoryAsync(CreateTicketCategoryDto dto, int companyId, CancellationToken ct = default);

        /// <summary>
        /// Permanently deletes a helpdesk ticket and all its comments and history.
        /// Admin/HR only. Returns true if the ticket was found and deleted; false if not found.
        /// </summary>
        Task<bool> DeleteTicketAsync(int id, int companyId, CancellationToken ct = default);
    }
}
