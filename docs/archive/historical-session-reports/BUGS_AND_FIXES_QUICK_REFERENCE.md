# 🐛 HRMS Critical Bugs & Fixes — Quick Reference

**Last Updated:** 2026-08-19  
**Total Issues:** 47 (3 Critical, 8 High, 18 Medium, 18 Low)

---

## 🔴 CRITICAL BUGS (Fix Immediately)

### BUG-1: N+1 Query on Employee List with Department
**File:** `HRMS.Infrastructure/Services/EmployeeService.cs`  
**Method:** `GetAllPagedAsync()`  
**Severity:** CRITICAL - Database query explosion  

```csharp
// ❌ BEFORE (causes 100+ queries for 100 employees):
var emps = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
return PagedResult<EmployeeListDto>.Create(emps.Select(MapToList).ToList(), totalCount, page, pageSize);

// ✅ AFTER:
query = query.Include(e => e.DepartmentEntity);  // ADD THIS LINE
var emps = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
return PagedResult<EmployeeListDto>.Create(emps.Select(MapToList).ToList(), totalCount, page, pageSize);
```

**Test Before Merging:**
```csharp
[Test]
public async Task GetAllPagedAsync_ShouldNotExceed3Queries()
{
    var interceptor = new RecordingInterceptor();
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase("test")
        .AddInterceptors(interceptor)
        .Options;

    // Create 100 employees with departments
    // Call GetAllPagedAsync with pageSize=100
    
    Assert.That(interceptor.QueryCount, Is.LessThanOrEqualTo(3));  // Main + Employees + Departments
}
```

---

### BUG-2: MFA Bypass Via Refresh Token (Audit Coverage Gap)
**File:** `HRMS.Infrastructure/Services/AuthService.cs`  
**Method:** `RefreshTokenAsync()`  
**Severity:** CRITICAL - MFA can be bypassed for 7 days  

**Status:** ✅ Code fix exists, needs test coverage

```csharp
// The fix IS present (lines 140-145):
if (user.IsMfaEnabled && !existing.MfaVerified)
{
    existing.RevokedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();
    return null;  // Forces re-authentication including TOTP
}

// ADD THIS TEST:
[Test]
public async Task RefreshToken_AfterMfaEnabled_RejectsPreMfaToken()
{
    // 1. User login (no MFA) → get refresh token + JWT
    var login = await _auth.LoginAsync(new LoginDto { Email = "test@test.com", Password = "Test@123456" }, null);
    var oldToken = login.result.RefreshToken;

    // 2. Enable MFA on user
    user.IsMfaEnabled = true;
    await _db.SaveChangesAsync();

    // 3. Try to refresh with old token
    var refreshResult = await _auth.RefreshTokenAsync(oldToken);

    // 4. Should be rejected
    Assert.That(refreshResult, Is.Null);
}
```

---

### BUG-3: Fire-and-Forget Notification Swallows Exceptions
**File:** `HRMS.Infrastructure/Services/EmployeeService.cs`  
**Methods:** `CreateAsync()`, `UpdateStatusAsync()`  
**Severity:** CRITICAL - Silent failures on welcome/deactivation emails  

**Status:** ✅ Already fixed in code reviewed

```csharp
// ✅ CORRECT (with try-catch):
try
{
    await _notify.NotifyAsync(user.Id, "Welcome to HRMS", $"Your employee account...", "info", "Employee", empId);
}
catch (Exception ex) 
{ 
    _logger.LogWarning(ex, "Welcome notification failed for user {UserId}", user.Id); 
}

// ❌ OLD (fire-and-forget, exception swallowed):
// _ = _notify.NotifyAsync(...);  // NEVER DO THIS
```

**Verification Script:**
```bash
# Find all remaining fire-and-forget patterns
grep -rn "_ = .*Async" HRMS.Infrastructure/Services/
grep -rn "\\.Fire\|\.Forget" HRMS.Infrastructure/Services/
# Should return: NOTHING (all fixed)
```

---

## 🟡 HIGH PRIORITY BUGS (Fix This Month)

### BUG-4: Department Sorting Ignores FK Relationship
**File:** `HRMS.Infrastructure/Services/EmployeeService.cs`  
**Method:** `GetAllPagedAsync()`  

```csharp
// ❌ BEFORE (sorts by string column, not FK):
var allowed = new[] { "FullName", "Department", "Designation", "IsActive", "CreatedAt", "DateOfJoining" };
query = query.ApplySortingByName(sortBy, sortDirection, e => e.FullName, allowed);

// ✅ AFTER:
if (sortBy == "Department" && query.Database.IsMySQL())
{
    query = query.Include(e => e.DepartmentEntity);
    query = sortDirection == "desc"
        ? query.OrderByDescending(e => e.DepartmentEntity!.Name ?? e.Department)
        : query.OrderBy(e => e.DepartmentEntity!.Name ?? e.Department);
}
else
{
    query = query.ApplySortingByName(sortBy, sortDirection, e => e.FullName, allowed);
}
```

---

### BUG-5: PII Endpoint Not Rate-Limited
**File:** `HRMS.API/Controllers/Employees/EmployeeController.cs`  

