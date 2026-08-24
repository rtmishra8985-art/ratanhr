# HRMS Full Architecture Structure - Complete Documentation

## 📐 Project Overview
**HRMS (Human Resource Management System)** is a production-grade, multi-tenant enterprise platform built with **Clean Architecture** principles.

---

## 🏗️ SOLUTION STRUCTURE

```
HRMS/
├── HRMS.Domain/                 # Core business logic & entities (Layer 1)
├── HRMS.Application/            # Use cases & business rules (Layer 2)
├── HRMS.Infrastructure/         # External services & persistence (Layer 3)
├── HRMS.API/                    # REST API & presentation (Layer 4)
├── HRMS.SPA.Source/            # React/TypeScript frontend (Separate)
├── HRMS.Tests/                 # Unit & integration tests
├── Dockerfile                   # Multi-stage build
├── docker-compose.yml           # Production stack
└── HRMS.sln                    # Visual Studio solution
```

---

## 🎯 LAYER 1: DOMAIN LAYER (`HRMS.Domain`)

**Purpose:** Contains pure business entities, enums, and domain logic with NO dependencies.

### Directory Structure

```
HRMS.Domain/
├── Entities/                    # Core domain objects
│   ├── Employee/
│   │   └── Employee.cs         # Multi-tenant employee entity
│   ├── Authentication/
│   │   ├── User.cs
│   │   └── Permission.cs
│   ├── Attendance/
│   │   ├── WebAttendance.cs
│   │   ├── ExcelAttendance.cs
│   │   └── BiometricLog.cs
│   ├── Leave/
│   │   ├── LeaveRequest.cs
│   │   ├── LeaveType.cs
│   │   ├── LeaveBalance.cs
│   │   └── LeaveBalanceAdjustment.cs
│   ├── Payroll/
│   │   ├── Payslip.cs
│   │   ├── SalaryStructure.cs
│   │   ├── Bonus.cs
│   │   └── Deduction.cs
│   ├── Company/
│   │   ├── Company.cs
│   │   ├── Department.cs
│   │   └── CompanyBranch.cs
│   ├── Recruitment/
│   │   ├── Candidate.cs
│   │   ├── Interview.cs
│   │   ├── JobRequisition.cs
│   │   └── OfferLetter.cs
│   ├── Performance/
│   │   ├── PerformanceCycle.cs
│   │   ├── EmployeeGoal.cs
│   │   └── PerformanceReview.cs
│   ├── Biometric/
│   │   ├── BiometricDevice.cs
│   │   ├── BiometricLog.cs
│   │   ├── BiometricSyncHistory.cs
│   │   └── BiometricSettings.cs
│   ├── Sales/
│   │   ├── SalesLead.cs
│   │   ├── SalesCustomer.cs
│   │   ├── SalesQuotation.cs
│   │   ├── SalesMeeting.cs
│   │   ├── SalesVisit.cs
│   │   └── SalesTask.cs
│   ├── Travel/
│   │   ├── TravelRequest.cs
│   │   ├── TravelApproval.cs
│   │   └── TravelHistory.cs
│   ├── Expense/
│   │   ├── ExpenseClaim.cs
│   │   ├── ExpenseItem.cs
│   │   ├── ExpenseApproval.cs
│   │   └── ExpensePolicy.cs
│   ├── Training/
│   │   ├── TrainingProgram.cs
│   │   └── TrainingEnrollment.cs
│   ├── Compliance/
│   │   ├── ComplianceChecklist.cs
│   │   └── ComplianceEvidence.cs
│   ├── ProjectManagement/
│   │   ├── ProjectAssignment.cs
│   │   └── EmployeeSkill.cs
│   ├── Helpdesk/
│   │   ├── HelpdeskTicket.cs
│   │   ├── HelpdeskCategory.cs
│   │   └── HelpdeskComment.cs
│   ├── Analytics/
│   │   └── AnalyticsSnapshot.cs
│   ├── DocumentManagement/
│   │   ├── DocumentTemplate.cs
│   │   └── EmployeeDocument.cs
│   ├── Onboarding/
│   │   ├── OnboardingTemplate.cs
│   │   └── OnboardingRecord.cs
│   ├── AuditLog.cs             # Compliance & audit trail
│   ├── Asset.cs                # Company assets
│   ├── Notification.cs         # System notifications
│   ├── HolidayCalendar.cs      # Holiday management
│   ├── Webhook.cs              # Event webhooks
│   └── Demo/
│       └── DemoSeedTracker.cs  # Demo mode tracking
│
├── Enums/                       # Domain constants
│   ├── LeaveStatus.cs
│   ├── EmployeeStatus.cs
│   ├── AttendanceStatus.cs
│   ├── BiometricDirection.cs
│   └── PayrollPeriod.cs
│
└── Common/
    └── ICompanyOwned.cs         # Interface: identifies multi-tenant entities
```

### Key Domain Entities

#### Employee Entity
```csharp
public class Employee : ICompanyOwned
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; }      // Auto-generated: EMP1234
    public int CompanyId { get; set; }             // Multi-tenant FK
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string FullName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Designation { get; set; }
    public int? DepartmentId { get; set; }        // FK to Department
    public Department? DepartmentEntity { get; set; }
    public DateOnly? DateOfJoining { get; set; }
    public string Status { get; set; } = "Active"; // Active|Inactive|Terminated
    public string? Aadhaar { get; set; }
    public string? PAN { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? BankAccountHolder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDemo { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

#### Multi-Tenancy: ICompanyOwned Interface
```csharp
public interface ICompanyOwned
{
    int? CompanyId { get; }
}
```
- All company-scoped entities implement this
- Enables EF Core global query filters for tenant isolation

---

## 🔄 LAYER 2: APPLICATION LAYER (`HRMS.Application`)

**Purpose:** Business logic, DTOs, validation, and service interfaces (NO infrastructure code).

### Directory Structure

```
HRMS.Application/
├── DTOs/                        # Data Transfer Objects
│   ├── Employee/
│   │   ├── CreateEmployeeDto.cs
│   │   ├── UpdateEmployeeDto.cs
│   │   └── EmployeeResponseDto.cs
│   ├── Auth/
│   │   ├── LoginDto.cs
│   │   ├── RegisterDto.cs
│   │   └── ChangePasswordDto.cs
│   ├── Payroll/
│   │   ├── CreateSalaryStructureDto.cs
│   │   ├── PayslipDto.cs
│   │   └── BonusDeductionDto.cs
│   ├── Leave/
│   │   ├── CreateLeaveRequestDto.cs
│   │   └── LeaveApprovalDto.cs
│   ├── Attendance/
│   │   ├── AttendanceCheckInDto.cs
│   │   └── AttendanceReportDto.cs
│   └── [Other domains]/
│
├── Interfaces/                  # Service contracts (repository pattern)
│   ├── IEmployeeService.cs
│   ├── IPayrollService.cs
│   ├── ILeaveService.cs
│   ├── IAttendanceService.cs
│   ├── IEmailService.cs
│   ├── IEmailQueueService.cs
│   ├── IAuthService.cs
│   ├── IBiometricService.cs
│   ├── IEncryptionService.cs
│   ├── ICacheService.cs
│   ├── IJwtService.cs
│   ├── IAnalyticsService.cs
│   ├── IReportService.cs
│   ├── IStreamingReportService.cs
│   ├── IWebhookService.cs
│   ├── IVirusScanService.cs
│   └── [~50+ total]
│
├── Validators/                  # FluentValidation rules
│   ├── CreateEmployeeValidator.cs
│   ├── CreateSalaryStructureValidator.cs
│   └── [Domain-specific]
│
└── Mapping/                     # AutoMapper profiles
    ├── EmployeeMapping.cs
    ├── PayrollMapping.cs
    └── [Domain-specific]
