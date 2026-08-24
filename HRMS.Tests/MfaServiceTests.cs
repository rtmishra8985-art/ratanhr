using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OtpNet;
using Xunit;

namespace HRMS.Tests;

public class MfaServiceTests
{
    private static IConfiguration BuildConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mfa:Issuer"] = "HRMSTests"
        }).Build();

    private static MfaService Build(HRMS.Infrastructure.Data.ApplicationDbContext db)
        => new MfaService(db, BuildConfig(), NullLogger<MfaService>.Instance);

    private static User SeedUser(HRMS.Infrastructure.Data.ApplicationDbContext db)
    {
        var user = new User
        {
            Email = "mfa@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
            Role = "employee",
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task SetupMfaAsync_Returns_QrCodeUri_And_ManualKey()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var user = SeedUser(db);
        var svc = Build(db);

        var result = await svc.SetupMfaAsync(user.Id);

        Assert.NotEmpty(result.QrCodeUri);
        Assert.NotEmpty(result.ManualEntryKey);
        Assert.StartsWith("otpauth://totp/", result.QrCodeUri);
        // MFA not yet enabled until ConfirmMfaSetupAsync
        Assert.False(db.Users.Find(user.Id)!.IsMfaEnabled);
    }

    [Fact]
    public async Task ConfirmMfaSetupAsync_WithValidCode_EnablesMfa()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var user = SeedUser(db);
        var svc = Build(db);

        await svc.SetupMfaAsync(user.Id);

        // Read the stored secret and generate a valid TOTP code
        var storedUser = db.Users.Find(user.Id)!;
        var secretBase32 = storedUser.TotpSecret!; // not encrypted in tests (no AesEncryptionService)
        var totp = new Totp(Base32Encoding.ToBytes(secretBase32));
        var code = totp.ComputeTotp();

        var ok = await svc.ConfirmMfaSetupAsync(user.Id, code);

        Assert.True(ok);
        Assert.True(db.Users.Find(user.Id)!.IsMfaEnabled);
    }

    [Fact]
    public async Task ConfirmMfaSetupAsync_WithInvalidCode_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var user = SeedUser(db);
        var svc = Build(db);
        await svc.SetupMfaAsync(user.Id);

        var ok = await svc.ConfirmMfaSetupAsync(user.Id, "000000");

        Assert.False(ok);
        Assert.False(db.Users.Find(user.Id)!.IsMfaEnabled);
    }

    [Fact]
    public async Task DisableMfaAsync_WithCorrectPassword_DisablesMfa()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var user = SeedUser(db);
        var dbUser = db.Users.Find(user.Id)!;
        dbUser.IsMfaEnabled = true;
        dbUser.TotpSecret   = "JBSWY3DPEHPK3PXP";
        db.SaveChanges();

        var svc = Build(db);
        var ok = await svc.DisableMfaAsync(user.Id, "Pass@123");

        Assert.True(ok);
        Assert.False(db.Users.Find(user.Id)!.IsMfaEnabled);
        Assert.Null(db.Users.Find(user.Id)!.TotpSecret);
    }

    [Fact]
    public async Task DisableMfaAsync_WithWrongPassword_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var user = SeedUser(db);
        var svc = Build(db);

        var ok = await svc.DisableMfaAsync(user.Id, "wrongpassword");

        Assert.False(ok);
    }
}
