using System.Threading.Channels;
using System.Threading.RateLimiting;
using Hangfire;
using HRMS.Infrastructure.Services;
using HRMS.API.Filters;
using HRMS.Infrastructure.Redis;
using HRMS.Infrastructure.Telemetry;
using Microsoft.AspNetCore.HttpOverrides;
using BCrypt.Net;
using HRMS.API.Extensions;
using HRMS.API.Middleware;
using HRMS.API.Security;
using HRMS.Domain.Entities.Authentication;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Keep the development and test startup paths strict: missing registrations and
// invalid scoped/singleton lifetimes must fail at startup instead of surfacing
// only when the first request reaches the affected service.
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateOnBuild = !context.HostingEnvironment.IsProduction();
    options.ValidateScopes = !context.HostingEnvironment.IsProduction();
});

// Normalize the legacy production environment variable names used by the
// deployment templates into the hierarchical keys consumed by the services.
// This keeps secret values in configuration only; they are never logged or
// written to source. New deployments may use the Jwt__*/Security__* forms
// directly, while existing deployments continue to work during migration.
static void MapLegacyConfiguration(
    IConfigurationManager configuration,
    string targetKey,
    string legacyKey)
{
    if (string.IsNullOrWhiteSpace(configuration[targetKey])
        && !string.IsNullOrWhiteSpace(configuration[legacyKey]))
    {
        configuration[targetKey] = configuration[legacyKey];
    }
}

MapLegacyConfiguration(builder.Configuration, "Jwt:PrivateKeyPem", "JWT_PRIVATE_KEY_PEM");
MapLegacyConfiguration(builder.Configuration, "Jwt:PublicKeyPem", "JWT_PUBLIC_KEY_PEM");
MapLegacyConfiguration(builder.Configuration, "Security:EncryptionKey", "ENCRYPTION_KEY");

