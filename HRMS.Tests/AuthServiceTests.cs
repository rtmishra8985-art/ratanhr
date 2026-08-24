using FluentAssertions;
using HRMS.Application.DTOs.Auth;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Tests for AuthService covering login, refresh tokens, lockout, MFA, and password change flows.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options, config: null, tenant: null);
    }

    public void Dispose() => _db.Dispose();

    // ─── Login Tests ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokenPair()
    {
        var (svc, user) = BuildService();

        var (result, error) = await svc.LoginAsync(new LoginDto
        {
            Email = user.Email, Password = "Test@1234", Portal = "admin"
        });

        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        var (svc, user) = BuildService();

        var (result, _) = await svc.LoginAsync(new LoginDto
        {
            Email = user.Email, Password = "WrongPassword!", Portal = "admin"
        });

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_NonExistentEmail_ReturnsNull()
    {
        var (svc, _) = BuildService();

        var (result, _) = await svc.LoginAsync(new LoginDto
        {
            Email = "nobody@nowhere.com", Password = "Test@1234", Portal = "admin"
        });

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_ReturnsNull()
    {
        var (svc, user) = BuildService(lockedOut: true);

        var (result, _) = await svc.LoginAsync(new LoginDto
        {
            Email = user.Email, Password = "Test@1234", Portal = "admin"
        });

        result.Should().BeNull("a locked account must not produce a token");
    }

    [Fact]
    public async Task LoginAsync_WrongPortal_ReturnsNull()
    {
        var (svc, user) = BuildService(role: "employee");

        var (result, _) = await svc.LoginAsync(new LoginDto
        {
            Email = user.Email, Password = "Test@1234", Portal = "superadmin"
        });

        result.Should().BeNull("portal mismatch must deny access");
    }

    // ─── Refresh Token Tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewPair()
    {
        var (svc, user) = BuildService();
        var (loginResult, _) = await svc.LoginAsync(new LoginDto
        {
            Email = user.Email, Password = "Test@1234", Portal = "admin"
        });
        loginResult.Should().NotBeNull();

        var refreshed = await svc.RefreshTokenAsync(loginResult!.RefreshToken);

        refreshed.Should().NotBeNull();
        refreshed!.Token.Should().NotBeNullOrWhiteSpace();
        refreshed.RefreshToken.Should().NotBe(loginResult.RefreshToken,
            "refresh tokens must be rotated on each use");
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidToken_ReturnsNull()
    {
        var (svc, _) = BuildService();

        var result = await svc.RefreshTokenAsync("totally-invalid-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_ExpiredToken_ReturnsNull()
    {
        var (svc, user) = BuildService();
        var (loginResult, _) = await svc.LoginAsync(new LoginDto
        {
            Email = user.Email, Password = "Test@1234", Portal = "admin"
        });
        loginResult.Should().NotBeNull();

        // Expire the stored token record directly
        var tokenRecord = await _db.RefreshTokens.FirstAsync(t => t.UserId == user.Id);
        tokenRecord.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await _db.SaveChangesAsync();

        var result = await svc.RefreshTokenAsync(loginResult!.RefreshToken);

        result.Should().BeNull("expired refresh tokens must be rejected");
    }

    [Fact]
    public async Task RefreshTokenAsync_RevokedToken_ReturnsNull()
    {
        var (svc, user) = BuildService();
        var (loginResult, _) = await svc.LoginAsync(new LoginDto
        {
            Email = user.Email, Password = "Test@1234", Portal = "admin"
        });
        loginResult.Should().NotBeNull();

        // Revoke the stored token record directly
        var tokenRecord = await _db.RefreshTokens.FirstAsync(t => t.UserId == user.Id);
        tokenRecord.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var result = await svc.RefreshTokenAsync(loginResult!.RefreshToken);

        result.Should().BeNull("revoked refresh tokens must be rejected");
    }

    [Fact]
    public async Task RefreshTokenAsync_UsedToken_IsRotated_OldTokenRejected()
    {
        var (svc, user) = BuildService();
        var (firstLogin, _) = await svc.LoginAsync(new LoginDto
        {
            Email = user.Email, Password = "Test@1234", Portal = "admin"
        });
        var firstRefreshToken = firstLogin!.RefreshToken;

        // Use the refresh token once
        var second = await svc.RefreshTokenAsync(firstRefreshToken);
        second.Should().NotBeNull();

        // Replay the same token — must fail after rotation
        var replay = await svc.RefreshTokenAsync(firstRefreshToken);

        replay.Should().BeNull("refresh token reuse must be rejected after rotation");
    }

    // ─── Password Tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePasswordAsync_ValidCurrentPassword_Succeeds()
    {
        var (svc, user) = BuildService();

        var success = await svc.ChangePasswordAsync(user.Id, new ChangePasswordDto
        {
            CurrentPassword = "Test@1234",
            NewPassword     = "NewSecure@5678"
        });

        success.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_Fails()
    {
        var (svc, user) = BuildService();

        var success = await svc.ChangePasswordAsync(user.Id, new ChangePasswordDto
        {
            CurrentPassword = "WrongCurrent!",
            NewPassword     = "NewSecure@5678"
        });

        success.Should().BeFalse();
    }

    // ─── MFA Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_MfaEnabledUser_RequiresMfaStep()
    {
        var (svc, user) = BuildService(mfaEnabled: true);

        var (result, _) = await svc.LoginAsync(new LoginDto
        {
            Email = user.Email, Password = "Test@1234", Portal = "admin"
        });

        result.Should().NotBeNull();
        result!.MfaRequired.Should().BeTrue("MFA-enabled users must complete second factor");
        result.Token.Should().BeNullOrWhiteSpace("no full access token before MFA is verified");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private (IAuthService svc, User user) BuildService(
        bool   lockedOut  = false,
        string role       = "admin",
        bool   mfaEnabled = false)
    {
        var user = new User
        {
            Email        = $"user_{Guid.NewGuid():N}@company.com",
            CompanyId    = 1,
            Role         = role,
            IsActive     = true,
            LockoutUntil = lockedOut ? DateTime.UtcNow.AddHours(1) : (DateTime?)null,
            IsMfaEnabled = mfaEnabled,
            TotpSecret   = mfaEnabled ? "BASE32SECRET" : null,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@1234")
        };
        _db.Users.Add(user);
        _db.SaveChanges();

        var jwtSvc = new Mock<IJwtService>();
        jwtSvc.Setup(j => j.GenerateToken(It.IsAny<User>(), It.IsAny<string?>()))
              .Returns("eyJ.access.token");
        jwtSvc.Setup(j => j.GenerateTempToken(It.IsAny<int>()))
              .Returns(Guid.NewGuid().ToString());

        var svc = new AuthService(
            _db,
            jwtSvc.Object,
            new Mock<ILogger<AuthService>>().Object,
            TestConfigurationFixture.Build(),
            new Mock<IAuditService>().Object,
            new Mock<IEmailService>().Object,
            null!,   // FileStorageService — only used for profile-picture uploads, not tested here
            new Mock<IHostEnvironment>().Object);

        return (svc, user);
    }
}
