using System.Threading;
using System.Threading.Tasks;
using HRMS.Application.Common;
using HRMS.Domain.Entities.Assets;

namespace HRMS.Infrastructure.Repositories
{
    /// <summary>
    /// Repository contract for the Asset Management module.
    /// Extends <see cref="IGenericRepository{T}"/> with asset-specific query methods.
    /// All operations are implicitly tenant-scoped via EF Core global query filters.
    /// </summary>
    public interface IAssetRepository : IGenericRepository<Asset>
    {
        /// <summary>Returns a paginated, filtered list of assets for the given company.</summary>
        Task<PagedResult<Asset>> GetPagedByCompanyAsync(
            int companyId,
            string? search,
            string? status,
            int? categoryId,
            string? sortBy,
            string? sortDirection,
            int page,
            int pageSize,
            CancellationToken ct = default);

        /// <summary>Returns a single asset including its category, scoped to the tenant.</summary>
        Task<Asset?> GetByIdWithCategoryAsync(int id, int companyId, CancellationToken ct = default);

        /// <summary>Returns the full lifecycle history for an asset, scoped to the tenant.</summary>
        Task<System.Collections.Generic.IEnumerable<AssetHistory>> GetHistoryAsync(
            int assetId, int companyId, CancellationToken ct = default);

        /// <summary>
        /// Returns true when an asset code is already in use within the company.
        /// Used during creation to enforce uniqueness.
        /// </summary>
        Task<bool> AssetCodeExistsAsync(string assetCode, int companyId, CancellationToken ct = default);
    }
}