```

### Service Interfaces (50+ Services)

```csharp
// Authentication & Authorization
IAuthService
IAdminUserService
IMfaService
IPermissionService
IRoleService

// Employee Management
IEmployeeService
IEmployeeDocumentService
IEmployeeExitService
IEmployeePromotionService
IEmployeeTransferService

// Attendance & Time Tracking
IAttendanceService
IGpsAttendanceService
ITimesheetService

// Leave Management
ILeaveService
ILeaveBalanceManagement

// Payroll
IPayrollService
IPayrollCalculator
IPayrollLockGuard
IPayrollBulkLockService
ISalaryStructureService
IPayslipService
IBonusDeductionService

// Recruitment
IRecruitmentService

// Performance & Analytics
IPerformanceService
IAnalyticsService
IReportService
IStreamingReportService

// Compliance & Audit
IAuditService
IComplianceService

// Communication
IEmailService
IEmailQueueService
INotificationService

// External Integrations
IBiometricService
IVirusScanService
IWebhookService

// Infrastructure
ICacheService
IEncryptionService
IJwtService
```

### Sample DTO Structure
```csharp
public class CreateEmployeeDto
{
    [Required]
    public string FirstName { get; set; }
    
    [Required]
    public string LastName { get; set; }
    
    [EmailAddress]
    public string Email { get; set; }
    
    [RegularExpression(@"^\d{12}$", ErrorMessage = "Aadhaar must be 12 digits")]
    public string Aadhaar { get; set; }
    
    [PII] // Custom attribute: triggers encryption
    public string PhoneNumber { get; set; }
    
    public int DepartmentId { get; set; }
    public int CompanyId { get; set; }
}
```

---

## 🏢 LAYER 3: INFRASTRUCTURE LAYER (`HRMS.Infrastructure`)

**Purpose:** Database, external services, repositories, and implementations.

### Directory Structure

```
HRMS.Infrastructure/
├── Data/                        # Database context & configuration
│   ├── ApplicationDbContext.cs  # Main EF Core DbContext (100+ DbSets)
│   ├── ApplicationDbContextFactory.cs
│   ├── Configurations/          # Entity configuration
│   │   ├── AssetConfiguration.cs
│   │   └── HelpdeskConfiguration.cs
│   ├── EncryptionAwareModelCacheKeyFactory.cs
│   ├── ReadReplicaDbContext.cs  # Optional read-only context
│   └── ValueConverters/         # EF value conversions
│
├── Persistence/                 # Database persistence patterns
│   └── [EF Core configurations]
│
├── Migrations/                  # EF Core migrations
│   └── MySql/
│       ├── 20260726000001_MySqlInitialSchema.cs
│       ├── 20260803000001_AddCompanyIdToTenantEntities.cs
│       ├── 20260811080000_FoldDbScriptIndexes.cs
│       ├── 20260812000000_AddBiometricTables.cs
│       └── [50+ migrations total]
│
├── Repositories/                # Data access layer
│   ├── GenericRepository.cs     # Base CRUD operations
│   ├── IGenericRepository.cs
│   ├── EmployeeRepository.cs
│   ├── AttendanceRepository.cs
│   ├── PayrollRepository.cs
│   ├── AuditLogRepository.cs
│   ├── CompanyRepository.cs
│   ├── AssetRepository.cs
│   ├── HelpdeskRepository.cs
│   └── UserRepository.cs
│
├── Services/                    # Business logic implementations (50+ services)
│   ├── EmployeeService.cs
│   ├── AuthService.cs
│   ├── PayrollService.cs
│   ├── PayslipService.cs
│   ├── LeaveService.cs
│   ├── LeaveService.Approval.cs (partial class)
│   ├── AttendanceService.cs
│   ├── GpsAttendanceService.cs
│   ├── TimesheetService.cs
│   ├── RecruitmentService.cs
│   ├── PerformanceService.cs
│   ├── AnalyticsService.cs
│   ├── ReportService.cs
│   ├── StreamingReportService.cs
│   ├── BiometricService.cs
│   ├── EmailService.cs
│   ├── EmailQueueService.cs
│   ├── EmailQueueWorker.cs     # Background job for email delivery
│   ├── NotificationService.cs
│   ├── AuditService.cs
│   ├── CacheService.cs
│   ├── AesGcmEncryptionService.cs
│   ├── CompanyService.cs
│   ├── OnboardingService.cs
│   ├── TrainingService.cs
│   ├── TravelService.cs
│   ├── ExpenseService.cs
│   ├── AssetService.cs
│   ├── HelpdeskService.cs
│   ├── AppreciationService.cs
│   ├── RoleService.cs
│   ├── PermissionService.cs
│   ├── AdminUserService.cs
│   ├── WebhookService.cs
│   ├── WebhookDispatcherService.cs
│   ├── ClamAvVirusScanService.cs
│   ├── ClamAvVirusScanAdapter.cs
│   ├── EmployeeExitService.cs
│   ├── EmployeePromotionService.cs
│   ├── EmployeeTransferService.cs
│   ├── EmployeeDocumentService.cs
│   ├── ShiftService.cs
│   ├── BonusDeductionService.cs
│   ├── SalesService.cs
│   ├── HolidayService.cs
│   ├── MfaService.cs
│   ├── TenantContext.cs         # Multi-tenancy context
│   ├── CompanyBranchService.cs
│   ├── CompanySettingsService.cs
│   └── [60+ total]
│
├── Jobs/                        # Hangfire background jobs
│   ├── PayslipPdfCleanupJob.cs
│   ├── LeaveBalanceResetJob.cs
│   ├── AuditLogPruneJob.cs
│   └── EmailQueueWorker.cs
│
├── Security/                    # Security implementations
│   ├── JwtTokenProvider.cs
│   ├── BcryptPasswordHasher.cs
│   ├── PasswordPolicyValidator.cs
│   ├── PasswordPolicy.cs
│   └── EncryptionKeyManager.cs
│
├── JWT/                         # JWT configuration
│   ├── JwtOptions.cs
│   └── JwtService.cs
│
├── Redis/                       # Redis caching & rate limiting
│   ├── RedisDistributedCache.cs
│   ├── RedisRateLimiter.cs
│   └── RateLimitPolicies.cs
│
├── Biometric/                   # Biometric provider
│   ├── BiometricProvider.cs
│   └── DeviceConnectorFactory.cs
│
├── FileStorage/                 # File upload handling
│   ├── S3FileStorage.cs
│   └── LocalFileStorage.cs
│
├── HealthChecks/                # Health check implementations
│   ├── EmailHealthCheck.cs
│   └── [Database, Redis checks]
│
├── Telemetry/                   # OpenTelemetry observability
│   ├── HrmsOpenTelemetrySetup.cs
│   ├── HrmsMetrics.cs           # Custom metrics
│   └── [Tracing configuration]
│
├── Extensions/                  # DI & configuration
│   ├── ServiceExtensions.cs     # All service registrations
│   └── [Other extensions]
│
├── Options/                     # Configuration options
│   ├── DatabaseOptions.cs
│   ├── PasswordPolicyOptions.cs
│   └── [Other settings]
│
├── BackgroundServices/          # Long-running background services
│   ├── EmailQueueWorker.cs
│   ├── BiometricSyncService.cs
│   └── [Others]
│
├── PDF/                         # PDF generation
│   └── PdfGenerator.cs
│
└── Payroll/                     # Complex payroll logic
    ├── PayrollCalculator.cs
    ├── PayrollLockGuard.cs
    └── SalaryComponentCalculator.cs
