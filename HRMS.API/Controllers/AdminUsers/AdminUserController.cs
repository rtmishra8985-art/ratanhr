using HRMS.Application.Common;
using HRMS.Application.DTOs.AdminUsers;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HRMS.API.Controllers.AdminUsers;

[ApiController]
[Route("api/admin-users")]
[Authorize(Roles = AppRoles.SuperAdminAndAdmin)]
public class AdminUserController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public AdminUserController(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // ── IDOR helper ────────────────────────────────────────────────────────
    // Returns the caller's companyId, or null if they are superadmin.

    /// <summary>List admin users. Superadmin sees all; admin sees only their company.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var companyId = CallerCompanyIdOrNull;

        var query = _db.Users
            .Where(u => u.Role == AppRoles.Admin && !u.IsDeleted);

        if (companyId.HasValue)
            query = query.Where(u => u.CompanyId == companyId.Value);

        query = query.OrderByDescending(u => u.CreatedAt);
        
        // FIX 1: Use PaginationHelper for consistent bounds
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);
        
        var total = await query.CountAsync();
        var users = await query
            .Skip(PaginationHelper.CalculateSkip(page, pageSize))
            .Take(pageSize)
            .Select(u => new HRMS.Application.DTOs.AdminUsers.AdminUserDto
            {
                Id        = u.Id,
                Email     = u.Email,
                FullName  = u.FullName,
                AdminRole = u.AdminRole,
                CompanyId = u.CompanyId,
                IsActive  = u.IsActive,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        var result = HRMS.Application.Common.PagedResult<HRMS.Application.DTOs.AdminUsers.AdminUserDto>
            .Create(users, total, page, pageSize);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<HRMS.Application.DTOs.AdminUsers.AdminUserDto>>.Ok(result));
    }

    /// <summary>Get a single admin user by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var companyId = CallerCompanyIdOrNull;

        var query = _db.Users
            .Where(u => u.Id == id && u.Role == AppRoles.Admin && !u.IsDeleted);

        // IDOR fix: a non-superadmin admin may only view users in their own company.
        if (companyId.HasValue)
            query = query.Where(u => u.CompanyId == companyId.Value);

        var user = await query
            .Select(u => new AdminUserDto
            {
                Id        = u.Id,
                Email     = u.Email,
                FullName  = u.FullName,
                AdminRole = u.AdminRole,
                CompanyId = u.CompanyId,
                IsActive  = u.IsActive,
                CreatedAt = u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null) return NotFound(ApiResponse.Fail("Admin user not found."));
        return Ok(ApiResponse<AdminUserDto>.Ok(user));
    }

    /// <summary>Create a new admin user (superadmin only)</summary>
    /// <remarks>
    /// FIX 6: HTTP status codes — REST conventions
    /// POST (resource creation) → 201 Created
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateAdminUserRequest req)
    {
        // FIX: return 409 Conflict (not 400 Bad Request) for duplicate email —
        // the request was well-formed; the conflict is a state problem, not a client error.
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return StatusCode(StatusCodes.Status409Conflict,
                ApiResponse.Fail("Email already registered."));

        // Item 8: server-side password complexity (PasswordPolicy is the single source of truth).
        if (!PasswordPolicy.IsValid(req.Password, out var pwError))
            return BadRequest(ApiResponse.Fail(pwError!));

        var user = new User
        {
            Email        = req.Email,
            PasswordHash = BcryptPasswordHasher.Hash(req.Password, _config),
            Role         = AppRoles.Admin,
            FullName     = req.FullName,
            AdminRole    = req.AdminRole,
            CompanyId    = req.CompanyId,
            IsActive     = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        
        // FIX 6: 201 Created for resource creation (was 200 OK previously).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { user.Id }, "Admin user created."));
    }

    /// <summary>Update an admin user's details (superadmin only)</summary>
    /// <remarks>
    /// FIX 6: HTTP status codes — REST conventions
    /// PUT (full replacement) → 200 OK
    /// </remarks>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAdminUserRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == AppRoles.Admin);
        if (user == null) return NotFound(ApiResponse.Fail("Admin user not found."));

        user.FullName  = req.FullName  ?? user.FullName;
        user.AdminRole = req.AdminRole ?? user.AdminRole;
        user.CompanyId = req.CompanyId ?? user.CompanyId;

        // Only update the password when a new one is explicitly provided.
        if (!string.IsNullOrWhiteSpace(req.NewPassword))
        {
            // Item 8: admin-initiated password set must meet the same policy.
            if (!PasswordPolicy.IsValid(req.NewPassword, out var pwError))
                return BadRequest(ApiResponse.Fail(pwError!));
            user.PasswordHash = BcryptPasswordHasher.Hash(req.NewPassword, _config);
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Admin user updated."));
    }

    /// <summary>Activate or deactivate an admin user (superadmin only)</summary>
    /// <remarks>
    /// FIX 6: HTTP status codes — REST conventions
    /// PATCH (partial update with response body) → 200 OK
    /// </remarks>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusReq req)
    {
        // Security fix: scope the lookup to admin role only.
        // FindAsync(id) returns any user by PK — without the role filter a superadmin
        // could toggle the IsActive flag of any employee or other superadmin.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == AppRoles.Admin);
        if (user == null) return NotFound(ApiResponse.Fail("Admin user not found."));
        
        user.IsActive = req.IsActive;
        await _db.SaveChangesAsync();
        
        return Ok(ApiResponse.Ok("Status updated."));
    }

    /// <summary>Delete an admin user (superadmin only)</summary>
    /// <remarks>
    /// FIX 6: HTTP status codes — REST conventions
    /// DELETE (resource removal) → 204 No Content (empty response body)
    /// Route intentionally unconstrained: with an {id:int} constraint a non-numeric id
    /// fails routing and returns 404 before authorization runs, which leaks endpoint
    /// existence and lets non-superadmins probe the route. Without the constraint the
    /// authorization filter runs first (403) and [ApiController] model binding rejects
    /// an unparseable id with 400.
    /// </remarks>
    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        // Security fix: scope lookup to admin role and guard against self-deletion.
        // Without the role filter, this endpoint would delete any user (employee,
        // superadmin) by primary key with no audit trail.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == AppRoles.Admin);
        if (user == null) return NotFound(ApiResponse.Fail("Admin user not found."));

        if (user.Id == UserId)
            return BadRequest(ApiResponse.Fail("You cannot delete your own account."));

        // FIX: soft delete instead of hard delete — preserves audit trail and foreign-key
        // integrity. IsActive is also set false so the account cannot be used even if the
        // IsDeleted filter were somehow bypassed.
        user.IsDeleted = true;
        user.IsActive  = false;
        user.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        
        return NoContent();
    }
}
