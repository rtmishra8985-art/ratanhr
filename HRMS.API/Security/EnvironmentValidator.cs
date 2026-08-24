using Microsoft.Extensions.Hosting;

namespace HRMS.API.Security;

/// <summary>
/// Validates required environment variables and configuration values at startup.
/// Called from Program.cs before the HTTP pipeline is configured.
/// Throws <see cref="InvalidOperationException"/> if any check fails, which
/// prevents the application from starting.
/// </summary>
public static class EnvironmentValidator
{
    // Placeholder / example domain patterns that must be rejected.
    private static readonly string[] ForbiddenAllowedHostsPatterns =
    [
        "REPLACE_WITH_PRODUCTION_HOSTS",
        "example.com",
        "example.org",
        "example.net",
        "yourcompany.com",
        "localhost",   // reject in Production; allowed only in Development
        "127.0.0.1",
    ];

    /// <summary>
    /// Validates the application environment. Throws on the first validation
    /// failure. Must be called after <see cref="IHostEnvironment"/> is available
    /// but before any background services or middleware that depend on the
    /// validated values.
    /// </summary>
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        var errors = new List<string>();

        // ── 1. Core secrets (all environments) ─────────────────────────────
        // These are the configuration keys consumed by JwtService and
        // AesEncryptionService. Environment variables use the equivalent
        // double-underscore form (Jwt__PrivateKeyPem, etc.).
        // Accept the legacy all-caps names too so existing deployments fail
        // only when the application itself cannot resolve a required value.
        RequireNonEmpty(configuration, "Jwt:PrivateKeyPem", "JWT_PRIVATE_KEY_PEM", errors);
        RequireNonEmpty(configuration, "Jwt:PublicKeyPem", "JWT_PUBLIC_KEY_PEM", errors);
        // EncryptionKey is only mandatory outside Development — local development
        // runs without PII-at-rest encryption for convenience.
        if (!environment.IsDevelopment())
        {
            RequireNonEmpty(configuration, "Security:EncryptionKey", "ENCRYPTION_KEY", errors);
        }

        // ── 2. Database ─────────────────────────────────────────────────────
        RequireNonEmpty(configuration, "ConnectionStrings:DefaultConnection", errors);

        // ── 3. ALLOWED_HOSTS (strict check outside Development) ─────────────
        if (!environment.IsDevelopment() && !IsExplicitTestEnvironment(environment))
        {
            ValidateAllowedHosts(configuration, errors);
        }

        // ── 4. Hangfire / Redis (mandatory outside Development + test) ───────
        if (!environment.IsDevelopment() && !IsExplicitTestEnvironment(environment))
        {
            ValidateHangfireRedis(configuration, errors);
        }

