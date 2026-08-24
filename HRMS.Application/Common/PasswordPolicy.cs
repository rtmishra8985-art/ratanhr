using System.Text.RegularExpressions;

namespace HRMS.Application.Common;

/// <summary>
/// Item 8 (audit) — single, server-side source of truth for password complexity.
///
/// Every server path that sets or changes a password MUST call
/// <see cref="Validate"/> (or <see cref="EnsureValid"/>) before hashing:
///   • self-service registration / admin create (AdminUserController, AdminUserService,
///     SuperAdminController)
///   • AuthService.ChangePasswordAsync
///   • AuthService.ResetPasswordAsync
///   • AdminUserService.ResetPasswordAsync (admin-initiated reset)
///   • EmployeeService.CreateAsync generated temporary passwords
///   • Program.SeedAsync superadmin bootstrap (SUPERADMIN_INITIAL_PASSWORD)
///
/// SPA-side validation is a UX affordance only and is never the security gate.
///
/// The character-class rules reuse the exact regex patterns already used by
/// HRMS.Application/Validators/LoginValidator.cs (ResetPasswordDtoValidator /
/// ChangePasswordDtoValidator) so client, validator, and service layers cannot drift.
///
/// Configuration: the static entry points delegate to <see cref="Current"/>, which
/// is installed once at startup from the DI-bound <see cref="PasswordPolicyOptions"/>
/// (see ServiceExtensions.AddInfrastructure / Program.cs). Callers that already have
/// DI should prefer <see cref="IPasswordPolicyValidator"/>; the static surface exists
/// so that service/seed paths outside the request pipeline cannot bypass the rule.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>Default minimum length. Raised from 8 to 12 per the v1.0.5 audit.</summary>
    public const int MinLength = 12;

    /// <summary>Upper bound — BCrypt silently truncates beyond 72 bytes.</summary>
    public const int MaxLength = 72;

    // ── Regex patterns (identical to LoginValidator.cs) ────────────────────
    public const string UpperPattern   = "[A-Z]";
    public const string LowerPattern   = "[a-z]";
    public const string DigitPattern   = "[0-9]";
    public const string SymbolPattern  = "[^a-zA-Z0-9]";

    private static readonly RegexOptions Opts = RegexOptions.CultureInvariant | RegexOptions.Compiled;
    internal static readonly Regex Upper  = new(UpperPattern,  Opts);
    internal static readonly Regex Lower  = new(LowerPattern,  Opts);
    internal static readonly Regex Digit  = new(DigitPattern,  Opts);
    internal static readonly Regex Symbol = new(SymbolPattern, Opts);

    /// <summary>
    /// Deny-list of trivially guessable passwords and common corporate patterns.
    /// Matching is case-insensitive and also rejects any password whose alphabetic
    /// core is a listed word — this catches "Password123!", "Welcome@2026",
    /// "Ratanhr#1" and friends, which otherwise pass every character-class rule.
    /// </summary>
    internal static readonly IReadOnlyCollection<string> BuiltInCommonPasswords = new[]
    {
        "password", "passw0rd", "pass", "secret", "letmein", "welcome", "welcome1",
        "admin", "administrator", "superadmin", "root", "guest", "test", "testing",
        "qwerty", "qwertyuiop", "asdfgh", "zxcvbn", "123456", "1234567", "12345678",
        "123456789", "1234567890", "111111", "000000", "abc123", "iloveyou",
        "monkey", "dragon", "sunshine", "princess", "football", "baseball",
        "master", "shadow", "michael", "superman", "trustno1", "starwars",
        "changeme", "default", "temporary", "temppass", "newpassword", "hrms",
        "ratanhr", "ratan", "company", "employee", "payroll", "login", "user"
    };

    private static PasswordPolicyValidator _current = new(new PasswordPolicyOptions());

    /// <summary>The active, configuration-bound policy instance.</summary>
    public static IPasswordPolicyValidator Current => _current;

    /// <summary>
    /// Installs the configuration-bound options. Called once from Program.cs after
    /// the host is built, so that non-DI static call sites honour appsettings.
    /// </summary>
    public static void Configure(PasswordPolicyOptions options)
        => _current = new PasswordPolicyValidator(options ?? new PasswordPolicyOptions());

    /// <summary>
    /// Validates <paramref name="password"/> against the policy.
    /// Returns every failure so the caller can surface a complete message.
    /// </summary>
    public static IReadOnlyList<string> Validate(string? password) => _current.Validate(password);

    /// <summary>True when the password satisfies every rule.</summary>
    public static bool IsValid(string? password) => _current.Validate(password).Count == 0;

    /// <summary>
    /// Validates and, on failure, returns a single joined message via <paramref name="error"/>.
    /// Convenience for controller paths that return 400 with one message.
    /// </summary>
    public static bool IsValid(string? password, out string? error)
    {
        var errors = _current.Validate(password);
        error = errors.Count == 0 ? null : string.Join(" ", errors);
        return errors.Count == 0;
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when the password violates the policy.
    /// Used by service-layer paths (AuthService / AdminUserService / seeders) so the
    /// rule is enforced even when the HTTP validator pipeline is bypassed.
    /// </summary>
    public static void EnsureValid(string? password, string paramName = "password")
    {
        if (!IsValid(password, out var error))
            throw new ArgumentException(error, paramName);
    }

    /// <summary>Human-readable policy description, reused in API error messages and Swagger.</summary>
    public static string Description => _current.Description;
}

