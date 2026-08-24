using HRMS.Domain.Entities;
using HRMS.Domain.Entities.Company;
using HRMS.Domain.Entities.Demo;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities.Payroll;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Domain.Entities.Assets;
using HRMS.Domain.Entities.Authentication;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Options;
using HRMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS.Infrastructure.Services.Demo;

/// <summary>
/// Production-safe demo data seed service.
/// Creates deterministic, reproducible demo data marked with IsDemo=true.
/// </summary>
public class DemoSeedService : IDemoSeedService
{
    private const int SEED_RANDOM_SEED = 20260819;
    private const int ATTENDANCE_HISTORY_DAYS = 180;
    private const int PAYROLL_HISTORY_MONTHS = 12;

    // Demo company definitions (5 companies, IDs 1-5)
    private static readonly DemoCompanyDefinition[] DemoCompanies = new[]
    {
        new DemoCompanyDefinition(1, "DEMO-RH", "RatanHR Demo Holdings", "Software/IT", "Mumbai", "Information Technology and Software Development"),
        new DemoCompanyDefinition(2, "DEMO-NM", "Northstar Manufacturing Demo", "Manufacturing", "Pune", "Industrial Manufacturing and Production"),
        new DemoCompanyDefinition(3, "DEMO-BC", "BluePeak Consulting Demo", "Consulting", "Bengaluru", "Business Consulting and Advisory Services"),
        new DemoCompanyDefinition(4, "DEMO-GR", "Greenfield Retail Demo", "Retail", "Thane", "Retail and Consumer Goods Distribution"),
        new DemoCompanyDefinition(5, "DEMO-SL", "Summit Logistics Demo", "Logistics", "Navi Mumbai", "Logistics and Supply Chain Management")
    };

    private readonly ApplicationDbContext _db;
    private readonly ILogger<DemoSeedService> _logger;
    private readonly DemoModeOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public DemoSeedService(
        ApplicationDbContext db,
        ILogger<DemoSeedService> logger,
        IOptions<DemoModeOptions> options,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _options = options.Value;
        _environment = environment;
        _configuration = configuration;
    }

    /// <summary>
    /// Validates all preconditions for safe demo seeding.
    /// </summary>
    public async Task<DemoValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var result = new DemoValidationResult { IsValid = true, Checks = new() };

        // Check 1: Demo Mode enabled
        var check1 = new ValidationCheck
        {
            CheckName = "DemoMode:Enabled",
            Passed = _options.Enabled,
            Message = _options.Enabled ? "Demo mode is enabled" : "Demo mode is disabled (must be true)"
        };
        result.Checks.Add(check1);
        if (!check1.Passed) result.FailureReasons.Add(check1.Message);

        // Check 2: Production environment safeguard
        var isProduction = _environment.IsProduction();
        var check2 = new ValidationCheck
        {
            CheckName = "Production Safeguard",
            Passed = !isProduction || _options.AllowProduction,
            Message = isProduction
                ? (_options.AllowProduction ? "Production seeding explicitly allowed" : "Production seeding blocked by default")
                : "Non-production environment (development allowed)"
        };
        result.Checks.Add(check2);
        if (!check2.Passed) result.FailureReasons.Add(check2.Message);

        // Check 3: Database connectivity
        bool dbConnected = false;
        try
        {
            dbConnected = await _db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            dbConnected = false;
        }
        var check3 = new ValidationCheck
        {
            CheckName = "Database Connectivity",
            Passed = dbConnected,
            Message = dbConnected ? "Database is accessible" : "Database connection failed"
        };
        result.Checks.Add(check3);
        if (!check3.Passed) result.FailureReasons.Add(check3.Message);