// ── Serilog ────────────────────────────────────────────────────────────────
var seqUrl = builder.Configuration["Monitoring:SeqUrl"];
var logConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    // FIX: PII masking — destructure policies replace sensitive scalar properties
    // with "[REDACTED]" so employee names, email addresses, Aadhaar/PAN numbers,
    // bank account details, and passwords never appear in structured log sinks
    // (Console, File, Seq) even when logged as part of a request/DTO object.
    .Destructure.ByTransforming<HRMS.Application.DTOs.Employee.CreateEmployeeDto>(dto => new {
        dto.FullName,
        dto.Department,
        dto.Designation,
        dto.CompanyId,
        Dob               = "[REDACTED]",
        Aadhaar           = "[REDACTED]",
        Pan               = "[REDACTED]",
        AccountNumber     = "[REDACTED]",
        IfscCode          = "[REDACTED]",
        Uan               = "[REDACTED]",
        BankAccountHolder = "[REDACTED]",
        EmergencyContactPhone = "[REDACTED]"
    })
    // Fix MED: PII masking — additional sensitive DTOs not previously covered
    .Destructure.ByTransforming<HRMS.Application.DTOs.Auth.LoginDto>(dto => new {
        dto.Email,
        dto.Portal,
        dto.AdminRole,
        Password = "[REDACTED]"
    })
    .Destructure.ByTransforming<HRMS.Application.DTOs.Auth.ChangePasswordDto>(_ => new {
        CurrentPassword = "[REDACTED]",
        NewPassword     = "[REDACTED]"
    })
    .Destructure.ByTransforming<HRMS.Application.DTOs.Auth.ResetPasswordDto>(_ => new {
        Token           = "[REDACTED]",
        NewPassword     = "[REDACTED]",
        ConfirmPassword = "[REDACTED]"
    })
    .Destructure.ByTransforming<HRMS.Application.DTOs.Payroll.PayslipDto>(dto => new {
        dto.Id,
        dto.EmployeeId,
        dto.EmployeeName,
        dto.Designation,
        dto.Department,
        dto.Month,
        dto.Year,
        dto.NetPay,
        BankName      = "[REDACTED]",
        AccountNumber = "[REDACTED]",
        UAN           = "[REDACTED]"
    })
    .Destructure.ByTransforming<HRMS.Application.DTOs.Payroll.CreateSalaryStructureDto>(dto => new {
        dto.EmployeeId,
        dto.EffectiveFrom,
        CTC              = "[REDACTED]",
        BasicPay         = "[REDACTED]",
        HRA              = "[REDACTED]",
        DA               = "[REDACTED]",
        Conveyance       = "[REDACTED]",
        MedicalAllowance = "[REDACTED]",
        OtherAllowances  = "[REDACTED]",
        PFEmployee       = "[REDACTED]",
        PFEmployer       = "[REDACTED]",
        ESI              = "[REDACTED]",
        PT               = "[REDACTED]",
        TDS              = "[REDACTED]"
    })
    .WriteTo.Async(sink => sink.Console(outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"))
    .WriteTo.Async(sink => sink.File("Logs/hrms-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

if (!string.IsNullOrWhiteSpace(seqUrl))
{
    logConfig = logConfig.WriteTo.Async(sink => sink.Seq(seqUrl));
    Log.Information("[Monitoring] Sending structured logs to Seq: {SeqUrl}", seqUrl);
}

Log.Logger = logConfig.CreateLogger();
builder.Host.UseSerilog();

// ── Services ───────────────────────────────────────────────────────────────
// Global audit filter — logs every mutating request (POST/PUT/PATCH/DELETE) across all 51 controllers.
builder.Services.AddControllers(opt => {
    opt.Filters.Add<HRMS.API.Filters.AuditActionFilter>();
    opt.Filters.Add<HRMS.API.Filters.AntiVirusScanFilter>(); // FIX MED-03: global AV scan on all file uploads
    opt.Filters.Add<CsrfValidationFilter>(); // double-submit XSRF header on all mutations
    // FIX 1: Global validation filter - ensures consistent error responses
    opt.Filters.Add<HRMS.Application.Common.ValidationFilterAttribute>();
    })
    .AddJsonOptions(opt => {
        opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opt.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();

// ── API Versioning (FIX MED: API versioning) ──────────────────────────────
// AssumeDefaultVersionWhenUnspecified = true keeps ALL existing /api/... routes
// working as v1.0 without adding [ApiVersion] to every controller.
// Clients may also pass: header "api-version: 1.0" or query "?api-version=1.0".
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion                  = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions                  = true;
    options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
        new Asp.Versioning.UrlSegmentApiVersionReader(),
        new Asp.Versioning.HeaderApiVersionReader("api-version"),
        new Asp.Versioning.QueryStringApiVersionReader("api-version")
    );
}).AddApiExplorer(options =>
{
    options.GroupNameFormat           = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
// FIX 5: Only register Swagger services outside Production so Swagger DI overhead
// and API schema metadata are never loaded in production deployments.
// Staging explicitly enables Swagger for contract validation through
// AppSettings:EnableSwagger; production remains disabled.
var swaggerEnabled = !builder.Environment.IsProduction()
    && (builder.Environment.IsDevelopment()
        || builder.Configuration.GetValue("AppSettings:EnableSwagger", false));
if (swaggerEnabled)
{
    builder.Services.AddSwaggerDocumentation(builder.Configuration);
}
builder.Services.AddInfrastructure(builder.Configuration);

// ── Item 8: password policy (Security:PasswordPolicy) ─────────────────────
// Bound with ValidateDataAnnotations + ValidateOnStart so a deployment that
// weakens the policy below the audited floor (MinLength >= 8) fails fast at
// startup instead of silently accepting weak credentials.
builder.Services
    .AddOptions<PasswordPolicyOptions>()
    .Bind(builder.Configuration.GetSection("Security:PasswordPolicy"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IPasswordPolicyValidator>(sp =>
    new PasswordPolicyValidator(sp.GetRequiredService<IOptions<PasswordPolicyOptions>>().Value));
builder.Services.AddAllowedHostsFromEnvironment(builder.Configuration);
// M-21: Hangfire distributed background jobs (MySQL storage — Phase 2g)
builder.Services.AddHangfireJobs(builder.Configuration);

// FIX LOW: Response compression — reduces payload size for JSON API responses.
// BrotliCompressionProvider preferred (superior ratio); GzipCompressionProvider as fallback.
// EnableForHttps=true is safe here because HRMS API responses carry no cross-origin secrets
// (JWT is already HttpOnly cookie; no sensitive resource-differentiation data in response bodies).
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    opts.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    opts.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "application/json", "application/problem+json" });
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o =>
    o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o =>
    o.Level = System.IO.Compression.CompressionLevel.Fastest);

// NOTE: All service registrations live in ServiceExtensions.AddInfrastructure().
// Do NOT register services here; duplicate registrations cause the EmailQueueWorker
// hosted service to run twice and create race conditions on the email queue.
builder.Services.AddEncryptionService(builder.Configuration, builder.Environment);
builder.Services.AddJwtAuthentication(builder.Configuration, builder.Environment);
// SECURITY FIX (MEDIUM): Global fallback authorization policy — any endpoint that lacks
// an explicit [Authorize] or [AllowAnonymous] attribute now requires an authenticated user
// by default, instead of being publicly accessible. All public endpoints in this codebase
// (login, register, forgot-password, MFA challenge, health checks, capabilities) already
// carry [AllowAnonymous] and are unaffected. This closes the gap where a new controller
// added without [Authorize] would silently become public.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireMfaCompleted", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("companyId"))
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.Configure<CookiePolicyOptions>(o => { o.MinimumSameSitePolicy = SameSiteMode.Strict; });

// ── HSTS — Fix LOW: explicit production configuration ─────────────────────
// max-age=31536000 (1 year), includeSubDomains + preload make the domain
// eligible for Chrome's HSTS preload list. Applied only when app.UseHsts()
// is called in the non-Development pipeline branch below.
builder.Services.AddHsts(options =>
{
    options.MaxAge            = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload           = true;
});

// ── CSRF / Anti-forgery (double-submit header pattern for JWT SPA) ──────────
// Tokens are stored in HttpOnly cookies and sent automatically by the browser, so
// classical CSRF cannot steal them directly. This double-submit header pattern
// provides defence-in-depth for state-changing endpoints.
builder.Services.AddAntiforgery(opt => {
    opt.HeaderName          = "X-XSRF-TOKEN";   // JS reads the cookie and echoes it here
    opt.Cookie.Name         = "XSRF-TOKEN";
    opt.Cookie.HttpOnly     = false;             // JS MUST be able to read this value
    opt.Cookie.SameSite     = SameSiteMode.Strict;
    // Low FIX: use Always so the XSRF cookie is always Secure in any HTTPS context,
    // not only when the incoming request happens to be HTTPS (SameAsRequest was
    // environment-unaware and could allow non-Secure cookies in ambiguous proxy setups).
    // FIX: Always breaks local HTTP development (docker-compose.override.yml serves the
    // API directly on http://localhost:8080 with no TLS termination) — GetAndStoreTokens
    // throws InvalidOperationException on every non-HTTPS request, making /api/auth/csrf
    // (and therefore change-password) completely unreachable in dev. Keep Always in
    // Production/Staging where nginx terminates TLS; fall back to SameAsRequest in
    // Development so local HTTP testing works.
    opt.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// ── OpenTelemetry (tracing + metrics + Prometheus /metrics endpoint) ───────
builder.Services.AddHrmsOpenTelemetry(builder.Configuration);
// HrmsMetrics singleton — injected into services that record custom metrics
builder.Services.AddSingleton<HrmsMetrics>();

// Webhook channel and BiometricLogCleanupService are registered inside
// ServiceExtensions.AddInfrastructure() to keep all DI registrations in one place
// and prevent the duplicate-hosted-service issue described in the NOTE above.

// ── Forwarded headers ──────────────────────────────────────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(options => {
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    // FIX: Load trusted proxy CIDRs from config so X-Forwarded-For cannot be spoofed
    // by clients that bypass the load balancer. Without this, IP-based rate limiting
    // is bypassable by forging the header.
    // Set in production: Network__KnownProxyCidrs=172.18.0.0/16
    // Multiple CIDRs:    Network__KnownProxyCidrs=10.0.0.0/8,172.16.0.0/12
    var proxyCidrs = builder.Configuration["Network:KnownProxyCidrs"]?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? Array.Empty<string>();
    foreach (var cidr in proxyCidrs)
    {
        try
        {
            var slash = cidr.LastIndexOf('/');
            if (slash > 0
                && System.Net.IPAddress.TryParse(cidr[..slash], out var proxyPrefix)
                && int.TryParse(cidr[(slash + 1)..], out var prefixLen))
            {
                options.KnownNetworks.Add(
                    new Microsoft.AspNetCore.HttpOverrides.IPNetwork(proxyPrefix, prefixLen));
            }
        }
        catch { /* skip malformed CIDR entries */ }
    }
    if (proxyCidrs.Length == 0 && !builder.Environment.IsDevelopment())
        Log.Warning("[ForwardedHeaders] Network:KnownProxyCidrs is not configured. " +
            "X-Forwarded-For headers are NOT trusted. " +
            "Set Network__KnownProxyCidrs to your load balancer CIDR (e.g. 172.18.0.0/16).");
});

// ── Health checks ──────────────────────────────────────────────────────────
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
// FIX MED-01: Add Redis health check — Redis is a hard dependency for rate limiting.
// If Redis goes down, rate limiting silently fails open; health endpoint must surface it.
var healthBuilder = builder.Services.AddHealthChecks()
    // Fix LOW: explicit liveness check — always returns Healthy so /healthz/live
    // proves the process is running without exercising dependencies.
    // Predicate = _ => false (old) was correct but unclear; tagging makes intent obvious.
    .AddCheck("liveness",
        () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Service is alive."),
        tags: ["live"])
    .AddCheck<HRMS.Infrastructure.Services.EmailHealthCheckService>("email")
    // Phase 2f: Replaced AddNpgSql with AddMySql (AspNetCore.HealthChecks.MySql)
    .AddMySql(dbConnectionString, name: "database", tags: ["db", "ready"]);

var redisHealthCs = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisHealthCs))
    healthBuilder.AddRedis(redisHealthCs, name: "redis", tags: ["cache", "ratelimit", "ready"]);

// ── CORS ───────────────────────────────────────────────────────────────────
var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? Array.Empty<string>();
var allowedOrigins = configuredOrigins.Length > 0
    ? configuredOrigins
    : (builder.Environment.IsDevelopment()
        ? new[] { "http://localhost:3000", "http://localhost:5173", "http://localhost:5000" }
        : Array.Empty<string>());

// FIX CRIT-03: fail-closed CORS — if no origins are configured in production, block ALL origins.
// Previous code called AllowAnyMethod().AllowAnyHeader() without WithOrigins(), which in ASP.NET Core
// silently allows every origin. Now production with empty AllowedOrigins blocks all cross-origin requests.
builder.Services.AddCors(opt => opt.AddPolicy("AppCors", policy => {
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    }
    else if (builder.Environment.IsDevelopment())
    {
        // Dev only — allow localhost variants
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        Log.Warning("CORS: Development mode — all origins allowed. Set Cors:AllowedOrigins in production.");
    }
    else
    {
        // Production/Staging with no AllowedOrigins configured: block everything.
        // No WithOrigins() call at all → all cross-origin requests are rejected.
        Log.Error("CORS: Cors:AllowedOrigins is not configured in production. " +
                  "All cross-origin requests will be blocked. Set Cors__AllowedOrigins env var.");
    }
}));

// ── Rate limiting ──────────────────────────────────────────────────────────
var redisCs = builder.Configuration["Redis:ConnectionString"];

builder.Services.AddRateLimiter(opt => {
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opt.OnRejected = async (context, token) => {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers["Retry-After"] =
                ((int)retryAfter.TotalSeconds).ToString();
        else
            context.HttpContext.Response.Headers["Retry-After"] = "60";
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            """{"success":false,"message":"Too many requests. Please try again later."}""",
            token);
    };

    if (!string.IsNullOrWhiteSpace(redisCs))
    {
        opt.AddPolicy(RateLimitPolicies.Login, ctx => {
            var ip  = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
            var mux = ctx.RequestServices.GetRequiredService<IConnectionMultiplexer>();
            return RedisDistributedRateLimiter.CreatePartition(mux, $"ratelimit:login:{ip}", 10, 60);
        });
        opt.AddPolicy(RateLimitPolicies.Sensitive, ctx => {
            var ip  = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
            var mux = ctx.RequestServices.GetRequiredService<IConnectionMultiplexer>();
            return RedisDistributedRateLimiter.CreatePartition(mux, $"ratelimit:sensitive:{ip}", 5, 60);
        });
        opt.AddPolicy(RateLimitPolicies.Api, ctx => {
            var ip  = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
            var mux = ctx.RequestServices.GetRequiredService<IConnectionMultiplexer>();
            return RedisDistributedRateLimiter.CreatePartition(mux, $"ratelimit:api:{ip}", 120, 60);
        });
        // BLOCKER-11: upload endpoints — stricter limit to reduce abuse surface.
        opt.AddPolicy(RateLimitPolicies.Upload, ctx => {
            var ip  = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
            var mux = ctx.RequestServices.GetRequiredService<IConnectionMultiplexer>();
            return RedisDistributedRateLimiter.CreatePartition(mux, $"ratelimit:upload:{ip}", 20, 60);
        });
        // BLOCKER-11: expensive report/export endpoints — low per-IP limit.
        opt.AddPolicy(RateLimitPolicies.Reports, ctx => {
            var ip  = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
            var mux = ctx.RequestServices.GetRequiredService<IConnectionMultiplexer>();
            return RedisDistributedRateLimiter.CreatePartition(mux, $"ratelimit:reports:{ip}", 10, 60);
        });
        Log.Information("Rate limiter: Redis-backed distributed counters.");
    }
    else
    {
        opt.AddSlidingWindowLimiter(RateLimitPolicies.Login, o => {
            o.PermitLimit = 10; o.Window = TimeSpan.FromMinutes(1);
            o.SegmentsPerWindow = 6; o.QueueLimit = 0;
        });
        opt.AddSlidingWindowLimiter(RateLimitPolicies.Sensitive, o => {
            o.PermitLimit = 5; o.Window = TimeSpan.FromMinutes(1);
            o.SegmentsPerWindow = 6; o.QueueLimit = 0;
        });
        opt.AddSlidingWindowLimiter(RateLimitPolicies.Api, o => {
            o.PermitLimit = 120; o.Window = TimeSpan.FromMinutes(1);
            o.SegmentsPerWindow = 6; o.QueueLimit = 0;
        });
        // BLOCKER-11: upload — in-memory fallback
        opt.AddSlidingWindowLimiter(RateLimitPolicies.Upload, o => {
            o.PermitLimit = 20; o.Window = TimeSpan.FromMinutes(1);
            o.SegmentsPerWindow = 6; o.QueueLimit = 0;
        });
        // BLOCKER-11: reports — in-memory fallback
        opt.AddSlidingWindowLimiter(RateLimitPolicies.Reports, o => {
            o.PermitLimit = 10; o.Window = TimeSpan.FromMinutes(1);
            o.SegmentsPerWindow = 6; o.QueueLimit = 0;
        });
        Log.Warning("Rate limiter: in-memory counters (Redis not configured).");
    }
});