/// <summary>
/// DI-friendly view of the password policy. Register via
/// <c>services.AddSingleton&lt;IPasswordPolicyValidator, PasswordPolicyValidator&gt;()</c>.
/// </summary>
public interface IPasswordPolicyValidator
{
    /// <summary>Every rule the password violates; empty when it is acceptable.</summary>
    IReadOnlyList<string> Validate(string? password);

    /// <summary>True when the password satisfies every rule.</summary>
    bool IsValid(string? password);

    /// <summary>Throws <see cref="ArgumentException"/> when the password is unacceptable.</summary>
    void EnsureValid(string? password, string paramName = "password");

    /// <summary>Human-readable policy description.</summary>
    string Description { get; }
}

/// <summary>
/// Options-driven implementation of <see cref="IPasswordPolicyValidator"/>.
/// </summary>
public sealed class PasswordPolicyValidator : IPasswordPolicyValidator
{
    private readonly PasswordPolicyOptions _o;
    private readonly HashSet<string> _denyList;

    public PasswordPolicyValidator(PasswordPolicyOptions options)
    {
        _o = options ?? throw new ArgumentNullException(nameof(options));
        _denyList = new HashSet<string>(
            PasswordPolicy.BuiltInCommonPasswords, StringComparer.OrdinalIgnoreCase);
        foreach (var extra in _o.AdditionalDeniedPasswords ?? Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(extra)) _denyList.Add(extra.Trim());
    }

    public IReadOnlyList<string> Validate(string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required.");
            return errors;
        }

        if (password.Length < _o.MinLength)
            errors.Add($"Password must be at least {_o.MinLength} characters.");
        if (password.Length > _o.MaxLength)
            errors.Add($"Password must not exceed {_o.MaxLength} characters.");
        if (_o.RequireUppercase && !PasswordPolicy.Upper.IsMatch(password))
            errors.Add("Password must contain at least one uppercase letter.");
        if (_o.RequireLowercase && !PasswordPolicy.Lower.IsMatch(password))
            errors.Add("Password must contain at least one lowercase letter.");
        if (_o.RequireDigit && !PasswordPolicy.Digit.IsMatch(password))
            errors.Add("Password must contain at least one digit.");
        if (_o.RequireSymbol && !PasswordPolicy.Symbol.IsMatch(password))
            errors.Add("Password must contain at least one special character.");
        if (_o.RejectCommonPasswords && IsCommon(password))
            errors.Add("Password is too common or predictable. Choose a less guessable password.");

        return errors;
    }

    public bool IsValid(string? password) => Validate(password).Count == 0;

    public void EnsureValid(string? password, string paramName = "password")
    {
        var errors = Validate(password);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), paramName);
    }

    public string Description
    {
        get
        {
            var parts = new List<string>();
            if (_o.RequireUppercase) parts.Add("an uppercase letter");
            if (_o.RequireLowercase) parts.Add("a lowercase letter");
            if (_o.RequireDigit)     parts.Add("a digit");
            if (_o.RequireSymbol)    parts.Add("a special character");

            var classes = parts.Count == 0 ? string.Empty : " and include " + string.Join(", ", parts);
            var common  = _o.RejectCommonPasswords
                ? ", and must not be a common password"
                : string.Empty;

            return $"Password must be at least {_o.MinLength} characters{classes}{common}.";
        }
    }

    private bool IsCommon(string password)
    {
        if (_denyList.Contains(password)) return true;

        // Strip digits/symbols so that "Password123!" reduces to "password".
        var core = new string(password.Where(char.IsLetter).ToArray());
        if (core.Length >= 4 && _denyList.Contains(core)) return true;

        // Reject repeated filler such as "aaaaaaaaaaaa1!" or "Aaaaaaaaaaa1!".
        // Comparison is case-insensitive so alternating case cannot defeat the rule
        // while still satisfying the upper/lower character-class requirements.
        var letters = password.Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray();
        if (letters.Length >= 6 && letters.Distinct().Count() == 1) return true;

        return false;
    }
}
