// BCrypt.Net namespace not needed — use fully-qualified BCrypt.Net.BCrypt.* calls below.
using HRMS.Application.Common;
using HRMS.Application.DTOs.SuperAdmin;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HRMS.API.Controllers.SuperAdmins;

[ApiController]
[Route("api/superadmins")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public class SuperAdminController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public SuperAdminController(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await _db.Users
            .Where(u => u.Role == AppRoles.SuperAdmin)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.Email, u.FullName, u.IsActive, u.CreatedAt })
            .ToPagedResultAsync(page, pageSize);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSuperAdminReq req)
    {
        // Item 8: delegate to the shared PasswordPolicy (min 12, upper/lower/digit/symbol,
        // common-password deny-list) instead of an inline duplicate of the rules.
        if (!PasswordPolicy.IsValid(req.Password, out var pwError))
            return BadRequest(ApiResponse.Fail(pwError!));

        // FIX: return 409 Conflict (not 400) for duplicate email.
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict(ApiResponse.Fail("Email already exists."));

        var user = new User
        {
            Email = req.Email,
            // Item 8 (final gate): EnsureValid throws ArgumentException (mapped to 400 by
            // ExceptionMiddleware) if the graceful IsValid check above is ever bypassed.
            PasswordHash = BcryptPasswordHasher.Hash(EnsurePolicy(req.Password), _config),
            Role = AppRoles.SuperAdmin,
            FullName = req.FullName
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { user.Id }, "Super admin created."));
    }

    /// <summary>
    /// Item 8 — last-line policy gate. Returns the password unchanged when it satisfies
    /// <see cref="PasswordPolicy"/>; otherwise throws <see cref="ArgumentException"/>, which
    /// ExceptionMiddleware maps to HTTP 400. Guarantees that no super-admin credential can
    /// reach BCrypt without a server-side complexity check.
    /// </summary>
    private static string EnsurePolicy(string? password)
    {
        PasswordPolicy.EnsureValid(password, nameof(password));
        return password!;
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusBody body)
    {
        // Security fix: scope FindAsync to superadmin role only.
        // Without this filter an authenticated superadmin could activate/deactivate
        // any user (employee, admin) by their primary key — not just other superadmins.
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == AppRoles.SuperAdmin);
        if (user == null) return NotFound(ApiResponse.Fail("Superadmin not found."));

        // Prevent the caller from deactivating themselves.
        if (user.Id == UserId)
            return BadRequest(ApiResponse.Fail("You cannot change your own active status."));

        user.IsActive = body.IsActive;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Updated."));
    }
}