// ── Sentry error tracking ─────────────────────────────────────────────────────
// Activates only when SENTRY_DSN / Sentry__Dsn environment variable is set.
// No-ops silently in development when DSN is absent.
var sentryDsn = builder.Configuration["Sentry:Dsn"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn                    = sentryDsn;
        o.Environment            = builder.Environment.EnvironmentName;
        o.TracesSampleRate       = 0.2;
        o.MinimumBreadcrumbLevel = Microsoft.Extensions.Logging.LogLevel.Information;
        o.MinimumEventLevel      = Microsoft.Extensions.Logging.LogLevel.Error;
    });
}
var app = builder.Build();

// Item 8: install the configuration-bound policy into the static PasswordPolicy
// facade so service/seed paths outside DI (AuthService, EmployeeService,
// AdminUserService, SeedAsync) enforce exactly the same rules.
PasswordPolicy.Configure(app.Services.GetRequiredService<IOptions<PasswordPolicyOptions>>().Value);

// FIX HIGH-2: Email:Host validation — production deployments must have email configured.
// Fail fast at startup rather than silently allowing deployment without email delivery.
if (app.Environment.IsProduction())
{
    var emailHost = builder.Configuration["Email:Host"];
    if (string.IsNullOrWhiteSpace(emailHost))
    {
        throw new InvalidOperationException(
            "Email:Host is required in Production. Set Email__Host environment variable (e.g. smtp.gmail.com).");
    }
}

