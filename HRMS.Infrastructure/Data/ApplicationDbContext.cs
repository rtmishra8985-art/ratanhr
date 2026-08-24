using HRMS.Domain.Entities;
using HRMS.Domain.Entities.Assets;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Authentication;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Expense;
using HRMS.Domain.Entities.Helpdesk;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities.Onboarding;
using HRMS.Domain.Entities.Payroll;
using HRMS.Domain.Entities.Performance;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Domain.Entities.Training;
using HRMS.Domain.Entities.Travel;
using HRMS.Domain.Entities.Analytics;
using HRMS.Domain.Entities.Email;
using HRMS.Domain.Entities.Timesheet;
using HRMS.Domain.Entities.Sales;
using HRMS.Domain.Entities.Webhook;
using HRMS.Domain.Entities.DocumentManagement;
using HRMS.Domain.Entities.Compliance;
using HRMS.Domain.Entities.ProjectManagement;
using HRMS.Domain.Entities.Configuration;
using HRMS.Domain.Entities.Demo;
using HRMS.Infrastructure.Security;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Company = HRMS.Domain.Entities.Company.Company;
using CompanyBranch = HRMS.Domain.Entities.Company.CompanyBranch;
using CompanySettings = HRMS.Domain.Entities.Company.CompanySettings;

