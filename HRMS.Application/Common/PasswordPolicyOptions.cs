using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.Common;

/// <summary>
/// Item 8 (audit) — configuration surface for <see cref="PasswordPolicy"/>.
///
/// Bound from the "PasswordPolicy" configuration section in
/// <c>ServiceExtensions.AddInfrastructure</c> with
/// <c>ValidateDataAnnotations().ValidateOnStart()</c>, so a misconfigured
/// deployment fails fast at startup instead of silently weakening the policy.
///
/// The defaults are the hardened v1.0.5 audit values; configuration may only be
/// used to make the policy stricter in practice — <see cref="MinLength"/> is
/// range-validated so it can never be dropped below 8.
/// </summary>
public sealed class PasswordPolicyOptions
{
    public const string SectionName = "PasswordPolicy";

    /// <summary>Minimum password length. Audit baseline is 12; never below 8.</summary>
    [Range(8, 72, ErrorMessage = "PasswordPolicy:MinLength must be between 8 and 72.")]
    public int MinLength { get; set; } = 12;

    /// <summary>Upper bound — BCrypt silently truncates beyond 72 bytes.</summary>
    [Range(16, 72, ErrorMessage = "PasswordPolicy:MaxLength must be between 16 and 72.")]
    public int MaxLength { get; set; } = 72;

    /// <summary>Require at least one A-Z character.</summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>Require at least one a-z character.</summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>Require at least one 0-9 character.</summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>Require at least one non-alphanumeric character.</summary>
    public bool RequireSymbol { get; set; } = true;

    /// <summary>Reject passwords matching the built-in common-password deny list.</summary>
    public bool RejectCommonPasswords { get; set; } = true;

    /// <summary>
    /// Deployment-specific additions to the deny list (tenant name, product name,
    /// city, etc.). Merged with the built-in list; matching is case-insensitive.
    /// </summary>
    public string[] AdditionalDeniedPasswords { get; set; } = Array.Empty<string>();
}
