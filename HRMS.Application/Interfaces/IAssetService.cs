using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HRMS.Application.DTOs.Assets;
using HRMS.Application.Common;

namespace HRMS.Application.Interfaces
{
    /// <summary>
    /// Service contract for the Asset Management module.
    /// All operations are tenant-scoped via <paramref name="companyId"/>.
    /// </summary>
    public interface IAssetService
    {
        /// <summary>Returns a paginated, filtered list of assets.</summary>
        Task<PagedResult<AssetDto>> GetAssetsAsync(AssetQueryDto query, int companyId, CancellationToken ct = default);

        /// <summary>Returns full detail for a single asset.</summary>
        Task<AssetDto?> GetAssetByIdAsync(int id, int companyId, CancellationToken ct = default);

        /// <summary>Creates a new asset record.</summary>
        Task<AssetDto> CreateAssetAsync(CreateAssetDto dto, int companyId, string createdByUserId, CancellationToken ct = default);

        /// <summary>Updates an existing asset's metadata.</summary>
        Task<AssetDto?> UpdateAssetAsync(int id, UpdateAssetDto dto, int companyId, string updatedByUserId, CancellationToken ct = default);

        /// <summary>Soft-deletes an asset (sets status to Retired).</summary>
        Task<bool> DeleteAssetAsync(int id, int companyId, CancellationToken ct = default);

        /// <summary>Assigns an asset to an employee.</summary>
        Task<AssetDto?> AssignAssetAsync(int id, AssignAssetDto dto, int companyId, string performedByUserId, CancellationToken ct = default);

        /// <summary>Returns an asset from an employee back to inventory.</summary>
        Task<AssetDto?> ReturnAssetAsync(int id, ReturnAssetDto dto, int companyId, string performedByUserId, CancellationToken ct = default);

        /// <summary>Returns the full lifecycle history of an asset.</summary>
        Task<IEnumerable<AssetHistoryDto>> GetAssetHistoryAsync(int id, int companyId, CancellationToken ct = default);

        /// <summary>Returns aggregate asset statistics for the dashboard.</summary>
        Task<AssetSummaryDto> GetAssetSummaryAsync(int companyId, CancellationToken ct = default);

        // ── Categories ────────────────────────────────────────────────────

        Task<IEnumerable<AssetCategoryDto>> GetCategoriesAsync(int companyId, CancellationToken ct = default);

        Task<AssetCategoryDto> CreateCategoryAsync(CreateAssetCategoryDto dto, int companyId, CancellationToken ct = default);
    }
}
