# HIGH Priority Fixes — Implementation Guide

**Status:** 4/8 HIGH fixes applied ✅  
**Remaining:** 4 HIGH fixes with detailed implementation instructions

---

## ✅ COMPLETED (4/8)

### HIGH-1: Rate Limit on PII Endpoint ✅
- **File:** `HRMS.API/Controllers/Employees/EmployeeController.cs`
- **Change:** Added `[EnableRateLimiting("sensitive")]` to `GetPii()` method
- **Effect:** PII endpoint limited to 5 requests/minute per IP
- **Status:** DEPLOYED

### HIGH-2: Email:Host Validation ✅
- **File:** `HRMS.API/Program.cs`
- **Change:** Added production-only validation that throws if Email:Host is missing
- **Line:** After `PasswordPolicy.Configure()` (~line 705)
- **Code:**
```csharp
if (app.Environment.IsProduction())
{
    var emailHost = builder.Configuration["Email:Host"];
    if (string.IsNullOrWhiteSpace(emailHost))
    {
        throw new InvalidOperationException(
            "Email:Host is required in Production. Set Email__Host environment variable.");
    }
}
```
- **Status:** DEPLOYED

### HIGH-3: Leave Balance Reset Job ✅
- **File:** `HRMS.Infrastructure/Jobs/LeaveBalanceResetJob.cs` (NEW)
- **Purpose:** Automatically resets employee leave balances annually (April 1st)
- **Idempotent:** Yes — checks for existing reset adjustments, won't double-apply
- **Registered in Program.cs:**
```csharp
recurringJobs.AddOrUpdate<LeaveBalanceResetJob>(
    "leave-balance-reset",
    j => j.RunAsync(),
    Hangfire.Cron.DayOfMonth(1),
    timeZone: TimeZoneInfo.Utc);
```
- **Status:** DEPLOYED

### HIGH-4: Audit Log Prune Job ✅
- **File:** `HRMS.Infrastructure/Jobs/AuditLogPruneJob.cs` (NEW)
- **Purpose:** Deletes audit logs older than 90 days (weekly, Sunday 2 AM UTC)
- **Retention:** 90 days (configurable via RetentionDays constant)
- **Registered in Program.cs:**
```csharp
recurringJobs.AddOrUpdate<AuditLogPruneJob>(
    "audit-log-prune",
    j => j.RunAsync(),
    "0 2 * * 0",
    timeZone: TimeZoneInfo.Utc);
```
- **Status:** DEPLOYED

---

## ⏳ REMAINING (4/8)

### HIGH-5: Attendance Summary Caching

**Difficulty:** Medium | **Time:** 30 min  
**File:** `HRMS.Infrastructure/Services/AttendanceService.cs`

**Current Issue:**
- `GetTodayAttendanceSummaryAsync()` recalculates attendance stats on every request
- Dashboard loads multiple times per user session → O(n) queries each time

**Fix Approach:**
1. Inject `IDistributedCache` into `AttendanceService` constructor
2. Check cache before calculation
3. If miss, calculate summary
4. Store in cache with 1-hour TTL
5. Cache key: `attendance:summary:{companyId}:{date:yyyy-MM-dd}`

**Code Pattern:**
```csharp
private readonly IDistributedCache _cache;

public async Task<AttendanceSummary> GetTodayAttendanceSummaryAsync(int companyId)
{
    var today = DateTime.UtcNow.Date;
    var cacheKey = $"attendance:summary:{companyId}:{today:yyyy-MM-dd}";
    
    // Try cache first
    var cached = await _cache.GetAsync<AttendanceSummary>(cacheKey);
    if (cached != null)
        return cached;
    
    // Calculate
    var summary = await _calculateSummaryAsync(companyId, today);
    
    // Store in cache for 1 hour
    await _cache.SetAsync(cacheKey, summary, TimeSpan.FromHours(1));
    
    return summary;
}
```

**Testing:**
```bash
# Call endpoint twice
curl https://localhost/api/attendance/summary
curl https://localhost/api/attendance/summary

# Second call should be much faster (cached)
# Verify: Check Redis with redis-cli KEYS "attendance:*"
```

**Expected Impact:** Dashboard load time reduced by 50-70%

---

### HIGH-6: Dynamic Rate Limiter Configuration

**Difficulty:** Low | **Time:** 20 min  
**Files:** 
- `HRMS.API/appsettings.json` (already updated)
- `HRMS.API/Program.cs` (rate limiter section, ~line 640)

**Current Issue:**
- Rate limits hardcoded in Program.cs (10, 5, 120, 20, 10)
- Changing limits requires code change + rebuild + redeploy

**Fix Approach:**
1. Read limits from `appsettings.json` → `RateLimiting` section (already added ✅)
2. Pass values to rate limiter policies instead of hardcoded numbers

**Code Changes in Program.cs (rate limiter section):**

```csharp
var rateLimits = new
{
    Login = builder.Configuration.GetValue("RateLimiting:LoginLimitPerMinute", 10),
    Sensitive = builder.Configuration.GetValue("RateLimiting:SensitiveLimitPerMinute", 5),
    Api = builder.Configuration.GetValue("RateLimiting:ApiLimitPerMinute", 120),
    Upload = builder.Configuration.GetValue("RateLimiting:UploadLimitPerMinute", 20),
    Reports = builder.Configuration.GetValue("RateLimiting:ReportsLimitPerMinute", 10)
};

Log.Information("Rate limits: Login={L}, Sensitive={S}, API={A}, Upload={U}, Reports={R}",
    rateLimits.Login, rateLimits.Sensitive, rateLimits.Api, rateLimits.Upload, rateLimits.Reports);

// Then in each policy, use rateLimits.PropertyName instead of hardcoded number
// Before: RedisDistributedRateLimiter.CreatePartition(..., 10, 60)
// After:  RedisDistributedRateLimiter.CreatePartition(..., rateLimits.Login, 60)
```

