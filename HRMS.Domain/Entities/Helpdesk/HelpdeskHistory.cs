using System;

namespace HRMS.Domain.Entities.Helpdesk
{
    /// <summary>
    /// Immutable audit log entry for every state change on a helpdesk ticket.
    /// </summary>
    public class HelpdeskHistory
    {
        public int Id { get; set; }

        public int TicketId { get; set; }
        public HelpdeskTicket? Ticket { get; set; }

        public string Action { get; set; } = string.Empty;

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public string? PerformedByUserId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