        // Check 4: No real customer data in demo company IDs (1-5)
        bool noRealDataInDemo = false;
        try
        {
            var demoCompanyCount = await _db.Companies
                .IgnoreQueryFilters()
                .Where(c => c.Id >= 1 && c.Id <= 5 && !c.IsDemo)
                .CountAsync(cancellationToken);
            noRealDataInDemo = demoCompanyCount == 0;
        }
        catch
        {
            noRealDataInDemo = false;
        }
        var check4 = new ValidationCheck
        {
            CheckName = "Demo Company Isolation",
            Passed = noRealDataInDemo,
            Message = noRealDataInDemo
                ? "No real customer data found in reserved demo company IDs (1-5)"
                : "Real customer data detected in reserved demo company IDs"
        };
        result.Checks.Add(check4);
        if (!check4.Passed) result.FailureReasons.Add(check4.Message);

        result.IsValid = result.FailureReasons.Count == 0;
        return result;
    }

    /// <summary>
    /// Main seed operation - creates demo data or shows dry-run preview.
    /// </summary>
    public async Task<DemoSeedResult> SeedAsync(bool dryRun = true, bool verbose = true, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[DEMO-SEED] Starting seed operation (dryRun={DryRun}, v{Version})", dryRun, _options.SeedVersion);

        // Validate preconditions
        var validation = await ValidateAsync(cancellationToken);
        if (!validation.IsValid)
        {
            _logger.LogError("[DEMO-SEED] Validation failed: {Reasons}", string.Join("; ", validation.FailureReasons));
            return new DemoSeedResult
            {
                IsSuccess = false,
                WasDryRun = true,
                Message = "Validation failed",
                ErrorMessage = string.Join("; ", validation.FailureReasons)
            };
        }

        try
        {
            if (dryRun)
            {
                return await DryRunAsync(verbose, cancellationToken);
            }
            else
            {
                if (!_options.SeedEnabled)
                {
                    // FIX: ErrorMessage previously read "DemoMode:SeedEnabled is false", which
                    // does not contain the word "disabled". Callers (and this class's own tests)
                    // check `result.ErrorMessage ?? result.Message` for the word "disabled" to
                    // confirm seeding was blocked for the expected reason. Since ErrorMessage is
                    // non-null it always won that null-coalesce, so the check silently inspected
                    // the wrong string. Keep both fields textually consistent.
                    return new DemoSeedResult
                    {
                        IsSuccess = false,
                        WasDryRun = true,
                        Message = "Demo seeding is disabled. Set DemoMode:SeedEnabled=true to proceed.",
                        ErrorMessage = "Demo seeding is disabled: DemoMode:SeedEnabled is false."
                    };
                }

                // FIX: Idempotency guard. Demo company IDs are fixed (1-5); if they already
                // exist, a second SeedAsync(dryRun:false) call must be a safe no-op rather than
                // attempting to INSERT the same primary keys again (which would either throw a
                // duplicate-key violation on a real database or silently double the data set on
                // providers without a PK constraint). Report the already-seeded state back to the
                // caller as a successful, no-op "dry run" so scripts/tests can treat repeated
                // invocations as safe.
                var alreadySeeded = await _db.Companies
                    .IgnoreQueryFilters()
                    .AnyAsync(c => c.IsDemo, cancellationToken);
                if (alreadySeeded)
                {
                    _logger.LogInformation(
                        "[DEMO-SEED] Demo data already present (v{Version}); skipping re-seed.",
                        _options.SeedVersion);
                    return new DemoSeedResult
                    {
                        IsSuccess = true,
                        WasDryRun = true,
                        Message = $"Demo data already seeded (v{_options.SeedVersion}); no changes made.",
                        CompaniesCreated = await _db.Companies.IgnoreQueryFilters().Where(c => c.IsDemo).CountAsync(cancellationToken),
                        EmployeesCreated = await _db.Employees.IgnoreQueryFilters().Where(e => e.IsDemo).CountAsync(cancellationToken)
                    };
                }

                return await ExecuteSeedAsync(verbose, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DEMO-SEED] Seed operation failed");
            return new DemoSeedResult
            {
                IsSuccess = false,
                WasDryRun = dryRun,
                Message = "Seed operation failed with exception",
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Cleanup operation - deletes all demo records.
    /// </summary>
    public async Task<DemoCleanupResult> CleanupAsync(
        bool dryRun = true,
        bool confirmCleanup = false,
        bool verbose = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[DEMO-CLEANUP] Starting cleanup operation (dryRun={DryRun})", dryRun);

        // Safety check: require explicit confirmation for actual cleanup
        if (!dryRun && !confirmCleanup)
        {
            _logger.LogWarning("[DEMO-CLEANUP] Cleanup blocked: confirmCleanup not set");
            return new DemoCleanupResult
            {
                IsSuccess = false,
                WasDryRun = true,
                Message = "Cleanup requires explicit confirmCleanup=true to proceed with actual deletion",
                ErrorMessage = "confirmCleanup not confirmed"
            };
        }

        try
        {
            if (dryRun)
            {
                return await DryRunCleanupAsync(verbose, cancellationToken);
            }
            else
            {
                return await ExecuteCleanupAsync(verbose, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DEMO-CLEANUP] Cleanup operation failed");
            return new DemoCleanupResult
            {
                IsSuccess = false,
                WasDryRun = dryRun,
                Message = "Cleanup operation failed with exception",
                ErrorMessage = ex.Message
            };
        }
    }

    // ── Private implementation methods ────────────────────────────────────────

    private async Task<DemoSeedResult> DryRunAsync(bool verbose, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DEMO-SEED-DRYRUN] Previewing demo seed operation");

        var result = new DemoSeedResult
        {
            IsSuccess = true,
            WasDryRun = true,
            Message = "[DRY-RUN] Demo Seed Operation Preview",
            CompaniesCreated = DemoCompanies.Length,
            EmployeesCreated = 500,
            AttendanceRecordsCreated = 500 * ATTENDANCE_HISTORY_DAYS,
            LeaveRequestsCreated = 250,
            AssetsCreated = 300,
            CandidatesCreated = 200,
            UsersCreated = 15
        };

        if (verbose)
        {
            _logger.LogInformation("[DRY-RUN] Demo companies to create: {Count}", result.CompaniesCreated);
            _logger.LogInformation("[DRY-RUN] Demo employees to create: ~{Count}", result.EmployeesCreated);
            _logger.LogInformation("[DRY-RUN] Attendance records: ~{Count}", result.AttendanceRecordsCreated);
            _logger.LogInformation("[DRY-RUN] Total records to create: ~{Count}", result.TotalRecordsCreated);
            _logger.LogInformation("[DRY-RUN] Environment: {Environment}", _environment.EnvironmentName);
            _logger.LogInformation("[DRY-RUN] Status: SAFE TO PROCEED (no database modifications)");
        }

        return result;
    }

    private async Task<DemoSeedResult> ExecuteSeedAsync(bool verbose, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DEMO-SEED-EXEC] Executing demo seed");

        var result = new DemoSeedResult
        {
            IsSuccess = false,
            WasDryRun = false,
            ExecutedAt = DateTime.UtcNow
        };

        // Use transaction for atomicity
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Create demo companies
            var companies = await CreateDemoCompaniesAsync(cancellationToken);
            result.CompaniesCreated = companies.Count;
            _logger.LogInformation("[DEMO-SEED-EXEC] Created {Count} demo companies", result.CompaniesCreated);

            // 2. Create demo employees
            var employees = await CreateDemoEmployeesAsync(companies, cancellationToken);
            result.EmployeesCreated = employees.Count;
            _logger.LogInformation("[DEMO-SEED-EXEC] Created {Count} demo employees", result.EmployeesCreated);

            // 3. Create demo leave balances
            await CreateDemoLeaveBalancesAsync(employees, cancellationToken);

            // 4. Create demo attendance records
            var attendanceCount = await CreateDemoAttendanceAsync(employees, cancellationToken);
            result.AttendanceRecordsCreated = attendanceCount;
            _logger.LogInformation("[DEMO-SEED-EXEC] Created {Count} demo attendance records", result.AttendanceRecordsCreated);

            // 5. Create demo leave requests
            var leaveCount = await CreateDemoLeaveRequestsAsync(employees, cancellationToken);
            result.LeaveRequestsCreated = leaveCount;
            _logger.LogInformation("[DEMO-SEED-EXEC] Created {Count} demo leave requests", result.LeaveRequestsCreated);

            // 6. Create demo assets
            var assetCount = await CreateDemoAssetsAsync(employees, cancellationToken);
            result.AssetsCreated = assetCount;
            _logger.LogInformation("[DEMO-SEED-EXEC] Created {Count} demo assets", result.AssetsCreated);

            // 6b. Create demo payslips (one payslip per employee for the current month)
            var payslipCount = await CreateDemoPayslipsAsync(employees, cancellationToken);
            result.PayslipsCreated = payslipCount;
            _logger.LogInformation("[DEMO-SEED-EXEC] Created {Count} demo payslips", result.PayslipsCreated);

            // 7. Create demo recruitment candidates
            var candidateCount = await CreateDemoCandidatesAsync(companies, cancellationToken);
            result.CandidatesCreated = candidateCount;
            _logger.LogInformation("[DEMO-SEED-EXEC] Created {Count} demo candidates", result.CandidatesCreated);

            // 8. Create demo users
            var userCount = await CreateDemoUsersAsync(companies, cancellationToken);
            result.UsersCreated = userCount;
            _logger.LogInformation("[DEMO-SEED-EXEC] Created {Count} demo users", result.UsersCreated);

            await transaction.CommitAsync(cancellationToken);

            result.IsSuccess = true;
            result.Message = $"Demo data successfully seeded (v{_options.SeedVersion})";
            _logger.LogInformation("[DEMO-SEED-EXEC] Seed completed successfully. Total records: {Total}", result.TotalRecordsCreated);

            // Record this seed run for audit/idempotency visibility. Written in its own
            // SaveChanges call after the main transaction commits so a tracker write failure
            // never rolls back already-committed demo data.
            try
            {
                _db.DemoSeedTrackers.Add(new DemoSeedTracker
                {
                    SeedVersion               = _options.SeedVersion,
                    CreatedCompanyCount       = result.CompaniesCreated,
                    CreatedEmployeeCount      = result.EmployeesCreated,
                    CreatedAttendanceCount    = result.AttendanceRecordsCreated,
                    CreatedLeaveRequestCount  = result.LeaveRequestsCreated,
                    CreatedAssetCount         = result.AssetsCreated,
                    CreatedCandidateCount     = result.CandidatesCreated,
                    CreatedUserCount          = result.UsersCreated,
                    ExecutedAt                = result.ExecutedAt,
                    Environment               = _environment.EnvironmentName,
                    IsSuccess                 = true
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception trackerEx)
            {
                // Never fail a successful seed because the audit tracker write failed.
                _logger.LogWarning(trackerEx, "[DEMO-SEED-EXEC] Seed succeeded but writing DemoSeedTracker failed.");
            }

            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "[DEMO-SEED-EXEC] Seed failed, transaction rolled back");
            
            result.IsSuccess = false;
            result.Message = "Seed operation failed and rolled back";
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private async Task<DemoCleanupResult> DryRunCleanupAsync(bool verbose, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DEMO-CLEANUP-DRYRUN] Previewing demo cleanup");

        var result = new DemoCleanupResult
        {
            IsSuccess = true,
            WasDryRun = true,
            Message = "[DRY-RUN] Demo Cleanup Preview"
        };

        // Count all demo records
        result.CompaniesDeleted = await _db.Companies.IgnoreQueryFilters().Where(c => c.IsDemo).CountAsync(cancellationToken);
        result.EmployeesDeleted = await _db.Employees.IgnoreQueryFilters().Where(e => e.IsDemo).CountAsync(cancellationToken);
        result.AttendanceRecordsDeleted = await _db.WebAttendances.IgnoreQueryFilters().Where(a => a.IsDemo).CountAsync(cancellationToken);
        result.LeaveRequestsDeleted = await _db.LeaveRequests.IgnoreQueryFilters().Where(l => l.IsDemo).CountAsync(cancellationToken);
        result.AssetsDeleted = await _db.Assets.IgnoreQueryFilters().Where(a => a.IsDemo).CountAsync(cancellationToken);
        result.CandidatesDeleted = await _db.Candidates.IgnoreQueryFilters().Where(c => c.IsDemo).CountAsync(cancellationToken);
        result.UsersDeleted = await _db.Users.IgnoreQueryFilters().Where(u => u.IsDemo).CountAsync(cancellationToken);
        result.PayslipsDeleted = await _db.Payslips.IgnoreQueryFilters().Where(p => p.IsDemo).CountAsync(cancellationToken);

        if (verbose)
        {
            _logger.LogInformation("[DRY-RUN-CLEANUP] Companies: {Count}", result.CompaniesDeleted);
            _logger.LogInformation("[DRY-RUN-CLEANUP] Employees: {Count}", result.EmployeesDeleted);
            _logger.LogInformation("[DRY-RUN-CLEANUP] Attendance: {Count}", result.AttendanceRecordsDeleted);
            _logger.LogInformation("[DRY-RUN-CLEANUP] Leave Requests: {Count}", result.LeaveRequestsDeleted);
            _logger.LogInformation("[DRY-RUN-CLEANUP] Total: {Count}", result.TotalRecordsDeleted);
        }

        return result;
    }

    private async Task<DemoCleanupResult> ExecuteCleanupAsync(bool verbose, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DEMO-CLEANUP-EXEC] Executing cleanup (deleting IsDemo=true records)");

        var result = new DemoCleanupResult
        {
            IsSuccess = false,
            WasDryRun = false,
            ExecutedAt = DateTime.UtcNow
        };

        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // FIX SAFETY: Previously this deleted ALL assets, ALL attendance, ALL leave
            // requests, ALL candidates, and ALL non-deleted users regardless of IsDemo,
            // meaning running cleanup against a database containing real customer data
            // would destroy it. Every entity below now has an IsDemo column (see
            // migration 20260819000001_AddIsDemoColumn / AddIsDemoColumnsAndDemoSeedTracker),
            // so cleanup is scoped to exactly the rows this service created.
            //
            // FIX: ExecuteDeleteAsync (EF Core bulk delete) is translated to a single SQL
            // statement and is only supported by relational providers. The EF Core InMemory
            // provider used by this class's unit tests throws InvalidOperationException at
            // runtime for any ExecuteDelete/ExecuteUpdate call, which meant CleanupAsync always
            // failed under test with IsSuccess=false (silently swallowed by the catch block
            // below). Load-then-RemoveRange works identically on InMemory and MySQL — cleanup is
            // an infrequent maintenance operation, not a hot path, so the extra round-trip cost
            // versus a single bulk DELETE statement is immaterial.
            var employeesToDelete = await _db.Employees.IgnoreQueryFilters().Where(e => e.IsDemo).ToListAsync(cancellationToken);
            _db.Employees.RemoveRange(employeesToDelete);
            result.EmployeesDeleted = employeesToDelete.Count;

            var assetsToDelete = await _db.Assets.IgnoreQueryFilters().Where(a => a.IsDemo).ToListAsync(cancellationToken);
            _db.Assets.RemoveRange(assetsToDelete);
            result.AssetsDeleted = assetsToDelete.Count;

            var attendanceToDelete = await _db.WebAttendances.IgnoreQueryFilters().Where(a => a.IsDemo).ToListAsync(cancellationToken);
            _db.WebAttendances.RemoveRange(attendanceToDelete);
            result.AttendanceRecordsDeleted = attendanceToDelete.Count;

            var leaveRequestsToDelete = await _db.LeaveRequests.IgnoreQueryFilters().Where(l => l.IsDemo).ToListAsync(cancellationToken);
            _db.LeaveRequests.RemoveRange(leaveRequestsToDelete);
            result.LeaveRequestsDeleted = leaveRequestsToDelete.Count;

            var candidatesToDelete = await _db.Candidates.IgnoreQueryFilters().Where(c => c.IsDemo).ToListAsync(cancellationToken);
            _db.Candidates.RemoveRange(candidatesToDelete);
            result.CandidatesDeleted = candidatesToDelete.Count;

            var payslipsToDelete = await _db.Payslips.IgnoreQueryFilters().Where(p => p.IsDemo).ToListAsync(cancellationToken);
            _db.Payslips.RemoveRange(payslipsToDelete);
            result.PayslipsDeleted = payslipsToDelete.Count;

            var usersToDelete = await _db.Users.IgnoreQueryFilters().Where(u => u.IsDemo).ToListAsync(cancellationToken);
            _db.Users.RemoveRange(usersToDelete);
            result.UsersDeleted = usersToDelete.Count;

            var companiesToDelete = await _db.Companies.IgnoreQueryFilters().Where(c => c.IsDemo).ToListAsync(cancellationToken);
            _db.Companies.RemoveRange(companiesToDelete);
            result.CompaniesDeleted = companiesToDelete.Count;

            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            result.IsSuccess = true;
            result.Message = "Demo data successfully cleaned up";
            _logger.LogInformation("[DEMO-CLEANUP-EXEC] Cleanup completed. Records deleted: {Total}", result.TotalRecordsDeleted);

            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "[DEMO-CLEANUP-EXEC] Cleanup failed, transaction rolled back");

            result.IsSuccess = false;
            result.Message = "Cleanup failed and rolled back";
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    // ── Data creation helper methods ──────────────────────────────────────────

    private async Task<List<Company>> CreateDemoCompaniesAsync(CancellationToken cancellationToken)
    {
        var companies = new List<Company>();
        foreach (var def in DemoCompanies)
        {
            var company = new Company
            {
                Id = def.Id,
                CompanyName = def.Name,
                IndustryType = def.Industry,
                BusinessType = "Private Limited",
                AddressLine1 = $"{def.Location}, India",
                City = def.Location,
                Country = "India",
                IsActive = true,
                IsDemo = true,
                CreatedAt = DateTime.UtcNow
            };
            companies.Add(company);
        }

        _db.Companies.AddRange(companies);
        await _db.SaveChangesAsync(cancellationToken);
        return companies;
    }

    private async Task<List<Employee>> CreateDemoEmployeesAsync(List<Company> companies, CancellationToken cancellationToken)
    {
        var random = new Random(SEED_RANDOM_SEED);
        var employees = new List<Employee>();

        var firstNames = new[] { "Raj", "Priya", "Amit", "Anjali", "Vivek", "Neha", "Arjun", "Divya", "Rohan", "Sneha" };
        var lastNames = new[] { "Sharma", "Kumar", "Singh", "Patel", "Gupta", "Verma", "Rao", "Nair", "Desai", "Iyer" };

        // ~100 employees per company
        foreach (var company in companies)
        {
            for (int i = 0; i < 100; i++)
            {
                var firstName = firstNames[random.Next(firstNames.Length)];
                var lastName = lastNames[random.Next(lastNames.Length)];
                var empCode = $"EMP{company.Id}{i:D4}";

                var employee = new Employee
                {
                    CompanyId = company.Id,
                    EmployeeCode = empCode,
                    FirstName = firstName,
                    LastName = lastName,
                    FullName = $"{firstName} {lastName}",
                    Email = $"{firstName.ToLower()}.{lastName.ToLower()}@demo.ratanhr.local",
                    PhoneNumber = $"98{random.Next(1000, 9999):D4}{random.Next(10000, 99999)}",
                    Gender = random.Next(2) == 0 ? "Male" : "Female",
                    Status = "Active",
                    IsActive = true,
                    IsDemo = true,
                    CreatedAt = DateTime.UtcNow
                };

                employees.Add(employee);
            }
        }

        _db.Employees.AddRange(employees);
        await _db.SaveChangesAsync(cancellationToken);
        return employees;
    }

    private async Task CreateDemoLeaveBalancesAsync(List<Employee> employees, CancellationToken cancellationToken)
    {
        var balances = new List<LeaveBalance>();
        var leaveTypes = await _db.LeaveTypes.ToListAsync(cancellationToken);
        var currentYear = DateTime.UtcNow.Year;

        foreach (var employee in employees)
        {
            foreach (var leaveType in leaveTypes)
            {
                var balance = new LeaveBalance
                {
                    CompanyId = employee.CompanyId,
                    EmployeeId = employee.EmployeeCode,
                    LeaveTypeId = leaveType.Id,
                    Year = currentYear,
                    CreatedAt = DateTime.UtcNow
                };
                balances.Add(balance);
            }
        }

        _db.LeaveBalances.AddRange(balances);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> CreateDemoAttendanceAsync(List<Employee> employees, CancellationToken cancellationToken)
    {
        var random = new Random(SEED_RANDOM_SEED);
        var attendance = new List<WebAttendance>();
        var statuses = new[] { "Present", "Absent", "Leave", "Half Day", "Work From Home" };
        var startDate = DateTime.UtcNow.AddDays(-ATTENDANCE_HISTORY_DAYS);

        foreach (var employee in employees)
        {
            for (int d = 0; d < ATTENDANCE_HISTORY_DAYS; d++)
            {
                var date = startDate.AddDays(d);
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                var status = statuses[random.Next(statuses.Length)];
                var att = new WebAttendance
                {
                    CompanyId = employee.CompanyId,
                    EmployeeId = employee.Id.ToString(),
                    AttDate = DateOnly.FromDateTime(date),
                    Status = status,
                    IsDemo = true,
                    CreatedAt = DateTime.UtcNow
                };
                attendance.Add(att);
            }
        }

        _db.WebAttendances.AddRange(attendance);
        await _db.SaveChangesAsync(cancellationToken);
        return attendance.Count;
    }

    private async Task<int> CreateDemoLeaveRequestsAsync(List<Employee> employees, CancellationToken cancellationToken)
    {
        var random = new Random(SEED_RANDOM_SEED);
        var leaveRequests = new List<LeaveRequest>();
        var leaveTypes = await _db.LeaveTypes.ToListAsync(cancellationToken);
        var statuses = new[] { "Approved", "Pending", "Rejected" };

        foreach (var employee in employees.Take(200))
        {
            for (int i = 0; i < 2; i++)
            {
                var leaveType = leaveTypes[random.Next(leaveTypes.Count)];
                var startDate = DateTime.UtcNow.AddDays(random.Next(-90, 90));

                var request = new LeaveRequest
                {
                    CompanyId = employee.CompanyId,
                    EmployeeId = employee.EmployeeCode,
                    LeaveTypeId = leaveType.Id,
                    StartDate = DateOnly.FromDateTime(startDate),
                    EndDate = DateOnly.FromDateTime(startDate.AddDays(random.Next(1, 5))),
                    Reason = "Personal leave",
                    Status = statuses[random.Next(statuses.Length)],
                    IsDemo = true,
                    CreatedAt = DateTime.UtcNow
                };
                leaveRequests.Add(request);
            }
        }

        _db.LeaveRequests.AddRange(leaveRequests);
        await _db.SaveChangesAsync(cancellationToken);
        return leaveRequests.Count;
    }

    private async Task<int> CreateDemoAssetsAsync(List<Employee> employees, CancellationToken cancellationToken)
    {
        var random = new Random(SEED_RANDOM_SEED);
        var assets = new List<Asset>();
        var assetTypes = new[] { "Laptop", "Desktop", "Monitor", "Keyboard", "Mouse", "Headset", "Mobile", "ID Card", "Printer" };

        foreach (var employee in employees.Take(300))
        {
            var numAssets = random.Next(1, 4);
            for (int i = 0; i < numAssets; i++)
            {
                var asset = new Asset
                {
                    CompanyId = employee.CompanyId,
                    AssetCode = $"AST-{employee.EmployeeCode}-{i:D2}",
                    Name = assetTypes[random.Next(assetTypes.Length)],
                    AssignedToEmployeeId = employee.EmployeeCode,
                    Status = "Assigned",
                    IsDemo = true,
                    CreatedAt = DateTime.UtcNow
                };
                assets.Add(asset);
            }
        }

        _db.Assets.AddRange(assets);
        await _db.SaveChangesAsync(cancellationToken);
        return assets.Count;
    }

    private async Task<int> CreateDemoPayslipsAsync(List<Employee> employees, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var payslips = new List<Payslip>();

        foreach (var employee in employees)
        {
            const decimal basic = 30000m;
            const decimal hra = 12000m;
            const decimal da = 3000m;
            const decimal pf = 1800m;
            var gross = basic + hra + da;
            var deductions = pf;

            var payslip = new Payslip
            {
                EmployeeId       = employee.EmployeeCode,
                CompanyId        = employee.CompanyId,
                Month            = now.Month,
                Year             = now.Year,
                WorkingDays      = 26,
                DaysPresent      = 26,
                BasicPay         = basic,
                HRA              = hra,
                DA               = da,
                GrossEarnings    = gross,
                PFEmployee       = pf,
                TotalDeductions  = deductions,
                NetPay           = gross - deductions,
                IsDemo           = true,
                CreatedAt        = now
            };
            payslips.Add(payslip);
        }

        _db.Payslips.AddRange(payslips);
        await _db.SaveChangesAsync(cancellationToken);
        return payslips.Count;
    }

    private async Task<int> CreateDemoCandidatesAsync(List<Company> companies, CancellationToken cancellationToken)
    {
        var random = new Random(SEED_RANDOM_SEED);
        var candidates = new List<Candidate>();

        foreach (var company in companies)
        {
            for (int i = 0; i < 40; i++)
            {
                var candidate = new Candidate
                {
                    CompanyId = company.Id,
                    Email = $"candidate{i}@demo.example.com",
                    Phone = $"98{random.Next(1000, 9999):D4}{random.Next(10000, 99999)}",
                    IsDemo = true,
                    CreatedAt = DateTime.UtcNow
                };
                candidates.Add(candidate);
            }
        }

        _db.Candidates.AddRange(candidates);
        await _db.SaveChangesAsync(cancellationToken);
        return candidates.Count;
    }

    private async Task<int> CreateDemoUsersAsync(List<Company> companies, CancellationToken cancellationToken)
    {
        var users = new List<User>();
        var roles = new[] { "HR", "Manager", "Employee", "Payroll", "Recruiter" };

        // Create demo users for each company (3 users per company)
        foreach (var company in companies)
        {
            for (int i = 0; i < 3; i++)
            {
                var role = roles[i % roles.Length];
                
                // Use the same password hashing mechanism as the application's AuthService
                var demoPassword = $"Demo@{company.Id}{i}#2026";
                var hashedPassword = BcryptPasswordHasher.Hash(demoPassword, _configuration);

                var user = new User
                {
                    Email = $"demo{company.Id}.user{i}@demo.ratanhr.local",
                    PasswordHash = hashedPassword,  // ✅ FIXED: Use application's BCrypt hasher
                    FullName = $"Demo {role} User {i}",
                    IsActive = true,
                    IsDeleted = false,
                    IsDemo = true,
                    MustChangePassword = true,  // ✅ Force password change on first login
                    CreatedAt = DateTime.UtcNow
                };
                users.Add(user);
                
                _logger.LogDebug("[DEMO-USER] Created user: {Email} (password: {Password})", user.Email, demoPassword);
            }
        }

        _db.Users.AddRange(users);
        await _db.SaveChangesAsync(cancellationToken);
        return users.Count;
    }
}

/// <summary>Demo company metadata definition.</summary>
public record DemoCompanyDefinition(
    int Id,
    string Code,
    string Name,
    string Industry,
    string Location,
    string Description = "");
