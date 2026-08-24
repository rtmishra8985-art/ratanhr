// FIX H-01: Migrated from HS256 (symmetric) to RS256 (asymmetric) JWT signing.
//   - Private key (PEM) is used for signing tokens; only the API holds it.
//   - Public key (PEM) is distributed for validation; can be shared safely.
//   - Generate key pair: scripts/generate-rsa-keys.sh
//   - Set env vars: Jwt__PrivateKeyPem and Jwt__PublicKeyPem
//
// FIX H-02: Access token expiry reduced from 8–12 hours to 30 minutes.
//   - Limits the exposure window if a token is compromised.
//   - Configured via Jwt:ExpiresInMinutes (default: 30).
//   - Refresh tokens remain long-lived (7 days) handled by AuthService.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace HRMS.Infrastructure.JWT;

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;
    private readonly ILogger<JwtService> _logger;

    // FIX (MED-RSA): RSA key objects are now created once and cached as lazy singletons.
    // Previously GetSigningKey/GetValidationKey called RSA.Create() + ImportFromPem on every
    // token generation and every token validation — O(N) RSA allocations under load with no
    // disposal, causing a memory leak. Lazy<T> ensures construction happens exactly once per
    // JwtService instance (which should be registered as a singleton in DI).
    private readonly Lazy<RsaSecurityKey> _signingKey;
    private readonly Lazy<RsaSecurityKey> _validationKey;

    public JwtService(IConfiguration config, ILogger<JwtService> logger)
    {
        _config        = config;
        _logger        = logger;
        _signingKey    = new Lazy<RsaSecurityKey>(LoadSigningKey);
        _validationKey = new Lazy<RsaSecurityKey>(LoadValidationKey);
    }

    // ── Key helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// H-01: Loads the RSA private key from PEM config for signing.
    /// Called once at first use; result is cached for the lifetime of this service.
    /// The private key MUST stay server-side; never expose it.
    /// </summary>
    private RsaSecurityKey LoadSigningKey()
    {
        var pem = _config["Jwt:PrivateKeyPem"];
        if (string.IsNullOrWhiteSpace(pem))
            throw new InvalidOperationException(
                "Jwt:PrivateKeyPem is not configured. " +
                "Set the Jwt__PrivateKeyPem environment variable. " +
                "Run scripts/generate-rsa-keys.sh to create a fresh RSA-2048 key pair.");

        var rsa = RSA.Create();
        rsa.ImportFromPem(PemKeyParser.Normalize(pem).AsSpan());
        return new RsaSecurityKey(rsa);
    }

    /// <summary>
    /// H-01: Loads the RSA public key from PEM config for token validation.
    /// Called once at first use; result is cached for the lifetime of this service.
    /// The public key can be distributed to services that only verify tokens.
    /// </summary>
    private RsaSecurityKey LoadValidationKey()
    {
        var pem = _config["Jwt:PublicKeyPem"];
        if (string.IsNullOrWhiteSpace(pem))
            throw new InvalidOperationException(
                "Jwt:PublicKeyPem is not configured. " +
                "Set the Jwt__PublicKeyPem environment variable. " +
                "Run scripts/generate-rsa-keys.sh to create a fresh RSA-2048 key pair.");

        var rsa = RSA.Create();
        rsa.ImportFromPem(PemKeyParser.Normalize(pem).AsSpan());
        return new RsaSecurityKey(rsa);
    }

    private RsaSecurityKey GetSigningKey()   => _signingKey.Value;
    private RsaSecurityKey GetValidationKey() => _validationKey.Value;

    // ── Token generation ─────────────────────────────────────────────────────

    public string GenerateToken(User user, string? employeeId = null)
    {
        // H-01: RS256 asymmetric signing — private key stays server-side
        var creds = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role),
            new("companyId", user.CompanyId?.ToString() ?? ""),
            new("adminRole", user.AdminRole ?? ""),
            new("mustChangePassword", user.MustChangePassword.ToString().ToLower())
        };

        if (!string.IsNullOrEmpty(employeeId))
            claims.Add(new Claim("employeeId", employeeId));

        // H-02: Default 30 minutes — significantly reduced from prior 8–12 h window
        var expiresInMinutes = _config.GetValue<double>("Jwt:ExpiresInMinutes", 30);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generates a short-lived (5 min) temp token after successful password login
    /// when MFA is required. Contains only sub (userId) claim; not a full session token.
    /// </summary>
    public string GenerateTempToken(int userId)
    {
        var creds = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer:   _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims:   new[] {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("mfa_pending", "true")
            },
            expires:  DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── Token validation ─────────────────────────────────────────────────────

    /// <summary>Fixed: M3 — validates a temp MFA token and returns its principal.</summary>
    public ClaimsPrincipal? ValidateTempToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = GetValidationKey(),
                ValidAlgorithms          = new[] { SecurityAlgorithms.RsaSha256 },
                ValidateIssuer           = true,
                ValidIssuer              = _config["Jwt:Issuer"],
                ValidateAudience         = true,
                ValidAudience            = _config["Jwt:Audience"],
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero
            }, out _);
            // Only accept tokens that carry the mfa_pending claim
            if (principal.FindFirst("mfa_pending")?.Value != "true") return null;
            return principal;
        }
        catch (SecurityTokenExpiredException)
        {
            // Expected for expired MFA challenge tokens — debug level only
            _logger.LogDebug("[JwtService] ValidateTempToken: token has expired.");
            return null;
        }
        catch (Exception ex)
        {
            // Unexpected validation failure — log at Warning so genuine crypto/config
            // problems are visible in production logs without leaking token content.
            _logger.LogWarning(ex, "[JwtService] ValidateTempToken: token validation failed.");
            return null;
        }
    }

    public int? ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = GetValidationKey(),
                ValidAlgorithms          = new[] { SecurityAlgorithms.RsaSha256 },
                ValidateIssuer           = true,
                ValidIssuer              = _config["Jwt:Issuer"],
                ValidateAudience         = true,
                ValidAudience            = _config["Jwt:Audience"],
                ValidateLifetime         = true
            }, out var validatedToken);

            var jwt = (JwtSecurityToken)validatedToken;
            return int.Parse(jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogDebug("[JwtService] ValidateToken: token has expired.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[JwtService] ValidateToken: token validation failed.");
            return null;
        }
    }
}
