using System.Threading;
using System.Threading.Tasks;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Helpdesk;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Helpdesk
{
    /// <summary>
    /// Manages helpdesk tickets, comments, categories, and dashboard summary.
    /// </summary>
    /// <remarks>
    /// All endpoints require authentication and are scoped to the authenticated user's tenant.
    /// Employees can create tickets and comment; HR Admin / Admin can assign and manage all tickets.
    /// </remarks>
    [ApiController]
    [Route("api/helpdesk")]
    [Authorize(Policy = "RequireMfaCompleted")]
    [Produces("application/json")]
    public class HelpdeskController : BaseController
    {
        private readonly IHelpdeskService _helpdesk;

        public HelpdeskController(IHelpdeskService helpdesk)
        {
            _helpdesk = helpdesk;
        }

        // ── Tickets ───────────────────────────────────────────────────────

        /// <summary>Returns a paginated, filtered list of helpdesk tickets for the tenant.</summary>
        /// <param name="query">Pagination, search, status, priority, and category filters.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Paged list of tickets.</response>
        [HttpGet("tickets")]
        [ProducesResponseType(typeof(PagedResult<TicketDto>), 200)]
        public async Task<IActionResult> GetTickets([FromQuery] TicketQueryDto query, CancellationToken ct)
            => Ok(await _helpdesk.GetTicketsAsync(query, CompanyId, ct));

        /// <summary>Returns full detail for a single ticket including comment count.</summary>
        /// <param name="id">Ticket identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Ticket detail.</response>
        /// <response code="404">Ticket not found.</response>
        [HttpGet("tickets/{id:int}")]
        [ProducesResponseType(typeof(TicketDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetTicket(int id, CancellationToken ct)
        {
            var result = await _helpdesk.GetTicketByIdAsync(id, CompanyId, ct);
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Creates a new support ticket on behalf of the authenticated employee.</summary>
        /// <param name="dto">Ticket creation payload (title, description, priority, category).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="201">Newly created ticket.</response>
        /// <response code="400">Validation error.</response>
        [HttpPost("tickets")]
        [ProducesResponseType(typeof(TicketDto), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateTicket([FromBody] CreateTicketDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _helpdesk.CreateTicketAsync(dto, CompanyId, EmployeeId ?? string.Empty, ct);
            return CreatedAtAction(nameof(GetTicket), new { id = result.Id }, result);
        }

        /// <summary>Updates a ticket's status, priority, category, title, or description.</summary>
        /// <param name="id">Ticket identifier.</param>
        /// <param name="dto">Fields to update (all optional — patch semantics).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Updated ticket.</response>
        /// <response code="404">Ticket not found.</response>
        [HttpPut("tickets/{id:int}")]
        [Authorize(Roles = AppRoles.HrAdminAdminSupport)]
        [ProducesResponseType(typeof(TicketDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateTicket(int id, [FromBody] UpdateTicketDto dto, CancellationToken ct)
        {
            var result = await _helpdesk.UpdateTicketAsync(id, dto, CompanyId, UserId.ToString(), ct);
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Assigns a ticket to a support agent and sets status to In Progress.</summary>
        /// <param name="id">Ticket identifier.</param>
        /// <param name="dto">Assignment payload (assignedToId).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Updated ticket with assignee info.</response>
        /// <response code="404">Ticket not found.</response>
        [HttpPatch("tickets/{id:int}/assign")]
        [Authorize(Roles = AppRoles.HrAdminAdminSupport)]
        [ProducesResponseType(typeof(TicketDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AssignTicket(int id, [FromBody] AssignTicketDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _helpdesk.AssignTicketAsync(id, dto, CompanyId, UserId.ToString(), ct);
            return result is null ? NotFound() : Ok(result);
        }

        // ── Comments ──────────────────────────────────────────────────────

        /// <summary>Returns all comments for a ticket, ordered oldest to newest.</summary>
        /// <param name="id">Ticket identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Ordered list of comments.</response>
        [HttpGet("tickets/{id:int}/comments")]
        [ProducesResponseType(typeof(System.Collections.Generic.IEnumerable<TicketCommentDto>), 200)]
        public async Task<IActionResult> GetComments(int id, CancellationToken ct)
        {
            // BUG FIX (confidentiality leak): only HR Admin / Admin / Support Agent may see
            // internal-only notes (CreateTicketCommentDto.IsInternal docs: "only visible to
            // agents"). This endpoint had no role restriction and was reachable by the plain
            // Employee role (see class-level remarks above), which previously received every
            // internal note verbatim. Employees now only ever see public comments.
            var includeInternal = User.IsInRole(AppRoles.HrAdmin)
                || User.IsInRole(AppRoles.LegacyAdmin)
                || User.IsInRole(AppRoles.SupportAgent)
                || User.IsInRole(AppRoles.SuperAdmin);
            return Ok(await _helpdesk.GetCommentsAsync(id, CompanyId, includeInternal, ct));
        }

        /// <summary>Adds a public comment or internal note to a ticket.</summary>
        /// <param name="id">Ticket identifier.</param>
        /// <param name="dto">Comment payload (message, isInternal).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="201">Newly created comment.</response>
        /// <response code="400">Validation error.</response>
        [HttpPost("tickets/{id:int}/comments")]
        [ProducesResponseType(typeof(TicketCommentDto), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> AddComment(int id, [FromBody] CreateTicketCommentDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // BUG FIX: an Employee caller could set isInternal=true on their own comment via
            // the request body, marking it as an agent-only internal note despite having no
            // agent privileges. Force isInternal to false for any caller who is not an
            // HR Admin / Admin / Support Agent / SuperAdmin, mirroring the read-side guard above.
            var canWriteInternal = User.IsInRole(AppRoles.HrAdmin)
                || User.IsInRole(AppRoles.LegacyAdmin)
                || User.IsInRole(AppRoles.SupportAgent)
                || User.IsInRole(AppRoles.SuperAdmin);
            if (!canWriteInternal)
                dto.IsInternal = false;
            var result = await _helpdesk.AddCommentAsync(id, dto, CompanyId, UserId.ToString(), ct);
            return StatusCode(201, result);
        }

        // ── Dashboard & Categories ─────────────────────────────────────────

        /// <summary>Returns aggregate statistics for the helpdesk dashboard.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Open, in-progress, resolved, closed, critical counts + avg resolution time.</response>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(HelpdeskSummaryDto), 200)]
        public async Task<IActionResult> GetSummary(CancellationToken ct)
            => Ok(await _helpdesk.GetSummaryAsync(CompanyId, ct));

        /// <summary>Returns all helpdesk categories for the tenant.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">List of categories with ticket counts.</response>
        [HttpGet("categories")]
        [ProducesResponseType(typeof(System.Collections.Generic.IEnumerable<TicketCategoryDto>), 200)]
        public async Task<IActionResult> GetCategories(CancellationToken ct)
            => Ok(await _helpdesk.GetCategoriesAsync(CompanyId, ct));

        /// <summary>Creates a new helpdesk category.</summary>
        /// <param name="dto">Category creation payload.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="201">Newly created category.</response>
        [HttpPost("categories")]
        [Authorize(Roles = AppRoles.HrAdminAndAdmin)]
        [ProducesResponseType(typeof(TicketCategoryDto), 201)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateTicketCategoryDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _helpdesk.CreateCategoryAsync(dto, CompanyId, ct);
            return StatusCode(201, result);
        }

        // ── Delete ────────────────────────────────────────────────────────

        /// <summary>
        /// Permanently deletes a helpdesk ticket including all comments and history.
        /// Admin and HR Admin only.
        /// </summary>
        /// <param name="id">Ticket identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="204">Ticket deleted successfully.</response>
        /// <response code="404">Ticket not found.</response>
        [HttpDelete("tickets/{id:int}")]
        [Authorize(Roles = AppRoles.HrAdminAndAdmin)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteTicket(int id, CancellationToken ct)
        {
            var deleted = await _helpdesk.DeleteTicketAsync(id, CompanyId, ct);
            return deleted ? NoContent() : NotFound();
        }
    }
}