**Testing:**
```bash
# Development — change appsettings.Development.json
"RateLimiting": { "LoginLimitPerMinute": 3 }  # Reduce to 3 for testing
# Restart app
# Attempt login 4 times → 4th should get 429

# Production — set environment variable
export RateLimiting__ApiLimitPerMinute=200
# Verify with:
docker exec hrms-api env | grep RateLimiting
```

**Expected Impact:** Operators can tune rate limits per environment without redeploying

---

### HIGH-7: Department Soft-Delete Cascade Logic

**Difficulty:** Medium | **Time:** 25 min  
**File:** `HRMS.Infrastructure/Services/DepartmentService.cs`

**Current Issue:**
- When a department is soft-deleted (`IsDeleted = true`), employees keep their `DepartmentId`
- This creates orphaned references and potential UI bugs
- Reporting queries may reference deleted departments

**Fix Approach:**
1. Add new method `SoftDeleteAsync(int deptId, int companyId)` to `DepartmentService`
2. When soft-deleting, also clear `DepartmentId = null` on all employees in that department
3. Update the delete endpoint to use this method instead of direct `IsDeleted = true`

**Code to Add:**

```csharp
public async Task<bool> SoftDeleteAsync(int deptId, int companyId)
{
    // Verify department exists and belongs to caller's company
    var dept = await _db.Departments
        .FirstOrDefaultAsync(d => d.Id == deptId && d.CompanyId == companyId);
    
    if (dept == null || dept.IsDeleted)
        return false; // Already deleted or not found
    
    // Mark department as deleted
    dept.IsDeleted = true;
    
    // Cascade: clear DepartmentId on all employees
    var employees = await _db.Employees
        .Where(e => e.DepartmentId == deptId && e.CompanyId == companyId)
        .ToListAsync();
    
    foreach (var emp in employees)
        emp.DepartmentId = null;
    
    await _db.SaveChangesAsync();
    
    // Audit log
    await _auditService.LogAsync(
        "DEPARTMENT_DELETE",
        "Department",
        deptId.ToString(),
        userId: null,
        details: $"Soft-deleted department. Cleared DepartmentId on {employees.Count} employees."
    );
    
    return true;
}
```

**Update Delete Endpoint:**

```csharp
// In DepartmentController.Delete():
// Before: dept.IsDeleted = true; await _db.SaveChangesAsync();
// After:  await _deptService.SoftDeleteAsync(id, companyId);
```

**Testing:**
```bash
# 1. Create department with employees
# 2. Delete department
# 3. Query employees
curl https://localhost/api/employees | jq '.[] | select(.employeeId=="E001") | .department'
# Expected: null (cleared by cascade)
```

**Expected Impact:** Data consistency — no orphaned employee-department references

---

### HIGH-8: DateTime Precision (ALREADY FIXED ✅)

- All DateTime columns mapped to `datetime(6)` in `ApplicationDbContext`
- Microsecond precision ensured by EF Core migrations
- No action required

---

## 🔧 Implementation Checklist

- [ ] HIGH-5: Add caching layer to AttendanceService
  - [ ] Inject IDistributedCache
  - [ ] Add cache check/write logic
  - [ ] Test with 2 requests, verify 2nd is faster
  
- [ ] HIGH-6: Make rate limiter config dynamic
  - [ ] Read config values at startup
  - [ ] Pass to each policy instead of hardcoded
  - [ ] Log the config values
  - [ ] Test by changing appsettings and restarting
  
- [ ] HIGH-7: Add department cascade delete
  - [ ] Create SoftDeleteAsync method
  - [ ] Update delete endpoint to call it
  - [ ] Test: verify employees cleared on delete
  - [ ] Verify audit log entry created

---

## 📊 Progress Summary

```
CRITICAL:   3/3 ✅ (100%) - Production ready
HIGH:       4/8 ✅ (50%)  - 4 remaining
MEDIUM:    0/18 (0%)  - Backlog
LOW:       0/18 (0%)  - Nice-to-haves

Total:     7/47 (15%)
```

---

## 🚀 Deployment Steps

### Week 1 — Deploy HIGH Fixes

**Monday:**
- ✅ Deploy CRITICAL fixes to production
- ✅ Run verification tests

**Tuesday-Wednesday:**
- Implement HIGH-5 (Attendance Caching)
- Implement HIGH-6 (Rate Limiter Config)

**Thursday-Friday:**
- Implement HIGH-7 (Department Cascade)
- Merge all to main branch
- Tag release `v1.0.5-high-fixes`
- Deploy to staging
- Smoke tests

**Next Week:**
- 24h staging validation
- Deploy to production
- Monitor logs and performance

---

## 📞 Support

For questions or blockers:
1. Check BUGS_AND_FIXES_QUICK_REFERENCE.md for code examples
2. Search logs for "HIGH-X" fix comments
3. Review test cases in HRMS.Tests/

