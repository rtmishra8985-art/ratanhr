using System;
using System.Collections.Generic;
using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Assets
{
    /// <summary>
    /// Represents a physical or digital asset owned by the company.
    /// </summary>
    public class Asset : ICompanyOwned
    {
        public int Id { get; set; }

        /// <summary>Unique asset code for tracking (e.g. AST-0001).</summary>
        public string AssetCode { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int? CategoryId { get; set; }
        public AssetCategory? Category { get; set; }

        public string? SerialNumber { get; set; }

        public DateTime? PurchaseDate { get; set; }

        public decimal? PurchasePrice { get; set; }

        public decimal? CurrentValue { get; set; }

        /// <summary>Available | Assigned | Under Maintenance | Lost | Damaged | Retired</summary>
        public string Status { get; set; } = "Available";

        public string? Location { get; set; }

        /// <summary>Employee currently holding this asset (null if unassigned).</summary>
        public string? AssignedToEmployeeId { get; set; }

        public DateTime? AssignedAt { get; set; }

        /// <summary>Tenant identifier for multi-tenant isolation.</summary>
        public int CompanyId { get; set; }
        int? ICompanyOwned.CompanyId => CompanyId;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// True when this asset was created by the demo-mode seed service
        /// (<see cref="HRMS.Infrastructure.Services.Demo.DemoSeedService"/>). Used by
        /// CleanupAsync to delete only demo assets and never touch real company assets.
        /// </summary>
        public bool IsDemo { get; set; } = false;

        public ICollection<AssetHistory> History { get; set; } = new List<AssetHistory>();
    }
}