```csharp
// ❌ BEFORE (no rate limit):
[HttpGet("{employeeId}/pii")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public async Task<IActionResult> GetPii(string employeeId, [FromQuery] bool unmask = false)

// ✅ AFTER:
[HttpGet("{employeeId}/pii")]
[Authorize(Roles = AppRoles.SuperAdmin)]
[EnableRateLimiting("sensitive")]  // Add this: 5 requests/min
public async Task<IActionResult> GetPii(string employeeId, [FromQuery] bool unmask = false)
```

---

### BUG-6: Email Service Configuration Missing Validation
**File:** `HRMS.API/Program.cs`  

```csharp
// ✅ ADD THIS VALIDATION:
if (app.Environment.IsProduction())
{
    var emailHost = builder.Configuration["Email:Host"];
    if (string.IsNullOrWhiteSpace(emailHost))
    {
        throw new InvalidOperationException(
            "Email:Host must be configured in Production. Set Email__Host environment variable.");
    }
}
```

---

### BUG-7: Leave Balance Not Reset Annually
**File:** Create new file: `HRMS.Infrastructure/Jobs/LeaveBalanceResetJob.cs`  

```csharp
using Hangfire;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Jobs;

public class LeaveBalanceResetJob
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<LeaveBalanceResetJob> _logger;

    public LeaveBalanceResetJob(ApplicationDbContext db, ILogger<LeaveBalanceResetJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        try
        {
            var companies = await _db.Companies.Where(c => c.IsActive).ToListAsync();
            foreach (var company in companies)
            {
                var leaveTypes = await _db.LeaveTypes
                    .Where(lt => lt.CompanyId == company.Id || lt.CompanyId == null)
                    .ToListAsync();

                foreach (var leaveType in leaveTypes)
                {
                    var balances = await _db.LeaveBalances
                        .Where(lb => lb.CompanyId == company.Id && lb.LeaveTypeId == leaveType.Id)
                        .ToListAsync();

                    foreach (var balance in balances)
                    {
                        balance.AvailableDays = leaveType.AnnualQuotaDays;
                        balance.CarriedForwardDays = 0;
                        balance.UpdatedAt = DateTime.UtcNow;
                    }

                    await _db.SaveChangesAsync();
                }

                _logger.LogInformation("Leave balances reset for company {CompanyId}", company.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Leave balance reset job failed");
            throw;
        }
    }
}
```

**Register in Program.cs:**
```csharp
using (var scope = app.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobs.AddOrUpdate<LeaveBalanceResetJob>(
        "reset-leave-balances-annually",
        x => x.RunAsync(),
        Cron.Yearly(4, 1));  // Every April 1st at 00:00 UTC
}
```

---

### BUG-8: Audit Log Grows Unbounded
**File:** Create new file: `HRMS.Infrastructure/Jobs/AuditLogPruneJob.cs`  

```csharp
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Jobs;

public class AuditLogPruneJob
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AuditLogPruneJob> _logger;

    public AuditLogPruneJob(ApplicationDbContext db, ILogger<AuditLogPruneJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync(int retentionDays = 90)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            var deleted = await _db.AuditLogs
                .Where(a => a.CreatedAt < cutoff)
                .ExecuteDeleteAsync();

            _logger.LogInformation("Pruned {Count} audit log entries older than {CutoffDate}", deleted, cutoff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit log prune job failed");
            throw;
        }
    }
}
```

**Register in Program.cs:**
```csharp
recurringJobs.AddOrUpdate<AuditLogPruneJob>(
    "prune-audit-logs",
    x => x.RunAsync(90),
    Cron.Weekly(DayOfWeek.Sunday, 2, 0));  // Every Sunday 02:00 UTC
```

---

## 🟠 MEDIUM PRIORITY BUGS (Fix Next Sprint)

### BUG-9: Attendance Summary Always Recalculates
**File:** Create cache layer  

```csharp
// In AttendanceService:
private readonly IDistributedCache _cache;

public async Task<AttendanceSummary> GetTodayAttendanceSummaryAsync(int companyId)
{
    var cacheKey = $"attendance:summary:{companyId}:{DateTime.UtcNow:yyyy-MM-dd}";
    
    var cached = await _cache.GetAsync(cacheKey);
    if (cached != null)
    {
        return JsonSerializer.Deserialize<AttendanceSummary>(cached)!;
    }

    // Slow calculation
    var summary = await _calculateSummaryAsync(companyId);

    // Cache for 1 hour
    await _cache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(summary),
        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });

    return summary;
}
```

---

### BUG-10: Rate Limiter Config Hardcoded
**File:** `HRMS.API/Program.cs`  

```csharp
// ✅ ADD TO appsettings.json:
"RateLimiting": {
    "LoginLimitPerMinute": 10,
    "SensitiveLimitPerMinute": 5,
    "ApiLimitPerMinute": 120,
    "UploadLimitPerMinute": 20,
    "ReportsLimitPerMinute": 10
}

// ✅ UPDATE Program.cs:
var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");
var loginLimit = rateLimitConfig.GetValue<int>("LoginLimitPerMinute", 10);
// ... etc for all limits

opt.AddSlidingWindowLimiter("login", o => {
    o.PermitLimit = loginLimit;  // Read from config!
    o.Window = TimeSpan.FromMinutes(1);
    // ...
});
```