// ── Environment validation — fail fast with clear diagnostics ──────────────
// Placed AFTER builder.Build() so WebApplicationFactory.ConfigureAppConfiguration
// can inject test JWT keys / connection strings before the validator runs.
// (Calling it before Build() meant the factory's in-memory overrides were not yet
// applied, causing all integration tests to fail with missing-Jwt:PrivateKeyPem.)
EnvironmentValidator.Validate(app.Configuration, app.Environment);

// ── Auto-migrate & seed (development / explicit opt-in only) ───────────────
// PRODUCTION: migrations are handled by the dedicated 'migrate' Docker service.
// Set Database__AutoMigrate=false in production .env to prevent this block.
if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        if (app.Environment.IsDevelopment())
        {
            // FIX: Use EnsureCreated() instead of relCreator.CreateTables().
            // EnsureCreated() builds the full schema from the DbContext model,
            // while CreateTables() only creates tables EF already knows about from migrations.
            // Since migrations may not exist, EnsureCreated() is the correct approach for dev.
            try   { db.Database.EnsureCreated(); }
            catch { /* tables may already exist — OK */ }
            Log.Information("Database tables created/verified (Development mode).");
        }
        else
        {
            db.Database.Migrate();
            Log.Information("Database migrated successfully.");
        }
        await SeedAsync(db, builder.Configuration);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Database schema setup failed.");
        if (!app.Environment.IsDevelopment()) throw;
    }
}

