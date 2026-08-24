using System;

namespace HRMS.Domain.Entities.Helpdesk
{
    /// <summary>
    /// A comment or reply on a helpdesk ticket.
    /// </summary>
    public class HelpdeskComment
    {
        public int Id { get; set; }

        public int TicketId { get; set; }
        public HelpdeskTicket? Ticket { get; set; }

        public string AuthorId { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        /// <summary>Internal notes are only visible to agents, not the ticket raiser.</summary>
        public bool IsInternal { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
