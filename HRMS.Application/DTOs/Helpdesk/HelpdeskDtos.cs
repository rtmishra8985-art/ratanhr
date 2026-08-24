using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.DTOs.Helpdesk
{
    // ── Responses ──────────────────────────────────────────────────────────

    /// <summary>Full ticket representation returned by the API.</summary>
    public class TicketDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? RaisedByEmployeeId { get; set; }
        public string? RaisedByName { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int CommentCount { get; set; }
    }

    /// <summary>Summary statistics for the helpdesk dashboard.</summary>
    public class HelpdeskSummaryDto
    {
        public int Open { get; set; }
        public int InProgress { get; set; }
        public int Resolved { get; set; }
        public int Closed { get; set; }
        public int Critical { get; set; }
        public double? AvgResolutionHours { get; set; }
    }

    /// <summary>A comment or reply on a ticket.</summary>
    public class TicketCommentDto
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public string AuthorId { get; set; } = string.Empty;
        public string? AuthorName { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Helpdesk category with aggregate ticket count.</summary>
    public class TicketCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int TicketCount { get; set; }
    }

    // ── Requests ───────────────────────────────────────────────────────────

    /// <summary>Payload for creating a new helpdesk ticket.</summary>
    public class CreateTicketDto
    {
        [Required(ErrorMessage = "Ticket title is required.")]
        [StringLength(300)]
        public string Title { get; set; } = string.Empty;

        [StringLength(5000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Priority is required.")]
        public string Priority { get; set; } = "Medium";

        public int? CategoryId { get; set; }
    }

    /// <summary>Payload for updating an existing ticket.</summary>
    public class UpdateTicketDto
    {
        [StringLength(300)]
        public string? Title { get; set; }

        [StringLength(5000)]
        public string? Description { get; set; }

        /// <summary>Allowed: Open | In Progress | Resolved | Closed | Cancelled</summary>
        public string? Status { get; set; }

        /// <summary>Allowed: Low | Medium | High | Critical</summary>
        public string? Priority { get; set; }

        public int? CategoryId { get; set; }
    }

    /// <summary>Payload for assigning a ticket to an agent.</summary>
    public class AssignTicketDto
    {
        [Required(ErrorMessage = "AssignedToId is required.")]
        public string AssignedToId { get; set; } = string.Empty;
    }

    /// <summary>Payload for adding a comment to a ticket.</summary>
    public class CreateTicketCommentDto
    {
        [Required(ErrorMessage = "Message is required.")]
        [StringLength(5000)]
        public string Message { get; set; } = string.Empty;

        /// <summary>Internal notes are only visible to agents.</summary>
        public bool IsInternal { get; set; } = false;
    }

    /// <summary>Payload for creating a ticket category.</summary>
    public class CreateTicketCategoryDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }

    /// <summary>Query parameters for paginating and filtering tickets.</summary>
    public class TicketQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public int? CategoryId { get; set; }
        public string? AssignedToId { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
    }
}
