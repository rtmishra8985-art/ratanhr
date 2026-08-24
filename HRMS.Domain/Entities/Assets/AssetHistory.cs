using System;

namespace HRMS.Domain.Entities.Assets
{
    /// <summary>
    /// Immutable audit log entry for every state change on an asset.
    /// </summary>
    public class AssetHistory
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        /// <summary>Action performed: Assigned | Returned | StatusChanged | Created | Updated.</summary>
        public string Action { get; set; } = string.Empty;

        public string? EmployeeId { get; set; }

        public string? Notes { get; set; }

        public string? PerformedByUserId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
