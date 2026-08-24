using HRMS.Application.DTOs.Auth;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OtpNet;

namespace HRMS.Infrastructure.Services;

public class MfaService : IMfaService
{
    private readonly ApplicationDbContext _db;
    // FIX CRITICAL: previously typed as the unqualified `AesEncryptionService`, which
    // in this file's namespace (HRMS.Infrastructure.Services) resolved to a dead,
    // never-registered duplicate class instead of the real, DI-registered
    // HRMS.Infrastructure.Security.AesEncryptionService. Because that dead type was
    // never added to the container, `_aes` was ALWAYS null at runtime, and every TOTP
    // MFA secret was silently stored in PLAINTEXT via the `secretBase32` fallback below.
    // Using the fully-qualified type (and the actual interface it's registered under)
    // guarantees DI resolves the real AES-256-GCM implementation.
    private readonly HRMS.Application.Interfaces.IEncryptionService? _aes;
    private readonly IConfiguration _config;
    private readonly ILogger<MfaService> _logger;

    public MfaService(ApplicationDbContext db, IConfiguration config,
                      ILogger<MfaService> logger,
                      HRMS.Application.Interfaces.IEncryptionService? aes = null)
    {
        _db = db; _config = config; _logger = logger; _aes = aes;
    }

    public async Task<MfaSetupResponseDto> SetupMfaAsync(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("User not found.");

        // Generate a fresh TOTP secret
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secretBase32 = Base32Encoding.ToString(secretBytes);

        // Encrypt before storing
        user.TotpSecret  = _aes != null ? _aes.Encrypt(secretBase32) : secretBase32;
        user.IsMfaEnabled = false; // enabled only after confirm
        await _db.SaveChangesAsync();

        var issuer   = _config["Mfa:Issuer"] ?? "HRMS";
        var qrUri    = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(user.Email)}" +
                       $"?secret={secretBase32}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";

        return new MfaSetupResponseDto
        {
            QrCodeUri     = qrUri,
            ManualEntryKey = secretBase32
        };
    }

    public async Task<bool> ConfirmMfaSetupAsync(int userId, string code)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || string.IsNullOrEmpty(user.TotpSecret)) return false;

        if (!VerifyCode(user.TotpSecret, code)) return false;

        user.IsMfaEnabled = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> VerifyMfaAsync(int userId, string code)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.IsMfaEnabled || string.IsNullOrEmpty(user.TotpSecret))
            return false;
        return VerifyCode(user.TotpSecret, code);
    }

    public async Task<bool> DisableMfaAsync(int userId, string currentPassword)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return false;
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)) return false;
        user.IsMfaEnabled = false;
        user.TotpSecret   = null;
        await _db.SaveChangesAsync();
        return true;
    }

    public bool IsMfaEnabled(int userId)
    {
        // Sync check for LoginAsync flow
        var user = _db.Users.FirstOrDefault(u => u.Id == userId);
        return user?.IsMfaEnabled == true;
    }

    private bool VerifyCode(string encryptedSecret, string code)
    {
        try
        {
            var secretBase32 = _aes != null ? _aes.Decrypt(encryptedSecret) ?? encryptedSecret : encryptedSecret;
            var secretBytes  = Base32Encoding.ToBytes(secretBase32);
            var totp         = new Totp(secretBytes);
            return totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TOTP verification error for secret (user-level)");
            return false;
        }
    }
}
