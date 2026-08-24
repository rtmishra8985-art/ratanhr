# RatanHR Demo Mode - Implementation Plan

**Status:** PHASE 1 INSPECTION COMPLETE ✅

## Architecture Inspection Results

### Multi-Tenancy Model ✅
- **Primary Tenant Context:** `ITenantContext` (scoped per HTTP request)
- **Implementation:** `TenantContext` with `CompanyId` (int?) and `IsSuperAdmin` (bool)
- **Source:** Middleware in Program.cs extracts `companyId` claim from JWT
- **Global Query Filters:** 62+ filters on DbSet entities in ApplicationDbContext
- **Filter Logic:** 
  - `_filterByTenant` = true when not super-admin and CompanyId is valid
  - Filters block reads when `CompanyId != _tenantCompanyId`
  - Super-admin (IsSuperAdmin=true) bypasses all filters
  - Null CompanyId records (system-wide) are visible to all tenants

### Database Isolation ✅
- **Company Entity:** `Id` (PK), `CompanyName`, `IsActive`, etc.
- **Employee Entity:** `CompanyId` (non-nullable FK to Company), `EmployeeCode`, `FullName`, etc.
- **94 Total Tables:** All company-scoped tables have `CompanyId` and active query filters
- **No existing `IsDemo` field:** Will need to add via migration

### Authentication & Authorization ✅
- **JWT:** RS256 (asymmetric), stored in HttpOnly cookies
- **Claims:** `companyId`, `userId`, roles (SuperAdmin, HR, Manager, Employee, etc.)
- **Password Hashing:** BCrypt (configurable work factor)
- **Roles:** SuperAdmin, HR, Manager, Employee, Recruiter, Payroll, etc.
- **Service Registration:** All 40+ services are scoped, respect ITenantContext

### Configuration System ✅
- **appsettings.json:** Environment-specific overrides
- **Pattern:** Configuration hierarchy: appsettings.json → appsettings.{Environment}.json → env vars
- **Secrets:** JWT keys, encryption keys, database connection via env vars
- **No production secrets in source code**

### Existing Seeding Pattern ✅
- **Location:** `SeedAsync()` in Program.cs (called during startup when `Database:AutoMigrate=true`)
- **Pattern:** Direct DbSet inserts, SaveChanges, logs but no passwords printed
- **Used for:** Initial superadmin creation, LeaveType defaults
- **Safety:** Random superadmin password generated on first boot, force MustChangePassword=true

### Migrations Infrastructure ✅
- **Location:** `/HRMS.Infrastructure/Migrations/MySql/`
- **Provider:** Pomelo.EntityFrameworkCore.MySql
- **Pattern:** Snake_case table/column names via ApplySnakeCaseConvention in OnModelCreating
- **Example:** `20260810080843_MySqlBaselineSchema.cs`

### Test Infrastructure ✅
- **Base Pattern:** `TestHelpers.CreateInMemoryDb()` and `CreateSqliteDb()`
- **Tenant Context:** Tests can pass ITenantContext for filtered queries
- **Example Tests:** `TenantIsolationRemediationTests.cs` shows multi-company isolation tests

### Existing Services ✅
- **Pattern:** Scoped services injected with ApplicationDbContext
- **Safety:** Most services have `companyId` parameter and pass to ITenantContext
- **Example:** EmployeeService, PayrollService, LeaveService, etc.

---

## PHASE 2: Demo Mode Architecture

### Overview
A production-safe demo mode that:
1. Creates 5 demo companies with realistic data
2. Marks all demo records with `IsDemo = true` flag
3. Isolates demo data via CompanyId (same database)
4. Requires explicit confirmation to seed
5. Disabled by default
6. Blocked in production unless explicitly opted-in
7. Provides dry-run and cleanup operations

### Schema Changes Required

**Migration:** `AddIsDemoColumnToTables`