```

### Application DbContext (100+ DbSets)
```csharp
public class ApplicationDbContext : DbContext
{
    // Authentication & Authorization
    public DbSet<User> Users { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    // Employee Management
    public DbSet<Employee> Employees { get; set; }
    public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }
    public DbSet<EmployeePromotion> EmployeePromotions { get; set; }
    public DbSet<EmployeeTransfer> EmployeeTransfers { get; set; }
    public DbSet<EmployeeExit> EmployeeExits { get; set; }
    public DbSet<EmployeeGoal> EmployeeGoals { get; set; }
    public DbSet<EmployeeSkill> EmployeeSkills { get; set; }

    // Attendance & Time Tracking
    public DbSet<WebAttendance> WebAttendances { get; set; }
    public DbSet<ExcelAttendance> ExcelAttendances { get; set; }
    public DbSet<Timesheet> Timesheets { get; set; }
    public DbSet<TimesheetEntry> TimesheetEntries { get; set; }
    public DbSet<GeoFence> GeoFences { get; set; }

    // Leave Management
    public DbSet<LeaveRequest> LeaveRequests { get; set; }
    public DbSet<LeaveType> LeaveTypes { get; set; }
    public DbSet<LeaveBalance> LeaveBalances { get; set; }
    public DbSet<LeaveBalanceAdjustment> LeaveBalanceAdjustments { get; set; }

    // Payroll
    public DbSet<Payslip> Payslips { get; set; }
    public DbSet<SalaryStructure> SalaryStructures { get; set; }
    public DbSet<Bonus> Bonuses { get; set; }
    public DbSet<Deduction> Deductions { get; set; }

    // Biometric
    public DbSet<BiometricDevice> BiometricDevices { get; set; }
    public DbSet<BiometricLog> BiometricLogs { get; set; }
    public DbSet<BiometricSyncHistory> BiometricSyncHistories { get; set; }
    public DbSet<BiometricSettings> BiometricSettings { get; set; }

    // Sales CRM
    public DbSet<SalesLead> SalesLeads { get; set; }
    public DbSet<SalesCustomer> SalesCustomers { get; set; }
    public DbSet<SalesQuotation> SalesQuotations { get; set; }
    public DbSet<SalesMeeting> SalesMeetings { get; set; }
    public DbSet<SalesVisit> SalesVisits { get; set; }
    public DbSet<SalesTask> SalesTasks { get; set; }

    // Recruitment
    public DbSet<JobRequisition> JobRequisitions { get; set; }
    public DbSet<Candidate> Candidates { get; set; }
    public DbSet<Interview> Interviews { get; set; }
    public DbSet<OfferLetter> OfferLetters { get; set; }

    // Travel & Expense
    public DbSet<TravelRequest> TravelRequests { get; set; }
    public DbSet<ExpenseClaim> ExpenseClaims { get; set; }
    public DbSet<ExpenseItem> ExpenseItems { get; set; }

    // Training & Development
    public DbSet<TrainingProgram> TrainingPrograms { get; set; }
    public DbSet<TrainingEnrollment> TrainingEnrollments { get; set; }

    // Performance Management
    public DbSet<PerformanceCycle> PerformanceCycles { get; set; }
    public DbSet<PerformanceReview> PerformanceReviews { get; set; }
    public DbSet<ContinuousFeedback> ContinuousFeedbacks { get; set; }

    // Compliance & Audit
    public DbSet<ComplianceChecklist> ComplianceChecklists { get; set; }
    public DbSet<ComplianceEvidence> ComplianceEvidences { get; set; }

