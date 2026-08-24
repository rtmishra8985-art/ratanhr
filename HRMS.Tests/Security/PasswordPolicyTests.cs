// ============================================================================
// Audit item 8 — unit tests for the shared password complexity policy.
//
// These tests exercise PasswordPolicyValidator directly (constructed from a
// fresh PasswordPolicyOptions) rather than the static PasswordPolicy facade,
// so that test ordering and any PasswordPolicy.Configure() call made by another
// test cannot influence the result. A separate fact asserts that the static
// facade's compile-time constants still match the audit baseline.
// ============================================================================
using HRMS.Application.Common;
using Xunit;

namespace HRMS.Tests.Security;

public class PasswordPolicyTests
{
    private static IPasswordPolicyValidator Sut(PasswordPolicyOptions? options = null)
        => new PasswordPolicyValidator(options ?? new PasswordPolicyOptions());

    // ── Minimum length ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Ab1!")]            // 4
    [InlineData("Ab1!xyzQ")]        // 8  — old (pre-audit) minimum, now rejected
    [InlineData("Ab1!xyzQwer")]     // 11 — one short of the 12-char baseline
    public void Rejects_passwords_shorter_than_minimum(string password)
    {
        var errors = Sut().Validate(password);
        Assert.Contains(errors, e => e.Contains("at least 12 characters"));
    }

    [Fact]
    public void Accepts_password_exactly_at_minimum_length()
    {
        const string password = "Tr#7vQmz9Kd2";   // exactly 12, all four classes
        Assert.Equal(12, password.Length);
        Assert.True(Sut().IsValid(password));
    }

    [Fact]
    public void Rejects_password_longer_than_maximum()
    {
        // BCrypt truncates beyond 72 bytes, so anything longer is refused outright.
        var password = "Tr#7vQmz9Kd2" + new string('x', 80);
        var errors = Sut().Validate(password);
        Assert.Contains(errors, e => e.Contains("must not exceed"));
    }

    [Fact]
    public void Rejects_null_and_whitespace()
    {
        Assert.Contains(Sut().Validate(null),   e => e.Contains("required"));
        Assert.Contains(Sut().Validate(""),     e => e.Contains("required"));
        Assert.Contains(Sut().Validate("     "), e => e.Contains("required"));
    }

    // ── Character classes ─────────────────────────────────────────────────

    [Fact]
    public void Requires_an_uppercase_letter()
    {
        var errors = Sut().Validate("tr#7vqmz9kd2");
        Assert.Contains(errors, e => e.Contains("uppercase"));
    }

    [Fact]
    public void Requires_a_lowercase_letter()
    {
        var errors = Sut().Validate("TR#7VQMZ9KD2");
        Assert.Contains(errors, e => e.Contains("lowercase"));
    }

    [Fact]
    public void Requires_a_digit()
    {
        var errors = Sut().Validate("Tr#vQmzXKdYz");
        Assert.Contains(errors, e => e.Contains("digit"));
    }

    [Fact]
    public void Requires_a_special_character()
    {
        var errors = Sut().Validate("Tr47vQmz9Kd2");
        Assert.Contains(errors, e => e.Contains("special character"));
    }

    [Fact]
    public void Reports_every_violation_at_once()
    {
        // Short, no uppercase, no digit, no symbol → four distinct messages.
        var errors = Sut().Validate("abcdef");
        Assert.True(errors.Count >= 4, $"Expected >= 4 errors, got {errors.Count}.");
    }

    // ── Common-password deny list ─────────────────────────────────────────

    [Theory]
    [InlineData("Password123!")]     // deny-listed core "password"
    [InlineData("Welcome@2026")]     // deny-listed core "welcome"
    [InlineData("Superadmin#12")]    // deny-listed core "superadmin"
    [InlineData("RatanHR@2026")]     // product name
    [InlineData("Changeme#2026")]    // deny-listed core "changeme"
    public void Rejects_common_passwords_even_when_all_classes_present(string password)
    {
        var sut = Sut();
        Assert.True(password.Length >= 12, "Test vector must clear the length rule.");
        var errors = sut.Validate(password);
        Assert.Contains(errors, e => e.Contains("common or predictable"));
    }

    [Fact]
    public void Honours_deployment_specific_deny_list_additions()
    {
        var sut = Sut(new PasswordPolicyOptions
        {
            AdditionalDeniedPasswords = new[] { "acmecorp" }
        });
        Assert.Contains(sut.Validate("Acmecorp@2026"), e => e.Contains("common or predictable"));
        // The built-in list is still merged in, not replaced.
        Assert.Contains(sut.Validate("Password123!"), e => e.Contains("common or predictable"));
    }

    [Fact]
    public void Deny_list_can_be_disabled_by_configuration()
    {
        var sut = Sut(new PasswordPolicyOptions { RejectCommonPasswords = false });
        Assert.True(sut.IsValid("Password123!"));
    }

    // ── Repeated-character patterns ───────────────────────────────────────

    [Theory]
    [InlineData("Aaaaaaaaaaa1!")]    // single repeated letter + filler
    [InlineData("Zzzzzzzzzzzz9#")]
    public void Rejects_repeated_character_filler(string password)
    {
        var errors = Sut().Validate(password);
        Assert.Contains(errors, e => e.Contains("common or predictable"));
    }

    [Fact]
    public void Allows_incidental_repetition_in_an_otherwise_strong_password()
    {
        // "ss" repeats but the alphabetic core is varied — must not be rejected.
        Assert.True(Sut().IsValid("Gr#8ssTqvNm4"));
    }

    // ── Valid passwords ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Tr#7vQmz9Kd2")]
    [InlineData("X4$knPwtLbR8vQ")]
    [InlineData("mQ7!zXbTr2Wn5Kd")]
    [InlineData("Correct-Horse7Battery")]
    public void Accepts_policy_compliant_passwords(string password)
    {
        Assert.Empty(Sut().Validate(password));
        Assert.True(Sut().IsValid(password));
    }

    // ── EnsureValid throwing surface ──────────────────────────────────────

    [Fact]
    public void EnsureValid_throws_for_a_weak_password()
    {
        var ex = Assert.Throws<ArgumentException>(() => Sut().EnsureValid("weak", "password"));
        Assert.Equal("password", ex.ParamName);
        Assert.Contains("at least 12 characters", ex.Message);
    }

    [Fact]
    public void EnsureValid_passes_for_a_strong_password()
    {
        var record = Record.Exception(() => Sut().EnsureValid("Tr#7vQmz9Kd2"));
        Assert.Null(record);
    }

    // ── Static facade / baseline constants ────────────────────────────────

    [Fact]
    public void Static_facade_constants_match_the_audit_baseline()
    {
        Assert.Equal(12, PasswordPolicy.MinLength);
        Assert.Equal(72, PasswordPolicy.MaxLength);
    }

    [Fact]
    public void Description_documents_the_active_rules()
    {
        var description = Sut().Description;
        Assert.Contains("12 characters", description);
        Assert.Contains("uppercase", description);
        Assert.Contains("special character", description);
        Assert.Contains("common password", description);
    }
}