Add `IsDemo` column (boolean, default=false) to these tables:
- `companies`
- `employees`
- `web_attendances`
- `leave_requests`
- `payslips`
- `bonuses`
- `deductions`
- `candidates` (recruitment)
- `assets`
- `users` (for demo users)
- `leave_types` (if not already system-wide)

**Reasoning:**
- CompanyId already isolates by company
- IsDemo adds a second layer: IsDemo=false by default ensures all production data is never touched
- Cleanup queries can safely delete only IsDemo=true records
- Reports/dashboards can explicitly filter where IsDemo=false to exclude demo data

### Configuration Structure

**appsettings.json additions:**
```json
{
  "DemoMode": {
    "Enabled": false,
    "SeedEnabled": false,
    "AllowProduction": false,
    "SeedVersion": "1.0.0",
    "DryRunByDefault": true
  }
}
```

**Environment validation:**
- In Production: DemoMode:Enabled must be false and AllowProduction must be false
- In Development: Defaults allow demo operations for testing
- Explicit safeguards prevent accidental production seeding

### Demo Companies (5 Total)

| Company | Code | Industry | Location | Employees |
|---------|------|----------|----------|-----------|
| RatanHR Demo Holdings | DEMO-RH | Software/IT | Mumbai | ~100 |
| Northstar Manufacturing Demo | DEMO-NM | Manufacturing | Pune | ~100 |
| BluePeak Consulting Demo | DEMO-BC | Consulting | Bengaluru | ~100 |
| Greenfield Retail Demo | DEMO-GR | Retail | Thane | ~100 |
| Summit Logistics Demo | DEMO-SL | Logistics | Navi Mumbai | ~100 |

**All use deterministic IDs and data**

### Demo Data Volume

- **Companies:** 5
- **Departments:** 20+ (HR, IT, Sales, Payroll, Finance, etc.)
- **Designations:** 25+ (CEO, Manager, Developer, etc.)
- **Employees:** ~500 total (deterministic, synthetic)
- **Attendance:** 180 days per employee
- **Payroll:** 12 months history
- **Leave Requests:** Proportional to employees
- **Recruitment Candidates:** ~200
- **Assets:** Laptops, phones, etc.
- **Users:** Demo logins (SuperAdmin, HR, Manager, Employee roles)
- **Leave Types:** Existing system defaults
- **Other:** Training, Performance, Helpdesk, Sales (if entities exist)

### Idempotency Strategy

**DemoSeedTracker Entity:**
```csharp
public class DemoSeedTracker
{
    public int Id { get; set; }
    public string SeedVersion { get; set; } // e.g., "1.0.0"
    public Guid SeedRunId { get; set; }      // unique per seed run
    public int CreatedCompanyCount { get; set; }
    public int CreatedEmployeeCount { get; set; }
    public int CreatedAttendanceCount { get; set; }
    // ... other counts ...
    public DateTime ExecutedAt { get; set; }
    public string Environment { get; set; } // "Development", "Production", etc.
    public bool IsSuccess { get; set; }
}
```

**Logic:**
- Before seeding, check if same SeedVersion already completed
- If yes: skip creation, log "Demo data already seeded (v1.0.0)"
- If no: proceed with deterministic creation using fixed random seed
- After completion: create DemoSeedTracker record

### Dry-Run Mode

**Operation:** `DemoSeedService.DryRunAsync(companyIdFilter: null, verbose: true)`

