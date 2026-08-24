using System.Collections.Generic;
using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Helpdesk
{
    /// <summary>
    /// Category for grouping helpdesk tickets (e.g. IT Support, HR Queries).
    /// </summary>
    public class HelpdeskCategory : ICompanyOwned
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int CompanyId { get; set; }
        int? ICompanyOwned.CompanyId => CompanyId;

        public ICollection<HelpdeskTicket> Tickets { get; set; } = new List<HelpdeskTicket>();
    }
}
