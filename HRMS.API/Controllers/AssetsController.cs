using System.Threading;
using System.Threading.Tasks;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Assets;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers
{
    /// <summary>
    /// Manages company assets including creation, assignment, return, and lifecycle tracking.
    /// </summary>
    /// <remarks>
    /// All endpoints require authentication and are scoped to the authenticated user's tenant.
    /// Role-based access: HR Admin can create/update/delete assets; all authenticated users can view.
    /// </remarks>
    [ApiController]
    [Route("api/assets")]
    [Authorize(Policy = "RequireMfaCompleted")]
    [Produces("application/json")]
    public class AssetsController : BaseController
    {
        private readonly IAssetService _assets;

        public AssetsController(IAssetService assets)
        {
            _assets = assets;
        }

        // ── Company-ID guard ─────────────────────────────────────────────
        //
        // FIX (AUDIT-V7): BaseController.CompanyId returns -1 when the companyId
        // JWT claim is absent or unparseable (e.g. SuperAdmin tokens, or a token
        // that was issued without the claim).  Passing -1 to IAssetService methods
        // would silently return an empty result set rather than failing fast.
        //
        // TryGetCompanyId() makes the failure explicit: every action that needs a
        // company-scoped query calls this helper first and returns 403 Forbidden
        // immediately if the claim cannot be resolved, instead of leaking a
        // confusing empty payload to the caller.
        //
        // IAssetService signatures intentionally remain int (not int?) because the
        // asset module has no SuperAdmin cross-tenant use case; SuperAdmin must
        // impersonate a specific tenant before accessing assets.

        /// <summary>
        /// Returns <c>true</c> and populates <paramref name="companyId"/> when the
        /// caller's <c>companyId</c> JWT claim is present and parseable.
        /// Returns <c>false</c> when the claim is absent (SuperAdmin without a
        /// tenant context, or a malformed token) — callers must return 403.
        /// </summary>
        private bool TryGetCompanyId(out int companyId)
        {
            companyId = CompanyId;   // BaseController.CompanyId returns -1 on failure
            return companyId != -1;
        }

        // ── Assets CRUD ───────────────────────────────────────────────────

        /// <summary>Returns a paginated, filtered list of assets for the tenant.</summary>
        /// <param name="query">Pagination, search, category, and status filters.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Paged list of assets.</response>
        /// <response code="403">Caller has no company context (SuperAdmin must impersonate a tenant first).</response>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<AssetDto>), 200)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> GetAssets([FromQuery] AssetQueryDto query, CancellationToken ct)
        {
            if (!TryGetCompanyId(out var cid))
                return Forbid();
            return Ok(await _assets.GetAssetsAsync(query, cid, ct));
        }

        /// <summary>Returns full detail for a single asset.</summary>
        /// <param name="id">Asset identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Asset detail.</response>
        /// <response code="403">Caller has no company context.</response>
        /// <response code="404">Asset not found.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(AssetDto), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetAsset(int id, CancellationToken ct)
        {
            if (!TryGetCompanyId(out var cid))
                return Forbid();
            var result = await _assets.GetAssetByIdAsync(id, cid, ct);
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Creates a new asset record.</summary>
        /// <param name="dto">Asset creation payload.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="201">Newly created asset.</response>
        /// <response code="400">Validation error.</response>
        /// <response code="403">Caller has no company context.</response>
        [HttpPost]
        [Authorize(Roles = AppRoles.HrAdminAndAdmin)]
        [ProducesResponseType(typeof(AssetDto), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> CreateAsset([FromBody] CreateAssetDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!TryGetCompanyId(out var cid))
                return Forbid();
            var result = await _assets.CreateAssetAsync(dto, cid, UserId.ToString(), ct);
            return CreatedAtAction(nameof(GetAsset), new { id = result.Id }, result);
        }

        /// <summary>Updates an existing asset's metadata.</summary>
        /// <param name="id">Asset identifier.</param>
        /// <param name="dto">Fields to update (all optional).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Updated asset.</response>
        /// <response code="403">Caller has no company context.</response>
        /// <response code="404">Asset not found.</response>
        [HttpPut("{id:int}")]
        [Authorize(Roles = AppRoles.HrAdminAndAdmin)]
        [ProducesResponseType(typeof(AssetDto), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateAsset(int id, [FromBody] UpdateAssetDto dto, CancellationToken ct)
        {
            if (!TryGetCompanyId(out var cid))
                return Forbid();
            var result = await _assets.UpdateAssetAsync(id, dto, cid, UserId.ToString(), ct);
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Retires (soft-deletes) an asset.</summary>
        /// <param name="id">Asset identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="204">Asset retired successfully.</response>
        /// <response code="403">Caller has no company context.</response>
        /// <response code="404">Asset not found.</response>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = AppRoles.HrAdminAndAdmin)]
        [ProducesResponseType(204)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteAsset(int id, CancellationToken ct)
        {
            if (!TryGetCompanyId(out var cid))
                return Forbid();
            var ok = await _assets.DeleteAssetAsync(id, cid, ct);
            return ok ? NoContent() : NotFound();
        }

        // ── Lifecycle actions ─────────────────────────────────────────────

        /// <summary>Assigns an asset to an employee.</summary>
        /// <param name="id">Asset identifier.</param>
        /// <param name="dto">Assignment payload including employee ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Updated asset with assignment info.</response>
        /// <response code="400">Asset is already assigned.</response>
        /// <response code="403">Caller has no company context.</response>
        /// <response code="404">Asset not found.</response>
        [HttpPost("{id:int}/assign")]
        [Authorize(Roles = AppRoles.HrAdminAndAdmin)]
        [ProducesResponseType(typeof(AssetDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AssignAsset(int id, [FromBody] AssignAssetDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!TryGetCompanyId(out var cid))
                return Forbid();
            var result = await _assets.AssignAssetAsync(id, dto, cid, UserId.ToString(), ct);
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Returns an asset from an employee back to inventory.</summary>
        /// <param name="id">Asset identifier.</param>
        /// <param name="dto">Return payload (condition, notes).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Updated asset with Available status.</response>
        /// <response code="403">Caller has no company context.</response>
        /// <response code="404">Asset not found.</response>
        [HttpPost("{id:int}/return")]
        [Authorize(Roles = AppRoles.HrAdminAndAdmin)]
        [ProducesResponseType(typeof(AssetDto), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ReturnAsset(int id, [FromBody] ReturnAssetDto dto, CancellationToken ct)
        {
            if (!TryGetCompanyId(out var cid))
                return Forbid();
            var result = await _assets.ReturnAssetAsync(id, dto, cid, UserId.ToString(), ct);
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Returns the full lifecycle history of an asset.</summary>
        /// <param name="id">Asset identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Ordered list of history entries (newest first).</response>
        /// <response code="403">Caller has no company context.</response>
        [HttpGet("{id:int}/history")]
        [ProducesResponseType(typeof(System.Collections.Generic.IEnumerable<AssetHistoryDto>), 200)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> GetAssetHistory(int id, CancellationToken ct)
        {
            if (!TryGetCompanyId(out var cid))
                return Forbid();
            return Ok(await _assets.GetAssetHistoryAsync(id, cid, ct));
        }

        /// <summary>Returns aggregate asset statistics for the dashboard.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Asset summary (total, assigned, available, etc.).</response>
        /// <response code="403">Caller has no company context.</response>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(AssetSummaryDto), 200)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> GetSummary(CancellationToken ct)
        {
            if (!TryGetCompanyId(out var cid))
                return Forbid();
            return Ok(await _assets.GetAssetSummaryAsync(cid, ct));
        }

        // ── Categories ────────────────────────────────────────────────────

        /// <summary>Returns all asset categories for the tenant.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">List of categories with asset counts.</response>
        /// <response code="403">Caller has no company context.</response>
        [HttpGet("categories")]
        [ProducesResponseType(typeof(System.Collections.Generic.IEnumerable<AssetCategoryDto>), 200)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> GetCategories(CancellationToken ct)
        {
            if (!TryGetCompanyId(out var cid))
                return Forbid();
            return Ok(await _assets.GetCategoriesAsync(cid, ct));
        }

        /// <summary>Creates a new asset category.</summary>
        /// <param name="dto">Category creation payload.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="201">Newly created category.</response>
        /// <response code="403">Caller has no company context.</response>
        [HttpPost("categories")]
        [Authorize(Roles = AppRoles.HrAdminAndAdmin)]
        [ProducesResponseType(typeof(AssetCategoryDto), 201)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateAssetCategoryDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!TryGetCompanyId(out var cid))
                return Forbid();
            var result = await _assets.CreateCategoryAsync(dto, cid, ct);
            return StatusCode(201, result);
        }
    }
}