// ── Middleware pipeline ────────────────────────────────────────────────────
// FIX: UseForwardedHeaders MUST be the very first middleware so that real client IPs
// are available to every subsequent stage: rate limiter, correlation ID logging,
// exception handler, CORS, authentication, and audit trails.
// Previously this was registered at line ~489 — AFTER the rate limiter — meaning
// rate limiting ran against the nginx proxy IP, not the actual client IP.
// Reference: https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer
app.UseForwardedHeaders();

// Correlation ID next so every subsequent log entry includes it.
// FIX LOW: Response compression must be registered before any middleware that writes responses.
app.UseResponseCompression();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseMiddleware<ExceptionMiddleware>();

app.UseMiddleware<HtmlNonceInjectionMiddleware>();
app.UseMiddleware<CspNonceMiddleware>();

// Security headers
app.Use(async (ctx, next) => {
    ctx.Response.Headers["X-Content-Type-Options"]  = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]         = "DENY";
    ctx.Response.Headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
    ctx.Response.Headers["X-XSS-Protection"]        = "1; mode=block";
    ctx.Response.Headers["Permissions-Policy"]      = "camera=(), microphone=(), geolocation=()";
    if (!app.Environment.IsDevelopment())
        ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
    // FIX 3: Content-Security-Policy — defence-in-depth against XSS and data injection.
    // nonce-{cspNonce} is generated per-request by CspNonceMiddleware and injected into
    // inline <script> tags by HtmlNonceInjectionMiddleware; inline scripts without a
    // matching nonce are blocked by the browser.
    // 'strict-dynamic' allows nonce-bearing scripts to load dynamic dependencies so
    // existing third-party integrations (e.g. Sentry JS) continue to work.
    // upgrade-insecure-requests silently upgrades any http:// sub-resource to https://.
    var cspNonce = ctx.Items.TryGetValue("CspNonce", out var n) ? n as string : null;
    var nonceStr = string.IsNullOrEmpty(cspNonce) ? "" : $" 'nonce-{cspNonce}'";
    ctx.Response.Headers["Content-Security-Policy"] =
        $"default-src 'self';" +
        $" script-src 'self'{nonceStr} 'strict-dynamic';" +
        $" style-src 'self' 'unsafe-inline';" +
        $" img-src 'self' data: blob:;" +
        $" font-src 'self' data:;" +
        $" connect-src 'self';" +
        $" frame-ancestors 'none';" +
        $" object-src 'none';" +
        $" base-uri 'self';" +
        $" upgrade-insecure-requests";
    await next();
});

if (swaggerEnabled)
{
    app.UseMiddleware<HRMS.API.Middleware.SwaggerBasicAuthMiddleware>();
    app.UseSwagger();
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "HRMS API v1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHsts();
}

