using BCrypt.Net;
using HRMS.Application.Common;
using HRMS.Application.DTOs.AdminUsers;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Service implementation for admin-user management.
/// Wraps the same EF queries used by AdminUserController so they can be
/// unit-tested without spinning up the full HTTP pipeline.
/// </summary>
public sealed class AdminUserService : IAdminUserService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService        _audit;
    private readonly IEmailService        _email;
    private readonly IConfiguration       _config;

    public AdminUserService(ApplicationDbContext db, IAuditService audit, IEmailService email, IConfiguration config)
    {
        _db = db; _audit = audit; _email = email; _config = config;
    }

    public async Task<List<AdminUserDto>> GetAdminsByCompanyAsync(int companyId)
        => await _db.Users
            .Where(u => u.CompanyId == companyId && u.Role == "admin")
            .Select(u => Map(u))
            .ToListAsync();

    public async Task<AdminUserDto?> GetAdminByIdAsync(int userId, int companyId)
    {
        var u = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId && u.Role == "admin");
        return u is null ? null : Map(u);
    }

    public async Task<int> CreateAdminAsync(int companyId, CreateAdminUserDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            throw new InvalidOperationException($"Email '{dto.Email}' is already registered.");

        // Item 8: full complexity policy (was a bare length >= 8 check).
        PasswordPolicy.EnsureValid(dto.Password, nameof(dto.Password));

        var user = new User
        {
            Email        = dto.Email,
            FullName     = dto.FullName,
            Role         = "admin",
            CompanyId    = companyId,
            IsActive     = true,
            // BUG FIX: was BCrypt.Net.BCrypt.HashPassword(dto.Password) using BCrypt.Net-Next's
            // hardcoded default work factor (11), inconsistent with BcryptPasswordHasher.Hash
            // (used by every other password-creation path in the app: AuthService, SeedAsync,
            // SuperAdminController) which reads the configurable Security:BcryptWorkFactor
            // (default 12). A deployment that raised the configured work factor for stronger
            // hashing would have had that setting silently ignored for admins created here.
            PasswordHash = BcryptPasswordHasher.Hash(dto.Password, _config)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user.Id;
    }

    public async Task<bool> AssignRoleAsync(int userId, int companyId, string role)
    {
        if (string.Equals(role, "superadmin", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Only SuperAdmin can grant the SuperAdmin role.");

        var u = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId);
        if (u is null) return false;
        u.Role = role;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(int userId, int companyId, string newPassword)
    {
        // Item 8: admin-initiated resets must satisfy the same policy as self-service.
        PasswordPolicy.EnsureValid(newPassword, nameof(newPassword));
        var u = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId);
        if (u is null) return false;
        // BUG FIX: same work-factor inconsistency as CreateAdminAsync above.
        u.PasswordHash = BcryptPasswordHasher.Hash(newPassword, _config);
        await _db.SaveChangesAsync();
        return true;
    }

    private static AdminUserDto Map(User u) => new()
    {
        Id        = u.Id,
        Email     = u.Email,
        FullName  = u.FullName,
        AdminRole = u.AdminRole,
        CompanyId = u.CompanyId,
        IsActive  = u.IsActive,
        CreatedAt = u.CreatedAt
    };
}
