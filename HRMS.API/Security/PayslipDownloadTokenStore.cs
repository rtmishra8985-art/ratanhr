using Microsoft.Extensions.Caching.Memory;

namespace HRMS.API.Security;

/// <summary>
/// FIX P3-1 (payslip download token binding).
///
/// Previously the PDF download token was an opaque GUID that was only used to build
/// the output filename. Any authenticated caller who obtained a token could replay it
/// against a *different* payslipId, and the token never expired or expired only with
/// the file on disk. That made the token a bearer credential with no subject binding.
///
/// This store binds every issued token to the triple (payslipId, userId, companyId),
/// gives it a short TTL, and makes it single-use: the first successful download
/// consumes the entry so a leaked URL (browser history, proxy log, shared link) cannot
/// be replayed at all — let alone against another payslip.
/// </summary>
public interface IPayslipDownloadTokenStore
{
    /// <summary>Issues and stores a token bound to the payslip and the caller.</summary>
    string Issue(int payslipId, int userId, int? companyId);

    /// <summary>
    /// Returns true when the token exists, has not expired, and was issued for exactly
    /// this payslip, user and company. Does NOT consume the token (used by the status poll).
    /// </summary>
    bool Validate(string token, int payslipId, int userId, int? companyId);

    /// <summary>
    /// Same checks as <see cref="Validate"/>, but atomically removes the entry on success
    /// so the token cannot be used a second time.
    /// </summary>
    bool ValidateAndConsume(string token, int payslipId, int userId, int? companyId);
}

public sealed class PayslipDownloadTokenStore : IPayslipDownloadTokenStore
{
    /// <summary>Short TTL — the token only has to survive one PDF generation + download.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private const string KeyPrefix = "payslip:dl:";

    private readonly IMemoryCache _cache;
    private readonly object       _gate = new();

    public PayslipDownloadTokenStore(IMemoryCache cache) => _cache = cache;

    private sealed record Binding(int PayslipId, int UserId, int? CompanyId);

    private static string Key(string token) => KeyPrefix + token;

    public string Issue(int payslipId, int userId, int? companyId)
    {
        // 256 bits of entropy — GUIDs are only 122 and are not guaranteed unpredictable.
        var token = Convert.ToHexString(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        _cache.Set(Key(token), new Binding(payslipId, userId, companyId),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });

        return token;
    }

    public bool Validate(string token, int payslipId, int userId, int? companyId)
        => Lookup(token, payslipId, userId, companyId, consume: false);

    public bool ValidateAndConsume(string token, int payslipId, int userId, int? companyId)
        => Lookup(token, payslipId, userId, companyId, consume: true);

    private bool Lookup(string token, int payslipId, int userId, int? companyId, bool consume)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        lock (_gate) // single-use must be atomic across concurrent replays
        {
            if (!_cache.TryGetValue(Key(token), out Binding? binding) || binding is null)
                return false;

            // All three components must match. A mismatch is a replay attempt, so the
            // entry is destroyed rather than left available for another guess.
            var ok = binding.PayslipId == payslipId
                  && binding.UserId    == userId
                  && binding.CompanyId == companyId;

            if (!ok || consume)
                _cache.Remove(Key(token));

            return ok;
        }
    }
}