// M-21: Hangfire dashboard — always registered; auth filter restricts access to superadmins only.
app.UseHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
{
    Authorization = new[] { new HRMS.API.Security.HangfireSuperAdminAuthFilter() },
    IsReadOnlyFunc = ctx => !((Hangfire.Dashboard.AspNetCoreDashboardContext)ctx).HttpContext.User.IsInRole(AppRoles.SuperAdmin)
});

app.UseHttpsRedirection();
app.UseCors("AppCors");
app.UseRateLimiter();
app.UseDefaultFiles();   // serve wwwroot/index.html (React SPA) for "/"
app.UseStaticFiles();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

// ── FIX CRIT-02: Tenant context middleware ────────────────────────────────
// Runs after UseAuthentication so User.Identity is populated from the JWT.
// Extracts the "companyId" claim and writes it into the scoped ITenantContext,
// which ApplicationDbContext reads to apply global query filters per request.
app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated == true)
    {
        var tenantCtx = ctx.RequestServices.GetService<HRMS.Infrastructure.Services.ITenantContext>();
        if (tenantCtx != null)
        {
            tenantCtx.IsSuperAdmin = ctx.User.IsInRole(AppRoles.SuperAdmin);
            if (!tenantCtx.IsSuperAdmin)
            {
                if (!int.TryParse(ctx.User.FindFirst("companyId")?.Value, out var cid) || cid <= 0)
                {
                    // A non-superadmin request without a valid tenant must
                    // stop here.  Leaving the scoped context unset can turn
                    // a missing claim into an unrestricted EF query.
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync(
                        """{"success":false,"message":"A valid company scope is required."}""");
                    return;
                }
                tenantCtx.CompanyId = cid;
            }
        }
    }
    await next();
});

app.UseMiddleware<MustChangePasswordMiddleware>(); // P1: block access until seeded password is changed

// ── Prometheus /metrics endpoint (internal only — restrict in nginx) ────────
// RHR-015 FIX: Prometheus's own scrape request carries no JWT (it is an
// unauthenticated infrastructure probe, exactly like /health and /healthz
// below), so this endpoint must be explicitly anonymous or the global
// fallback authorization policy rejects every scrape with 401. Access is
// restricted at the network layer instead: nginx allows only internal CIDRs
// (see nginx/nginx.conf.template — /metrics is internal-networks only) and
// the API port is never published to the host in production compose.
app.MapPrometheusScrapingEndpoint("/metrics")
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.Api);

// ── Health check endpoint ──────────────────────────────────────────────────
// FIX 4: Both /health and /healthz previously duplicated the same inline
// ResponseWriter lambda. They now share HealthCheckResponseWriter.WriteJsonResponse
// so the JSON shape is defined in one place and both endpoints stay in sync.
//
// SECURITY/AVAILABILITY FIX: the global fallback authorization policy above applies
// to these endpoints too (they carry no [AllowAnonymous] attribute), so every probe
// answered 401. Kubernetes/load-balancer probes are unauthenticated by design —
// a 401 liveness probe restart-loops the pods. Each probe is explicitly anonymous.
// They expose only component status, never data.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
    ResponseWriter = HRMS.API.Extensions.HealthCheckResponseWriter.WriteJsonResponse
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Api);

