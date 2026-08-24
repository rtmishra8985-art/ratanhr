using System;
using System.Collections.Generic;
using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Helpdesk
{
    /// <summary>
    /// A support ticket raised by an employee.
    /// </summary>
    public class HelpdeskTicket : ICompanyOwned
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>Open | In Progress | Resolved | Closed | Cancelled</summary>
        public string Status { get; set; } = "Open";

        /// <summary>Low | Medium | High | Critical</summary>
        public string Priority { get; set; } = "Medium";

        public int? CategoryId { get; set; }
        public HelpdeskCategory? Category { get; set; }

        public string? RaisedByEmployeeId { get; set; }

        public string? AssignedToUserId { get; set; }

        public int CompanyId { get; set; }
        int? ICompanyOwned.CompanyId => CompanyId;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public ICollection<HelpdeskComment> Comments { get; set; } = new List<HelpdeskComment>();
        public ICollection<HelpdeskHistory> History { get; set; } = new List<HelpdeskHistory>();
    }
}
