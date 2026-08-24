using System.Security.Cryptography;
using BCrypt.Net;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Auth;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.FileStorage;
using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class AuthService : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration     = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan ResetTokenLifetime   = TimeSpan.FromMinutes(30);

    private readonly ApplicationDbContext _db;
    private readonly IJwtService          _jwt;
    private readonly ILogger<AuthService> _logger;
    private readonly IConfiguration       _config;
    private readonly IAuditService        _audit;
    private readonly IEmailService        _email;
    private readonly FileStorageService   _fileStorage;
    private readonly IHostEnvironment     _env;

    public AuthService(ApplicationDbContext db, IJwtService jwt,
                       ILogger<AuthService> logger, IConfiguration config,
                       IAuditService audit, IEmailService email,
                       FileStorageService fileStorage, IHostEnvironment env)
    {
        _fileStorage = fileStorage;
        _db = db; _jwt = jwt; _logger = logger;
        _config = config; _audit = audit; _email = email;
        _env = env;
    }

    private static string HashToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
    private static string GenerateSecureToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public async Task<(LoginResponseDto? result, string? error)> LoginAsync(LoginDto dto, string? ipAddress = null)
    {
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Email == dto.Email && u.IsActive && !u.IsDeleted);
        if (user == null)
        {
            await _audit.LogAsync("LOGIN_FAIL", "User", null, null, null, null,
                "Invalid credentials for an unknown account.", false);
            return (null, "Invalid credentials. Please check email, password, and portal.");
        }

        if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
        {
            var mins = Math.Ceiling((user.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
            await _audit.LogAsync("LOGIN_LOCKED", "User", user.Id.ToString(), user.Id, user.Id.ToString(), null,
                $"Account locked for {mins} more minutes.", false);
            return (null, $"Account temporarily locked. Try again in {mins} minute(s).");
        }

        if ((dto.Portal == AppRoles.Employee && user.Role != AppRoles.Employee) ||
            (dto.Portal == AppRoles.Admin    && user.Role != AppRoles.Admin)    ||
            (dto.Portal == AppRoles.SuperAdmin && user.Role != AppRoles.SuperAdmin))
        {
            await _audit.LogAsync("LOGIN_FAIL", "User", user.Id.ToString(), user.Id, user.Id.ToString(), null,
                $"Portal mismatch: user role did not match the requested portal.", false);
            return (null, "Invalid credentials. Please check email, password, and portal.");
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts += 1;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginAttempts = 0;
                _logger.LogWarning("Account locked after repeated failures for user {UserId}", user.Id);
                await _audit.LogAsync("ACCOUNT_LOCKED", "User", user.Id.ToString(), user.Id, user.Id.ToString(), null,
                    $"Locked after {MaxFailedAttempts} failed attempts.", false);
            }
            else
            {
                await _audit.LogAsync("LOGIN_FAIL", "User", user.Id.ToString(), user.Id, user.Id.ToString(), null,
                    $"Bad password (attempt {user.FailedLoginAttempts}).", false);
            }
            await _db.SaveChangesAsync();
            return (null, "Invalid credentials. Please check email, password, and portal.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;

        // FIX [6]: MFA bypass — if user has TOTP MFA enabled, do NOT issue a full JWT.
        // Return a short-lived temp token instead; the caller must complete
        // POST /api/auth/mfa/verify before receiving a real session.
        if (user.IsMfaEnabled)
        {
            await _db.SaveChangesAsync();
            var tempToken = _jwt.GenerateTempToken(user.Id);
            await _audit.LogAsync("LOGIN_MFA_REQUIRED", "User", user.Id.ToString(), user.Id, user.Id.ToString(), null,
                "MFA step required; temporary token issued.", true);
            return (new LoginResponseDto
            {
                MfaRequired = true,
                TempToken   = tempToken,
                Role        = user.Role,
                FullName    = user.FullName ?? user.Email,
                UserId      = user.Id,
                ExpiresAt   = DateTime.UtcNow.AddMinutes(10),
            }, null);
        }

        string? employeeId = null;
        if (user.Role == AppRoles.Employee)
        {
            // LOW-04 fix: AsNoTracking — this is a read-only lookup; employeeId is only
            // used to embed a claim in the JWT and never written back to the DB.
            var emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == user.Id);
            employeeId = emp?.EmployeeCode;
        }

        var token = _jwt.GenerateToken(user, employeeId);
        var refreshRaw = GenerateSecureToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId      = user.Id,
            TokenHash   = HashToken(refreshRaw),
            ExpiresAt   = DateTime.UtcNow.Add(RefreshTokenLifetime),
            MfaVerified = false  // password-only login; no TOTP step completed
        });
        await _db.SaveChangesAsync();

        await _audit.LogAsync("LOGIN_SUCCESS", "User", user.Id.ToString(), user.Id, user.Id.ToString(), null,
            $"Portal:{dto.Portal}", true);

        // FIX (MED-EXPIRY): ExpiresAt previously used Jwt:ExpiresInHours (12h default),
        // but the actual JWT is issued with Jwt:ExpiresInMinutes (30 min default).
        // Clients were told the token lasted 12 hours when it expired after 30 minutes.
        var expiresInMinutes = _config.GetValue<double>("Jwt:ExpiresInMinutes", 30);
        return (new LoginResponseDto
        {
            Token               = token,
            RefreshToken        = refreshRaw,
            Role                = user.Role,
            FullName            = user.FullName ?? user.Email,
            UserId              = user.Id,
            CompanyId           = user.CompanyId,
            EmployeeId          = employeeId,
            MustChangePassword  = user.MustChangePassword,
            ExpiresAt           = DateTime.UtcNow.AddMinutes(expiresInMinutes)
        }, null);
    }

    public async Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        var hash = HashToken(refreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (existing == null || !existing.IsActive) return null;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == existing.UserId);
        if (user == null || !user.IsActive || user.IsDeleted) return null;

        // FIX [1]: MFA bypass via refresh token.
        // A token with MfaVerified=false was issued from a password-only login before TOTP
        // was completed (or before MFA was enabled on the account). If the account now has
        // MFA enabled, this token must not produce a full JWT — the user must go through the
        // full login + TOTP flow again to obtain a MfaVerified=true token via IssueRefreshTokenAsync.
        if (user.IsMfaEnabled && !existing.MfaVerified)
        {
            existing.RevokedAt = DateTime.UtcNow; // invalidate the pre-MFA token
            await _db.SaveChangesAsync();
            return null; // force full re-authentication including TOTP
        }

        var newRaw = GenerateSecureToken();
        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedByTokenHash = HashToken(newRaw);
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId      = user.Id,
            TokenHash   = HashToken(newRaw),
            ExpiresAt   = DateTime.UtcNow.Add(RefreshTokenLifetime),
            MfaVerified = existing.MfaVerified  // carry forward the TOTP-verified status
        });

        string? employeeId = null;
        if (user.Role == AppRoles.Employee)
        {
            // LOW-04 fix: AsNoTracking — read-only lookup for JWT claim embedding only;
            // the employee entity is never mutated in this code path.
            var emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == user.Id);
            employeeId = emp?.EmployeeCode;
        }

        await _db.SaveChangesAsync();
        // FIX (MED-EXPIRY): Align with actual JWT lifetime (ExpiresInMinutes, not ExpiresInHours).
        var expiresInMinutes = _config.GetValue<double>("Jwt:ExpiresInMinutes", 30);

        return new LoginResponseDto
        {
            Token              = _jwt.GenerateToken(user, employeeId),
            RefreshToken       = newRaw,
            Role               = user.Role,
            FullName           = user.FullName ?? user.Email,
            UserId             = user.Id,
            CompanyId          = user.CompanyId,
            EmployeeId         = employeeId,
            MustChangePassword = user.MustChangePassword,
            ExpiresAt          = DateTime.UtcNow.AddMinutes(expiresInMinutes)
        };
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return false;
        var hash = HashToken(refreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (existing == null) return false;
        existing.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ForgotPasswordAsync(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        // Always respond the same way — prevents account enumeration.
        if (user == null)
        {
            _logger.LogInformation("Password reset requested for an unknown account.");
            return true;
        }

        var rawToken = GenerateSecureToken();
        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId    = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.Add(ResetTokenLifetime)
        });
        await _db.SaveChangesAsync();

        // Build the reset link and email it.
        // FIX: previously pointed at "{appBase}/reset-password.html?token=...", a static
        // page that was removed from wwwroot (see Program.cs — legacy *.html pages were
        // archived under /legacy-ui) and never replaced. Every password-reset email sent
        // a link to a 404. The React SPA now owns this route (ResetPasswordPage.tsx via
        // App.tsx's /reset-password route), served through the same SPA fallback as every
        // other client-side route.
        var appBase   = _config["Email:AppBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        var resetLink = $"{appBase}/reset-password?token={rawToken}";

        await _email.SendPasswordResetAsync(email, user.FullName ?? email, resetLink);

        // SECURITY FIX: Only log the reset link (which contains a raw valid token) in
        // Development. In Production the link is delivered exclusively via email; logging
        // it would expose a live account-takeover token to anyone with log access (Seq,
        // CloudWatch, ELK, etc.). Always gate token-bearing values on IsDevelopment().
        if (_env.IsDevelopment())
        {
            _logger.LogDebug(
                "[DEV ONLY] Password reset link generated (valid {Min} min).",
                ResetTokenLifetime.TotalMinutes);
        }
        else
        {
            _logger.LogInformation(
                "Password reset email dispatched (token valid {Min} min).",
                ResetTokenLifetime.TotalMinutes);
        }

        return true;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordDto dto)
    {
        if (dto.NewPassword != dto.ConfirmPassword) return false;
        // Item 8: server-side complexity gate. The SPA and the FluentValidation
        // validator both check this, but neither is the security boundary — this
        // service is reachable from any caller that skips the MVC pipeline.
        if (!PasswordPolicy.IsValid(dto.NewPassword, out var policyError))
        {
            _logger.LogWarning("Password reset rejected: new password failed the complexity policy.");
            await _audit.LogAsync("PASSWORD_RESET_REJECTED", "User", null, null, null, null,
                policyError, false);
            return false;
        }
        if (string.IsNullOrWhiteSpace(dto.Token)) return false;

        var hash = HashToken(dto.Token);
        var resetToken = await _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (resetToken == null || !resetToken.IsValid) return false;

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == resetToken.UserId);
        if (user == null || user.IsDeleted) return false;

        // Item 8 (final gate): assert the policy immediately before hashing so no
        // future refactor can reach BCrypt with an unvalidated password. The graceful
        // check above preserves the false-return API contract; this throws only if
        // that check is ever removed or bypassed.
        PasswordPolicy.EnsureValid(dto.NewPassword, nameof(dto.NewPassword));
        user.PasswordHash        = BcryptPasswordHasher.Hash(dto.NewPassword, _config);
        user.MustChangePassword  = false;
        user.FailedLoginAttempts = 0;
        user.LockoutUntil        = null;
        // FIX #12: Mark token used AND remove it from the DB so reuse is impossible
        // regardless of how IsValid is computed in the future.
        resetToken.UsedAt = DateTime.UtcNow;
        _db.PasswordResetTokens.Remove(resetToken);

        // Revoke all active sessions on password reset.
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync();
        foreach (var rt in activeTokens) rt.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("PASSWORD_RESET", "User", user.Id.ToString(), user.Id, user.Id.ToString());
        return true;
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user == null || user.IsDeleted) return false;
        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash)) return false;
        // Item 8: server-side complexity gate (see PasswordPolicy).
        if (!PasswordPolicy.IsValid(dto.NewPassword, out var policyError))
        {
            _logger.LogWarning("Password change rejected for user {UserId}: complexity policy not met.", userId);
            await _audit.LogAsync("PASSWORD_CHANGE_REJECTED", "User", userId.ToString(), userId,
                userId.ToString(), null, policyError, false);
            return false;
        }
        // Reject reuse of the current password even when the DTO validator is bypassed.
        if (string.Equals(dto.CurrentPassword, dto.NewPassword, StringComparison.Ordinal)) return false;
        // Item 8 (final gate): see ResetPasswordAsync — EnsureValid guarantees that
        // every hashing call site in this service is policy-checked.
        PasswordPolicy.EnsureValid(dto.NewPassword, nameof(dto.NewPassword));
        user.PasswordHash       = BcryptPasswordHasher.Hash(dto.NewPassword, _config);
        user.MustChangePassword = false;
        // FIX #2: Revoke all active refresh tokens so an attacker who obtained a
        // stolen session cannot keep using it after the legitimate user changes password.
        // Mirrors the behaviour of ResetPasswordAsync.
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync();
        foreach (var rt in activeTokens) rt.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("PASSWORD_CHANGE", "User", userId.ToString(), userId, userId.ToString(),
            details: $"Revoked {activeTokens.Count} active refresh token(s).");
        return true;
    }

    public async Task<UserProfileDto?> GetProfileAsync(int userId)
    {
        // LOW-04 fix: AsNoTracking — GetProfileAsync is a pure read (user entity is never
        // mutated here; fields are only mapped to a DTO). Avoids EF change-tracker overhead
        // on the hot path called by the SPA on every page load.
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null || u.IsDeleted) return null;
        return new UserProfileDto
        {
            Id                 = u.Id,
            Email              = u.Email,
            FullName           = u.FullName,
            Role               = u.Role,
            AdminRole          = u.AdminRole,
            CompanyId          = u.CompanyId,
            ProfilePicturePath = u.ProfilePicturePath,
            IsActive           = u.IsActive,
            CreatedAt          = u.CreatedAt
        };
    }

    public async Task<bool> UpdateProfileAsync(int userId, UpdateProfileDto dto)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null || u.IsDeleted) return false;
        if (!string.IsNullOrWhiteSpace(dto.FullName)) u.FullName = dto.FullName.Trim();
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateProfilePictureAsync(int userId, IFormFile file)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null || u.IsDeleted) return false;

        // Persist the file via the shared FileStorageService
        // Item 9: profile pictures are images only; UploadValidator enforces the five gates
        // (size, extension, MIME/extension agreement, magic bytes, GUID-safe name).
        var path = await _fileStorage.SaveFileAsync(file, "profile", UploadProfile.Image);
        u.ProfilePicturePath = path;
        await _db.SaveChangesAsync();
        return true;
    }

    // FIX [2] — companion to MFA controller fix:
    // Issues a refresh token that is explicitly marked MfaVerified=true.
    // Must only be called from MfaController.Verify after a successful TOTP check.
    // This is what allows RefreshTokenAsync to serve MFA-enabled accounts without
    // forcing a full re-login on every access-token expiry.
    public async Task<string> IssueRefreshTokenAsync(int userId)
    {
        var raw = GenerateSecureToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId      = userId,
            TokenHash   = HashToken(raw),
            ExpiresAt   = DateTime.UtcNow.Add(RefreshTokenLifetime),
            MfaVerified = true  // token created after successful TOTP — carries full session trust
        });
        await _db.SaveChangesAsync();
        return raw;
    }
}