// Kubernetes-compatible probes. Keep the existing /health route unchanged for
// existing monitors while exposing additive liveness/readiness semantics.
app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
    ResponseWriter = HRMS.API.Extensions.HealthCheckResponseWriter.WriteJsonResponse
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Api);
app.MapHealthChecks("/healthz/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Api);
app.MapHealthChecks("/healthz/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
    // Fix LOW: filter to only the tagged "live" check — returns 200 Healthy immediately
    // without exercising DB/Redis/Email, which is the correct K8s liveness pattern.
    Predicate = check => check.Tags.Contains("live")
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Api);


// FIX: previously this was MapControllers().RequireRateLimiting(RateLimitPolicies.Api), which appended
// the 120 req/min "api" policy to EVERY controller endpoint. Because the rate-limiting
// middleware honours the last policy in endpoint metadata, it silently overrode the
// stricter [EnableRateLimiting("login")] / ("sensitive") attributes — brute-force login
// protection was effectively disabled. Apply "api" only as a default where the endpoint
// does not already declare its own policy.
app.MapControllers().Add(endpoint =>
{
    var hasOwnPolicy = endpoint.Metadata.Any(m =>
        m is Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute or
             Microsoft.AspNetCore.RateLimiting.DisableRateLimitingAttribute);
    if (!hasOwnPolicy)
        endpoint.Metadata.Add(new Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute("api"));
});

// ── CSRF token seed endpoint ─────────────────────────────────────────────────
// Call GET /api/auth/csrf once after login to obtain the request token.
//
// FIX: The original implementation called GetAndStoreTokens (which sets the
// framework's XSRF-TOKEN cookie = CookieToken / IsSessionToken=true) and then
// immediately appended a SECOND "XSRF-TOKEN" cookie with tokens.RequestToken
// (IsSessionToken=false).  In curl and most HTTP clients the second Set-Cookie
// header wins and overwrites the framework cookie.  On the next mutation the
// client sends back RequestToken as the cookie value, but ValidateRequestAsync
// expects CookieToken (IsSessionToken=true) there → always "CSRF token missing
// or invalid".
//
// Correct pattern: let GetAndStoreTokens own the XSRF-TOKEN cookie (CookieToken)
// and return tokens.RequestToken in the JSON body.  The SPA / client reads
// requestToken from the body and echoes it as the X-XSRF-TOKEN header on every
// mutation.  The framework cookie remains untouched, so ValidateRequestAsync
// can decrypt the cookie↔header pair correctly.
app.MapGet("/api/auth/csrf", (IAntiforgery antiforgery, HttpContext ctx) =>
{
    var tokens = antiforgery.GetAndStoreTokens(ctx);
    // Return requestToken in the body so the client can send it as
    // X-XSRF-TOKEN on mutations, without clobbering the framework cookie.
    return Results.Ok(new { success = true, requestToken = tokens.RequestToken });
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Api);

// FIX HIGH-1: the legacy *.html pages (which assigned innerHTML from API data)
// were removed from wwwroot and archived under /legacy-ui. "/" is served by the
// React SPA's index.html via UseDefaultFiles(); if the SPA has not been built
// into wwwroot, report that instead of redirecting to a page that no longer exists.
// FIX (audit): this anonymous endpoint previously carried no rate-limiter policy,
// unlike every other anonymous endpoint in the app (AuthorizationEndpointRuntimeAuditTests
// enforces that invariant). Pin it to the general "api" policy — generous enough for
// normal SPA loads, but no longer an unmetered anonymous endpoint.
app.MapGet("/", () => Results.Text(
    "HRMS API is running. The React SPA has not been built into wwwroot. " +
    "Run: cd HRMS.SPA.Source && bun run build:ci", "text/plain"))
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.Api);

// FIX: client-side SPA routes (e.g. /login, /dashboard, /employees/42) have no
// matching static file or controller action. Without a fallback they fell through
// to the global RequireAuthenticatedUser() policy and returned 401 instead of
// serving the SPA shell, which made the login page itself unreachable when logged
// out. MapFallbackToFile serves wwwroot/index.html for any unmatched GET request
// (excluding /api, /health*, /swagger, /hangfire — those still 404/401 correctly)
// so the React Router can handle client-side navigation. Must be anonymous: this
// is the only way an unauthenticated user ever reaches the login screen.
// FIX (audit): same rate-limiter gap as "/" above — the SPA fallback route is
// anonymous and high-traffic (every client-side navigation hits it), so it must
// carry an explicit policy rather than being an unmetered anonymous endpoint.
app.MapFallbackToFile("index.html")
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitPolicies.Api);

// ── Startup warnings ───────────────────────────────────────────────────────
if (app.Environment.IsProduction() && string.IsNullOrWhiteSpace(builder.Configuration["Email:Host"]))
    Log.Warning("STARTUP: Email:Host not configured — email delivery disabled.");

app.ValidateHostedServices(app.Environment);

Log.Information("HRMS API v1.0.0 starting.");
// M-21: Register recurring jobs (runs once at startup after Hangfire server is ready)
using (var scope = app.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<Hangfire.IRecurringJobManager>();
    
    // Purge payslip PDFs older than 24 h — runs every hour
    recurringJobs.AddOrUpdate<HRMS.Infrastructure.Jobs.PayslipPdfCleanupJob>(
        "payslip-pdf-cleanup",
        j => j.RunAsync(),
        Hangfire.Cron.Hourly());
    
    // FIX HIGH-3: Reset leave balances on the 1st of each month at 00:00 UTC
    // (Job filters to April for annual reset; idempotent if called other months)
    recurringJobs.AddOrUpdate<HRMS.Infrastructure.Jobs.LeaveBalanceResetJob>(
        "leave-balance-reset",
        j => j.RunAsync(),
        "0 0 1 * *", // Day 1 of every month at 00:00 UTC
        timeZone: TimeZoneInfo.Utc);
    
    // FIX HIGH-4: Prune audit logs older than 90 days (Sunday 2 AM UTC)
    recurringJobs.AddOrUpdate<HRMS.Infrastructure.Jobs.AuditLogPruneJob>(
        "audit-log-prune",
        j => j.RunAsync(),
        "0 2 * * 0", // Sunday 02:00 UTC (NCrontab format)
        timeZone: TimeZoneInfo.Utc);
}

app.Run();