    // Organization
    public DbSet<Company> Companies { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<CompanyBranch> CompanyBranches { get; set; }

    // Assets & Helpdesk
    public DbSet<Asset> Assets { get; set; }
    public DbSet<HelpdeskTicket> HelpdeskTickets { get; set; }
    public DbSet<HelpdeskComment> HelpdeskComments { get; set; }

    // Other
    public DbSet<HolidayCalendar> HolidayCalendars { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<WebhookSubscription> WebhookSubscriptions { get; set; }
    public DbSet<WebhookOutbox> WebhookOutboxes { get; set; }
    public DbSet<OnboardingTemplate> OnboardingTemplates { get; set; }
    public DbSet<OnboardingRecord> OnboardingRecords { get; set; }
    public DbSet<Appreciation> Appreciations { get; set; }
    public DbSet<DemoSeedTracker> DemoSeedTrackers { get; set; }

    // ... 100+ DbSets total
}
```

### Multi-Tenancy Implementation

#### Global Query Filters
```csharp
// Applied to every DbSet read automatically
mb.Entity<Employee>().HasQueryFilter(e =>
    !_filterByTenant || e.CompanyId == _tenantCompanyId);

mb.Entity<LeaveRequest>().HasQueryFilter(r =>
    !_filterByTenant || r.CompanyId == _tenantCompanyId);

mb.Entity<Payslip>().HasQueryFilter(p =>
    !_filterByTenant || p.CompanyId == _tenantCompanyId);
```

#### Tenant Context
```csharp
public interface ITenantContext
{
    int? CompanyId { get; set; }
    bool IsSuperAdmin { get; set; }
}

public class TenantContext : ITenantContext
{
    public int? CompanyId { get; set; }
    public bool IsSuperAdmin { get; set; }
}
```

---

## 🌐 LAYER 4: API LAYER (`HRMS.API`)

**Purpose:** REST endpoints, HTTP handling, and presentation logic.

### Directory Structure

```
HRMS.API/
├── Program.cs                   # Startup configuration (900+ lines)
│   ├── Service registrations
│   ├── Authentication & authorization
│   ├── Rate limiting (Redis-backed)
│   ├── CORS & CSRF protection
│   ├── OpenTelemetry setup
│   ├── Health checks
│   └── Middleware pipeline
│
├── Controllers/                 # REST endpoints (51 controllers)
│   ├── BaseController.cs        # Base class with tenant context
│   ├── Authentication/
│   │   └── AuthController.cs    # Login, register, password reset
│   ├── Employees/
│   │   ├── EmployeeController.cs
│   │   ├── EmployeeDocumentsController.cs
│   │   ├── EmployeeExitController.cs
│   │   ├── EmployeePromotionController.cs
│   │   └── EmployeeTransferController.cs
│   ├── Payroll/
│   │   ├── PayrollController.cs
│   │   ├── PayslipController.cs
│   │   └── SalaryStructureController.cs
│   ├── Attendance/
│   │   ├── AttendanceController.cs
│   │   ├── TimesheetController.cs
│   │   └── GpsAttendanceController.cs
│   ├── Leave/
│   │   ├── LeaveController.cs
│   │   └── LeaveBalanceController.cs
│   ├── Recruitment/
│   │   ├── RecruitmentController.cs
│   │   └── CandidateController.cs
│   ├── Performance/
│   │   └── PerformanceController.cs
│   ├── Biometric/
│   │   ├── BiometricDeviceController.cs
│   │   └── BiometricSyncController.cs
│   ├── Sales/
│   │   ├── SalesLeadController.cs
│   │   ├── SalesCustomerController.cs
│   │   └── SalesQuotationController.cs
│   ├── Travel/
│   │   └── TravelController.cs
│   ├── Expense/
│   │   └── ExpenseController.cs
│   ├── Training/
│   │   └── TrainingController.cs
│   ├── Compliance/
│   │   └── ComplianceController.cs
│   ├── Reports/
│   │   ├── ReportController.cs
│   │   └── ExportController.cs
│   ├── Helpdesk/
│   │   └── HelpdeskController.cs
│   ├── Admin/
│   │   ├── AdminUsersController.cs
│   │   └── SuperAdminsController.cs
│   ├── Companies/
│   │   ├── CompanyController.cs
│   │   └── DepartmentController.cs
│   ├── Email/
│   │   └── EmailController.cs
│   ├── Notification/
│   │   └── NotificationController.cs
│   ├── Analytics/
│   │   └── AnalyticsController.cs
│   ├── Webhook/
│   │   └── WebhookController.cs
│   ├── Onboarding/
│   │   └── OnboardingController.cs
│   ├── Asset/
│   │   └── AssetsController.cs
│   └── Dashboard/
│       └── DashboardController.cs
│
├── Middleware/                  # HTTP request/response handling
│   ├── CorrelationIdMiddleware.cs    # Request tracing
│   ├── ExceptionMiddleware.cs        # Global error handling
│   ├── CspNonceMiddleware.cs         # Content-Security-Policy
│   ├── HtmlNonceInjectionMiddleware.cs
│   ├── MustChangePasswordMiddleware.cs
│   ├── SwaggerBasicAuthMiddleware.cs
│   └── TenantContextMiddleware.cs    # Multi-tenancy isolation
│
├── Filters/                     # Action filters
│   ├── AuditActionFilter.cs     # Logs all mutations
│   ├── AntiVirusScanFilter.cs   # Scans file uploads
│   ├── CsrfValidationFilter.cs  # CSRF protection
│   ├── ValidationFilterAttribute.cs
│   └── RateLimitingFilter.cs
│
├── Security/                    # Security policies
│   ├── HangfireSuperAdminAuthFilter.cs
│   ├── CsrfValidationFilter.cs
│   └── PasswordPolicy.cs
│
├── Extensions/                  # Helper methods
│   ├── HealthCheckResponseWriter.cs
│   ├── SwaggerDocumentation.cs
│   ├── ServiceExtensions.cs
│   ├── JwtExtensions.cs
│   └── EncryptionServiceExtensions.cs
│
├── Services/                    # API-specific services
│   └── [Non-business-logic API services]
│
├── Swagger/                     # API documentation
│   └── [Swagger configuration]
│
├── appsettings.json             # Default configuration
├── appsettings.Development.json
├── appsettings.Production.json
└── wwwroot/                     # Static files (React SPA build)
    ├── index.html               # SPA entry point
    ├── assets/                  # JS/CSS bundles
    └── uploads/                 # User file uploads
```

### Sample Controller
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]  // Global fallback policy: authenticated users only
public class EmployeeController : BaseController
{
    private readonly IEmployeeService _employeeService;
    
    public EmployeeController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpGet]
    [EnableRateLimiting("api")]  // 120 req/min per IP
    public async Task<ApiResponse<IEnumerable<EmployeeResponseDto>>> GetAll()
    {
        var employees = await _employeeService.GetAllAsync(CompanyId);
        return Ok(employees);
    }

    [HttpPost]
    [EnableRateLimiting("api")]
    public async Task<ApiResponse<EmployeeResponseDto>> Create(CreateEmployeeDto dto)
    {
        dto.CompanyId = CompanyId;  // Tenant scoping
        var result = await _employeeService.CreateAsync(dto);
        return Created(result);
    }

    [HttpPut("{id}")]
    [EnableRateLimiting("api")]
    public async Task<ApiResponse<EmployeeResponseDto>> Update(int id, UpdateEmployeeDto dto)
    {
        var result = await _employeeService.UpdateAsync(id, dto, CompanyId);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [EnableRateLimiting("api")]
    public async Task<ApiResponse> Delete(int id)
    {
        await _employeeService.DeleteAsync(id, CompanyId);
        return NoContent();
    }
}
```

---

## 🎨 LAYER 5: FRONTEND LAYER (`HRMS.SPA.Source`)

**Purpose:** React + TypeScript SPA built with Vite (not Create React App).

### Directory Structure

```
HRMS.SPA.Source/
├── src/
│   ├── components/              # Reusable React components
│   │   ├── layout/
│   │   │   ├── MainLayout.tsx
│   │   │   ├── Sidebar.tsx
│   │   │   └── Header.tsx
│   │   ├── shared/
│   │   │   ├── Button.tsx
│   │   │   ├── Modal.tsx
│   │   │   ├── Table.tsx
│   │   │   ├── Form.tsx
│   │   │   ├── DataGrid.tsx
│   │   │   ├── Pagination.tsx
│   │   │   └── ErrorBoundary.tsx
│   │   └── ui/
│   │       ├── Card.tsx
│   │       ├── Alert.tsx
│   │       ├── Toast.tsx
│   │       ├── Loading.tsx
│   │       ├── DatePicker.tsx
│   │       └── MultiSelect.tsx
│   │
│   ├── pages/                   # Page components (route-level)
│   │   ├── Employee/
│   │   │   ├── EmployeeList.tsx
│   │   │   ├── EmployeeDetail.tsx
│   │   │   ├── EmployeeCreate.tsx
│   │   │   └── EmployeeEdit.tsx
│   │   ├── Payroll/
│   │   │   ├── PayrollDashboard.tsx
│   │   │   ├── PayslipList.tsx
│   │   │   └── SalaryStructure.tsx
│   │   ├── Attendance/
│   │   │   ├── AttendanceList.tsx
│   │   │   ├── AttendanceCheckIn.tsx
│   │   │   └── TimesheetList.tsx
│   │   ├── Leave/
│   │   │   ├── LeaveRequest.tsx
│   │   │   ├── LeaveApproval.tsx
│   │   │   └── LeaveBalance.tsx
│   │   ├── Recruitment/
│   │   │   ├── CandidateList.tsx
│   │   │   └── JobRequisition.tsx
│   │   ├── Sales/
│   │   │   ├── SalesLeadList.tsx
│   │   │   └── SalesQuotation.tsx
│   │   ├── Reports/
│   │   │   ├── AttendanceReport.tsx
│   │   │   ├── PayrollReport.tsx
│   │   │   ├── LeaveReport.tsx
│   │   │   └── ExportReport.tsx
│   │   ├── Admin/
│   │   │   ├── AdminDashboard.tsx
│   │   │   ├── UserManagement.tsx
│   │   │   └── RoleManagement.tsx
│   │   ├── Auth/
│   │   │   ├── Login.tsx
│   │   │   ├── Register.tsx
│   │   │   ├── ForgotPassword.tsx
│   │   │   └── ChangePassword.tsx
│   │   └── [Other domain pages]/
│   │
│   ├── api-client/              # API communication
│   │   ├── apiClient.ts         # Axios instance with interceptors
│   │   ├── employeeApi.ts
│   │   ├── payrollApi.ts
│   │   ├── attendanceApi.ts
│   │   ├── leaveApi.ts
│   │   ├── authApi.ts
│   │   └── [Domain-specific APIs]
│   │
│   ├── contexts/                # React Context (state management)
│   │   ├── AuthContext.tsx      # Authentication state
│   │   ├── TenantContext.tsx    # Multi-tenant context
│   │   ├── UserContext.tsx      # Current user info
│   │   └── [Other contexts]
│   │
│   ├── hooks/                   # Custom React hooks
│   │   ├── useAuth.ts           # Auth context hook
│   │   ├── useApi.ts            # API call hook
│   │   ├── useForm.ts           # Form state hook
│   │   ├── usePagination.ts
│   │   ├── useLocalStorage.ts
│   │   └── [Other hooks]
│   │
│   ├── utils/                   # Utility functions
│   │   ├── dateUtils.ts         # Date formatting
│   │   ├── numberUtils.ts       # Currency, percentages
│   │   ├── stringUtils.ts       # String manipulation
│   │   ├── validationUtils.ts   # Form validation
│   │   ├── downloadUtils.ts     # CSV/PDF export
│   │   └── [Other utilities]
│   │
│   ├── types/                   # TypeScript type definitions
│   │   ├── employee.ts
│   │   ├── payroll.ts
│   │   ├── attendance.ts
│   │   ├── leave.ts
│   │   ├── auth.ts
│   │   ├── api.ts              # API response types
│   │   └── [Domain-specific types]
│   │
│   ├── locales/                 # i18n translations
│   │   ├── en.json              # English
│   │   ├── es.json              # Spanish
│   │   ├── fr.json              # French
│   │   └── [Other languages]
│   │
│   ├── lib/                     # Third-party library config
│   │   ├── axios.ts
│   │   ├── i18n.ts
│   │   └── [Library setup]
│   │
│   ├── vite-plugins/            # Vite plugins
│   │   └── [Custom plugins]
│   │
│   ├── __tests__/               # Unit & integration tests
│   │   ├── components/
│   │   ├── utils/
│   │   ├── hooks/
│   │   └── api-client/
│   │
│   ├── App.tsx                  # Root component
│   ├── main.tsx                 # React entry point
│   ├── index.css                # Global styles
│   └── setupTests.ts            # Test configuration
│
├── e2e/                         # End-to-end tests (Playwright)
│   ├── auth.spec.ts
│   ├── employee.spec.ts
│   ├── payroll.spec.ts
│   └── [Domain-specific E2E]
│
├── public/                      # Static assets
│   ├── logo.png
│   ├── favicon.ico
│   └── [Images, icons]
│
├── vite.config.ts               # Vite configuration
├── vite.config.local.ts         # Local development config
├── tsconfig.json                # TypeScript config
├── package.json                 # Bun dependencies
├── bun.lock                     # Bun lock file
└── index.html                   # HTML entry point
```

### API Client Example
```typescript
// api-client/employeeApi.ts
import { apiClient } from './apiClient';
import { Employee, CreateEmployeeDto } from '@/types/employee';

export const employeeApi = {
  getAll: async (companyId: number) => {
    const response = await apiClient.get<Employee[]>(`/api/employees`, {
      params: { companyId }
    });
    return response.data;
  },

  getById: async (id: number) => {
    const response = await apiClient.get<Employee>(`/api/employees/${id}`);
    return response.data;
  },

  create: async (dto: CreateEmployeeDto) => {
    const response = await apiClient.post<Employee>(`/api/employees`, dto);
    return response.data;
  },

  update: async (id: number, dto: Partial<CreateEmployeeDto>) => {
    const response = await apiClient.put<Employee>(`/api/employees/${id}`, dto);
    return response.data;
  },

  delete: async (id: number) => {
    await apiClient.delete(`/api/employees/${id}`);
  }
};
```

---

## 🗄️ DATABASE LAYER

### Database Schema Highlights

**Primary Technologies:**
- **MySQL 8.4** (migrated from PostgreSQL in Phase 4)
- **EF Core 8** with Pomelo provider
- **100+ tables** with comprehensive indexing

### Key Tables & Relationships

```
┌─────────────────┐
│   Companies     │ (Tenant)
├─────────────────┤
│ id (PK)         │
│ name            │
│ domain          │
│ active          │
└────────┬────────┘
         │
         └──────┬──────────────────────────────────┐
                │                                  │
         ┌──────▼──────────┐         ┌────────────▼───────┐
         │  Employees      │         │  Departments       │
         ├─────────────────┤         ├────────────────────┤
         │ id (PK)         │         │ id (PK)            │
         │ employee_code   │         │ name               │
         │ company_id (FK) │         │ company_id (FK)    │
         │ dept_id (FK)    │◄────────│ manager_id (FK)    │
         │ first_name      │         └────────────────────┘
         │ last_name       │
         │ email           │
         │ phone           │
         │ aadhaar (enc)   │ Encryption: AES-256-GCM
         │ pan (enc)       │
         │ status          │
         │ is_active       │
         │ is_deleted      │ Soft delete
         │ created_at      │
         └──────┬──────────┘
                │
         ┌──────▼──────────┐
         │ Attendance      │
         ├─────────────────┤
         │ id (PK)         │
         │ employee_id(FK) │
         │ check_in        │
         │ check_out       │
         │ company_id (FK) │
         │ is_deleted      │
         └─────────────────┘

        ┌──────────────────┐
        │  PaySlips        │
        ├──────────────────┤
        │ id (PK)          │
        │ employee_id (FK) │
        │ company_id (FK)  │
        │ month/year       │
        │ base_salary      │
        │ gross_pay        │
        │ deductions       │
        │ net_pay          │
        │ created_at       │
        └──────────────────┘
```

### Indexing Strategy
```sql
-- Tenant + Employee queries
CREATE INDEX idx_employees_company_id ON employees(company_id);
CREATE INDEX idx_employees_company_dept ON employees(company_id, department_id);

-- Date-range reports
CREATE INDEX idx_attendance_company_date ON web_attendance(company_id, check_in_date);
CREATE INDEX idx_payslips_month_year ON payslips(company_id, month, year);

-- Soft-delete visibility
CREATE INDEX idx_users_is_deleted ON users(is_deleted);
CREATE INDEX idx_attendance_is_deleted ON web_attendance(is_deleted);
```

---

## 🔐 Security Architecture

### Multi-Layered Security

```
┌─────────────────────────────────────────────────┐
│         HTTPS + TLS Termination (Nginx)         │
├─────────────────────────────────────────────────┤
│  CORS | CSRF (Double-Submit) | CSP Nonce       │
├─────────────────────────────────────────────────┤
│  Authentication (JWT RS256) | MFA               │
├─────────────────────────────────────────────────┤
│  Authorization (Role-Based)                     │
├─────────────────────────────────────────────────┤
│  Tenant Isolation (Global Query Filters)        │
├─────────────────────────────────────────────────┤
│  Encryption (AES-256-GCM for PII)               │
├─────────────────────────────────────────────────┤
│  Rate Limiting (Redis-backed)                   │
├─────────────────────────────────────────────────┤
│  Audit Logging (All mutations tracked)          │
├─────────────────────────────────────────────────┤
│  Virus Scanning (ClamAV for uploads)            │
└─────────────────────────────────────────────────┘
```

### Encryption Strategy
- **PII Fields:** AES-256-GCM (employee names, Aadhaar, PAN, bank details)
- **Backups:** AES-256-CBC + gzip compression
- **JWT:** RS256 asymmetric (private key for signing, public for verification)
- **Passwords:** BCrypt with salt

### Authentication Flow
```
1. User submits credentials → POST /api/auth/login
2. AuthService validates email + password
3. JWT token generated (RS256, 15-min expiry)
4. HttpOnly cookie set in response
5. Refresh token stored in Redis (7-day TTL)
6. Subsequent requests include JWT in Authorization header
7. CorrelationIdMiddleware → AuthenticationMiddleware → TenantContextMiddleware
```

---

## 🎯 Service Boundary Patterns

### Example: Leave Management Service

```csharp
public interface ILeaveService
{
    // Single Responsibility: manages leave domain logic only
    Task<LeaveRequest> CreateAsync(CreateLeaveRequestDto dto, int companyId);
    Task<LeaveRequest> ApproveAsync(int id, ApprovalDto dto, int companyId);
    Task<LeaveRequest> RejectAsync(int id, string reason, int companyId);
    Task<IEnumerable<LeaveRequest>> GetPendingAsync(int companyId);
    Task<LeaveBalance> GetBalanceAsync(int employeeId, int companyId);
}

public class LeaveService : ILeaveService
{
    private readonly IGenericRepository<LeaveRequest> _leaveRepo;
    private readonly IGenericRepository<LeaveBalance> _balanceRepo;
    private readonly IEmployeeService _employeeService;
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;

    public async Task<LeaveRequest> CreateAsync(CreateLeaveRequestDto dto, int companyId)
    {
        // Validation
        if (dto.StartDate >= dto.EndDate)
            throw new ValidationException("End date must be after start date");

        // Authorization
        if (!_tenantContext.IsSuperAdmin && _tenantContext.CompanyId != companyId)
            throw new UnauthorizedAccessException();

        // Business logic
        var employee = await _employeeService.GetByIdAsync(dto.EmployeeId, companyId);
        var balance = await _balanceRepo.FirstOrDefaultAsync(
            b => b.EmployeeId == dto.EmployeeId && b.LeaveTypeId == dto.LeaveTypeId);

        int daysRequested = (dto.EndDate - dto.StartDate).Days + 1;
        if (balance.RemainingDays < daysRequested)
            throw new BusinessRuleException("Insufficient leave balance");

        // Create entity
        var leave = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            CompanyId = companyId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            LeaveTypeId = dto.LeaveTypeId,
            Reason = dto.Reason,
            Status = LeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        // Persist
        await _leaveRepo.AddAsync(leave);
        await _leaveRepo.SaveChangesAsync();

        // Side effects
        await _emailService.SendLeaveRequestNotificationAsync(employee.Email);
        await _auditService.LogAsync("LeaveRequest", "Created", leave.Id, companyId);

        return leave;
    }
}
```

---

## 🔄 Data Flow Diagram

```
USER (Browser)
    │
    ├─→ HTTPS Request
    │   └─→ API Gateway / Nginx (TLS Termination)
    │
    └─→ ASP.NET Core API
        ├─→ Authentication Middleware (JWT validation)
        ├─→ Tenant Context Middleware (CompanyId extraction)
        │
        ├─→ Controller (REST endpoint)
        │   ├─→ Input validation
        │   ├─→ Authorization check
        │   └─→ Service call
        │
        ├─→ Service Layer (Business logic)
        │   ├─→ Repository calls
        │   ├─→ Cache layer (Redis)
        │   ├─→ External service calls (Email, Biometric, etc.)
        │   └─→ Side effects (Webhooks, Audit logs)
        │
        ├─→ Repository Layer (Data access)
        │   ├─→ EF Core DbContext
        │   │   ├─→ Global query filters (tenant isolation)
        │   │   ├─→ Value converters (encryption/decryption)
        │   │   └─→ Change tracking
        │   │
        │   └─→ MySQL 8.4 Database
        │       ├─→ 100+ tables
        │       ├─→ Composite indexes
        │       └─→ Soft-delete support
        │
        ├─→ Response Builder (DTO mapping)
        │
        └─→ HTTPS Response
            └─→ Browser (JSON + HttpOnly JWT Cookie)
```

---

## 🏭 Background Jobs (Hangfire)

```
┌─────────────────────────────────────┐
│   Hangfire Dashboard (Superadmin)   │
│   /hangfire                         │
└────────────────┬────────────────────┘
                 │
         ┌───────┴────────┐
         │                │
    ┌────▼──────┐    ┌───▼──────┐
    │  Recurring │    │  One-Time │
    │   Jobs     │    │   Jobs    │
    └────┬───────┘    └───┬───────┘
         │                │
    ┌────▼────────────────▼────┐
    │    Redis (Job Queue)      │
    │  ├─ Email delivery        │
    │  ├─ Payslip PDF cleanup   │
    │  ├─ Leave balance reset   │
    │  └─ Audit log pruning     │
    └────┬──────────────────────┘
         │
    ┌────▼──────────────────────┐
    │   Worker Processes        │
    │  (ServiceProvider scope)   │
    └───────────────────────────┘
```

### Recurring Jobs
```csharp
// Registered in Program.cs at startup
recurringJobs.AddOrUpdate<PayslipPdfCleanupJob>(
    "payslip-pdf-cleanup",
    j => j.RunAsync(),
    Hangfire.Cron.Hourly());  // Every hour

recurringJobs.AddOrUpdate<LeaveBalanceResetJob>(
    "leave-balance-reset",
    j => j.RunAsync(),
    "0 0 1 * *",  // Day 1 of every month at 00:00 UTC
    timeZone: TimeZoneInfo.Utc);

recurringJobs.AddOrUpdate<AuditLogPruneJob>(
    "audit-log-prune",
    j => j.RunAsync(),
    "0 2 * * 0",  // Sunday 2 AM UTC
    timeZone: TimeZoneInfo.Utc);
```

---

## 📊 Observability Stack

```
┌──────────────────────────────────────────┐
│        Application Metrics                │
│  ├─ Custom metrics (HrmsMetrics)          │
│  ├─ Request count / latency               │
│  ├─ Database query performance            │
│  └─ Business metrics (payslip count, etc) │
└────────────┬─────────────────────────────┘
             │
       ┌─────▼───────┐
       │ Prometheus  │
       │ /metrics    │ (Scrape every 15s)
       └─────┬───────┘
             │
    ┌────────┴────────┐
    │                 │
┌───▼──────┐    ┌───▼──────┐
│  Grafana  │    │ Alertmngr │
│  Dashboards    │ Slack/Email
│  (Visualization)
└───────────┘    └──────────┘

┌──────────────────────────────────────────┐
│        Distributed Traces                  │
│  ├─ Request trace ID (CorrelationId)      │
│  ├─ Service-to-service spans              │
│  ├─ Database query spans                  │
│  └─ External service calls                │
└────────────┬─────────────────────────────┘
             │
       ┌─────▼───────┐
       │  Jaeger     │
       │ (all-in-one)│ (OTLP gRPC 4317)
       │   Trace UI  │ (localhost:16686)
       └─────────────┘

┌──────────────────────────────────────────┐
│       Structured Logs                      │
│  ├─ Serilog JSON output                   │
│  ├─ PII masking (Destructuring)           │
│  ├─ CorrelationId per request             │
│  └─ Audit trail (all mutations)           │
└────────────┬─────────────────────────────┘
             │
    ┌────────┴────────┐
    │                 │
┌───▼──────┐    ┌───▼──────┐
│  Console  │    │   Seq    │
│  (STDOUT) │    │(optional) │
└────────────┘    └──────────┘
```

---

## 🚀 Deployment Architecture

### Docker Compose Services

```yaml
services:
  # One-time migration job (runs, exits, then API starts)
  migrate:
    build: . (target: migrate)
    depends_on: mysql (healthy), backfill (complete)
    restart: "no"  # Run once
    
  # Data backfill for existing deployments
  backfill:
    image: mysql:8.4
    depends_on: mysql (healthy)
    restart: "no"
    
  # Primary data store
  mysql:
    image: mysql:8.4@sha256:...
    volumes: [hrms_mysqldata:/var/lib/mysql]
    healthcheck: mysqladmin ping
    
  # Cache + job queue
  redis:
    image: redis:7.4-alpine@sha256:...
    volumes: [hrms_redis:/data]
    
  # Application server
  api:
    build: . (target: runtime)
    depends_on: [mysql (healthy), redis (healthy), migrate (complete)]
    ports: [expose 8080, not to host in prod]
    
  # TLS termination + static files
  nginx:
    image: nginx:1.27.0-alpine@sha256:...
    ports: [80:80, 443:443]
    volumes: [./nginx/nginx.conf.template, certs, uploads]
    
  # ACME / Let's Encrypt certificate renewal
  certbot:
    image: certbot/certbot:v2.11.0@sha256:...
    volumes: [certs, webroot]
    
  # Metrics collection
  prometheus:
    image: prom/prometheus:v2.53.0
    ports: [127.0.0.1:9090]
    
  # Metrics visualization
  grafana:
    image: grafana/grafana:11.1.0
    ports: [127.0.0.1:3000]
    
  # Trace collection & UI
  jaeger:
    image: jaegertracing/all-in-one:1.58
    ports: [127.0.0.1:16686, 127.0.0.1:4317]
    
  # Alert routing
  alertmanager:
    image: prom/alertmanager:v0.27.0
    ports: [127.0.0.1:9093]
    
  # Encrypted database backups
  backup:
    image: mysql:8.4@sha256:...
    volumes: [./backups:/backups]
    command: crond -f
    
  # Off-site S3 backup (optional profile)
  offsite-backup:
    image: amazon/aws-cli:2.17.0
    profiles: [offsite]
    
  # Antivirus scanning
  clamav:
    image: clamav/clamav:1.4_base
    healthcheck: clamdscan --ping 3

networks:
  hrms_internal: (bridge, internal only)

volumes:
  hrms_mysqldata:
  hrms_redis:
  hrms_certbot_conf:
  hrms_prometheus:
  hrms_grafana:
```

---

## 📋 API Endpoint Summary

### Authentication (5 endpoints)
```
POST   /api/auth/login              → LoginDto
POST   /api/auth/register           → RegisterDto
POST   /api/auth/refresh            → RefreshDto
POST   /api/auth/logout
POST   /api/auth/csrf               → CSRF token (GET)
POST   /api/auth/forgot-password    → ForgotPasswordDto
POST   /api/auth/reset-password     → ResetPasswordDto
```

### Employee Management (15 endpoints)
```
GET    /api/employees               → IEnumerable<EmployeeDto>
GET    /api/employees/{id}          → EmployeeDto
POST   /api/employees               → CreateEmployeeDto
PUT    /api/employees/{id}          → UpdateEmployeeDto
DELETE /api/employees/{id}
GET    /api/employees/export        → CSV/Excel
POST   /api/employees/{id}/documents → Upload document
GET    /api/employees/{id}/promotion → Promotion history
POST   /api/employees/{id}/transfer  → Create transfer
POST   /api/employees/{id}/exit      → Employee exit
```

### Payroll (10 endpoints)
```
GET    /api/payroll/payslips        → IEnumerable<PayslipDto>
GET    /api/payroll/payslips/{id}   → PayslipDto (PDF)
POST   /api/payroll/salary-structure → CreateSalaryStructureDto
PUT    /api/payroll/salary-structure/{id} → UpdateDto
GET    /api/payroll/salary-structure/{employeeId} → SalaryStructureDto
POST   /api/payroll/bonus           → CreateBonusDto
POST   /api/payroll/deduction       → CreateDeductionDto
GET    /api/payroll/lock-status     → PayrollLockStatusDto
POST   /api/payroll/lock            → BulkLockDto
```

### Attendance (8 endpoints)
```
GET    /api/attendance              → IEnumerable<AttendanceDto>
POST   /api/attendance/check-in     → CheckInDto
POST   /api/attendance/check-out    → CheckOutDto
GET    /api/attendance/report       → AttendanceReportDto
POST   /api/timesheet               → CreateTimesheetDto
GET    /api/timesheet/{id}          → TimesheetDto
POST   /api/gps-attendance          → GPSCheckInDto
```

### Leave (8 endpoints)
```
GET    /api/leave/requests          → IEnumerable<LeaveRequestDto>
POST   /api/leave/requests          → CreateLeaveRequestDto
GET    /api/leave/balance/{employeeId} → LeaveBalanceDto
POST   /api/leave/approve/{id}      → ApprovalDto
POST   /api/leave/reject/{id}       → RejectionDto
GET    /api/leave/types             → IEnumerable<LeaveTypeDto>
```

### Recruitment (6 endpoints)
```
GET    /api/recruitment/candidates  → IEnumerable<CandidateDto>
POST   /api/recruitment/candidates  → CreateCandidateDto
GET    /api/recruitment/interviews  → IEnumerable<InterviewDto>
POST   /api/recruitment/interview   → CreateInterviewDto
```

### Reports (5 endpoints)
```
GET    /api/reports/attendance      → AttendanceReportDto
GET    /api/reports/payroll         → PayrollReportDto
GET    /api/reports/leave           → LeaveReportDto
GET    /api/reports/export          → Download CSV/PDF
POST   /api/reports/streaming       → Streaming large dataset
```

### Admin (5 endpoints)
```
GET    /api/admin/users             → IEnumerable<AdminUserDto>
POST   /api/admin/users             → CreateAdminUserDto
GET    /api/admin/audit-logs        → IEnumerable<AuditLogDto>
POST   /api/admin/settings          → UpdateSettingsDto
```

---

## 🎯 Key Architectural Patterns

### 1. **Clean Architecture**
- Independent layers with clear dependencies
- Business logic separated from infrastructure
- Testable: services can be tested without databases

### 2. **Repository Pattern**
- `IGenericRepository<T>` for CRUD operations
- Entity-specific repositories for complex queries
- Single source of truth for data access

### 3. **Dependency Injection**
- Registered in `ServiceExtensions.AddInfrastructure()`
- Scoped services per HTTP request
- Constructor injection throughout

### 4. **Multi-Tenancy**
- Global query filters enforce tenant isolation
- `ITenantContext` scoped per request
- CompanyId validation at service layer

### 5. **Service Locator Pattern (Limited)**
- `RequestServices.GetService<T>()` for middleware/filters only
- Minimized; constructor injection preferred

### 6. **Factory Pattern**
- `BiometricProviderFactory` for device connectors
- `ServiceExtensions` factories for complex service setup

### 7. **Middleware Pipeline**
- Ordered execution: ForwardedHeaders → CorrelationId → Exception → Auth → Tenant
- Each middleware a single responsibility

### 8. **Event-Driven**
- Webhooks for external system integration
- Hangfire for async processing
- Audit logging on all mutations

---

## 📈 Scalability Considerations

### Horizontal Scaling
- Stateless API layer (no session state)
- Distributed cache (Redis) for rate limiting
- Load-balanced behind Nginx

### Database Optimization
- Composite indexes for common queries
- Soft-delete support (IsDeleted flag)
- Query optimization with LINQ expressions

### Async Processing
- Hangfire for long-running jobs
- Email delivery via background worker
- Report generation streaming

### Caching Strategy
- Redis for rate limiting counters
- In-memory cache for configuration
- Response compression (Brotli/Gzip)

---

## ✅ Testing Strategy

### Unit Tests
- Service-layer logic
- Validator tests
- Helper/utility functions

### Integration Tests
- Controller endpoints
- Repository operations
- EF Core migrations

### E2E Tests (Playwright)
- User workflows (login, create employee, etc.)
- Multi-step processes (leave request → approval)
- Report generation

---

## 🔗 Dependencies Summary

### Core Frameworks
- **ASP.NET Core 8.0** - Web framework
- **Entity Framework Core 8** - ORM
- **Pomelo MySQL Provider** - MySQL 8.4 support

### Authentication & Security
- **System.IdentityModel.Tokens.Jwt** - JWT handling
- **BCrypt.Net-Next** - Password hashing
- **System.Security.Cryptography** - AES encryption

### Observability
- **Serilog** - Structured logging
- **OpenTelemetry** - Distributed tracing
- **Prometheus** - Metrics
- **Jaeger** - Trace visualization

### Background Jobs
- **Hangfire** - Distributed job queue
- **StackExchange.Redis** - Redis client

### Data & APIs
- **AutoMapper** - DTO mapping
- **FluentValidation** - Input validation
- **Swagger/OpenAPI** - API documentation

### Frontend
- **React 18+** - UI framework
- **TypeScript** - Type safety
- **Vite** - Build tool (not Create React App)
- **Axios** - HTTP client
- **React Query** - Server state management
- **Playwright** - E2E testing

---

## 🎓 Summary

**HRMS** is a **production-grade, enterprise-scale HRMS platform** with:
- ✅ Clean Architecture (4 distinct layers)
- ✅ Multi-tenancy (100+ domain entities)
- ✅ Comprehensive security (JWT, encryption, rate limiting)
- ✅ Full observability (tracing, metrics, logs)
- ✅ Scalable design (async, caching, indexing)
- ✅ Complete test coverage (unit, integration, E2E)
- ✅ Professional DevOps (Docker, Compose, migrations)

The system manages the complete HR lifecycle: recruitment, onboarding, attendance, leave, payroll, performance, compliance, and analytics — all within a secure, multi-tenant SaaS platform.