**Output:**
```
[DRY-RUN] Demo Seed Operation (v1.0.0)
[DRY-RUN] Environment: Development
[DRY-RUN] Timestamp: 2026-08-19T10:30:00Z

[DRY-RUN] COMPANIES TO CREATE: 5
  - DEMO-RH (RatanHR Demo Holdings, Mumbai)
  - DEMO-NM (Northstar Manufacturing Demo, Pune)
  - ... (3 more)

[DRY-RUN] EMPLOYEES TO CREATE: ~500
[DRY-RUN] ATTENDANCE RECORDS: ~90,000 (500 emp * 180 days)
[DRY-RUN] PAYROLL RECORDS: ~6,000 (500 emp * 12 months)
[DRY-RUN] LEAVE REQUESTS: ~2,000
[DRY-RUN] RECRUITMENT CANDIDATES: ~200
[DRY-RUN] ASSETS: ~1,000

[DRY-RUN] EXISTING DEMO DATA (if any):
  - SeedRunId: {guid} (v1.0.0) - executed 2026-08-15 10:00:00
  - Company Count: 5, Employee Count: 500
  - Status: SKIP (same version, no action taken)

[DRY-RUN] SAFETY CHECKS:
  ✓ IsDemo column exists on all target tables
  ✓ No real customer data (CompanyId > 1000) detected
  ✓ Environment: Development (demo seeding allowed)
  ✓ DemoMode:SeedEnabled = true
  ✓ DemoMode:Enabled = true

[DRY-RUN] SUMMARY:
  - Total records to be created: ~99,700
  - Database will NOT be modified (dry-run mode)
  - Status: SAFE TO PROCEED
```

### Cleanup Mode

**Operation:** `DemoSeedService.CleanupAsync(dryRun: true)`

**Safety:**
- Requires `DemoMode:SeedEnabled = true`
- Only deletes records where `IsDemo = true`
- Uses transaction (rollback on error)
- Prints counts before deletion
- Refuses execution if safety filters cannot be proven

**Output:**
```
[CLEANUP-DRY-RUN] Demo Cleanup Operation

[CLEANUP-DRY-RUN] RECORDS TO DELETE (IsDemo = true):
  - Companies: 5
  - Employees: 500
  - WebAttendances: 90,000
  - Payslips: 6,000
  - LeaveRequests: 2,000
  - Bonuses: 500
  - Deductions: 500
  - Candidates: 200
  - Assets: 1,000
  - Users (demo): 15
  - EmployeeSkills: 1,500
  - ProjectAssignments: 2,000
  
[CLEANUP-DRY-RUN] TOTAL RECORDS TO DELETE: ~107,215
[CLEANUP-DRY-RUN] Database will NOT be modified (dry-run mode)
[CLEANUP-DRY-RUN] Status: SAFE TO DELETE
```

**Actual Cleanup:**
- `DemoSeedService.CleanupAsync(dryRun: false)` performs the deletion
- Requires explicit confirmation: `--confirm-cleanup`
- Deletes in correct foreign-key order (children first)

### Safety Guarantees

**Prevented Actions:**
- ❌ Cannot modify `IsDemo = false` records (filters protect them)
- ❌ Cannot bypass CompanyId isolation
- ❌ Cannot seed if `DemoMode:Enabled = false` (default)
- ❌ Cannot seed in Production without `DemoMode:AllowProduction = true` AND env var
- ❌ Cannot cleanup without explicit confirmation
- ❌ Cannot send real emails (notifications disabled for demo users)
- ❌ Cannot create real credentials (synthetic passwords only)

**Verification:**
- Before seeding: check IsDemo column exists
- Before seeding: verify no real customer data in target companies
- Before cleanup: count records and require confirmation
- After seeding: validate records are correctly marked IsDemo = true
- After cleanup: verify deletion succeeded and DemoSeedTracker updated

---

## PHASE 3: Implementation Steps

### Step 1: Create Migration
- Add `IsDemo` column (bool, default=false) to all target tables
- File: `20260819_AddIsDemoColumn.cs`

### Step 2: Create DemoSeedService
- Location: `HRMS.Infrastructure/Services/Demo/DemoSeedService.cs`
- Methods:
  - `SeedAsync(dryRun: bool = false)` - main seed operation
  - `CleanupAsync(dryRun: bool = true)` - delete demo records
  - `ValidateAsync()` - check safety preconditions
  - `GenerateDeterministicData()` - create synthetic records