        // ── 5. Fail fast ────────────────────────────────────────────────────
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Startup validation failed ({errors.Count} error(s)):\n" +
                string.Join("\n", errors.Select((e, i) => $"  [{i + 1}] {e}")));
        }
    }

    // ── ALLOWED_HOSTS validation ─────────────────────────────────────────────

    private static void ValidateAllowedHosts(IConfiguration configuration, List<string> errors)
    {
        var raw = configuration["ALLOWED_HOSTS"] ?? configuration["AllowedHosts"];

        if (string.IsNullOrWhiteSpace(raw))
        {
            errors.Add(
                "ALLOWED_HOSTS is missing or empty. " +
                "Set a semicolon-separated list of permitted Host header values " +
                "(e.g. api.yourcompany.com;yourcompany.com).");
            return;
        }

        if (raw.Trim() == "*")
        {
            errors.Add(
                "ALLOWED_HOSTS is set to '*' (wildcard), which is forbidden in Production. " +
                "Specify exact host names.");
            return;
        }

        foreach (var forbidden in ForbiddenAllowedHostsPatterns)
        {
            if (raw.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"ALLOWED_HOSTS contains a forbidden placeholder or example value: '{forbidden}'. " +
                    "Replace with the actual production host name(s).");
                return;
            }
        }

        // Validate each individual entry is a plausible host name / IP
        var hosts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts.Length == 0)
        {
            errors.Add("ALLOWED_HOSTS is set but contains no valid entries after splitting on ';'.");
            return;
        }

        foreach (var host in hosts)
        {
            if (host.StartsWith('.'))
            {
                // Wildcard subdomain prefix — allowed by ASP.NET Core, warn but do not fail
                continue;
            }
            if (!IsPlausibleHostName(host))
            {
                errors.Add(
                    $"ALLOWED_HOSTS entry '{host}' does not look like a valid host name. " +
                    "Remove placeholder or malformed entries.");
            }
        }
    }

    private static bool IsPlausibleHostName(string host)
    {
        // Accepts domain names and IPv4 addresses; port suffixes (host:port) also valid.
        if (string.IsNullOrWhiteSpace(host)) return false;

        // Strip optional port
        var portIdx = host.LastIndexOf(':');
        var bare = portIdx >= 0 ? host[..portIdx] : host;

        // Must contain at least one dot (e.g. api.example.com) OR be a single label
        // followed by a digit-only octet sequence (IPv4).  Simple heuristic only.
        return bare.Length >= 1 &&
               bare.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_');
    }

    // ── Hangfire / Redis validation ──────────────────────────────────────────

    private static void ValidateHangfireRedis(IConfiguration configuration, List<string> errors)
    {
        // Explicit in-memory storage is never permitted outside Development,
        // even when Redis is also configured.
        if (configuration.GetValue<bool?>("Hangfire:UseInMemory") == true)
        {
            errors.Add(
                "Hangfire:UseInMemory must not be 'true' outside Development. " +
                "In-memory Hangfire storage loses queued jobs on restart. " +
                "Set Hangfire__UseInMemory=false and use Redis-backed storage.");
            return;
        }

        var useRedis = configuration.GetValue<bool?>("Hangfire:UseRedis");

        var redisConnection = configuration["Hangfire:RedisConnectionString"]
            ?? configuration["REDIS_CONNECTION_STRING"]
            ?? configuration["Redis:ConnectionString"];

        if (useRedis == false)
        {
            errors.Add(
                "Hangfire:UseRedis must be 'true' in Production and non-Development environments. " +
                "In-memory Hangfire storage is not permitted outside Development. " +
                "Set Hangfire__UseRedis=true and provide Hangfire__RedisConnectionString.");
            return;
        }

        if (useRedis is null && !string.IsNullOrWhiteSpace(redisConnection))
        {
            // Redis is configured through the shared Redis:ConnectionString key;
            // Hangfire:UseRedis is implied.
            return;
        }

        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            errors.Add(
                "Hangfire:RedisConnectionString is missing or empty. " +
                "Redis-backed Hangfire is mandatory in Production. " +
                "Set Hangfire__RedisConnectionString to a valid StackExchange.Redis connection string.");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void RequireNonEmpty(
        IConfiguration configuration,
        string key,
        string? legacyEnvironmentKey,
        List<string> errors)
    {
        var value = configuration[key]
            ?? (legacyEnvironmentKey is null ? null : configuration[legacyEnvironmentKey]);
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(legacyEnvironmentKey is null
                ? $"Required configuration key '{key}' is missing or empty."
                : $"Required configuration key '{key}' (environment variable {legacyEnvironmentKey}) is missing or empty.");
        }
    }

    private static void RequireNonEmpty(
        IConfiguration configuration,
        string key,
        List<string> errors) =>
        RequireNonEmpty(configuration, key, legacyEnvironmentKey: null, errors: errors);

    /// <summary>
    /// Returns true when the environment is one of the known integration-test
    /// environments where in-memory Hangfire is explicitly permitted.
    /// </summary>
    private static bool IsExplicitTestEnvironment(IHostEnvironment env) =>
        env.EnvironmentName.Equals("Test", StringComparison.OrdinalIgnoreCase) ||
        env.EnvironmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase) ||
        env.EnvironmentName.Equals("IntegrationTest", StringComparison.OrdinalIgnoreCase);
}
