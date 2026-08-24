using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HRMS.Application.DTOs.Assets;
using HRMS.Application.Interfaces;
using HRMS.Application.Common;
using HRMS.Domain.Entities.Assets;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Repositories;

namespace HRMS.Infrastructure.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IAssetService"/> backed by EF Core.
    /// Core entity read operations (paginated list, single-entity fetch, history)
    /// are delegated to <see cref="IAssetRepository"/> so the repository layer is
    /// actually exercised at runtime.  Write operations and category queries that
    /// have no dedicated repository method continue to use the DbContext directly.
    /// </summary>
    public class AssetService : IAssetService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAssetRepository _repo;
        private readonly ILogger<AssetService> _logger;

        public AssetService(
            ApplicationDbContext db,
            IAssetRepository repo,
            ILogger<AssetService> logger)
        {
            _db     = db;
            _repo   = repo;
            _logger = logger;
        }

        // ── Assets ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<PagedResult<AssetDto>> GetAssetsAsync(AssetQueryDto query, int companyId, CancellationToken ct = default)
        {
            // Delegate to repository — avoids bypassing the repository layer.
            var paged = await _repo.GetPagedByCompanyAsync(
                companyId,
                query.Search,
                query.Status,
                query.CategoryId,
                query.SortBy,
                query.SortDirection,
                query.Page,
                query.PageSize,
                ct);

            return new PagedResult<AssetDto>
            {
                Items      = paged.Items.Select(MapToDto).ToList(),
                TotalCount = paged.TotalCount,
                Page       = paged.Page,
                PageSize   = paged.PageSize,
            };
        }

        /// <inheritdoc/>
        public async Task<AssetDto?> GetAssetByIdAsync(int id, int companyId, CancellationToken ct = default)
        {
            // Delegate to repository — includes Category and History navigation properties.
            var asset = await _repo.GetByIdWithCategoryAsync(id, companyId, ct);
            return asset is null ? null : MapToDto(asset);
        }

        /// <inheritdoc/>
        public async Task<AssetDto> CreateAssetAsync(CreateAssetDto dto, int companyId, string createdByUserId, CancellationToken ct = default)
        {
            // Delegate uniqueness check to repository — avoids duplicate asset codes within a tenant.
            if (await _repo.AssetCodeExistsAsync(dto.AssetCode, companyId, ct))
                throw new InvalidOperationException($"Asset code '{dto.AssetCode}' is already in use within this company.");

            var asset = new Asset
            {
                AssetCode      = dto.AssetCode,
                Name           = dto.Name,
                Description    = dto.Description,
                CategoryId     = dto.CategoryId,
                SerialNumber   = dto.SerialNumber,
                PurchaseDate   = dto.PurchaseDate,
                PurchasePrice  = dto.PurchasePrice,
                CurrentValue   = dto.PurchasePrice,
                Location       = dto.Location,
                Status         = "Available",
                CompanyId      = companyId,
            };

            _db.Assets.Add(asset);
            AddHistory(asset, "Created", null, createdByUserId, "Asset created");
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Asset {Code} created by {User}", asset.AssetCode, createdByUserId);
            return MapToDto(asset);
        }

        /// <inheritdoc/>
        public async Task<AssetDto?> UpdateAssetAsync(int id, UpdateAssetDto dto, int companyId, string updatedByUserId, CancellationToken ct = default)
        {
            var asset = await _db.Assets
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, ct);

            if (asset is null) return null;

            if (dto.Name        is not null) asset.Name        = dto.Name;
            if (dto.CategoryId  is not null) asset.CategoryId  = dto.CategoryId;
            if (dto.Description is not null) asset.Description = dto.Description;
            if (dto.Location    is not null) asset.Location    = dto.Location;
            if (dto.Status      is not null) asset.Status      = dto.Status;
            if (dto.CurrentValue.HasValue)   asset.CurrentValue = dto.CurrentValue;

            asset.UpdatedAt = DateTime.UtcNow;
            AddHistory(asset, "Updated", null, updatedByUserId, "Asset metadata updated");
            await _db.SaveChangesAsync(ct);
            return MapToDto(asset);
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAssetAsync(int id, int companyId, CancellationToken ct = default)
        {
            var asset = await _db.Assets
                .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId && a.DeletedAt == null, ct);

            if (asset is null) return false;

            // Soft-delete: retire and stamp is_deleted + deleted_at to preserve full history.
            // Status "Retired" signals the asset is no longer in active use;
            // IsDeleted is the EF Core global query filter marker; deleted_at is the audit stamp.
            asset.IsDeleted = true;
            asset.Status    = "Retired";
            asset.DeletedAt = DateTime.UtcNow;
            asset.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        /// <inheritdoc/>
        public async Task<AssetDto?> AssignAssetAsync(int id, AssignAssetDto dto, int companyId, string performedByUserId, CancellationToken ct = default)
        {
            var asset = await _db.Assets
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, ct);

            if (asset is null) return null;
            if (asset.Status == "Assigned")
                throw new InvalidOperationException($"Asset {asset.AssetCode} is already assigned.");

            asset.Status               = "Assigned";
            asset.AssignedToEmployeeId = dto.EmployeeId;
            asset.AssignedAt           = DateTime.UtcNow;
            asset.UpdatedAt            = DateTime.UtcNow;

            AddHistory(asset, "Assigned", dto.EmployeeId, performedByUserId, dto.Notes);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Asset {Code} assigned to {Employee}", asset.AssetCode, dto.EmployeeId);
            return MapToDto(asset);
        }

        /// <inheritdoc/>
        public async Task<AssetDto?> ReturnAssetAsync(int id, ReturnAssetDto dto, int companyId, string performedByUserId, CancellationToken ct = default)
        {
            var asset = await _db.Assets
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, ct);

            if (asset is null) return null;

            var previousEmployee = asset.AssignedToEmployeeId;
            asset.Status               = dto.Condition?.ToLower() == "damaged" ? "Damaged" : "Available";
            asset.AssignedToEmployeeId = null;
            asset.AssignedAt           = null;
            asset.UpdatedAt            = DateTime.UtcNow;

            AddHistory(asset, "Returned", previousEmployee, performedByUserId,
                       $"Returned{(dto.Notes is not null ? ": " + dto.Notes : string.Empty)}");
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Asset {Code} returned", asset.AssetCode);
            return MapToDto(asset);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<AssetHistoryDto>> GetAssetHistoryAsync(int id, int companyId, CancellationToken ct = default)
        {
            // Delegate to repository — includes tenant-scoped JOIN guard.
            var history = await _repo.GetHistoryAsync(id, companyId, ct);
            return history.Select(h => new AssetHistoryDto
            {
                Id         = h.Id,
                AssetId    = h.AssetId,
                Action     = h.Action,
                EmployeeId = h.EmployeeId,
                Notes      = h.Notes,
                Timestamp  = h.Timestamp,
            });
        }

        /// <inheritdoc/>
        public async Task<AssetSummaryDto> GetAssetSummaryAsync(int companyId, CancellationToken ct = default)
        {
            var assets = await _db.Assets
                .Where(a => a.CompanyId == companyId && a.DeletedAt == null)
                .ToListAsync(ct);

            return new AssetSummaryDto
            {
                Total            = assets.Count,
                Assigned         = assets.Count(a => a.Status == "Assigned"),
                Available        = assets.Count(a => a.Status == "Available"),
                UnderMaintenance = assets.Count(a => a.Status == "Under Maintenance"),
                Lost             = assets.Count(a => a.Status == "Lost"),
                TotalValue       = assets.Sum(a => a.CurrentValue ?? 0),
            };
        }

        // ── Categories ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<IEnumerable<AssetCategoryDto>> GetCategoriesAsync(int companyId, CancellationToken ct = default)
        {
            // FIX O3: compute per-category counts with a single GROUP BY query, then merge
            // in memory. The original c.Assets.Count() inside a Select() projection could
            // produce a correlated subquery per category row depending on EF Core translation;
            // this approach is explicit and always exactly two DB round-trips.
            // FIX: Explicit !IsDeleted guard ensures soft-deleted assets are excluded from
            // category counts even if the EF global query filter is ever disabled or bypassed.
            var countMap = await _db.Assets
                .Where(a => a.CompanyId == companyId && !a.IsDeleted)
                .GroupBy(a => a.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .Where(x => x.CategoryId != null)
                .ToDictionaryAsync(x => x.CategoryId!.Value, x => x.Count, ct);

            var cats = await _db.AssetCategories
                .Where(c => c.CompanyId == companyId)
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(ct);

            return cats.Select(c => new AssetCategoryDto
            {
                Id          = c.Id,
                Name        = c.Name,
                Description = c.Description,
                AssetCount  = countMap.GetValueOrDefault(c.Id, 0),
            });
        }

        /// <inheritdoc/>
        public async Task<AssetCategoryDto> CreateCategoryAsync(CreateAssetCategoryDto dto, int companyId, CancellationToken ct = default)
        {
            var category = new AssetCategory
            {
                Name        = dto.Name,
                Description = dto.Description,
                CompanyId   = companyId,
            };
            _db.AssetCategories.Add(category);
            await _db.SaveChangesAsync(ct);

            return new AssetCategoryDto { Id = category.Id, Name = category.Name, Description = category.Description, AssetCount = 0 };
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static AssetDto MapToDto(Asset a) => new()
        {
            Id                   = a.Id,
            AssetCode            = a.AssetCode,
            Name                 = a.Name,
            Description          = a.Description,
            CategoryId           = a.CategoryId,
            CategoryName         = a.Category?.Name,
            SerialNumber         = a.SerialNumber,
            PurchaseDate         = a.PurchaseDate,
            PurchasePrice        = a.PurchasePrice,
            CurrentValue         = a.CurrentValue,
            Status               = a.Status,
            Location             = a.Location,
            AssignedToEmployeeId = a.AssignedToEmployeeId,
            AssignedToName       = null, // resolved by caller when needed
            AssignedAt           = a.AssignedAt,
            CreatedAt            = a.CreatedAt,
            UpdatedAt            = a.UpdatedAt,
        };

        private static void AddHistory(Asset asset, string action, string? employeeId, string? performedBy, string? notes)
        {
            asset.History.Add(new AssetHistory
            {
                Action              = action,
                EmployeeId          = employeeId,
                PerformedByUserId   = performedBy,
                Notes               = notes,
                Timestamp           = DateTime.UtcNow,
            });
        }
    }
}