### Step 3: Create DemoSeedTracker Entity & DbSet
- Entity: `HRMS.Domain/Entities/Demo/DemoSeedTracker.cs`
- Add DbSet to ApplicationDbContext

### Step 4: Register DemoSeedService
- Add to ServiceExtensions: `services.AddScoped<IDemoSeedService, DemoSeedService>();`

### Step 5: Create Admin API Endpoint
- POST `/api/admin/demo/seed` - trigger seed with confirmation
- GET `/api/admin/demo/seed/dry-run` - preview without modifications
- DELETE `/api/admin/demo/cleanup` - delete demo data with confirmation

**Authorization:** SuperAdmin only, rate-limited

### Step 6: Add Configuration Binding
- Update `appsettings.json` with `DemoMode` section
- Create `DemoModeOptions` class

### Step 7: Add Tests
- 14+ test cases for safety, isolation, idempotency, cleanup

### Step 8: Build & Verify
- `dotnet build` → 0 errors
- `dotnet test` → all tests pass
- Docker build & MySQL container running

---

## PHASE 4: Files to Create/Modify

### New Files to Create:
1. `HRMS.Domain/Entities/Demo/DemoSeedTracker.cs`
2. `HRMS.Infrastructure/Services/Demo/IDemoSeedService.cs`
3. `HRMS.Infrastructure/Services/Demo/DemoSeedService.cs`
4. `HRMS.Infrastructure/Services/Demo/DemoDataGenerator.cs`
5. `HRMS.Infrastructure/Options/DemoModeOptions.cs`
6. `HRMS.API/Controllers/AdminDemoController.cs`
7. `HRMS.Tests/Demo/DemoSeedServiceTests.cs`
8. `HRMS.Tests/Demo/DemoSeedIsolationTests.cs`
9. `HRMS.Tests/Demo/DemoCleanupSafetyTests.cs`
10. `Migrations/MySql/20260819_AddIsDemoColumn.cs`

### Files to Modify:
1. `HRMS.Infrastructure/Data/ApplicationDbContext.cs` - add DbSet, query filters
2. `HRMS.API/Extensions/ServiceExtensions.cs` - register services
3. `appsettings.json` - add DemoMode configuration
4. `HRMS.API/Program.cs` - update environment validator

### No Changes Required:
- Existing authentication/authorization (works as-is)
- Existing tenant filters (work as-is)
- Existing migrations (backward compatible)
- Existing services (can be reused)

---

## PHASE 5: Safety Validation Checklist

Before seeding production:
- [ ] DemoMode:Enabled = false in production appsettings
- [ ] DemoMode:AllowProduction = false in production appsettings
- [ ] Environment validator blocks seeding in production
- [ ] IsDemo column exists on all target tables
- [ ] No real customer data has CompanyId <= 5 (reserved for demo)
- [ ] DemoSeedService.ValidateAsync() passes all checks
- [ ] Test suite passes (no regressions)
- [ ] Dry-run succeeds and shows correct record counts
- [ ] Cleanup dry-run succeeds

---

## Success Criteria

✅ Demo seed creates 5 companies with ~500 employees
✅ All demo records marked `IsDemo = true`
✅ Demo companies use deterministic IDs/data (idempotent)
✅ Dry-run shows what would be created without modifying database
✅ Cleanup deletes only demo records (IsDemo = true)
✅ Cross-company isolation verified (Demo Company A cannot see Company B)
✅ Real customer data never modified/deleted
✅ Production seeding blocked by default
✅ 14+ test cases pass
✅ Full existing test suite still passes
✅ Docker build succeeds
✅ MySQL connectivity verified

---

## Timeline
- Phase 2: Design ✅
- Phase 3: Core implementation (DemoSeedService, migration, configuration)
- Phase 4: Admin API endpoints
- Phase 5: Tests
- Phase 6: Build & Docker verification
- Phase 7: Dry-run verification
- Phase 8: Production safety audit