namespace HRMS.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    private readonly IConfiguration?  _config;
    private readonly ITenantContext?  _tenant;

    // ── Primary constructor (production) ─────────────────────────────────
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
                                IConfiguration? config = null,
                                ITenantContext? tenant = null)
        : base(options) { _config = config; _tenant = tenant; }

    /// <summary>
    /// True when PII value converters (AES-256-GCM) are part of this context's model.
    /// Used by <see cref="EncryptionAwareModelCacheKeyFactory"/> so EF Core never
    /// reuses a converter-free compiled model for an encryption-enabled context.
    /// </summary>
    internal bool PiiEncryptionEnabled
        => !string.IsNullOrWhiteSpace(_config?["Security:EncryptionKey"]);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, EncryptionAwareModelCacheKeyFactory>();
    }

    // ── Null-safe tenant filter helpers ───────────────────────────────────
    // EF Core's ParameterExtractingExpressionVisitor eagerly evaluates every
    // closure-captured member access in a HasQueryFilter expression — including
    // _tenant!.IsSuperAdmin, _tenant!.CompanyId.HasValue, and _tenant!.CompanyId —
    // before any || short-circuit can fire.  When _tenant is null that causes
    // NullReferenceException across all 35+ entity filters.
    //
    // Fix: encapsulate the three-part null/superadmin/noCompany check in a single
    // bool property that only uses safe null checks.  The entity filter then becomes
    // a simple two-arm OrElse that EF Core can evaluate without ever touching
    // _tenant members when _tenant is null.
    //
    //   _filterByTenant  →  true  means "actively restrict to one company"
    //                        false means "no restriction" (null / superadmin / no CompanyId)
    //   _tenantCompanyId →  null-safe read of the company to filter by
    //
    // Parameter extractor evaluates both properties safely (no member access on null).
    // When _filterByTenant is false the OrElse short-circuits before _tenantCompanyId
    // is compared, so even a null value there is harmless.
    private bool _filterByTenant =>
        _tenant != null && !_tenant.IsSuperAdmin && _tenant.CompanyId.HasValue;

    private int? _tenantCompanyId =>
        _tenant?.CompanyId;

    // ── Auth ──────────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();

    // ── Company ───────────────────────────────────────────────────────────
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyBranch> CompanyBranches => Set<CompanyBranch>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    // ── Employee ──────────────────────────────────────────────────────────
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeTransfer> EmployeeTransfers => Set<EmployeeTransfer>();
    public DbSet<EmployeePromotion> EmployeePromotions => Set<EmployeePromotion>();
    public DbSet<EmployeeExit> EmployeeExits => Set<EmployeeExit>();

    // ── Attendance ────────────────────────────────────────────────────────
    public DbSet<WebAttendance> WebAttendances => Set<WebAttendance>();
    public DbSet<ExcelAttendance> ExcelAttendances => Set<ExcelAttendance>();
    public DbSet<Shift> Shifts => Set<Shift>();

    // ── GPS Attendance ────────────────────────────────────────────────────
    public DbSet<AttendanceGps> AttendanceGpsLogs => Set<AttendanceGps>();
    public DbSet<GeoFence> GeoFences => Set<GeoFence>();
    public DbSet<GeoFenceHistory> GeoFenceHistories => Set<GeoFenceHistory>();
    public DbSet<AttendanceLocationAudit> AttendanceLocationAudits => Set<AttendanceLocationAudit>();
    public DbSet<AttendanceDevice> AttendanceDevices => Set<AttendanceDevice>();

    // ── Biometric ─────────────────────────────────────────────────────────
    public DbSet<BiometricDevice>      BiometricDevices       => Set<BiometricDevice>();
    public DbSet<BiometricLog>         BiometricLogs          => Set<BiometricLog>();
    public DbSet<BiometricSyncHistory> BiometricSyncHistories => Set<BiometricSyncHistory>();
    public DbSet<BiometricSettings>    BiometricSettings      => Set<BiometricSettings>();

    // ── Payroll ───────────────────────────────────────────────────────────
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<Bonus> Bonuses => Set<Bonus>();
    public DbSet<Deduction> Deductions => Set<Deduction>();
    /// <summary>Phase 1 – B: Payroll period lock records per company/month/year.</summary>
    public DbSet<PayrollLock> PayrollLocks => Set<PayrollLock>();

    // ── Leave ─────────────────────────────────────────────────────────────
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveBalanceAdjustment> LeaveBalanceAdjustments => Set<LeaveBalanceAdjustment>();

    // ── Organisation ──────────────────────────────────────────────────────
    public DbSet<HolidayCalendar> HolidayCalendars => Set<HolidayCalendar>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();

    // ── Notifications ─────────────────────────────────────────────────────
    public DbSet<Notification> Notifications => Set<Notification>();

    // ── Other ─────────────────────────────────────────────────────────────
    public DbSet<Appreciation> Appreciations => Set<Appreciation>();
    public DbSet<Permission> Permissions => Set<Permission>();

    // ── Security ──────────────────────────────────────────────────────────
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // ── Audit ─────────────────────────────────────────────────────────────
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<AnalyticsSnapshot> AnalyticsSnapshots => Set<AnalyticsSnapshot>();
    public DbSet<TimesheetEntry> TimesheetEntries => Set<TimesheetEntry>();
    /// <summary>Weekly-aggregate timesheet headers (each covers Mon–Sun).</summary>
    public DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public DbSet<EmailQueueItem> EmailQueue => Set<EmailQueueItem>();

    // ── Sales / Mini CRM ─────────────────────────────────────────────────
    public DbSet<SalesLead>           SalesLeads           => Set<SalesLead>();
    public DbSet<SalesCustomer>       SalesCustomers       => Set<SalesCustomer>();
    public DbSet<SalesFollowUp>       SalesFollowUps       => Set<SalesFollowUp>();
    public DbSet<SalesMeeting>        SalesMeetings        => Set<SalesMeeting>();
    public DbSet<SalesVisit>          SalesVisits          => Set<SalesVisit>();
    public DbSet<SalesTask>           SalesTasks           => Set<SalesTask>();
    public DbSet<SalesQuotation>      SalesQuotations      => Set<SalesQuotation>();
    public DbSet<SalesLeadAssignment> SalesLeadAssignments => Set<SalesLeadAssignment>();

    // ── Recruitment ───────────────────────────────────────────────────────
    public DbSet<JobRequisition> JobRequisitions => Set<JobRequisition>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<OfferLetter> OfferLetters => Set<OfferLetter>();

    // ── Performance ───────────────────────────────────────────────────────
    public DbSet<PerformanceCycle> PerformanceCycles => Set<PerformanceCycle>();
    public DbSet<EmployeeGoal> EmployeeGoals => Set<EmployeeGoal>();
    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();
    public DbSet<ContinuousFeedback> ContinuousFeedbacks => Set<ContinuousFeedback>();

    // ── Asset Management ──────────────────────────────────────────────────────
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<AssetHistory> AssetHistories => Set<AssetHistory>();

    // ── Helpdesk ──────────────────────────────────────────────────────────────
    public DbSet<HelpdeskTicket> HelpdeskTickets => Set<HelpdeskTicket>();
    public DbSet<HelpdeskCategory> HelpdeskCategories => Set<HelpdeskCategory>();
    public DbSet<HelpdeskComment> HelpdeskComments => Set<HelpdeskComment>();
    public DbSet<HelpdeskHistory> HelpdeskHistories => Set<HelpdeskHistory>();

    // ── Fixed: M1 — Training ──────────────────────────────────────────────────
    public DbSet<TrainingProgram>    TrainingPrograms    => Set<TrainingProgram>();
    public DbSet<TrainingEnrollment> TrainingEnrollments => Set<TrainingEnrollment>();

    // ── Fixed: M2 — Expenses ──────────────────────────────────────────────────
    public DbSet<ExpenseClaim>    ExpenseClaims    => Set<ExpenseClaim>();
    public DbSet<ExpenseApproval> ExpenseApprovals => Set<ExpenseApproval>();
    public DbSet<ExpenseHistory>  ExpenseHistories => Set<ExpenseHistory>();
    public DbSet<ExpenseItem>     ExpenseItems     => Set<ExpenseItem>();

    // ── Fixed: M6 — Travel ────────────────────────────────────────────────────
    public DbSet<TravelRequest>  TravelRequests  => Set<TravelRequest>();
    public DbSet<TravelApproval> TravelApprovals => Set<TravelApproval>();
    public DbSet<TravelHistory>  TravelHistories => Set<TravelHistory>();

    // ── Fixed: M7 — Onboarding ────────────────────────────────────────────────
    public DbSet<OnboardingTemplate> OnboardingTemplates => Set<OnboardingTemplate>();
    public DbSet<OnboardingRecord>   OnboardingRecords   => Set<OnboardingRecord>();

    // ── Fixed: M10 — Webhooks ─────────────────────────────────────────────────
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookOutbox>       WebhookOutbox        => Set<WebhookOutbox>();

    // ── NEW TABLES (Added 2026-08-15) ────────────────────────────────────────────────
    // Document Management
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();

    // Compliance Management
    public DbSet<ComplianceChecklist> ComplianceChecklists => Set<ComplianceChecklist>();
    public DbSet<ComplianceEvidence> ComplianceEvidences => Set<ComplianceEvidence>();

    // Employee Skills & Projects
    public DbSet<EmployeeSkill> EmployeeSkills => Set<EmployeeSkill>();
    public DbSet<ProjectAssignment> ProjectAssignments => Set<ProjectAssignment>();

    // Expense & Payroll
    public DbSet<ExpensePolicy> ExpensePolicies => Set<ExpensePolicy>();
    public DbSet<SalaryStructureComponent> SalaryStructureComponents => Set<SalaryStructureComponent>();

    // Employee Bank & Emergency Contact
    public DbSet<BankAccountDetail> BankAccountDetails => Set<BankAccountDetail>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();

    // Recognition & Awards
    public DbSet<AwardRecognition> AwardRecognitions => Set<AwardRecognition>();

    // Analytics & Configuration
    public DbSet<ApiAuditLog> ApiAuditLogs => Set<ApiAuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // Demo mode tracking
    public DbSet<DemoSeedTracker> DemoSeedTrackers => Set<DemoSeedTracker>();

    // Low FIX: auto-populate CreatedAt / UpdatedAt on every SaveChanges call
    // so callers never forget to stamp these fields manually.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Added)
            {
                var ci = entry.Entity.GetType().GetProperty("CreatedAt");
                if (ci != null && ci.CanWrite)
                {
                    var current = ci.GetValue(entry.Entity);
                    // Only set if still at default (Jan 1 0001) — preserve explicit values.
                    if (current is DateTime dt && dt == default)
                        ci.SetValue(entry.Entity, now);
                }
            }
            if (entry.State is Microsoft.EntityFrameworkCore.EntityState.Added
                             or Microsoft.EntityFrameworkCore.EntityState.Modified)
            {
                var ui = entry.Entity.GetType().GetProperty("UpdatedAt");
                if (ui != null && ui.CanWrite)
                    ui.SetValue(entry.Entity, now);
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Synchronous SaveChanges override — stamps CreatedAt/UpdatedAt identically to
    /// SaveChangesAsync so callers that use the sync path don't bypass audit timestamps.
    /// </summary>
    public override int SaveChanges()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Added)
            {
                var ci = entry.Entity.GetType().GetProperty("CreatedAt");
                if (ci != null && ci.CanWrite)
                {
                    var current = ci.GetValue(entry.Entity);
                    if (current is DateTime dt && dt == default)
                        ci.SetValue(entry.Entity, now);
                }
            }
            if (entry.State is Microsoft.EntityFrameworkCore.EntityState.Added
                             or Microsoft.EntityFrameworkCore.EntityState.Modified)
            {
                var ui = entry.Entity.GetType().GetProperty("UpdatedAt");
                if (ui != null && ui.CanWrite)
                    ui.SetValue(entry.Entity, now);
            }
        }
        return base.SaveChanges();
    }

    /// <summary>
    /// Optimistic-concurrency token configuration for the <c>row_version</c> columns.
    /// MySQL/PostgreSQL get a true database-generated row version via IsRowVersion().
    /// SQLite (used by the in-process test suite) has no server-generated row version:
    /// IsRowVersion() would mark the column store-generated, EF would send no value and
    /// the NOT NULL column would fail on insert. There the column is kept as a plain
    /// concurrency token whose value is supplied by the entity itself.
    /// </summary>
    private void ConfigureRowVersion(PropertyBuilder<byte[]> property)
    {
        var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        if (isSqlite)
            property.IsConcurrencyToken();
        else
            property.IsRowVersion();
    }


    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Apply all IEntityTypeConfiguration<T> implementations from this assembly.
        // AssetConfiguration and HelpdeskConfiguration are discovered automatically.
        mb.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(mb);

        // ── PII column encryption (AES-256-GCM at the app layer) ──────────
        var encryptionKey = _config?["Security:EncryptionKey"];
        // In production the encryption key is mandatory — PII must never be stored in plaintext.
        // BUG FIX (original): the previous check ANDed isProduction (true when the env var is
        // null OR "Production") with a second, stricter condition that only matched the literal
        // string "Production". When ASPNETCORE_ENVIRONMENT was unset/null (a very plausible
        // container misconfiguration), isProduction was true but the second condition was
        // false, so the throw never fired and PII silently persisted unencrypted.
        // REGRESSION FIX (this pass): the first fix above over-corrected and broke EF design-time
        // tooling. ApplicationDbContextFactory (used by every `dotnet ef` command) deliberately
        // constructs this context with config: null so migrations/scaffolding never need JWT
        // keys, Redis, or the encryption key. _config == null must remain a no-op here — only a
        // *present* configuration with a missing/blank ASPNETCORE_ENVIRONMENT should fail closed.
        if (_config is not null)
        {
            var envName = _config["ASPNETCORE_ENVIRONMENT"];
            var isProduction = string.IsNullOrWhiteSpace(envName) || envName == "Production";
            if (string.IsNullOrWhiteSpace(encryptionKey) && isProduction)
            {
                throw new InvalidOperationException(
                    "Security:EncryptionKey is not configured. " +
                    "Set the ENCRYPTION_KEY environment variable before starting the application in production.");
            }
        }
        ValueConverter<string?, string?>? piiConverter = null;
        if (!string.IsNullOrWhiteSpace(encryptionKey))
        {
            var enc = new HRMS.Infrastructure.Security.AesEncryptionService(encryptionKey);
            piiConverter = new ValueConverter<string?, string?>(
                v => enc.Encrypt(v),
                v => enc.Decrypt(v));
        }

        // ── User ──────────────────────────────────────────────────────────
        mb.Entity<User>(e => {
            e.ToTable("users"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Email).HasColumnName("email").IsRequired().HasMaxLength(255);
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
            e.Property(x => x.Role).HasColumnName("role").IsRequired().HasMaxLength(20);
            e.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255);
            e.Property(x => x.AdminRole).HasColumnName("admin_role").HasMaxLength(50);
            e.Property(x => x.TotpSecret).HasColumnName("totp_secret").HasMaxLength(500);
            e.Property(x => x.IsMfaEnabled).HasColumnName("is_mfa_enabled").HasDefaultValue(false);
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.ProfilePicturePath).HasColumnName("profile_picture_path").HasMaxLength(500);
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.MustChangePassword).HasColumnName("must_change_password").HasDefaultValue(false);
            e.Property(x => x.FailedLoginAttempts).HasColumnName("failed_login_attempts").HasDefaultValue(0);
            e.Property(x => x.LockoutUntil).HasColumnName("lockout_until");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // FIX: map soft-delete columns added to support AdminUserController.Delete.
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            e.HasIndex(x => x.Email).IsUnique();
        });

        // ── Role ──────────────────────────────────────────────────────────
        mb.Entity<Role>(e => {
            e.ToTable("roles"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(50);
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.IsSystemRole).HasColumnName("is_system_role").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ── RefreshToken ──────────────────────────────────────────────────
        mb.Entity<RefreshToken>(e => {
            e.ToTable("refresh_tokens"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.Property(x => x.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash");
            e.Property(x => x.MfaVerified).HasColumnName("mfa_verified").HasDefaultValue(false);
            e.HasIndex(x => x.TokenHash).IsUnique();
        });

        // ── PasswordResetToken ────────────────────────────────────────────
        mb.Entity<PasswordResetToken>(e => {
            e.ToTable("password_reset_tokens"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UsedAt).HasColumnName("used_at");
            e.HasIndex(x => x.TokenHash).IsUnique();
        });

        // ── Company ───────────────────────────────────────────────────────
        mb.Entity<Company>(e => {
            e.ToTable("companies"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyName).HasColumnName("company_name").IsRequired();
            e.Property(x => x.CompanyFounderName).HasColumnName("company_founder_name");
            e.Property(x => x.PhoneNumber).HasColumnName("phone_number");
            e.Property(x => x.EmailAddress).HasColumnName("email_address");
            e.Property(x => x.IndustryType).HasColumnName("industry_type");
            e.Property(x => x.BusinessType).HasColumnName("business_type");
            e.Property(x => x.CIN).HasColumnName("cin");
            e.Property(x => x.TIN).HasColumnName("tin");
            e.Property(x => x.PAN).HasColumnName("pan");
            e.Property(x => x.TAN).HasColumnName("tan");
            e.Property(x => x.AddressLine1).HasColumnName("address_line1");
            e.Property(x => x.AddressLine2).HasColumnName("address_line2");
            e.Property(x => x.City).HasColumnName("city");
            e.Property(x => x.StateProvince).HasColumnName("state_province");
            e.Property(x => x.Country).HasColumnName("country").HasDefaultValue("India");
            e.Property(x => x.PostalCode).HasColumnName("postal_code");
            e.Property(x => x.LogoPath).HasColumnName("logo_path");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── CompanyBranch ─────────────────────────────────────────────────
        mb.Entity<CompanyBranch>(e => {
            e.ToTable("company_branches"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.BranchName).HasColumnName("branch_name").IsRequired();
            e.Property(x => x.AddressLine1).HasColumnName("address_line1");
            e.Property(x => x.AddressLine2).HasColumnName("address_line2");
            e.Property(x => x.City).HasColumnName("city");
            e.Property(x => x.StateProvince).HasColumnName("state_province");
            e.Property(x => x.Country).HasColumnName("country");
            e.Property(x => x.PostalCode).HasColumnName("postal_code");
            e.Property(x => x.PhoneNumber).HasColumnName("phone_number");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.BranchManagerName).HasColumnName("branch_manager_name");
            e.Property(x => x.IsHeadOffice).HasColumnName("is_head_office").HasDefaultValue(false);
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── CompanySettings ───────────────────────────────────────────────
        mb.Entity<CompanySettings>(e => {
            e.ToTable("company_settings"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.WorkingDaysPerMonth).HasColumnName("working_days_per_month").HasDefaultValue(26);
            e.Property(x => x.PFPercentage).HasColumnName("pf_percentage").HasPrecision(5, 2).HasDefaultValue(12.00m);
            e.Property(x => x.ESIPercentage).HasColumnName("esi_percentage").HasPrecision(5, 2).HasDefaultValue(0.75m);
            e.Property(x => x.PTAmount).HasColumnName("pt_amount").HasPrecision(10, 2).HasDefaultValue(200.00m);
            e.Property(x => x.PayslipFooterNote).HasColumnName("payslip_footer_note");
            e.Property(x => x.TimeZone).HasColumnName("time_zone").HasDefaultValue("Asia/Kolkata");
            e.Property(x => x.CheckInTime).HasColumnName("check_in_time");
            e.Property(x => x.CheckOutTime).HasColumnName("check_out_time");
            e.Property(x => x.OvertimeThresholdMinutes).HasColumnName("overtime_threshold_minutes");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.CompanyId).IsUnique();
        });

        // ── Employee ──────────────────────────────────────────────────────
        mb.Entity<Employee>(e => {
            e.ToTable("employees"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            // employee_id is the string business key (EmployeeCode). The int EmployeeId
            // property is a [NotMapped] alias for the Id PK and must not own this column.
            e.Ignore(x => x.EmployeeId);
            e.Property(x => x.EmployeeCode).HasColumnName("employee_id").IsRequired().HasMaxLength(20);
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.FullName).HasColumnName("full_name").IsRequired().HasMaxLength(255);
            e.Property(x => x.Gender).HasColumnName("gender").HasMaxLength(20);
            e.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
            e.Property(x => x.Nationality).HasColumnName("nationality").HasMaxLength(100);
            e.Property(x => x.MaritalStatus).HasColumnName("marital_status").HasMaxLength(50);
            e.Property(x => x.BloodGroup).HasColumnName("blood_group").HasMaxLength(10);
            e.Property(x => x.PermanentAddress).HasColumnName("permanent_address");
            e.Property(x => x.CurrentAddress).HasColumnName("current_address");
            e.Property(x => x.Aadhaar).HasColumnName("aadhaar").HasMaxLength(500);
            e.Property(x => x.PAN).HasColumnName("pan").HasMaxLength(500);
            e.Property(x => x.IdentityDocs).HasColumnName("identity_docs");
            e.Property(x => x.MedicalConditions).HasColumnName("medical_conditions");
            e.Property(x => x.Hobbies).HasColumnName("hobbies");
            e.Property(x => x.Languages).HasColumnName("languages");
            e.Property(x => x.DateOfJoining).HasColumnName("date_of_joining");
            e.Property(x => x.Designation).HasColumnName("designation").HasMaxLength(200);
            e.Property(x => x.Department).HasColumnName("department").HasMaxLength(200);
            e.Property(x => x.Skills).HasColumnName("skills");
            e.Property(x => x.Responsibilities).HasColumnName("responsibilities");
            e.Property(x => x.BankAccountHolder).HasColumnName("bank_account_holder").HasMaxLength(500);
            e.Property(x => x.BankName).HasColumnName("bank_name").HasMaxLength(500);
            e.Property(x => x.BranchName).HasColumnName("branch_name").HasMaxLength(500);
            e.Property(x => x.AccountNumber).HasColumnName("account_number").HasMaxLength(500);
            e.Property(x => x.IFSCCode).HasColumnName("ifsc_code").HasMaxLength(500);
            e.Property(x => x.UAN).HasColumnName("uan").HasMaxLength(500);
            e.Property(x => x.Qualification).HasColumnName("qualification").HasMaxLength(200);
            e.Property(x => x.Institution).HasColumnName("institution").HasMaxLength(200);
            e.Property(x => x.YearOfPassing).HasColumnName("year_of_passing");
            e.Property(x => x.Specialization).HasColumnName("specialization").HasMaxLength(200);
            e.Property(x => x.EducationalDocs).HasColumnName("educational_docs");
            e.Property(x => x.PassportPhoto).HasColumnName("passport_photo");
            e.Property(x => x.PreviousEmployer).HasColumnName("previous_employer").HasMaxLength(200);
            e.Property(x => x.JobTitle).HasColumnName("job_title").HasMaxLength(200);
            e.Property(x => x.Duration).HasColumnName("duration").HasMaxLength(100);
            e.Property(x => x.ExpResponsibilities).HasColumnName("exp_responsibilities");
            e.Property(x => x.ExperienceDocs).HasColumnName("experience_docs");
            e.Property(x => x.EmergencyContactName).HasColumnName("emergency_contact_name").HasMaxLength(200);
            e.Property(x => x.EmergencyContactRelationship).HasColumnName("emergency_contact_relationship").HasMaxLength(100);
            e.Property(x => x.EmergencyContactPhone).HasColumnName("emergency_contact_phone").HasMaxLength(50);
            e.Property(x => x.EmergencyContactAddress).HasColumnName("emergency_contact_address");
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.EmployeeCode).IsUnique();

            // Apply PII encryption for sensitive fields
            if (piiConverter != null)
            {
                e.Property(x => x.Aadhaar).HasConversion(piiConverter);
                e.Property(x => x.PAN).HasConversion(piiConverter);
                e.Property(x => x.AccountNumber).HasConversion(piiConverter);
                e.Property(x => x.IFSCCode).HasConversion(piiConverter);
                e.Property(x => x.BankAccountHolder).HasConversion(piiConverter);
                e.Property(x => x.BankName).HasConversion(piiConverter);
                e.Property(x => x.BranchName).HasConversion(piiConverter);
                e.Property(x => x.UAN).HasConversion(piiConverter);
            }
            // P5: optional shift assignment
            e.Property(x => x.ShiftId).HasColumnName("shift_id");
        });

        // ── EmployeeDocument ──────────────────────────────────────────────
        mb.Entity<EmployeeDocument>(e => {
            e.ToTable("employee_documents"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.DocumentType).HasColumnName("document_type").IsRequired();
            e.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
            e.Property(x => x.IsVerified).HasColumnName("is_verified").HasDefaultValue(false);
            e.Property(x => x.UploadedAt).HasColumnName("uploaded_at");
            e.Property(x => x.VerifiedAt).HasColumnName("verified_at");
        });

        // ── EmployeeTransfer ──────────────────────────────────────────────
        mb.Entity<EmployeeTransfer>(e => {
            e.ToTable("employee_transfers"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            e.Property(x => x.FromDepartment).HasColumnName("from_department");
            e.Property(x => x.ToDepartment).HasColumnName("to_department");
            e.Property(x => x.FromCompanyId).HasColumnName("from_company_id");
            e.Property(x => x.ToCompanyId).HasColumnName("to_company_id");
            e.Property(x => x.EffectiveDate).HasColumnName("effective_date");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.Remarks).HasColumnName("remarks");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── EmployeePromotion ─────────────────────────────────────────────
        mb.Entity<EmployeePromotion>(e => {
            e.ToTable("employee_promotions"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            e.Property(x => x.FromDesignation).HasColumnName("from_designation");
            e.Property(x => x.ToDesignation).HasColumnName("to_designation");
            e.Property(x => x.EffectiveDate).HasColumnName("effective_date");
            e.Property(x => x.Remarks).HasColumnName("remarks");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // DB-2 fix: was unmapped, falling back to decimal(65,30).
            e.Property(x => x.SalaryIncrement).HasColumnName("salary_increment").HasPrecision(14, 2);
        });

        // ── EmployeeExit ──────────────────────────────────────────────────
        mb.Entity<EmployeeExit>(e => {
            e.ToTable("employee_exits"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            e.Property(x => x.ExitType).HasColumnName("exit_type").HasMaxLength(50);
            e.Property(x => x.NoticePeriodDays).HasColumnName("notice_period_days");
            e.Property(x => x.LastWorkingDate).HasColumnName("last_working_date");
            e.Property(x => x.ExitReason).HasColumnName("exit_reason");
            e.Property(x => x.IsCompleted).HasColumnName("is_completed").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            // DB-2 fix: these were unmapped decimal? columns, so EF Core's MySQL provider
            // fell back to its maximum-precision convention, decimal(65,30). That wastes
            // storage/index space and risks silent truncation on arithmetic with other
            // decimal(18,2)-scale columns. INR currency amounts need at most 2 decimal
            // places; 14 integer digits comfortably covers any real gratuity/settlement.
            e.Property(x => x.GratuityAmount).HasColumnName("gratuity_amount").HasPrecision(14, 2);
            e.Property(x => x.SettlementAmount).HasColumnName("settlement_amount").HasPrecision(14, 2);
        });

        mb.Entity<WebAttendance>(e => {
            e.ToTable("web_attendances"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            // Fix: CompanyId added so global query filter can scope reads per tenant.
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.AttDate).HasColumnName("att_date");
            e.Property(x => x.CheckIn).HasColumnName("check_in");
            e.Property(x => x.CheckOut).HasColumnName("check_out");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // Added for back-dated attendance audit (Phase 1 – C)
            e.Property(x => x.AdminEditReason).HasColumnName("admin_edit_reason").HasMaxLength(500);
            e.HasIndex(x => new { x.EmployeeId, x.AttDate })
                .IsUnique()
                .HasDatabaseName("ux_attendance_employee_date");
            e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_web_attendances_company_id");
        });

        // ── PayrollLock (Phase 1 – B) ─────────────────────────────────────
        mb.Entity<PayrollLock>(e => {
            e.ToTable("payroll_locks"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.Month).HasColumnName("month");
            e.Property(x => x.Year).HasColumnName("year");
            e.Property(x => x.IsLocked).HasColumnName("is_locked").HasDefaultValue(true);
            e.Property(x => x.LockedAt).HasColumnName("locked_at");
            e.Property(x => x.LockedByUserId).HasColumnName("locked_by_user_id");
            e.Property(x => x.UnlockedAt).HasColumnName("unlocked_at");
            e.Property(x => x.UnlockedByUserId).HasColumnName("unlocked_by_user_id");
            e.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
            // One lock entry per company/month/year (re-locked = updated in place)
            e.HasIndex(x => new { x.CompanyId, x.Month, x.Year }).IsUnique();
            // Phase 2d: Replaced UseXminAsConcurrencyToken() (PostgreSQL xmin) with IsRowVersion()
            // MySQL uses a TIMESTAMP(6) row-version column for optimistic concurrency.
            // EF Core raises DbUpdateConcurrencyException on concurrent lock/unlock writes.
            ConfigureRowVersion(e.Property(x => x.RowVersion).HasColumnName("row_version"));
        });

        // ── ExcelAttendance ───────────────────────────────────────────────
        mb.Entity<ExcelAttendance>(e => {
            e.ToTable("excel_attendances"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            e.Property(x => x.AttDate).HasColumnName("att_date");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.HoursWorked).HasColumnName("hours_worked").HasPrecision(14, 2);
            e.Property(x => x.CompanyId).HasColumnName("company_id");
        });

        // ── Shift ─────────────────────────────────────────────────────────
        mb.Entity<Shift>(e => {
            e.ToTable("shifts"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            e.Property(x => x.StartTime).HasColumnName("start_time");
            e.Property(x => x.EndTime).HasColumnName("end_time");
            e.Property(x => x.GracePeriodMinutes).HasColumnName("grace_period_minutes").HasDefaultValue(15);
            // P5 additions — shift-aware attendance thresholds
            e.Property(x => x.LateThresholdMinutes).HasColumnName("late_threshold_minutes").HasDefaultValue(0);
            e.Property(x => x.HalfDayThresholdHours).HasColumnName("half_day_threshold_hours").HasPrecision(4, 1).HasDefaultValue(4.0m);
            e.Property(x => x.EarlyExitThresholdMinutes).HasColumnName("early_exit_threshold_minutes").HasDefaultValue(60);
            e.Property(x => x.IsNightShift).HasColumnName("is_night_shift").HasDefaultValue(false);
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── Payslip ───────────────────────────────────────────────────────
        mb.Entity<Payslip>(e => {
            e.ToTable("payslips"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            // Fix: CompanyId added so global query filter can scope reads per tenant.
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_payslips_company_id");
            // Phase 1 remediation: database-level tenant FK. Previously company_id was
            // NOT NULL with only an index + global query filter, so a bad write could
            // orphan a payslip against a non-existent company. RESTRICT rather than the
            // CASCADE used by company_branches/company_settings: payslips are statutory
            // financial records and must block company deletion instead of vanishing.
            e.HasOne(x => x.Company)
             .WithMany()
             .HasForeignKey(x => x.CompanyId)
             .HasConstraintName("fk_payslips_company_id")
             .OnDelete(DeleteBehavior.Restrict);
            // Phase 2d: Replaced UseXminAsConcurrencyToken() (PostgreSQL xmin) with IsRowVersion()
            // Prevents concurrent payroll runs from silently overwriting payslip amounts.
            // MySQL uses a TIMESTAMP(6) row-version column for optimistic concurrency.
            ConfigureRowVersion(e.Property(x => x.RowVersion).HasColumnName("row_version"));
            e.Property(x => x.Month).HasColumnName("month");
            e.Property(x => x.Year).HasColumnName("year");
            e.Property(x => x.WorkingDays).HasColumnName("working_days");
            e.Property(x => x.DaysPresent).HasColumnName("days_present");
            e.Property(x => x.BasicPay).HasColumnName("basic_pay").HasPrecision(14, 2);
            e.Property(x => x.HRA).HasColumnName("hra").HasPrecision(14, 2);
            e.Property(x => x.DA).HasColumnName("da").HasPrecision(14, 2);
            e.Property(x => x.Conveyance).HasColumnName("conveyance").HasPrecision(14, 2);
            e.Property(x => x.MedicalAllowance).HasColumnName("medical_allowance").HasPrecision(14, 2);
            e.Property(x => x.OtherAllowances).HasColumnName("other_allowances").HasPrecision(14, 2);
            // Item 5 fix: new earnings components.
            e.Property(x => x.OvertimePay).HasColumnName("overtime_pay").HasPrecision(14, 2).HasDefaultValue(0m);
            e.Property(x => x.BonusAmount).HasColumnName("bonus_amount").HasPrecision(14, 2).HasDefaultValue(0m);
            e.Property(x => x.Arrears).HasColumnName("arrears").HasPrecision(14, 2).HasDefaultValue(0m);
            e.Property(x => x.GrossEarnings).HasColumnName("gross_earnings").HasPrecision(14, 2);
            e.Property(x => x.PFEmployee).HasColumnName("pf_employee").HasPrecision(14, 2);
            e.Property(x => x.PFEmployer).HasColumnName("pf_employer").HasPrecision(14, 2);
            e.Property(x => x.ESI).HasColumnName("esi").HasPrecision(14, 2);
            e.Property(x => x.PT).HasColumnName("pt").HasPrecision(14, 2);
            e.Property(x => x.TDS).HasColumnName("tds").HasPrecision(14, 2);
            e.Property(x => x.OtherDeductions).HasColumnName("other_deductions").HasPrecision(14, 2);
            e.Property(x => x.TotalDeductions).HasColumnName("total_deductions").HasPrecision(14, 2);
            e.Property(x => x.NetPay).HasColumnName("net_pay").HasPrecision(14, 2);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Generated");
            // FIX P1: DB-level guard against duplicate payslips for the same employee/period
            // (read-then-write in GeneratePayslipAsync is not race-safe on its own).
            e.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.Month, x.Year }).IsUnique();
        });

        // ── SalaryStructure ───────────────────────────────────────────────
        mb.Entity<SalaryStructure>(e => {
            e.ToTable("salary_structures"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            // Fix: CompanyId added so global query filter can scope reads per tenant.
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_salary_structures_company_id");
            e.Property(x => x.CTC).HasColumnName("ctc").HasPrecision(14, 2);
            e.Property(x => x.BasicPay).HasColumnName("basic_pay").HasPrecision(14, 2);
            e.Property(x => x.HRA).HasColumnName("hra").HasPrecision(14, 2);
            e.Property(x => x.DA).HasColumnName("da").HasPrecision(14, 2);
            e.Property(x => x.Conveyance).HasColumnName("conveyance").HasPrecision(14, 2);
            e.Property(x => x.MedicalAllowance).HasColumnName("medical_allowance").HasPrecision(14, 2);
            e.Property(x => x.OtherAllowances).HasColumnName("other_allowances").HasPrecision(14, 2);
            e.Property(x => x.PFEmployee).HasColumnName("pf_employee").HasPrecision(14, 2);
            e.Property(x => x.PFEmployer).HasColumnName("pf_employer").HasPrecision(14, 2);
            e.Property(x => x.ESI).HasColumnName("esi").HasPrecision(14, 2);
            e.Property(x => x.PT).HasColumnName("pt").HasPrecision(14, 2);
            e.Property(x => x.TDS).HasColumnName("tds").HasPrecision(14, 2);
            e.Property(x => x.EffectiveFrom).HasColumnName("effective_from");
            e.Property(x => x.EffectiveTo).HasColumnName("effective_to");
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // Tax regime fields (Phase 1 — AddOldRegimeTdsFields migration)
            e.Property(x => x.IsOldRegime).HasColumnName("is_old_regime").HasDefaultValue(false);
            e.Property(x => x.Section80CDeduction).HasColumnName("section_80c_deduction").HasPrecision(14, 2).HasDefaultValue(0m);
        });

        // ── Bonus ─────────────────────────────────────────────────────────
        mb.Entity<Bonus>(e => {
            e.ToTable("bonuses"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            // Fix: CompanyId added so global query filter can scope reads per tenant.
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_bonuses_company_id");
            e.Property(x => x.BonusType).HasColumnName("bonus_type").IsRequired().HasMaxLength(100);
            e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(14, 2);
            e.Property(x => x.Month).HasColumnName("month");
            e.Property(x => x.Year).HasColumnName("year");
            e.Property(x => x.Remarks).HasColumnName("remarks");
            e.Property(x => x.IsTaxable).HasColumnName("is_taxable").HasDefaultValue(true);
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── Deduction ─────────────────────────────────────────────────────
        mb.Entity<Deduction>(e => {
            e.ToTable("deductions"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            // Fix: CompanyId added so global query filter can scope reads per tenant.
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_deductions_company_id");
            e.Property(x => x.DeductionType).HasColumnName("deduction_type").IsRequired().HasMaxLength(100);
            e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(14, 2);
            e.Property(x => x.Month).HasColumnName("month");
            e.Property(x => x.Year).HasColumnName("year");
            e.Property(x => x.Remarks).HasColumnName("remarks");
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── LeaveType ─────────────────────────────────────────────────────
        mb.Entity<LeaveType>(e => {
            e.ToTable("leave_types"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            e.Property(x => x.AnnualQuotaDays).HasColumnName("annual_quota_days");
            e.Property(x => x.IsPaid).HasColumnName("is_paid");
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── LeaveRequest ──────────────────────────────────────────────────
        mb.Entity<LeaveRequest>(e => {
            e.ToTable("leave_requests"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.LeaveTypeId).HasColumnName("leave_type_id");
            e.Property(x => x.StartDate).HasColumnName("start_date");
            e.Property(x => x.EndDate).HasColumnName("end_date");
            e.Property(x => x.TotalDays).HasColumnName("total_days");
            e.Property(x => x.Reason).HasColumnName("reason");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Pending");
            e.Property(x => x.ApprovedByUserId).HasColumnName("approved_by_user_id");
            e.Property(x => x.ApproverRemarks).HasColumnName("approver_remarks");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.DecidedAt).HasColumnName("decided_at");
            e.HasIndex(x => new { x.EmployeeId, x.Status });
        });

        // ── LeaveBalance ──────────────────────────────────────────────────
        mb.Entity<LeaveBalance>(e => {
            e.ToTable("leave_balances");
            e.HasKey(x => x.BalanceId);
            e.Property(x => x.BalanceId).ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
        });

        // ── LeaveBalanceAdjustment ────────────────────────────────────────
        mb.Entity<LeaveBalanceAdjustment>(e => {
            e.ToTable("leave_balance_adjustments"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired().HasMaxLength(20);
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.LeaveTypeId).HasColumnName("leave_type_id");
            e.Property(x => x.Year).HasColumnName("year");
            e.Property(x => x.Days).HasColumnName("days");
            e.Property(x => x.Reason).HasColumnName("reason").IsRequired().HasMaxLength(500);
            e.Property(x => x.AdjustedByUserId).HasColumnName("adjusted_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year });
        });

        // ── HolidayCalendar ───────────────────────────────────────────────
        mb.Entity<HolidayCalendar>(e => {
            e.ToTable("holiday_calendars"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
            e.Property(x => x.Date).HasColumnName("date");
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            e.Property(x => x.IsOptional).HasColumnName("is_optional").HasDefaultValue(false);
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.CompanyId, x.Date });
        });

        // ── Department ────────────────────────────────────────────────────
        mb.Entity<Department>(e => {
            e.ToTable("departments"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── Designation ───────────────────────────────────────────────────
        mb.Entity<Designation>(e => {
            e.ToTable("designations"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── Notification ──────────────────────────────────────────────────
        mb.Entity<Notification>(e => {
            e.ToTable("notifications"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.Title).HasColumnName("title").IsRequired().HasMaxLength(300);
            e.Property(x => x.Message).HasColumnName("message").IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(20).HasDefaultValue("info");
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100);
            e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(100);
            e.Property(x => x.IsRead).HasColumnName("is_read").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ReadAt).HasColumnName("read_at");
            e.HasIndex(x => new { x.CompanyId, x.UserId, x.IsRead });
        });
        mb.Entity<Notification>().HasQueryFilter(n =>
            !_filterByTenant || n.CompanyId == _tenantCompanyId);

        // ── Appreciation ──────────────────────────────────────────────────
        mb.Entity<Appreciation>(e => {
            e.ToTable("appreciations"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.AwardTitle).HasColumnName("award_title").HasMaxLength(200);
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.CertificatePath).HasColumnName("certificate_path");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.AwardedByUserId).HasColumnName("awarded_by_user_id");
        });

        // ── Permission ────────────────────────────────────────────────────
        mb.Entity<Permission>(e => {
            e.ToTable("permissions"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Role).HasColumnName("role").IsRequired().HasMaxLength(50);
            e.Property(x => x.AddEmployee).HasColumnName("add_employee");
            e.Property(x => x.EditEmployee).HasColumnName("edit_employee");
            e.Property(x => x.ViewEmployee).HasColumnName("view_employee");
            e.Property(x => x.DeleteEmployee).HasColumnName("delete_employee");
            e.Property(x => x.AttendanceUpload).HasColumnName("attendance_upload");
            e.Property(x => x.PayrollGenerate).HasColumnName("payroll_generate");
            e.Property(x => x.ReportsAttendance).HasColumnName("reports_attendance");
            e.Property(x => x.ReportsEmployee).HasColumnName("reports_employee");
            e.Property(x => x.Appreciation).HasColumnName("appreciation");
            e.Property(x => x.LogoUpload).HasColumnName("logo_upload");
            e.Property(x => x.ManageAdminUsers).HasColumnName("manage_admin_users");
            e.Property(x => x.LeaveManagement).HasColumnName("leave_management").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Role).IsUnique();
        });

        // ── AuditLog ──────────────────────────────────────────────────────
        mb.Entity<AuditLog>(e => {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Action).HasColumnName("action").IsRequired().HasMaxLength(100);
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100);
            e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(100);
            e.Property(x => x.PerformedBy).HasColumnName("performed_by");
            e.Property(x => x.PerformedByName).HasColumnName("performed_by_name").HasMaxLength(200);
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            e.Property(x => x.Details).HasColumnName("details");
            e.Property(x => x.Success).HasColumnName("success").HasDefaultValue(true);
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.HasIndex(x => x.Action);
            e.HasIndex(x => x.OccurredAt);
            e.HasIndex(x => x.PerformedBy);
        });

        // ── Recruitment ───────────────────────────────────────────────────
        mb.Entity<JobRequisition>(e => {
            e.ToTable("job_requisitions"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(x => x.DepartmentName).HasColumnName("department_name").HasMaxLength(100);
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.OpeningsCount).HasColumnName("openings_count");
            e.Property(x => x.ExperienceRequired).HasColumnName("experience_required").HasMaxLength(100);
            e.Property(x => x.SkillsRequired).HasColumnName("skills_required");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
            e.Property(x => x.JobType).HasColumnName("job_type").HasMaxLength(30);
            e.Property(x => x.MinSalary).HasColumnName("min_salary").HasColumnType("numeric(18,2)");
            e.Property(x => x.MaxSalary).HasColumnName("max_salary").HasColumnType("numeric(18,2)");
            e.Property(x => x.Location).HasColumnName("location").HasMaxLength(200);
            e.Property(x => x.ClosingDate).HasColumnName("closing_date");
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.CompanyId);
        });

        mb.Entity<Candidate>(e => {
            e.ToTable("candidates"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
            e.Property(x => x.JobRequisitionId).HasColumnName("job_requisition_id");
            e.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
            e.Property(x => x.Address).HasColumnName("address");
            e.Property(x => x.CurrentDesignation).HasColumnName("current_designation").HasMaxLength(150);
            e.Property(x => x.CurrentCompany).HasColumnName("current_company").HasMaxLength(150);
            e.Property(x => x.TotalExperience).HasColumnName("total_experience").HasColumnType("numeric(4,1)");
            e.Property(x => x.Skills).HasColumnName("skills");
            e.Property(x => x.QualificationSummary).HasColumnName("qualification_summary");
            e.Property(x => x.ResumeFilePath).HasColumnName("resume_file_path").HasMaxLength(500);
            e.Property(x => x.SourceChannel).HasColumnName("source_channel").HasMaxLength(50);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.CompanyId);
        });

        mb.Entity<Interview>(e => {
            e.ToTable("interviews"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
            e.Property(x => x.CandidateId).HasColumnName("candidate_id").IsRequired();
            e.Property(x => x.JobRequisitionId).HasColumnName("job_requisition_id");
            e.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
            e.Property(x => x.InterviewType).HasColumnName("interview_type").HasMaxLength(50);
            e.Property(x => x.Venue).HasColumnName("venue").HasMaxLength(300);
            e.Property(x => x.InterviewerNames).HasColumnName("interviewer_names");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
            e.Property(x => x.FeedbackScore).HasColumnName("feedback_score");
            e.Property(x => x.FeedbackNotes).HasColumnName("feedback_notes");
            e.Property(x => x.Recommendation).HasColumnName("recommendation").HasMaxLength(30);
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.CompanyId);
        });

        mb.Entity<OfferLetter>(e => {
            e.ToTable("offer_letters"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
            e.Property(x => x.CandidateId).HasColumnName("candidate_id").IsRequired();
            e.Property(x => x.JobRequisitionId).HasColumnName("job_requisition_id");
            e.Property(x => x.OfferedDesignation).HasColumnName("offered_designation").HasMaxLength(150).IsRequired();
            e.Property(x => x.OfferedDepartment).HasColumnName("offered_department").HasMaxLength(100);
            e.Property(x => x.OfferedSalary).HasColumnName("offered_salary").HasColumnType("numeric(18,2)").IsRequired();
            e.Property(x => x.JoiningDate).HasColumnName("joining_date");
            e.Property(x => x.OfferIssuedAt).HasColumnName("offer_issued_at");
            e.Property(x => x.ExpiryDate).HasColumnName("expiry_date");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
            e.Property(x => x.ApprovedByUserId).HasColumnName("approved_by_user_id");
            e.Property(x => x.ApprovalNotes).HasColumnName("approval_notes");
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.CompanyId);
        });

        // ── Performance ───────────────────────────────────────────────────
        mb.Entity<PerformanceCycle>(e => {
            e.ToTable("performance_cycles"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.StartDate).HasColumnName("start_date");
            e.Property(x => x.EndDate).HasColumnName("end_date");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.ReviewType).HasColumnName("review_type").HasMaxLength(30);
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.CompanyId);
        });

        mb.Entity<EmployeeGoal>(e => {
            e.ToTable("employee_goals"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").HasMaxLength(50).IsRequired();
            e.Property(x => x.PerformanceCycleId).HasColumnName("performance_cycle_id");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.GoalType).HasColumnName("goal_type").HasMaxLength(30);
            e.Property(x => x.Category).HasColumnName("category").HasMaxLength(20);
            e.Property(x => x.TargetValue).HasColumnName("target_value").HasColumnType("numeric(18,4)");
            e.Property(x => x.AchievedValue).HasColumnName("achieved_value").HasColumnType("numeric(18,4)");
            e.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(30);
            e.Property(x => x.DueDate).HasColumnName("due_date");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
            e.Property(x => x.Weight).HasColumnName("weight");
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.CompanyId, x.EmployeeId });
        });

        mb.Entity<PerformanceReview>(e => {
            e.ToTable("performance_reviews"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").HasMaxLength(50).IsRequired();
            e.Property(x => x.ReviewerId).HasColumnName("reviewer_id");
            e.Property(x => x.PerformanceCycleId).HasColumnName("performance_cycle_id");
            e.Property(x => x.ReviewType).HasColumnName("review_type").HasMaxLength(20);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.SelfRating).HasColumnName("self_rating").HasColumnType("numeric(3,1)");
            e.Property(x => x.ManagerRating).HasColumnName("manager_rating").HasColumnType("numeric(3,1)");
            e.Property(x => x.FinalRating).HasColumnName("final_rating").HasColumnType("numeric(3,1)");
            e.Property(x => x.SelfComments).HasColumnName("self_comments");
            e.Property(x => x.ManagerComments).HasColumnName("manager_comments");
            e.Property(x => x.HrComments).HasColumnName("hr_comments");
            e.Property(x => x.OverallComments).HasColumnName("overall_comments");
            e.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            e.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.CompanyId, x.EmployeeId });
        });

        mb.Entity<ContinuousFeedback>(e => {
            e.ToTable("continuous_feedback"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
            e.Property(x => x.FromEmployeeId).HasColumnName("from_employee_id").HasMaxLength(50).IsRequired();
            e.Property(x => x.ToEmployeeId).HasColumnName("to_employee_id").HasMaxLength(50).IsRequired();
            e.Property(x => x.FeedbackText).HasColumnName("feedback_text").IsRequired();
            e.Property(x => x.FeedbackType).HasColumnName("feedback_type").HasMaxLength(20);
            e.Property(x => x.IsAnonymous).HasColumnName("is_anonymous");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.CompanyId, x.ToEmployeeId });
        });

        // FIX 6: Employee->Department FK
        mb.Entity<Employee>().HasOne(x => x.DepartmentEntity).WithMany()
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        mb.Entity<Employee>().Property(x => x.DepartmentId).HasColumnName("department_id");

        mb.Entity<AnalyticsSnapshot>(e => { e.ToTable("analytics_snapshots"); e.HasKey(x => x.Id); e.Property(x=>x.Id).HasColumnName("id").ValueGeneratedOnAdd(); e.Property(x=>x.CompanyId).HasColumnName("company_id"); e.Property(x=>x.SnapshotType).HasColumnName("snapshot_type").HasMaxLength(50); e.Property(x=>x.Period).HasColumnName("period").HasMaxLength(10); e.Property(x=>x.Value).HasColumnName("value").HasColumnType("numeric(18,4)"); e.Property(x=>x.Metadata).HasColumnName("metadata"); e.Property(x=>x.CreatedAt).HasColumnName("created_at"); });
        mb.Entity<TimesheetEntry>(e => { e.ToTable("timesheet_entries"); e.HasKey(x => x.Id); e.Property(x=>x.Id).HasColumnName("id").ValueGeneratedOnAdd(); e.Property(x=>x.CompanyId).HasColumnName("company_id"); e.Property(x=>x.EmployeeId).HasColumnName("employee_id").HasMaxLength(50); e.Property(x=>x.WorkDate).HasColumnName("work_date"); e.Property(x=>x.ProjectCode).HasColumnName("project_code").HasMaxLength(100); e.Property(x=>x.TaskDescription).HasColumnName("task_description"); e.Property(x=>x.HoursWorked).HasColumnName("hours_worked").HasColumnType("numeric(5,2)"); e.Property(x=>x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Draft"); e.Property(x=>x.ManagerRemarks).HasColumnName("manager_remarks"); e.Property(x=>x.ApprovedByUserId).HasColumnName("approved_by_user_id"); e.Property(x=>x.ApprovedAt).HasColumnName("approved_at"); e.Property(x=>x.CreatedAt).HasColumnName("created_at"); e.Property(x=>x.UpdatedAt).HasColumnName("updated_at"); });

        // ── Timesheet (weekly aggregate) ──────────────────────────────────
        mb.Entity<Timesheet>(e => {
            e.ToTable("timesheets"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").HasMaxLength(50);
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.WeekStartDate).HasColumnName("week_start_date");
            e.Property(x => x.WeekEndDate).HasColumnName("week_end_date");
            e.Property(x => x.TotalHours).HasColumnName("total_hours").HasColumnType("numeric(6,2)");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Draft");
            e.Property(x => x.ManagerRemarks).HasColumnName("manager_remarks");
            e.Property(x => x.ApprovedByUserId).HasColumnName("approved_by_user_id");
            e.Property(x => x.ApprovedAt).HasColumnName("approved_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.EmployeeId, x.WeekStartDate });
            e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_timesheets_company_id");
        });
        mb.Entity<EmailQueueItem>(e => { e.ToTable("email_queue"); e.HasKey(x => x.Id); e.Property(x=>x.Id).HasColumnName("id").ValueGeneratedOnAdd(); e.Property(x=>x.ToAddress).HasColumnName("to_address").HasMaxLength(320); e.Property(x=>x.Subject).HasColumnName("subject").HasMaxLength(500); e.Property(x=>x.HtmlBody).HasColumnName("html_body"); e.Property(x=>x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Pending"); e.Property(x=>x.RetryCount).HasColumnName("retry_count").HasDefaultValue(0); e.Property(x=>x.LastError).HasColumnName("last_error"); e.Property(x=>x.SentAt).HasColumnName("sent_at"); e.Property(x=>x.CreatedAt).HasColumnName("created_at"); e.Property(x=>x.NextRetryAt).HasColumnName("next_retry_at"); });

        // ── GPS Attendance ──────────────────────────────────────────────────
        mb.Entity<GeoFence>(e => {
            e.ToTable("geofences");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
            e.Property(x => x.FenceType).HasColumnName("fence_type").IsRequired().HasMaxLength(30).HasDefaultValue("Office");
            e.Property(x => x.Latitude).HasColumnName("latitude");
            e.Property(x => x.Longitude).HasColumnName("longitude");
            e.Property(x => x.RadiusMetres).HasColumnName("radius_metres").HasDefaultValue(200.0);
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.Address).HasColumnName("address");
            e.Property(x => x.AllowOutsideCheckin).HasColumnName("allow_outside_checkin").HasDefaultValue(false);
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            e.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasMany(x => x.History).WithOne(x => x.GeoFence)
                .HasForeignKey(x => x.GeoFenceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.GpsLogs).WithOne(x => x.GeoFence)
                .HasForeignKey(x => x.GeoFenceId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.CompanyId);
            e.HasIndex(x => new { x.CompanyId, x.IsActive });
        });

        mb.Entity<GeoFenceHistory>(e => {
            e.ToTable("geofence_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.GeoFenceId).HasColumnName("geofence_id");
            e.Property(x => x.Action).HasColumnName("action").IsRequired().HasMaxLength(50);
            e.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(100);
            e.Property(x => x.ChangeDetails).HasColumnName("change_details");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.GeoFence).WithMany(x => x.History)
                .HasForeignKey(x => x.GeoFenceId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.GeoFenceId);
        });

        mb.Entity<AttendanceGps>(e => {
            e.ToTable("attendance_gps");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.WebAttendanceId).HasColumnName("web_attendance_id");
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired().HasMaxLength(100);
            e.Property(x => x.Latitude).HasColumnName("latitude");
            e.Property(x => x.Longitude).HasColumnName("longitude");
            e.Property(x => x.Accuracy).HasColumnName("accuracy");
            e.Property(x => x.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(20).HasDefaultValue("CheckIn");
            e.Property(x => x.Timestamp).HasColumnName("timestamp");
            e.Property(x => x.GeoFenceId).HasColumnName("geofence_id");
            e.Property(x => x.DistanceMetres).HasColumnName("distance_metres");
            e.Property(x => x.IsInsideGeofence).HasColumnName("is_inside_geofence");
            e.Property(x => x.DeviceType).HasColumnName("device_type").HasMaxLength(50);
            e.Property(x => x.Browser).HasColumnName("browser").HasMaxLength(200);
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
            e.Property(x => x.Network).HasColumnName("network").HasMaxLength(30);
            e.Property(x => x.BatteryLevel).HasColumnName("battery_level");
            e.Property(x => x.GpsStatus).HasColumnName("gps_status").HasMaxLength(30);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.GeoFence).WithMany(x => x.GpsLogs)
                .HasForeignKey(x => x.GeoFenceId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.CompanyId);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.GeoFenceId);
        });

        mb.Entity<AttendanceLocationAudit>(e => {
            e.ToTable("attendance_location_audit");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired().HasMaxLength(100);
            e.Property(x => x.Latitude).HasColumnName("latitude");
            e.Property(x => x.Longitude).HasColumnName("longitude");
            e.Property(x => x.Accuracy).HasColumnName("accuracy");
            e.Property(x => x.GeoFenceId).HasColumnName("geofence_id");
            e.Property(x => x.DistanceMetres).HasColumnName("distance_metres");
            e.Property(x => x.IsInsideGeofence).HasColumnName("is_inside_geofence");
            e.Property(x => x.WasAllowed).HasColumnName("was_allowed");
            e.Property(x => x.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(20);
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
            e.Property(x => x.Browser).HasColumnName("browser").HasMaxLength(200);
            e.Property(x => x.DeviceType).HasColumnName("device_type").HasMaxLength(50);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.CompanyId, x.EmployeeId });
            e.HasIndex(x => x.CreatedAt);
        });

        mb.Entity<AttendanceDevice>(e => {
            e.ToTable("attendance_devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired().HasMaxLength(100);
            e.Property(x => x.DeviceFingerprint).HasColumnName("device_fingerprint").IsRequired().HasMaxLength(512);
            e.Property(x => x.DeviceType).HasColumnName("device_type").HasMaxLength(50);
            e.Property(x => x.Browser).HasColumnName("browser").HasMaxLength(200);
            e.Property(x => x.LastIpAddress).HasColumnName("last_ip_address").HasMaxLength(50);
            e.Property(x => x.IsTrusted).HasColumnName("is_trusted").HasDefaultValue(true);
            e.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at");
            e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
            e.Property(x => x.UseCount).HasColumnName("use_count").HasDefaultValue(1);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.EmployeeId, x.DeviceFingerprint });
        });

        // ── Missing indexes (added by v6 audit) ──────────────────────────────
        mb.Entity<User>().HasIndex(x => x.CompanyId).HasDatabaseName("ix_users_company_id");
        mb.Entity<Employee>().HasIndex(x => x.CompanyId).HasDatabaseName("ix_employees_company_id");
        mb.Entity<WebAttendance>().HasIndex(x => x.AttDate).HasDatabaseName("ix_web_attendances_att_date");
        mb.Entity<Employee>().HasIndex(x => x.ShiftId).HasDatabaseName("ix_employees_shift_id");
        mb.Entity<ExcelAttendance>().HasIndex(x => x.EmployeeId).HasDatabaseName("ix_excel_attendances_employee_id");
        mb.Entity<ExcelAttendance>().HasIndex(x => x.CompanyId).HasDatabaseName("ix_excel_attendances_company_id");
        mb.Entity<ExcelAttendance>().HasIndex(x => x.AttDate).HasDatabaseName("ix_excel_attendances_att_date");
        mb.Entity<Shift>().HasIndex(x => x.CompanyId).HasDatabaseName("ix_shifts_company_id");

        // ── Item 6: folded in from db_performance.sql / db_indexes_fix.sql /
        //    db_softdelete_fix.sql (2026-08-11).
        //    The standalone .sql scripts are retired: they were applied out-of-band,
        //    were invisible to the EF model snapshot, and several referenced table or
        //    column names that do not exist in this schema (web_attendance,
        //    training_records, attendance_date, assets.employee_id). Everything below
        //    is verified against the model snapshot and ships as migration
        //    20260811080000_FoldDbScriptIndexes.
        //
        //    Deliberately NOT re-created (already covered, would be redundant):
        //      employees(company_id)                  -> ix_employees_company_id
        //      web_attendances(att_date)              -> ix_web_attendances_att_date
        //      web_attendances(employee_id)           -> ux_attendance_employee_date prefix
        //      payslips(company_id, employee_id)      -> unique (company,emp,month,year) prefix
        //      helpdesk_tickets(employee_id)          -> ix on raised_by_employee_id
        //    Soft-delete columns from db_softdelete_fix.sql (users.is_deleted/deleted_at,
        //    assets/appreciations/helpdesk_tickets deleted_at+updated_at,
        //    onboarding_records.deleted_at) already exist in the baseline migration, so
        //    only their supporting indexes are added here.

        // FK indexes
        mb.Entity<Employee>().HasIndex(x => x.UserId).HasDatabaseName("ix_employees_user_id");
        mb.Entity<Payslip>().HasIndex(x => x.EmployeeId).HasDatabaseName("ix_payslips_employee_id");
        mb.Entity<Bonus>().HasIndex(x => x.EmployeeId).HasDatabaseName("ix_bonuses_employee_id");
        mb.Entity<Deduction>().HasIndex(x => x.EmployeeId).HasDatabaseName("ix_deductions_employee_id");
        mb.Entity<EmployeeDocument>().HasIndex(x => x.EmployeeId).HasDatabaseName("ix_employee_documents_employee_id");
        mb.Entity<EmployeeTransfer>().HasIndex(x => x.EmployeeId).HasDatabaseName("ix_employee_transfers_employee_id");
        mb.Entity<EmployeePromotion>().HasIndex(x => x.EmployeeId).HasDatabaseName("ix_employee_promotions_employee_id");
        mb.Entity<EmployeeExit>().HasIndex(x => x.EmployeeId).HasDatabaseName("ix_employee_exits_employee_id");
        mb.Entity<RefreshToken>().HasIndex(x => x.UserId).HasDatabaseName("ix_refresh_tokens_user_id");
        mb.Entity<PasswordResetToken>().HasIndex(x => x.UserId).HasDatabaseName("ix_password_reset_tokens_user_id");
        mb.Entity<Asset>().HasIndex(x => x.AssignedToEmployeeId).HasDatabaseName("ix_assets_assigned_to_employee_id");
        mb.Entity<TrainingEnrollment>().HasIndex(x => x.EmployeeId).HasDatabaseName("ix_training_enrollments_employee_id");

        // Multi-tenant composite indexes (company_id + employee_id)
        mb.Entity<WebAttendance>().HasIndex(x => new { x.CompanyId, x.EmployeeId })
            .HasDatabaseName("ix_web_attendances_company_employee");
        mb.Entity<LeaveRequest>().HasIndex(x => new { x.CompanyId, x.EmployeeId })
            .HasDatabaseName("ix_leave_requests_company_employee");
        mb.Entity<Bonus>().HasIndex(x => new { x.CompanyId, x.EmployeeId })
            .HasDatabaseName("ix_bonuses_company_employee");
        mb.Entity<Deduction>().HasIndex(x => new { x.CompanyId, x.EmployeeId })
            .HasDatabaseName("ix_deductions_company_employee");

        // Date-range / period report indexes
        mb.Entity<LeaveRequest>().HasIndex(x => new { x.StartDate, x.EndDate })
            .HasDatabaseName("ix_leave_requests_start_end");
        mb.Entity<Payslip>().HasIndex(x => new { x.Month, x.Year })
            .HasDatabaseName("ix_payslips_month_year");

        // Soft-delete supporting indexes
        mb.Entity<User>().HasIndex(x => x.IsDeleted).HasDatabaseName("ix_users_is_deleted");
        mb.Entity<Asset>().HasIndex(x => new { x.CompanyId, x.DeletedAt })
            .HasDatabaseName("ix_assets_company_deleted");
        mb.Entity<Appreciation>().HasIndex(x => new { x.EmployeeId, x.DeletedAt })
            .HasDatabaseName("ix_appreciations_employee_deleted");
        mb.Entity<HelpdeskTicket>().HasIndex(x => new { x.CompanyId, x.DeletedAt })
            .HasDatabaseName("ix_helpdesk_tickets_company_deleted");
        mb.Entity<OnboardingRecord>().HasIndex(x => new { x.EmployeeId, x.DeletedAt })
            .HasDatabaseName("ix_onboarding_records_employee_deleted");



                // ── Seed data ─────────────────────────────────────────────────────
        // SECURITY FIX (CRIT-01): The HasData seed for the superadmin User has been REMOVED.
        // Reason: HasData bakes the password hash into every migration — committing a known
        // hash to source control means anyone with repo access knows the initial password.
        //
        // Replacement: SeedAsync in Program.cs generates a RANDOM password at first startup,
        // prints it to the application log, and forces MustChangePassword=true. It also
        // detects and resets any existing superadmin whose hash matches the old known hash.
        //
        // ACTION REQUIRED: Run a new migration to remove the HasData-seeded row:
        //   dotnet ef migrations add RemoveHardcodedSuperadminSeed --project HRMS.Infrastructure \
        //          --startup-project HRMS.API
        // Then run the SeedAsync-based startup once to create the superadmin with a random
        // password (printed to the log on first boot).
        //
        // DO NOT re-add HasData for User — leave seeding entirely to SeedAsync.

        mb.Entity<LeaveType>().HasData(
            new LeaveType { Id = 1, CompanyId = null, Name = "Casual Leave",  AnnualQuotaDays = 12, IsPaid = true,  IsActive = true, CreatedAt = new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc) },
            new LeaveType { Id = 2, CompanyId = null, Name = "Sick Leave",    AnnualQuotaDays = 8,  IsPaid = true,  IsActive = true, CreatedAt = new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc) },
            new LeaveType { Id = 3, CompanyId = null, Name = "Earned Leave",  AnnualQuotaDays = 15, IsPaid = true,  IsActive = true, CreatedAt = new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc) }
        );

        // ── CRIT-02 FIX: Global query filters for multi-tenancy ──────────────
        // Defense-in-depth: these filters auto-scope every EF Core read to the
        // caller's company. Service-layer .Where() guards remain as a second layer.
        //
        // Filter logic:
        //   • _tenant == null          → migration/test/background context — unrestricted
        //   • _tenant.IsSuperAdmin     → superadmin caller — cross-tenant access allowed
        //   • !_tenant.CompanyId.HasValue → superadmin via null CompanyId — unrestricted
        //   • otherwise                → restrict to caller's company
        //
        // Global query filters — applied to every DbSet read unless:
        //   • _tenant == null          → migration/test/background context — unrestricted
        //   • _tenant.IsSuperAdmin     → superadmin caller — cross-tenant access allowed
        //   • !_tenant.CompanyId.HasValue → superadmin via null CompanyId — unrestricted
        //   • entity.CompanyId == null → legacy record (visible to all companies)
        //
        // Fix: WebAttendance, Payslip, Bonus, Deduction, SalaryStructure now carry their
        // own CompanyId property and are protected directly by this filter layer, replacing
        // the previous service-layer WHERE guards as the primary defence.
        // FIX: soft-deleted Users are invisible to all EF queries automatically.
        // This ensures login, token refresh, and any future query path cannot
        // authenticate or surface a soft-deleted admin account without an explicit
        // IgnoreQueryFilters() call.
        mb.Entity<User>().HasQueryFilter(u => !u.IsDeleted);

        mb.Entity<Employee>().HasQueryFilter(e =>
            !_filterByTenant || e.CompanyId == _tenantCompanyId);

        // FIX CRITICAL-4: Also filter out soft-deleted WebAttendance records so they are
        // invisible to all queries by default. IgnoreQueryFilters() can be used in admin
        // reconciliation queries that need to inspect deleted records.
        mb.Entity<WebAttendance>().HasQueryFilter(a =>
            !a.IsDeleted &&
            (!_filterByTenant || a.CompanyId == _tenantCompanyId));

        mb.Entity<ExcelAttendance>().HasQueryFilter(a =>
            !_filterByTenant || a.CompanyId == _tenantCompanyId);

        mb.Entity<Shift>().HasQueryFilter(s =>
            !_filterByTenant || s.CompanyId == _tenantCompanyId);

        mb.Entity<LeaveRequest>().HasQueryFilter(r =>
            !_filterByTenant || r.CompanyId == _tenantCompanyId);

        // Payslip.CompanyId is non-nullable, so no null-tenant escape hatch here.
        mb.Entity<Payslip>().HasQueryFilter(p =>
            !_filterByTenant || p.CompanyId == _tenantCompanyId);

        mb.Entity<Bonus>().HasQueryFilter(b =>
            !_filterByTenant || b.CompanyId == _tenantCompanyId);

        mb.Entity<Deduction>().HasQueryFilter(d =>
            !_filterByTenant || d.CompanyId == _tenantCompanyId);

        mb.Entity<SalaryStructure>().HasQueryFilter(s =>
            !_filterByTenant || s.CompanyId == _tenantCompanyId);

        mb.Entity<ContinuousFeedback>().HasQueryFilter(f =>
            !_filterByTenant || f.CompanyId == _tenantCompanyId);

        mb.Entity<AnalyticsSnapshot>().HasQueryFilter(s =>
            !_filterByTenant || s.CompanyId == _tenantCompanyId);

        mb.Entity<TimesheetEntry>().HasQueryFilter(te =>
            !_filterByTenant || te.CompanyId == _tenantCompanyId);

        mb.Entity<Timesheet>().HasQueryFilter(ts =>
            !_filterByTenant || ts.CompanyId == _tenantCompanyId);

        // Soft-deleted geofences must remain in the database for audit/history,
        // but must be invisible to normal reads and relationship includes.
        // Administrative recovery/audit queries can explicitly use IgnoreQueryFilters().
        mb.Entity<GeoFence>().HasQueryFilter(f =>
            !f.IsDeleted &&
            (!_filterByTenant || f.CompanyId == _tenantCompanyId));

        // FIX (HIGH-TENANT): Recruitment entities lacked HasQueryFilter, allowing
        // cross-tenant reads on job requisitions, candidates, interviews, and offer letters.
        mb.Entity<JobRequisition>().HasQueryFilter(j =>
            !_filterByTenant || j.CompanyId == _tenantCompanyId);

        mb.Entity<Candidate>().HasQueryFilter(c =>
            !_filterByTenant || c.CompanyId == _tenantCompanyId);

        mb.Entity<Interview>().HasQueryFilter(i =>
            !_filterByTenant || i.CompanyId == _tenantCompanyId);

        mb.Entity<OfferLetter>().HasQueryFilter(o =>
            !_filterByTenant || o.CompanyId == _tenantCompanyId);

        // FIX (HIGH-TENANT): Performance entities lacked HasQueryFilter, allowing
        // cross-tenant reads on cycles, goals, and reviews.
        mb.Entity<PerformanceCycle>().HasQueryFilter(p =>
            !_filterByTenant || p.CompanyId == _tenantCompanyId);

        mb.Entity<EmployeeGoal>().HasQueryFilter(g =>
            !_filterByTenant || g.CompanyId == _tenantCompanyId);

        mb.Entity<PerformanceReview>().HasQueryFilter(r =>
            !_filterByTenant || r.CompanyId == _tenantCompanyId);

        // ── LOW-02 FIX: WebhookSubscription entity configuration ─────────────
        // Previously declared as DbSet<WebhookSubscription> with no model configuration,
        // meaning EF relied entirely on conventions with no indexes or constraints.
        mb.Entity<WebhookSubscription>(e => {
            e.ToTable("webhook_subscriptions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_webhook_subscriptions_company_id");
            e.HasIndex(x => x.IsActive).HasDatabaseName("ix_webhook_subscriptions_is_active");
        });

        // ── Biometric ────────────────────────────────────────────────────────
        mb.Entity<BiometricDevice>(e => {
            e.ToTable("biometric_devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            e.Property(x => x.VendorName).HasColumnName("vendor").HasMaxLength(30);
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            e.Property(x => x.Port).HasColumnName("port");
            e.Property(x => x.Location).HasColumnName("location").HasMaxLength(200);
            e.Property(x => x.SerialNumber).HasColumnName("serial_number").HasMaxLength(100);
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.IsEnabled).HasColumnName("is_enabled");
            e.Property(x => x.LastSyncAt).HasColumnName("last_sync_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_biometric_devices_company_id");
            e.HasIndex(x => new { x.CompanyId, x.IpAddress, x.Port })
             .HasDatabaseName("ix_biometric_devices_company_ip_port").IsUnique();
        });

        mb.Entity<BiometricLog>(e => {
            e.ToTable("biometric_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.BiometricDeviceId).HasColumnName("device_id");
            e.Property(x => x.UserId).HasColumnName("employee_id").HasMaxLength(50);
            e.Property(x => x.PunchedAt).HasColumnName("punch_time");
            e.Property(x => x.Direction).HasColumnName("direction").HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.DeviceSerial).HasColumnName("device_serial").HasMaxLength(100);
            e.Property(x => x.IsProcessed).HasColumnName("is_processed");
            e.Property(x => x.WebAttendanceId).HasColumnName("web_attendance_id");
            e.Property(x => x.SkipReason).HasColumnName("skip_reason");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_biometric_logs_company_id");
            e.HasIndex(x => x.BiometricDeviceId).HasDatabaseName("ix_biometric_logs_device_id");
            e.HasIndex(x => new { x.CompanyId, x.IsProcessed }).HasDatabaseName("ix_biometric_logs_company_processed");
            e.HasIndex(x => new { x.UserId, x.PunchedAt }).HasDatabaseName("ix_biometric_logs_employee_punch_time");
            // Bind the existing Device navigation to the existing BiometricDeviceId FK.
            // Previously this used HasOne<BiometricDevice>() (no navigation), so EF treated
            // BiometricLog.Device as a *separate* relationship and synthesised a shadow FK
            // property "DeviceId". Once the snake_case fallback convention below ran, that
            // shadow property was named "device_id" — the same column BiometricDeviceId is
            // explicitly mapped to — producing:
            //   'BiometricLog.BiometricDeviceId' and 'BiometricLog.DeviceId' are both mapped
            //   to column 'device_id' in 'biometric_logs'
            // which made the design-time model (and therefore every dotnet-ef command) fail.
            e.HasOne(x => x.Device).WithMany(d => d.Logs).HasForeignKey(x => x.BiometricDeviceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<BiometricSyncHistory>(e => {
            e.ToTable("biometric_sync_histories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.BiometricDeviceId).HasColumnName("device_id");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.Property(x => x.IsSuccess).HasColumnName("status");
            e.Property(x => x.RecordsCreated).HasColumnName("logs_created");
            e.Property(x => x.RecordsUpdated).HasColumnName("logs_updated");
            e.Property(x => x.RecordsSkipped).HasColumnName("logs_skipped");
            e.Property(x => x.ErrorMessage).HasColumnName("error_message");
            e.Property(x => x.IsAutomatic).HasColumnName("is_auto_sync");
            e.HasIndex(x => x.CompanyId).HasDatabaseName("ix_biometric_sync_histories_company_id");
            e.HasIndex(x => x.BiometricDeviceId).HasDatabaseName("ix_biometric_sync_histories_device_id");
            e.HasIndex(x => x.StartedAt).HasDatabaseName("ix_biometric_sync_histories_started_at");
            // Same shadow-FK collision as BiometricLog: the Device navigation was never bound
            // to BiometricDeviceId, so EF synthesised a second FK. BiometricDeviceId is
            // nullable here (vendor-level syncs have no single device), so the delete
            // behaviour is Cascade to match fk_biometric_sync_histories_device_id in
            // 20260802000001_MySqlFullSchema — no schema change, mapping only.
            e.HasOne(x => x.Device).WithMany(d => d.SyncHistories).HasForeignKey(x => x.BiometricDeviceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<BiometricSettings>(e => {
            e.ToTable("biometric_settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.AutoSyncEnabled).HasColumnName("auto_sync_enabled");
            e.Property(x => x.SyncIntervalMinutes).HasColumnName("sync_interval_minutes");
            e.Property(x => x.GraceTimeMinutes).HasColumnName("grace_time_minutes");
            e.Property(x => x.EnableDuplicatePunchDetection).HasColumnName("deduplicate_punches");
            e.Property(x => x.LogRetentionDays).HasColumnName("log_retention_days");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.CompanyId).IsUnique().HasDatabaseName("ix_biometric_settings_company_id");
        });

        mb.Entity<BiometricDevice>().HasQueryFilter(d =>
            !_filterByTenant || d.CompanyId == _tenantCompanyId);
        mb.Entity<BiometricLog>().HasQueryFilter(l =>
            !_filterByTenant || l.CompanyId == _tenantCompanyId);
        mb.Entity<BiometricSyncHistory>().HasQueryFilter(h =>
            !_filterByTenant || h.CompanyId == _tenantCompanyId);
        mb.Entity<BiometricSettings>().HasQueryFilter(s =>
            !_filterByTenant || s.CompanyId == _tenantCompanyId);

        // ── Part A fix: ToTable mappings for entities that had HasQueryFilter but no ToTable ──
        // Without these, EF uses PascalCase convention names instead of the snake_case SQL names.
        mb.Entity<TrainingProgram>().ToTable("training_programs");
        mb.Entity<TrainingEnrollment>().ToTable("training_enrollments");
        mb.Entity<ExpenseClaim>().ToTable("expense_claims");
        mb.Entity<TravelRequest>().ToTable("travel_requests");
        mb.Entity<HelpdeskTicket>().ToTable("helpdesk_tickets");

        // DB-2 fix: ExpenseClaim, ExpenseItem, and TravelRequest were otherwise fully
        // convention-mapped, so their decimal columns fell back to decimal(65,30).
        mb.Entity<ExpenseClaim>().Property(x => x.TotalAmount).HasPrecision(14, 2);
        mb.Entity<ExpenseClaim>().Property(x => x.TotalGst).HasPrecision(14, 2);
        mb.Entity<ExpenseItem>().Property(x => x.Amount).HasPrecision(14, 2);
        mb.Entity<ExpenseItem>().Property(x => x.GstAmount).HasPrecision(14, 2);
        mb.Entity<TravelRequest>().Property(x => x.AdvanceAmount).HasPrecision(14, 2);
        mb.Entity<TravelRequest>().Property(x => x.EstimatedCost).HasPrecision(14, 2);

        // ── FIX: Missing tenant filters for Travel, Expense, Training, Onboarding ──
        // These entities carry CompanyId but lacked HasQueryFilter, allowing cross-tenant
        // reads whenever service-layer WHERE guards were accidentally omitted.
        // Soft-deleted rows are excluded here as well: both entities carry IsDeleted but
        // had no global filter, so a query that forgot an explicit `!IsDeleted` guard
        // resurfaced deleted claims/requests in listings, totals and reports.
        mb.Entity<TravelRequest>().HasQueryFilter(tr =>
            !tr.IsDeleted && (!_filterByTenant || tr.CompanyId == null || tr.CompanyId == _tenantCompanyId));

        mb.Entity<ExpenseClaim>().HasQueryFilter(e =>
            !e.IsDeleted && (!_filterByTenant || e.CompanyId == null || e.CompanyId == _tenantCompanyId));


        mb.Entity<TrainingProgram>().HasQueryFilter(tp =>
            !_filterByTenant || tp.CompanyId == _tenantCompanyId);

        mb.Entity<OnboardingTemplate>().HasQueryFilter(ot =>
            !_filterByTenant || ot.CompanyId == _tenantCompanyId);

        // ── H-02 FIX: Asset, HelpdeskTicket, Appreciation, LeaveType, Department, CompanyBranch ──
        // These six entity types carried CompanyId but had no HasQueryFilter.
        // Any EF Core LINQ query on their DbSet could return cross-tenant rows when the
        // service layer omitted an explicit .Where(x => x.CompanyId == ...) guard.
        // CompanyId == null on Appreciation/LeaveType/Department means a system-wide global
        // record visible to all tenants (e.g. default leave types) — intentionally included.
        mb.Entity<Asset>(e => {
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });
        mb.Entity<Asset>().HasQueryFilter(a =>
            !a.IsDeleted &&
            (!_filterByTenant || a.CompanyId == _tenantCompanyId));

        mb.Entity<HelpdeskTicket>().HasQueryFilter(h =>
            !_filterByTenant || h.CompanyId == _tenantCompanyId);

        mb.Entity<Appreciation>().HasQueryFilter(a =>
            !_filterByTenant || a.CompanyId == null || a.CompanyId == _tenantCompanyId);

        // LeaveType.CompanyId == null → system-wide default visible to all tenants.
        mb.Entity<LeaveType>().HasQueryFilter(lt =>
            !_filterByTenant || lt.CompanyId == null || lt.CompanyId == _tenantCompanyId);

        // Department.CompanyId == null → global department visible to all tenants.
        mb.Entity<Department>().HasQueryFilter(d =>
            !_filterByTenant || d.CompanyId == null || d.CompanyId == _tenantCompanyId);

        mb.Entity<CompanyBranch>().HasQueryFilter(b =>
            !_filterByTenant || b.CompanyId == _tenantCompanyId);

        // ── Sales / Mini CRM ──────────────────────────────────────────────
        mb.Entity<SalesLead>(e => {
            e.ToTable("sales_leads"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.LeadNo).HasColumnName("lead_no").HasMaxLength(20);
            e.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(200);
            e.Property(x => x.ContactPerson).HasColumnName("contact_person").HasMaxLength(200);
            e.Property(x => x.Mobile).HasColumnName("mobile").HasMaxLength(20);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(200);
            e.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
            e.Property(x => x.State).HasColumnName("state").HasMaxLength(100);
            e.Property(x => x.Country).HasColumnName("country").HasMaxLength(100);
            e.Property(x => x.Address).HasColumnName("address");
            e.Property(x => x.LeadSource).HasColumnName("lead_source").HasMaxLength(100);
            e.Property(x => x.Industry).HasColumnName("industry").HasMaxLength(100);
            e.Property(x => x.EmployeeOwnerId).HasColumnName("employee_owner_id").HasMaxLength(50);
            e.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(20);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
            e.Property(x => x.Remarks).HasColumnName("remarks");
            e.Property(x => x.ExpectedValue).HasColumnName("expected_value").HasColumnType("numeric(18,2)");
            e.Property(x => x.NextFollowUpDate).HasColumnName("next_follow_up_date");
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            e.HasIndex(x => new { x.CompanyId, x.Status }).HasDatabaseName("ix_sales_leads_company_status");
        });

        mb.Entity<SalesCustomer>(e => {
            e.ToTable("sales_customers"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.CustomerCode).HasColumnName("customer_code").HasMaxLength(20);
            e.Property(x => x.Gst).HasColumnName("gst").HasMaxLength(20);
            e.Property(x => x.Pan).HasColumnName("pan").HasMaxLength(15);
            e.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(200);
            e.Property(x => x.BillingAddress).HasColumnName("billing_address");
            e.Property(x => x.ShippingAddress).HasColumnName("shipping_address");
            e.Property(x => x.ContactPerson).HasColumnName("contact_person").HasMaxLength(200);
            e.Property(x => x.ContactPhone).HasColumnName("contact_phone").HasMaxLength(20);
            e.Property(x => x.ContactEmail).HasColumnName("contact_email").HasMaxLength(200);
            e.Property(x => x.AssignedSalesPersonId).HasColumnName("assigned_sales_person_id").HasMaxLength(50);
            e.Property(x => x.SalesLeadId).HasColumnName("sales_lead_id");
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        });

        mb.Entity<SalesFollowUp>(e => {
            e.ToTable("sales_follow_ups"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.SalesLeadId).HasColumnName("sales_lead_id");
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.ReminderDate).HasColumnName("reminder_date");
            e.Property(x => x.ReminderTime).HasColumnName("reminder_time");
            e.Property(x => x.Mode).HasColumnName("mode").HasMaxLength(20);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        });

        mb.Entity<SalesMeeting>(e => {
            e.ToTable("sales_meetings"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.SalesLeadId).HasColumnName("sales_lead_id");
            e.Property(x => x.SalesCustomerId).HasColumnName("sales_customer_id");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(300);
            e.Property(x => x.MeetingDate).HasColumnName("meeting_date");
            e.Property(x => x.MeetingTime).HasColumnName("meeting_time");
            e.Property(x => x.Location).HasColumnName("location").HasMaxLength(300);
            e.Property(x => x.GoogleMapUrl).HasColumnName("google_map_url").HasMaxLength(500);
            e.Property(x => x.MeetingType).HasColumnName("meeting_type").HasMaxLength(20);
            e.Property(x => x.Outcome).HasColumnName("outcome");
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        });

        mb.Entity<SalesVisit>(e => {
            e.ToTable("sales_visits"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.SalesLeadId).HasColumnName("sales_lead_id");
            e.Property(x => x.SalesCustomerId).HasColumnName("sales_customer_id");
            e.Property(x => x.VisitedEmployeeId).HasColumnName("visited_employee_id").HasMaxLength(50);
            e.Property(x => x.CheckInLatitude).HasColumnName("check_in_latitude").HasColumnType("numeric(10,7)");
            e.Property(x => x.CheckInLongitude).HasColumnName("check_in_longitude").HasColumnType("numeric(10,7)");
            e.Property(x => x.CheckInAddress).HasColumnName("check_in_address");
            e.Property(x => x.CheckInPhotoPath).HasColumnName("check_in_photo_path").HasMaxLength(500);
            e.Property(x => x.CheckInTime).HasColumnName("check_in_time");
            e.Property(x => x.CheckOutTime).HasColumnName("check_out_time");
            e.Property(x => x.DurationMinutes).HasColumnName("duration_minutes");
            e.Property(x => x.DistanceKm).HasColumnName("distance_km").HasColumnType("numeric(10,2)");
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        });

        mb.Entity<SalesTask>(e => {
            e.ToTable("sales_tasks"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.SalesLeadId).HasColumnName("sales_lead_id");
            e.Property(x => x.SalesCustomerId).HasColumnName("sales_customer_id");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(300);
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.AssignedToEmployeeId).HasColumnName("assigned_to_employee_id").HasMaxLength(50);
            e.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(20);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
            e.Property(x => x.Deadline).HasColumnName("deadline");
            e.Property(x => x.ReminderDate).HasColumnName("reminder_date");
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        });

        mb.Entity<SalesQuotation>(e => {
            e.ToTable("sales_quotations"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.QuotationNumber).HasColumnName("quotation_number").HasMaxLength(20);
            e.Property(x => x.SalesLeadId).HasColumnName("sales_lead_id");
            e.Property(x => x.SalesCustomerId).HasColumnName("sales_customer_id");
            e.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
            e.Property(x => x.Tax).HasColumnName("tax").HasColumnType("numeric(18,2)");
            e.Property(x => x.Discount).HasColumnName("discount").HasColumnType("numeric(18,2)");
            e.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(18,2)");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.ValidUntil).HasColumnName("valid_until");
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        });

        // Tenant isolation filters for all Sales entities
        mb.Entity<SalesLead>().HasQueryFilter(l =>
            !_filterByTenant || l.CompanyId == _tenantCompanyId);
        mb.Entity<SalesCustomer>().HasQueryFilter(c =>
            !_filterByTenant || c.CompanyId == _tenantCompanyId);
        mb.Entity<SalesFollowUp>().HasQueryFilter(f =>
            !_filterByTenant || f.CompanyId == _tenantCompanyId);
        mb.Entity<SalesMeeting>().HasQueryFilter(m =>
            !_filterByTenant || m.CompanyId == _tenantCompanyId);
        mb.Entity<SalesVisit>().HasQueryFilter(v =>
            !_filterByTenant || v.CompanyId == _tenantCompanyId);
        mb.Entity<SalesTask>().HasQueryFilter(st =>
            !_filterByTenant || st.CompanyId == _tenantCompanyId);
        mb.Entity<SalesQuotation>().HasQueryFilter(q =>
            !_filterByTenant || q.CompanyId == _tenantCompanyId);
        mb.Entity<SalesLeadAssignment>().HasQueryFilter(a =>
            !_filterByTenant || a.CompanyId == _tenantCompanyId);

        // ── Audit fix: 8 entity types carrying CompanyId but missing HasQueryFilter ──
        // These filters close the cross-tenant data-leak identified in the security audit.
        // LeaveBalance / LeaveBalanceAdjustment / EmployeeDocument are always company-scoped.
        mb.Entity<LeaveBalance>().HasQueryFilter(lb =>
            !_filterByTenant || lb.CompanyId == _tenantCompanyId);

        mb.Entity<LeaveBalanceAdjustment>().HasQueryFilter(lba =>
            !_filterByTenant || lba.CompanyId == _tenantCompanyId);

        // HolidayCalendar.CompanyId == null → system-wide holiday visible to all tenants.
        mb.Entity<HolidayCalendar>().HasQueryFilter(h =>
            !_filterByTenant || h.CompanyId == null || h.CompanyId == _tenantCompanyId);

        // Designation.CompanyId == null → global designation visible to all tenants.
        mb.Entity<Designation>().HasQueryFilter(d =>
            !_filterByTenant || d.CompanyId == null || d.CompanyId == _tenantCompanyId);

        mb.Entity<EmployeeDocument>().HasQueryFilter(ed =>
            !_filterByTenant || ed.CompanyId == _tenantCompanyId);

        // EmployeePromotion / EmployeeTransfer / EmployeeExit: CompanyId added in
        // migration 20260803000001_AddCompanyIdToTenantEntities.
        mb.Entity<EmployeePromotion>().HasQueryFilter(ep =>
            !_filterByTenant || ep.CompanyId == _tenantCompanyId);

        mb.Entity<EmployeeTransfer>().HasQueryFilter(et =>
            !_filterByTenant || et.CompanyId == _tenantCompanyId);

        mb.Entity<EmployeeExit>().HasQueryFilter(ee =>
            !_filterByTenant || ee.CompanyId == _tenantCompanyId);

        // ── NEW TABLES QUERY FILTERS (Added 2026-08-15) ────────────────────────────────
        // Multi-tenant isolation for all 12 new tables
        
        // Document Management
        mb.Entity<DocumentTemplate>().HasQueryFilter(dt =>
            !_filterByTenant || dt.CompanyId == _tenantCompanyId);

        // Compliance Management
        mb.Entity<ComplianceChecklist>().HasQueryFilter(cc =>
            !_filterByTenant || cc.CompanyId == _tenantCompanyId);

        mb.Entity<ComplianceEvidence>().HasQueryFilter(ce =>
            !_filterByTenant || ce.CompanyId == _tenantCompanyId);

        // Employee Skills & Projects
        mb.Entity<EmployeeSkill>().HasQueryFilter(es =>
            !_filterByTenant || es.CompanyId == _tenantCompanyId);

        mb.Entity<ProjectAssignment>().HasQueryFilter(pa =>
            !_filterByTenant || pa.CompanyId == _tenantCompanyId);

        // Expense & Payroll
        mb.Entity<ExpensePolicy>().HasQueryFilter(ep =>
            !_filterByTenant || ep.CompanyId == _tenantCompanyId);

        mb.Entity<SalaryStructureComponent>().HasQueryFilter(ssc =>
            !_filterByTenant || ssc.CompanyId == _tenantCompanyId);

        // Employee Bank & Emergency Contact
        mb.Entity<BankAccountDetail>().HasQueryFilter(bad =>
            !_filterByTenant || bad.CompanyId == _tenantCompanyId);

        mb.Entity<EmergencyContact>().HasQueryFilter(ec =>
            !_filterByTenant || ec.CompanyId == _tenantCompanyId);

        // Recognition & Awards
        mb.Entity<AwardRecognition>().HasQueryFilter(ar =>
            !_filterByTenant || ar.CompanyId == _tenantCompanyId);

        // Analytics & Configuration
        mb.Entity<ApiAuditLog>().HasQueryFilter(aal =>
            !_filterByTenant || aal.CompanyId == _tenantCompanyId);

        mb.Entity<SystemSetting>().HasQueryFilter(ss =>
            !_filterByTenant || ss.CompanyId == _tenantCompanyId);

        // ── Demo mode tracking (DemoSeedTracker) ────────────────────────────
        // Not tenant-scoped: seed operations are global/system-level, not owned by a
        // single company, so no HasQueryFilter is applied here.
        mb.Entity<DemoSeedTracker>(e => {
            e.ToTable("demo_seed_trackers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.SeedVersion).HasColumnName("seed_version").HasMaxLength(20);
            e.Property(x => x.SeedRunId).HasColumnName("seed_run_id");
            e.Property(x => x.CreatedCompanyCount).HasColumnName("created_company_count");
            e.Property(x => x.CreatedEmployeeCount).HasColumnName("created_employee_count");
            e.Property(x => x.CreatedAttendanceCount).HasColumnName("created_attendance_count");
            e.Property(x => x.CreatedLeaveRequestCount).HasColumnName("created_leave_request_count");
            e.Property(x => x.CreatedPayslipCount).HasColumnName("created_payslip_count");
            e.Property(x => x.CreatedBonusCount).HasColumnName("created_bonus_count");
            e.Property(x => x.CreatedDeductionCount).HasColumnName("created_deduction_count");
            e.Property(x => x.CreatedCandidateCount).HasColumnName("created_candidate_count");
            e.Property(x => x.CreatedAssetCount).HasColumnName("created_asset_count");
            e.Property(x => x.CreatedUserCount).HasColumnName("created_user_count");
            e.Property(x => x.CreatedSkillCount).HasColumnName("created_skill_count");
            e.Property(x => x.CreatedProjectAssignmentCount).HasColumnName("created_project_assignment_count");
            e.Property(x => x.CreatedAwardCount).HasColumnName("created_award_count");
            e.Property(x => x.ExecutedAt).HasColumnName("executed_at");
            e.Property(x => x.Environment).HasColumnName("environment").HasMaxLength(30);
            e.Property(x => x.IsSuccess).HasColumnName("is_success");
            e.Property(x => x.ErrorMessage).HasColumnName("error_message");
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Ignore(x => x.TotalRecordsCreated);
            e.HasIndex(x => x.SeedVersion).HasDatabaseName("ix_demo_seed_trackers_seed_version");
        });

        // ── IsDemo columns (WebAttendance, Payslip, User, Asset, Candidate, LeaveRequest) ───
        // Explicit HasDefaultValue(false) so existing rows backfill to false on migration,
        // matching the CLR default already declared on each entity.
        mb.Entity<WebAttendance>().Property(x => x.IsDemo).HasColumnName("is_demo").HasDefaultValue(false);
        mb.Entity<Payslip>().Property(x => x.IsDemo).HasColumnName("is_demo").HasDefaultValue(false);
        mb.Entity<User>().Property(x => x.IsDemo).HasColumnName("is_demo").HasDefaultValue(false);
        mb.Entity<Asset>().Property(x => x.IsDemo).HasColumnName("is_demo").HasDefaultValue(false);
        mb.Entity<Candidate>().Property(x => x.IsDemo).HasColumnName("is_demo").HasDefaultValue(false);
        mb.Entity<LeaveRequest>().Property(x => x.IsDemo).HasColumnName("is_demo").HasDefaultValue(false);

        mb.Entity<SalesLeadAssignment>(e => {
            e.ToTable("sales_lead_assignments"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.SalesLeadId).HasColumnName("sales_lead_id");
            e.Property(x => x.AssignedToEmployeeId).HasColumnName("assigned_to_employee_id").HasMaxLength(50);
            e.Property(x => x.AssignedByUserId).HasColumnName("assigned_by_user_id");
            e.Property(x => x.ReassignedFromEmployeeId).HasColumnName("reassigned_from_employee_id").HasMaxLength(50);
            e.Property(x => x.ActionType).HasColumnName("action_type").HasMaxLength(20).HasDefaultValue("Assigned");
            e.Property(x => x.Remarks).HasColumnName("remarks");
            e.Property(x => x.AssignedAt).HasColumnName("assigned_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            e.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.SalesLeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CompanyId, x.SalesLeadId }).HasDatabaseName("ix_sales_lead_assignments_company_lead");
            e.HasIndex(x => x.AssignedToEmployeeId).HasDatabaseName("ix_sales_lead_assignments_employee");
            // Phase 2 — integer FK columns
            e.Property<int?>("AssignedToEmployeeFk").HasColumnName("assigned_to_employee_fk");
            e.Property<int?>("ReassignedFromEmployeeFk").HasColumnName("reassigned_from_employee_fk");
        });

        // ── Phase 2 — OnboardingTemplate: map new Description / Title columns ────
        mb.Entity<OnboardingTemplate>(e => {
            e.ToTable("onboarding_templates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(500);
            e.Property(x => x.Description).HasColumnName("description");
            // MySQL 8.4 rejects defaults on TEXT/LONGTEXT columns. The entity
            // initializer supplies the empty JSON array for new records.
            e.Property(x => x.Steps).HasColumnName("steps").IsRequired();
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Ignore(x => x.DisplayTitle);
            e.Ignore(x => x.IsLegacyFormat);
        });

        // ── Phase 2 — OnboardingRecord: map new EmployeeFk integer FK column ─────
        mb.Entity<OnboardingRecord>(e => {
            e.ToTable("onboarding_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.EmployeeId).HasColumnName("employee_id").HasMaxLength(20).IsRequired();
            e.Property(x => x.EmployeeFk).HasColumnName("employee_fk");
            e.Property(x => x.TemplateId).HasColumnName("template_id");
            // MySQL 8.4 rejects defaults on TEXT/LONGTEXT columns. The entity
            // initializer supplies the empty JSON array for new records.
            e.Property(x => x.CompletedSteps).HasColumnName("completed_steps").IsRequired();
            e.Property(x => x.AssignedTo).HasColumnName("assigned_to");
            e.Property(x => x.DueDate).HasColumnName("due_date");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.Template).WithMany()
                .HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Phase 1 remediation — snake_case fallback naming convention ──────────
        // Root cause of the EF "pending model changes" drift: this context maps
        // tables/columns explicitly (ToTable/HasColumnName), but 12 entities and
        // ~300 properties were never given a mapping. For those, EF fell back to
        // the CLR/DbSet names (PascalCase) while every migration created
        // snake_case objects — so the model and the migrated schema disagreed
        // (e.g. the runtime "Unknown column 'b.CreatedAt'" failure).
        // This loop applies the repository's snake_case naming rule to ONLY the
        // objects with no explicit mapping; anything configured above is left
        // untouched, so no existing mapping or product behaviour changes.
        var irregularTableNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Entities whose migrated table name is not the plural of the DbSet name.
            ["asset_histories"]      = "asset_history",
            ["expense_attachment"]   = "expense_attachments",
            ["expense_histories"]    = "expense_history",
            ["helpdesk_histories"]   = "helpdesk_history",
            ["travel_histories"]     = "travel_history",
        };

        // NOTE ON THE ANNOTATION CHECK (this is the subtle part).
        //
        // The previous implementation skipped an entity when
        //   entityType.FindAnnotation(RelationalAnnotationNames.TableName) is null
        // That test never passed for the entities that actually needed fixing.
        // EF Core's built-in TableNameFromDbSetConvention *writes* the TableName
        // annotation for every entity exposed as a DbSet<T> (using the DbSet
        // property name, e.g. "AssetCategories"). The annotation was therefore
        // already present -- by convention, not by intent -- so the fallback body
        // never ran. These 11 entities kept PascalCase convention names while the
        // migrations created snake_case tables, which is exactly what kept
        // `has-pending-model-changes` returning true:
        //   AssetCategory, AssetHistory, ExpenseApproval, ExpenseHistory,
        //   ExpenseItem, HelpdeskCategory, HelpdeskComment, HelpdeskHistory,
        //   TravelApproval, TravelHistory, WebhookOutbox
        //
        // The correct discriminator is the annotation's *configuration source*,
        // not its existence: override names that came from a convention, and
        // leave anything configured explicitly (ToTable / HasColumnName) alone.
        foreach (var entityType in mb.Model.GetEntityTypes())
        {
            var conventionEntityType = (IConventionEntityType)entityType;

            // Owned types that share their owner's table must not be renamed --
            // giving them their own table name would split them out into a
            // separate table and silently change the schema.
            var isTableSplittingOwnedType =
                entityType.IsOwned() && entityType.FindOwnership() is { IsUnique: true };

            if (!isTableSplittingOwnedType
                && conventionEntityType.GetTableNameConfigurationSource() != ConfigurationSource.Explicit)
            {
                var tableName = ToSnakeCase(entityType.GetTableName() ?? entityType.ClrType.Name);
                if (irregularTableNames.TryGetValue(tableName, out var mapped)) tableName = mapped;
                entityType.SetTableName(tableName);
            }

            foreach (var property in entityType.GetProperties())
            {
                if (((IConventionProperty)property).GetColumnNameConfigurationSource()
                    != ConfigurationSource.Explicit)
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }
        }

        // ── Phase 2e: Explicitly set datetime(6) for all DateTime/DateTimeOffset columns ──
        // Pomelo defaults to datetime(6) for DateTime, but the MySQL migration spec requires
        // explicit HasColumnType declarations for all timestamp columns.
        foreach (var entityType in mb.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var clrType = property.ClrType;
                var unwrapped = Nullable.GetUnderlyingType(clrType) ?? clrType;
                if (unwrapped == typeof(DateTime) || unwrapped == typeof(DateTimeOffset))
                {
                    property.SetColumnType("datetime(6)");
                }
            }
        }

        // ── Phase 1 fix: align CreatedAt with the MySQL column default ────────────
        // The MySQL schema declares audit columns as
        //     created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        // while the EF model carried no default at all, so scaffolding emitted
        //     defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
        // and every model-vs-database comparison reported drift on created_at.
        // Declaring the same default in the model removes the mismatch. SaveChanges /
        // SaveChangesAsync still stamp an explicit UTC value, so the database default
        // only applies to rows inserted outside EF (raw SQL, data-fix scripts).
        // SQLite (in-process test suite) has no CURRENT_TIMESTAMP(6) — skip it there.
        var supportsMySqlTimestampDefault =
            Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;
        if (supportsMySqlTimestampDefault)
        {
            foreach (var entityType in mb.Model.GetEntityTypes())
            {
                var createdAt = entityType.FindProperty("CreatedAt");
                if (createdAt is null) continue;
                var createdAtClrType =
                    Nullable.GetUnderlyingType(createdAt.ClrType) ?? createdAt.ClrType;
                if (createdAtClrType != typeof(DateTime)) continue;

                createdAt.SetDefaultValue(null);
                createdAt.SetDefaultValueSql("CURRENT_TIMESTAMP(6)");
                createdAt.ValueGenerated = ValueGenerated.OnAdd;
            }
        }

    }

    /// <summary>
    /// Converts a PascalCase/camelCase identifier to the snake_case form used by
    /// every HRMS MySQL migration (e.g. "CreatedAt" -> "created_at",
    /// "TotalGST" -> "total_gst"). Used only as a fallback for model objects with
    /// no explicit ToTable/HasColumnName mapping.
    /// </summary>
    internal static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new System.Text.StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                var previousIsLower = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                var nextIsLower     = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (i > 0 && (previousIsLower || (char.IsUpper(name[i - 1]) && nextIsLower)))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