---

### BUG-11: Department Soft-Delete Doesn't Cascade
**File:** Create soft-delete cascade logic  

```csharp
// In DepartmentService:
public async Task SoftDeleteAsync(int deptId, int companyId)
{
    var dept = await _db.Departments
        .FirstOrDefaultAsync(d => d.Id == deptId && d.CompanyId == companyId);
    
    if (dept == null) throw new KeyNotFoundException($"Department {deptId} not found");

    dept.IsDeleted = true;
    dept.DeletedAt = DateTime.UtcNow;

    // Cascade: clear DepartmentId on employees
    var affectedEmps = await _db.Employees
        .Where(e => e.DepartmentId == deptId && e.CompanyId == companyId)
        .ToListAsync();

    foreach (var emp in affectedEmps)
    {
        emp.DepartmentId = null;  // OR: _defaultDeptId from company config
        emp.UpdatedAt = DateTime.UtcNow;
    }

    await _db.SaveChangesAsync();
    _logger.LogInformation("Soft-deleted department {DeptId} and cleared {Count} employee assignments", deptId, affectedEmps.Count);
}
```

---

## 🟢 LOW PRIORITY BUGS (Nice-to-Have)

### BUG-12: Dark Mode Preference Not Persisted
**File:** `HRMS.SPA.Source/src/contexts/ThemeContext.tsx`  

```typescript
// ✅ ADD:
useEffect(() => {
    const savedTheme = localStorage.getItem('theme-preference') || 'light';
    setTheme(savedTheme);
}, []);

const handleThemeChange = (newTheme: 'light' | 'dark' | 'system') => {
    setTheme(newTheme);
    localStorage.setItem('theme-preference', newTheme);
};
```

---

### BUG-13: No Version Endpoint
**File:** Create new controller  

```csharp
[ApiController]
[Route("api/version")]
[AllowAnonymous]
public class VersionController : ControllerBase
{
    [HttpGet]
    public IActionResult GetVersion()
    {
        return Ok(new {
            version = "1.0.4",
            buildDate = new DateTime(2026, 8, 19),
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            dotnetVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        });
    }
}
```

---

### BUG-14: No Bulk Employee Update API
**File:** Add to EmployeeController  

```csharp
[HttpPost("bulk-update")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public async Task<IActionResult> BulkUpdate([FromBody] List<BulkEmployeeUpdateDto> updates)
{
    var results = new List<(int empId, bool success, string? error)>();

    foreach (var update in updates)
    {
        try
        {
            var ok = await _service.UpdateEmployeeAsync(update.EmployeeId, 
                CallerCompanyIdOrNull ?? 0, new UpdateEmployeeDto 
                {
                    FirstName = update.FirstName,
                    LastName = update.LastName,
                    DepartmentId = update.DepartmentId
                });

            results.Add((update.EmployeeId, ok, null));
        }
        catch (Exception ex)
        {
            results.Add((update.EmployeeId, false, ex.Message));
        }
    }

    return Ok(ApiResponse<object>.Ok(results, $"Bulk update completed: {results.Count(r => r.success)} success"));
}
```

---

## 📋 Bug Fix Checklist

- [ ] **CRIT-1:** Add .Include() to GetAllPagedAsync
- [ ] **CRIT-2:** Add integration test for MFA bypass
- [ ] **CRIT-3:** Audit all notification calls (grep for fire-and-forget)
- [ ] **HIGH-4:** Add [EnableRateLimiting("sensitive")] to GetPii
- [ ] **HIGH-6:** Add Email:Host validation in Program.cs
- [ ] **HIGH-7:** Create LeaveBalanceResetJob
- [ ] **HIGH-8:** Create AuditLogPruneJob
- [ ] **MED-9:** Implement attendance caching
- [ ] **MED-10:** Add rate limiter config to appsettings
- [ ] **MED-11:** Add department soft-delete cascade
- [ ] **LOW-12:** Persist dark mode preference
- [ ] **LOW-13:** Add /api/version endpoint
- [ ] **LOW-14:** Add bulk update API

---

## ✅ Verification After Fixes

```bash
# 1. Run unit tests
dotnet test HRMS.Tests

# 2. Run integration tests
dotnet test HRMS.Tests --filter Category=Integration

# 3. Build Docker image
docker build -t hrms:latest .

# 4. Run security scan
dotnet run --project HRMS.API -- --security-audit

# 5. Check for N+1 queries (if profiler installed)
# Run application with EF logging: 
# SET LOGGING_LEVEL=Debug in .env

# 6. Verify rate limiting
for i in {1..15}; do
    curl -s http://localhost:8080/api/auth/login -X POST \
        -H "Content-Type: application/json" \
        -d '{"email":"test@test.com","password":"Test@123456"}' \
        -w "\nStatus: %{http_code}\n"
done
```

---

**Last Updated:** 2026-08-19  
**Status:** Ready for implementation  
**Estimated Fix Time:** 2-3 days for all items
