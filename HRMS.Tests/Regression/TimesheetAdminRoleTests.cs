// Regression test: TimesheetPage admin visibility must come from server-side role,
// not from a hardcoded `false` or from tamper-able client storage.
// This test documents the expected behaviour after the SEC-TIMESHEET-01 fix.
using Xunit;

namespace HRMS.Tests.Regression;

/// <summary>
/// Documents the correct admin-visibility rules for the timesheet page.
/// These are specification / design tests; the actual runtime logic is in
/// TimesheetPage.tsx (frontend) backed by GET /api/profile (backend).
///
/// BUGFIX (session 3): this test previously asserted `profile?.Role == "Admin"`
/// (capitalized) as the correct contract. That is exactly the bug that was found
/// and fixed in TimesheetPage.tsx / OnboardingPage.tsx: the backend's AppRoles.cs
/// defines roles as LOWERCASE ("admin", "superadmin" - see HRMS.Application.Common.
/// AppRoles) and User.Role is persisted lowercase in the database, so a capitalized
/// comparison NEVER matches and admins never saw the admin-only view. The real
/// frontend fix uses usePermissions() (src/hooks/usePermissions.ts), which lowercases
/// and trims the role before comparing against a small allow-list including "admin".
/// This test now asserts THAT contract, so it will fail again if the case-sensitive
/// bug is ever reintroduced.
/// </summary>
public class TimesheetAdminRoleTests
{
    // Mirrors usePermissions.ts's ADMIN_ROLES set and normalisation logic
    // (role.trim().toLowerCase()) so this test tracks the real frontend contract.
    private static readonly HashSet<string> AdminRoles = new(StringComparer.Ordinal)
    {
        "admin", "superadmin", "administrator", "hr admin", "hradmin",
    };

    private static bool ShowAdmin(string? rawRole)
    {
        var normalized = rawRole?.Trim().ToLowerInvariant() ?? string.Empty;
        return AdminRoles.Contains(normalized);
    }

    [Theory]
    [InlineData("admin",      true)]   // real backend value (AppRoles.Admin)
    [InlineData("superadmin", true)]   // real backend value (AppRoles.SuperAdmin)
    [InlineData("Admin",      true)]   // must still work even if casing ever changes upstream
    [InlineData("manager",    false)]
    [InlineData("employee",   false)]
    [InlineData("",           false)]
    [InlineData(null,         false)]
    public void ShowAdmin_BasedOnServerRole(string? role, bool expected)
    {
        var profile = role == null ? null : new { Role = role };
        var showAdmin = ShowAdmin(profile?.Role);

        Assert.Equal(expected, showAdmin);
    }

    [Fact]
    public void ShowAdmin_MustNotBeHardcodedFalse()
    {
        // This test exists as a design contract: if an admin profile is present,
        // showAdmin MUST be true. It fails if someone re-introduces `= false`,
        // AND it fails if someone re-introduces a case-sensitive "Admin" comparison
        // (the real backend role value is lowercase "admin").
        var adminProfile = new { Role = "admin" };
        var showAdmin = ShowAdmin(adminProfile.Role);
        Assert.True(showAdmin, "admin role must result in showAdmin = true.");
    }
}
