using HRMS.Application.DTOs.Auth;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.FileStorage;
using HRMS.Infrastructure.Security;
using HRMS.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HRMS.Tests.Authentication;

/// <summary>
/// FIX: Previously both tests in this class failed to even construct the test fixture:
/// `new Mock<FileStorageService>()` throws `ArgumentException: Can not instantiate proxy
/// of class ... Could not find a parameterless constructor` because FileStorageService is a
/// concrete class whose only constructor requires (string uploadsRoot, IOptions<FileUploadOptions>?),
/// and Moq's dynamic proxy generation for class mocks needs either a parameterless constructor
/// or the real constructor arguments passed to `new Mock<T>(args)`.
///
/// Neither MFA-bypass test below ever calls a file-storage method — AuthService only uses
/// `_fileStorage` inside `UpdateProfilePictureAsync`, which these tests never invoke — so a
/// real, harmless `FileStorageService` instance (pointed at a throwaway temp directory) is used
/// instead of a mock. This removes the need to mock a concrete class entirely while keeping
/// every other dependency mocked as before.
/// </summary>
public class MfaBypassTests : IDisposable
{
    private ApplicationDbContext _context = null!;
    private AuthService _authService = null!;
    private Mock<IJwtService> _jwtServiceMock = null!;
    private Mock<ILogger<AuthService>> _loggerMock = null!;
    // FIX: Mock<IConfiguration>().Object only satisfies calls this test explicitly sets up
    // (like c["Jwt:ExpiresInMinutes"]). BcryptPasswordHasher.Hash() calls the
    // ConfigurationBinder.GetValue<int>() extension method, which internally calls
    // configuration.GetSection(key).Value — a member Moq's bare mock never stubs, so it
    // threw NullReferenceException deep inside the framework's binder. A real IConfiguration
    // built from an in-memory collection behaves correctly for both the explicit indexer
    // lookups this test needs AND GetSection/GetValue calls made by production code paths
    // like BcryptPasswordHasher.
    private IConfiguration _config = null!;
    private Mock<IAuditService> _auditMock = null!;
    private Mock<IEmailService> _emailMock = null!;
    private FileStorageService _fileStorage = null!;
    private Mock<IHostEnvironment> _envMock = null!;

    public MfaBypassTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"test_mfa_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options, null);
        _jwtServiceMock = new Mock<IJwtService>();
        _loggerMock = new Mock<ILogger<AuthService>>();
        _auditMock = new Mock<IAuditService>();
        _emailMock = new Mock<IEmailService>();
        // FIX: real instance instead of Mock<FileStorageService>() — see class-level doc comment.
        _fileStorage = new FileStorageService(
            Path.Combine(Path.GetTempPath(), "hrms-mfa-tests-" + Guid.NewGuid()));
        _envMock = new Mock<IHostEnvironment>();

        // Real configuration — see field-level doc comment on _config.
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:ExpiresInMinutes"] = "30",
                ["Email:AppBaseUrl"] = "http://localhost:5000",
                [BcryptPasswordHasher.ConfigurationKey] = BcryptPasswordHasher.DefaultWorkFactor.ToString()
            })
            .Build();

        _authService = new AuthService(
            _context,
            _jwtServiceMock.Object,
            _loggerMock.Object,
            _config,
            _auditMock.Object,
            _emailMock.Object,
            _fileStorage,
            _envMock.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    [Fact]
    public async Task RefreshToken_AfterMfaEnabled_ShouldRejectPreMfaToken()
    {
        // FIX CRIT-2: Test that a refresh token created before MFA was enabled
        // is rejected after MFA is enabled on the account.
        // This prevents an attacker with a stolen pre-MFA refresh token from
        // obtaining new JWTs after the user enables MFA.

        // Step 1: Create a user without MFA
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            PasswordHash = BcryptPasswordHasher.Hash("Test@123456", _config),
            Role = "Employee",
            IsActive = true,
            IsMfaEnabled = false,  // MFA disabled initially
            CompanyId = 1
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Step 2: User logs in (no MFA required, gets a refresh token with MfaVerified=false)
        var refreshTokenRaw = "test_refresh_token_12345";
        var refreshTokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(refreshTokenRaw)));

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            MfaVerified = false  // This token was issued before TOTP
        };
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        // Step 3: Admin enables MFA on the user
        user.IsMfaEnabled = true;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Step 4: Try to refresh with the old pre-MFA token
        var result = await _authService.RefreshTokenAsync(refreshTokenRaw);

        // Step 5: Verify the token is rejected (returns null)
        Assert.Null(result);

        // Step 6: Verify the token was revoked in the database
        var revokedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == refreshTokenHash);
        Assert.NotNull(revokedToken);
        Assert.NotNull(revokedToken.RevokedAt);
    }

    [Fact]
    public async Task RefreshToken_WithMfaVerifiedToken_ShouldSucceed()
    {
        // Verify that a token with MfaVerified=true CAN be refreshed even when MFA is enabled

        var user = new User
        {
            Id = 2,
            Email = "mfa@test.com",
            PasswordHash = BcryptPasswordHasher.Hash("Test@123456", _config),
            Role = "Employee",
            IsActive = true,
            IsMfaEnabled = true,  // MFA already enabled
            CompanyId = 1
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var refreshTokenRaw = "mfa_verified_token_67890";
        var refreshTokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(refreshTokenRaw)));

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            MfaVerified = true  // User completed TOTP verification
        };
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        // Mock JWT generation
        var newJwt = "new_jwt_token";
        _jwtServiceMock.Setup(x => x.GenerateToken(It.IsAny<User>(), It.IsAny<string?>()))
            .Returns(newJwt);

        // Attempt refresh
        var result = await _authService.RefreshTokenAsync(refreshTokenRaw);

        // Should succeed
        Assert.NotNull(result);
        Assert.Equal(newJwt, result.Token);
    }
}
