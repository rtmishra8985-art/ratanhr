namespace HRMS.API.Security;

/// <summary>
/// Centralized rate-limiter policy names and constants.
/// Prevents typos and ensures all controllers use consistent policy names.
/// Each policy enforces specific limits per IP address per minute.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Login endpoint — 10 requests per minute per IP.
    /// Applied to POST /api/auth/login, POST /api/auth/login-with-mfa
    /// Protects against brute-force attack attempts.
    /// </summary>
    public const string Login = "login";

    /// <summary>
    /// Sensitive operations — 5 requests per minute per IP.
    /// Applied to password change, password reset, MFA setup endpoints.
    /// </summary>
    public const string Sensitive = "sensitive";

    /// <summary>
    /// Standard API endpoints — 120 requests per minute per IP.
    /// Applied as a fallback to any endpoint without an explicit policy.
    /// </summary>
    public const string Api = "api";

    /// <summary>
    /// File upload endpoints — 20 requests per minute per IP.
    /// Applied to all file upload routes to prevent disk/bandwidth exhaustion.
    /// </summary>
    public const string Upload = "upload";

    /// <summary>
    /// Expensive report/export endpoints — 10 requests per minute per IP.
    /// Applied to /api/reports, /api/payroll/export, etc.
    /// </summary>
    public const string Reports = "reports";

    /// <summary>
    /// Validates that a policy name is recognized.
    /// Useful for compile-time checks in unit tests.
    /// </summary>
    public static bool IsValidPolicy(string policyName) =>
        policyName switch
        {
            Login or Sensitive or Api or Upload or Reports => true,
            _ => false
        };
}
