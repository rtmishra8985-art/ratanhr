using Hangfire;
// BLOCKER-1 FIX: Hangfire.InMemory (1.0.0) replaced the legacy Hangfire.MemoryStorage
// package. Its namespace is Hangfire.InMemory and the extension method is
// UseInMemoryStorage(), not UseMemoryStorage(). The csproj correctly references
// Hangfire.InMemory; only the using directive and method call were stale.
using Hangfire.InMemory;
using Hangfire.Redis.StackExchange;
using HRMS.Application.Interfaces;
using HRMS.Application.Interfaces.Biometric;
using HRMS.Application.Validators;
using HRMS.Infrastructure.BackgroundServices;
using HRMS.Infrastructure.Biometric;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.FileStorage;
using HRMS.Infrastructure.HealthChecks;
using HRMS.Infrastructure.Jobs;
using HRMS.Infrastructure.JWT;
using HRMS.Infrastructure.Payroll;
using HRMS.Infrastructure.Repositories;
using HRMS.Infrastructure.Security;
using HRMS.Infrastructure.Services;
using HRMS.API.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Threading.Channels;

namespace HRMS.API.Extensions;

/// <summary>
/// Extension methods that register application services on <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// Registers the shared application and infrastructure services used by the API.
    /// The database provider is configured without opening a network connection so
    /// startup remains deterministic; health checks and migrations own connectivity.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var primaryConnection =
            configuration.GetConnectionString("DefaultConnection")
            ?? configuration["Database:PrimaryConnection"]
            ?? string.Empty;
        var replicaConnection = configuration["Database:ReplicaConnection"];
        var useReplica = configuration.GetValue("Database:EnableReadReplica", false)
            && !string.IsNullOrWhiteSpace(replicaConnection);

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<FileUploadOptions>(configuration.GetSection("FileUpload"));
        services.Configure<ClamAvOptions>(configuration.GetSection("ClamAv"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ClamAvOptions>>().Value);

        static void ConfigureMySql(DbContextOptionsBuilder options, string connection)
            => options.UseMySql(
                connection,
                ServerVersion.Parse("8.4.11-mysql"),
                mysql => mysql.EnableRetryOnFailure(3));

        // The context carries the scoped ITenantContext used by EF query filters.
        // A singleton factory would resolve that scoped dependency from the root
        // provider when a background worker creates a context. Keep the factory
        // scoped so every request/worker scope gets the correct tenant context.
        services.AddDbContextFactory<ApplicationDbContext>(
            options => ConfigureMySql(options, primaryConnection),
            lifetime: ServiceLifetime.Scoped);

        if (useReplica)
        {
            services.AddDbContext<ReadReplicaDbContext>(options =>
                ConfigureMySql(options, replicaConnection!));
        }

        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<FileStorageService>(sp =>
            new FileStorageService(
                Path.Combine(
                    configuration["FileStorage:RootPath"]
                    ?? Path.Combine(AppContext.BaseDirectory, "uploads")),
                sp.GetService<IOptions<FileUploadOptions>>()));
        services.AddSingleton<IFileStorageService>(sp =>
            sp.GetRequiredService<FileStorageService>());

        var webhookChannel = Channel.CreateBounded<WebhookJob>(
            new BoundedChannelOptions(1_000)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });
        services.AddSingleton(webhookChannel);
        services.AddSingleton(webhookChannel.Reader);
        services.AddSingleton(webhookChannel.Writer);

        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IAppreciationService, AppreciationService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBiometricService, BiometricService>();
        services.AddScoped<IBonusDeductionService, BonusDeductionService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddScoped<ICompanyBranchService, CompanyBranchService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ICompanySettingsService, CompanySettingsService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IEmailQueueService, EmailQueueService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmployeeDocumentService, EmployeeDocumentService>();
        services.AddScoped<IEmployeeExitService, EmployeeExitService>();
        services.AddScoped<IEmployeePromotionService, EmployeePromotionService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IEmployeeTransferService, EmployeeTransferService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IGpsAttendanceService, GpsAttendanceService>();
        services.AddScoped<IHelpdeskService, HelpdeskService>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IPayrollLockGuard, PayrollLockGuard>();
        services.AddScoped<IPayrollService, PayrollService>();
        services.AddScoped<IPayslipService, PayslipService>();
        // FIX P3-1: single-use, subject-bound payslip PDF download tokens.
        // Singleton so bindings survive across requests (backed by IMemoryCache).
        services.AddSingleton<HRMS.API.Security.IPayslipDownloadTokenStore,
                              HRMS.API.Security.PayslipDownloadTokenStore>();
        services.AddScoped<IPerformanceService, PerformanceService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRecruitmentService, RecruitmentService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ISalaryStructureService, SalaryStructureService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IStreamingReportService, StreamingReportService>();
        services.AddScoped<ITimesheetService, TimesheetService>();
        services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<ITravelService, TravelService>();
        services.AddScoped<IWebhookService, WebhookService>();

        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IPayrollRepository, PayrollRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IHelpdeskRepository, HelpdeskRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IGenericRepository<HRMS.Domain.Entities.Employee.Employee>,
            GenericRepository<HRMS.Domain.Entities.Employee.Employee>>();

        services.AddSingleton<IPayrollCalculator, IndianPayrollCalculator>();
        services.AddSingleton<IClamAvVirusScanService, ClamAvVirusScanService>();
        // AntiVirusScanFilter is registered globally and depends on the
        // application-level interface. Keep the adapter registration explicit
        // so filter activation cannot fail for every request.
        services.AddSingleton<HRMS.Application.Interfaces.IVirusScanService,
            ClamAvVirusScanAdapter>();
        services.AddScoped<PayrollLockGuard>();
        services.AddSingleton<EmailQueueWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<EmailQueueWorker>());
        services.AddHostedService<WebhookDispatcherService>();
        services.AddHostedService<TokenCleanupService>();
        services.AddHostedService<BiometricLogCleanupService>();
        services.AddHostedService<BiometricHostedService>();
        services.AddScoped<IPayrollBulkLockService>(sp =>
            sp.GetService<IConnectionMultiplexer>() is { } mux
                ? new RedisPayrollBulkLockService(
                    mux,
                    sp.GetRequiredService<ILogger<RedisPayrollBulkLockService>>())
                : new InMemoryPayrollBulkLockService(
                    sp.GetRequiredService<ILogger<InMemoryPayrollBulkLockService>>()));

        services.AddBiometricCapabilities();
        services.AddScoped<IBiometricDeviceRepository, BiometricDeviceRepository>();
        services.AddScoped<IBiometricLogRepository, BiometricLogRepository>();
        services.AddScoped<IBiometricSyncHistoryRepository, BiometricSyncHistoryRepository>();
        services.AddScoped<BiometricSettingsRepository>();
        services.AddScoped<IBiometricDeviceService, BiometricDeviceService>();
        services.AddScoped<IBiometricSyncService, BiometricSyncService>();
        services.AddSingleton<IBiometricProvider, AnvizProvider>();
        services.AddSingleton<IBiometricProvider, EsslProvider>();
        services.AddSingleton<IBiometricProvider, HikvisionProvider>();
        services.AddSingleton<IBiometricProvider, MatrixProvider>();
        services.AddSingleton<IBiometricProvider, RealtimeProvider>();
        services.AddSingleton<IBiometricProvider, SupremaProvider>();
        services.AddSingleton<IBiometricProvider, ZKTecoProvider>();
        services.AddSingleton<IBiometricProviderFactory, BiometricProviderFactory>();

        var redisConnection =
            configuration["Redis:ConnectionString"]
            ?? configuration["REDIS_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            var multiplexer = ConnectionMultiplexer.Connect(redisConnection);
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        }

        services.AddHrmsValidators();
        return services;
    }

    /// <summary>Registers Hangfire with in-memory storage only for Development/Test.</summary>
    public static IServiceCollection AddHangfireJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environments.Production;
        var useInMemory = configuration.GetValue("Hangfire:UseInMemory", false)
            || environment.Equals(Environments.Development, StringComparison.OrdinalIgnoreCase)
            || environment.Equals("Test", StringComparison.OrdinalIgnoreCase)
            || environment.Equals("IntegrationTest", StringComparison.OrdinalIgnoreCase);

        if (useInMemory)
        {
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseInMemoryStorage());
        }
        else
        {
            // FIX: single source of truth for Redis. Hangfire-specific key wins,
            // then the shared Redis:ConnectionString, then the legacy env name.
            var redisConnection = configuration["Hangfire:RedisConnectionString"]
                ?? configuration["Redis:ConnectionString"]
                ?? configuration["REDIS_CONNECTION_STRING"];
            if (string.IsNullOrWhiteSpace(redisConnection))
                throw new InvalidOperationException(
                    "Hangfire:RedisConnectionString is required outside Development.");

            var multiplexer = ConnectionMultiplexer.Connect(redisConnection);
            if (!services.Any(d => d.ServiceType == typeof(IConnectionMultiplexer)))
                services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseRedisStorage(multiplexer, new RedisStorageOptions
                {
                    Prefix = "hangfire:",
                    SucceededListSize = 500,
                    DeletedListSize = 500
                }));
        }

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Max(1, Environment.ProcessorCount * 2);
            options.Queues = ["critical", "default", "low"];
        });
        return services;
    }

    /// <summary>Registers the protected Swagger/OpenAPI document.</summary>
    public static IServiceCollection AddSwaggerDocumentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "RatanHR API",
                Version = "v1"
            });
            options.AddSecurityDefinition("Bearer",
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header
                });
            options.OperationFilter<AuthorizeOperationFilter>();
        });
        return services;
    }

    /// <summary>Registers AES-256-GCM PII encryption when a key is configured.</summary>
    public static IServiceCollection AddEncryptionService(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var key = configuration["Security:EncryptionKey"]
            ?? configuration["ENCRYPTION_KEY"];
        if (!string.IsNullOrWhiteSpace(key))
        {
            services.AddSingleton<HRMS.Infrastructure.Security.AesEncryptionService>(_ => new HRMS.Infrastructure.Security.AesEncryptionService(key));
            services.AddSingleton<HRMS.Application.Interfaces.IEncryptionService>(sp =>
                sp.GetRequiredService<HRMS.Infrastructure.Security.AesEncryptionService>());
        }
        else if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Security:EncryptionKey is required outside Development.");
        }
        return services;
    }

    /// <summary>Registers RS256 JWT authentication using the configured public PEM key.</summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<IJwtService, JwtService>();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.SaveToken = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = BuildPublicKey(configuration),
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (string.IsNullOrWhiteSpace(context.Token)
                        && context.Request.Cookies.TryGetValue("hrms_access_token", out var cookie))
                    {
                        context.Token = cookie;
                    }
                    return Task.CompletedTask;
                }
            };
        });
        return services;
    }

    private static SecurityKey BuildPublicKey(IConfiguration configuration)
    {
        var pem = configuration["Jwt:PublicKeyPem"];
        if (string.IsNullOrWhiteSpace(pem))
            throw new InvalidOperationException(
                "Jwt:PublicKeyPem is required to configure JWT authentication.");
        var rsa = RSA.Create();
        rsa.ImportFromPem(PemKeyParser.Normalize(pem).AsSpan());
        return new RsaSecurityKey(rsa);
    }

    // NOTE: the duplicate AddHangfireWithStorage() registration path was removed
    // (Phase 1 blocker). AddHangfireJobs() above is the single Hangfire entry point.

    /// <summary>
    /// Configures ASP.NET Core <c>AllowedHosts</c> from the <c>ALLOWED_HOSTS</c>
    /// environment variable (semicolon-separated).
    ///
    /// The value must already have been validated by
    /// <see cref="Security.EnvironmentValidator.Validate"/> before this is called.
    /// If <c>ALLOWED_HOSTS</c> is missing the key is left unset, which lets the
    /// development fallback in appsettings.Development.json take effect.
    /// </summary>
    public static IServiceCollection AddAllowedHostsFromEnvironment(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedHosts = configuration["ALLOWED_HOSTS"];
        if (!string.IsNullOrWhiteSpace(allowedHosts))
        {
            // Overwrite the key that UseHostFiltering reads.
            // appsettings.Production.json must NOT contain a committed wildcard.
            ((IConfigurationRoot)configuration)
                .GetSection("AllowedHosts")
                .Value = allowedHosts;
        }
        return services;
    }

}

