using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Assets;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Repositories
{
    /// <summary>
    /// EF Core-backed repository for the Asset Management module.
    /// Inherits generic CRUD + tenant-guard from <see cref="GenericRepository{T}"/>.
    /// All query methods apply an explicit <paramref name="companyId"/> filter as a
    /// secondary defence-in-depth layer on top of the EF global query filter.
    /// </summary>
    public class AssetRepository : GenericRepository<Asset>, IAssetRepository
    {
        public AssetRepository(ApplicationDbContext ctx, ITenantContext? tenant = null)
            : base(ctx, tenant) { }

        /// <inheritdoc/>
        public async Task<PagedResult<Asset>> GetPagedByCompanyAsync(
            int companyId,
            string? search,
            string? status,
            int? categoryId,
            string? sortBy,
            string? sortDirection,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var q = _ctx.Assets
                .Include(a => a.Category)
                .Where(a => a.CompanyId == companyId && a.DeletedAt == null)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(a => a.Name.Contains(search) || a.AssetCode.Contains(search));

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(a => a.Status == status);

            if (categoryId.HasValue)
                q = q.Where(a => a.CategoryId == categoryId.Value);

            q = (sortBy?.ToLower(), sortDirection?.ToLower()) switch
            {
                ("name",         "desc") => q.OrderByDescending(a => a.Name),
                ("name",         _)      => q.OrderBy(a => a.Name),
                ("purchasedate", "desc") => q.OrderByDescending(a => a.PurchaseDate),
                ("purchasedate", _)      => q.OrderBy(a => a.PurchaseDate),
                ("status",       "desc") => q.OrderByDescending(a => a.Status),
                ("status",       _)      => q.OrderBy(a => a.Status),
                _                        => q.OrderBy(a => a.Id),
            };

            return await q.ToPagedResultAsync(page, pageSize, ct: ct);
        }

        /// <inheritdoc/>
        public async Task<Asset?> GetByIdWithCategoryAsync(int id, int companyId, CancellationToken ct = default)
            // Explicit by-id lookup intentionally includes retired/soft-deleted assets:
            // the detail view and audit trail must still resolve an asset after it has been
            // retired (DELETE = retire). Callers that need only live inventory use
            // GetPagedAsync/list queries, which keep the DeletedAt == null filter.
        => await _ctx.Assets
                .IgnoreQueryFilters()
                .Include(a => a.Category)
                .Include(a => a.History)
                .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, ct);


        /// <inheritdoc/>
        public async Task<IEnumerable<AssetHistory>> GetHistoryAsync(
            int assetId, int companyId, CancellationToken ct = default)
            => await _ctx.Set<AssetHistory>()
                .Where(h => h.AssetId == assetId)
                .Join(_ctx.Assets.Where(a => a.CompanyId == companyId),
                      h => h.AssetId, a => a.Id, (h, _) => h)
                .OrderByDescending(h => h.Timestamp)
                .ToListAsync(ct);

        /// <inheritdoc/>
        public async Task<bool> AssetCodeExistsAsync(string assetCode, int companyId, CancellationToken ct = default)
            => await _ctx.Assets
                .AnyAsync(a => a.AssetCode == assetCode && a.CompanyId == companyId && a.DeletedAt == null, ct);
    }
}
