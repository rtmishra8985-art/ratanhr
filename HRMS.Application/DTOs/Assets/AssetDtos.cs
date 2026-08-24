using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.DTOs.Assets
{
    // ── Responses ──────────────────────────────────────────────────────────

    /// <summary>Full asset representation returned by the API.</summary>
    public class AssetDto
    {
        public int Id { get; set; }
        public string AssetCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? CurrentValue { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? AssignedToEmployeeId { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Summary statistics for the asset dashboard.</summary>
    public class AssetSummaryDto
    {
        public int Total { get; set; }
        public int Assigned { get; set; }
        public int Available { get; set; }
        public int UnderMaintenance { get; set; }
        public int Lost { get; set; }
        public decimal TotalValue { get; set; }
    }

    /// <summary>Single audit entry in an asset's lifecycle history.</summary>
    public class AssetHistoryDto
    {
        public int Id { get; set; }
        public int AssetId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? Notes { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>Asset category with aggregate headcount.</summary>
    public class AssetCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int AssetCount { get; set; }
    }

    // ── Requests ───────────────────────────────────────────────────────────

    /// <summary>Payload for creating a new asset.</summary>
    public class CreateAssetDto
    {
        [Required(ErrorMessage = "Asset name is required.")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Asset code is required.")]
        [StringLength(50)]
        public string AssetCode { get; set; } = string.Empty;

        public int? CategoryId { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? SerialNumber { get; set; }

        public DateTime? PurchaseDate { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Purchase price must be non-negative.")]
        public decimal? PurchasePrice { get; set; }

        [StringLength(200)]
        public string? Location { get; set; }
    }

    /// <summary>Payload for updating an existing asset's metadata.</summary>
    public class UpdateAssetDto
    {
        [StringLength(200)]
        public string? Name { get; set; }

        public int? CategoryId { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(200)]
        public string? Location { get; set; }

        /// <summary>Allowed: Available | Under Maintenance | Lost | Damaged | Retired</summary>
        public string? Status { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? CurrentValue { get; set; }
    }

    /// <summary>Payload for assigning an asset to an employee.</summary>
    public class AssignAssetDto
    {
        [Required(ErrorMessage = "EmployeeId is required.")]
        public string EmployeeId { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    /// <summary>Payload for returning an asset from an employee.</summary>
    public class ReturnAssetDto
    {
        /// <summary>Condition of the asset upon return (Good | Damaged | Lost).</summary>
        public string? Condition { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    /// <summary>Payload for creating an asset category.</summary>
    public class CreateAssetCategoryDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }

    /// <summary>Query parameters for paginating and filtering assets.</summary>
    public class AssetQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public string? Status { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
    }
}