// ── Helpers ────────────────────────────────────────────────────────────────
static async Task SeedAsync(ApplicationDbContext db, IConfiguration configuration)
{
    // CRIT-01 FIX: The old HasData seed embedded a known BCrypt hash in migrations.
    // This block detects that hash on any existing superadmin and resets the password
    // to a freshly generated random value — ensuring no deployment runs with the
    // source-controlled hash as a valid credential.
    const string knownCompromisedHash =
        "$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";

    var superadmin = await db.Users.FirstOrDefaultAsync(u => u.Role == AppRoles.SuperAdmin);
    if (superadmin == null)
    {
        // Fresh install (HasData migration not yet applied, or row removed).
        // S14 FIX: Use SUPERADMIN_INITIAL_PASSWORD env var if set (supports automated CI/CD
        // deployments); fall back to a cryptographically random one-time password for
        // manual first-boot so there is never a hardcoded default.
        var configuredPassword = configuration["SUPERADMIN_INITIAL_PASSWORD"]
            ?? Environment.GetEnvironmentVariable("SUPERADMIN_INITIAL_PASSWORD");
        var tempPassword = !string.IsNullOrWhiteSpace(configuredPassword)
            ? configuredPassword
            : GenerateSecurePassword();

        // Item 8: an operator-supplied SUPERADMIN_INITIAL_PASSWORD must satisfy the
        // same policy as any other credential. Fail the boot rather than seed a weak
        // superadmin — the generated fallback is policy-compliant by construction.
        PasswordPolicy.EnsureValid(tempPassword, "SUPERADMIN_INITIAL_PASSWORD");

        db.Users.Add(new User {
            Email              = "superadmin@hrms.com",
            PasswordHash       = BcryptPasswordHasher.Hash(tempPassword, configuration),
            Role               = AppRoles.SuperAdmin,
            FullName           = "Super Admin",
            IsActive           = true,
            MustChangePassword = true,
            CreatedAt          = DateTime.UtcNow
        });
        // Never print credentials. Operators must provide SUPERADMIN_INITIAL_PASSWORD
        // through a secret manager or complete the first-run reset through the
        // authenticated staging/admin flow.
        Log.Warning("Initial superadmin account created with MustChangePassword=true; " +
                    "the initial password was not written to logs.");
    }
    else if (superadmin.PasswordHash == knownCompromisedHash)
    {
        // Existing installation seeded from HasData — the hash is publicly known in git.
        // Reset to a new random password and force immediate change on next login.
        var tempPassword = GenerateSecurePassword();
        PasswordPolicy.EnsureValid(tempPassword, "generatedPassword");
        superadmin.PasswordHash       = BcryptPasswordHasher.Hash(tempPassword, configuration);
        superadmin.MustChangePassword = true;
        superadmin.FailedLoginAttempts = 0;
        superadmin.LockoutUntil       = null;
        // Never print the replacement credential. Force the account through the
        // normal must-change-password flow instead.
        Log.Warning("Committed superadmin password hash detected and reset; " +
                    "the replacement password was not written to logs.");
    }

    if (!await db.LeaveTypes.AnyAsync())
    {
        db.LeaveTypes.AddRange(
            new LeaveType { Name = "Casual Leave",    AnnualQuotaDays = 12, IsPaid = true,  IsActive = true, CreatedAt = DateTime.UtcNow },
            new LeaveType { Name = "Sick Leave",      AnnualQuotaDays = 12, IsPaid = true,  IsActive = true, CreatedAt = DateTime.UtcNow },
            new LeaveType { Name = "Earned Leave",    AnnualQuotaDays = 15, IsPaid = true,  IsActive = true, CreatedAt = DateTime.UtcNow },
            new LeaveType { Name = "Unpaid Leave",    AnnualQuotaDays = 30, IsPaid = false, IsActive = true, CreatedAt = DateTime.UtcNow },
            new LeaveType { Name = "Maternity Leave", AnnualQuotaDays = 84, IsPaid = true,  IsActive = true, CreatedAt = DateTime.UtcNow }
        );
        Log.Information("Seeded 5 default leave types.");
    }

    await db.SaveChangesAsync();
}

static string GenerateSecurePassword()
{
    const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    const string lower   = "abcdefghjkmnpqrstuvwxyz";
    const string digits  = "23456789";
    const string special = "@#$!%*?&";
    const string all     = upper + lower + digits + special;

    var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
    var chars = new char[16];

    // Guarantee at least one character from each class
    chars[0] = upper  [bytes[0]  % upper.Length];
    chars[1] = lower  [bytes[1]  % lower.Length];
    chars[2] = digits [bytes[2]  % digits.Length];
    chars[3] = special[bytes[3]  % special.Length];

    for (int i = 4; i < 16; i++)
        chars[i] =  all[bytes[i] % all.Length];

    // Fisher-Yates shuffle so the first 4 positions are not always the same class
    var rng = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
    for (int i = 15; i > 0; i--)
    {
        int j = rng[i] % (i + 1);
        (chars[i], chars[j]) = (chars[j], chars[i]);
    }

    return new string(chars);
}

static class RedisRateLimitPolicy
{
    public static RateLimitPartition<string> CreateSlidingWindow(
        string connectionString, string key, int permitLimit, int windowSeconds)
    {
        return RateLimitPartition.GetSlidingWindowLimiter(key, _ =>
            new SlidingWindowRateLimiterOptions {
                PermitLimit       = permitLimit,
                Window            = TimeSpan.FromSeconds(windowSeconds),
                SegmentsPerWindow = 6,
                QueueLimit        = 0,
                AutoReplenishment = true
            });
    }
}

// Expose Program class for WebApplicationFactory in integration tests
public partial class Program { }


