# PHASE 7: BUILD & FULL TEST SUITE VERIFICATION

**Objective:** Verify compilation, all tests pass, Docker integration ready  
**Status:** Ready to execute  
**Effort:** ~10 minutes

---

## STEP 1: Apply Manual Code Additions

### Required edits BEFORE building:

#### Edit 1: ApplicationDbContext.cs
**Locate line ~95** (search for "public DbSet<User>")  
**Add after User DbSet:**

```csharp
public DbSet<HRMS.Domain.Entities.Demo.DemoSeedTracker> DemoSeedTrackers { get; set; } = null!;
```

#### Edit 2: ServiceExtensions.cs  
**Locate method "AddInfrastructure"** around line 100  
**Find: "services.AddScoped<IWebhookService, WebhookService>();"**  
**Add after:**

```csharp
services.AddScoped<IDemoSeedService, DemoSeedService>();
services.Configure<DemoModeOptions>(configuration.GetSection(DemoModeOptions.SectionName));
```

#### Edit 3: Add using statements if missing
```csharp
using HRMS.Infrastructure.Services.Demo;
using HRMS.Infrastructure.Options;
```

---

## STEP 2: Verify Using Statements

**ApplicationDbContext.cs should have:**
```csharp
using HRMS.Domain.Entities.Demo;
```

**ServiceExtensions.cs should have:**
```csharp
using HRMS.Infrastructure.Options;
using HRMS.Infrastructure.Services.Demo;
```

**AdminDemoController.cs already has:**
```csharp
using HRMS.Infrastructure.Services.Demo;
```

---

## STEP 3: Clean Build

```bash
cd "C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new"

# Clean previous build
dotnet clean --configuration Release

# Restore packages
dotnet restore

# Build
dotnet build --configuration Release
```

### Expected Output:
```
Build succeeded. 0 errors, 0-2 warnings
```

### If build FAILS:
1. Check ApplicationDbContext DbSet declaration
2. Verify ServiceExtensions service registrations
3. Ensure all `using` statements are present
4. Look for line number mismatch in edits
5. Check spelling: `DemoSeedTracker`, `IDemoSeedService`, `DemoModeOptions`

---

## STEP 4: Run Full Test Suite

```bash
# Run ALL tests (existing + new demo tests)
dotnet test --configuration Release --logger "console;verbosity=detailed"
```

### Expected Results:

**New Demo Tests (~36 tests):**
```
✅ DemoSeedServiceTests.cs (13 tests)
  ✓ DryRun_DoesNotModifyDatabase
  ✓ DryRun_ReturnsEstimatedCounts
  ✓ Seed_CreatesCompanies
  ✓ Seed_CreatesEmployees
  ✓ AllRecords_MarkedWithIsDemo
  ✓ Seed_Idempotent_SameVersionNotDuplicated
  ✓ DemoSeedTracker_RecordsOperation
  ✓ Cleanup_DeletesOnlyDemoRecords
  ✓ CleanupDryRun_DoesNotModifyDatabase
  ✓ Cleanup_RequiresConfirmation
  ✓ DemoCompanies_HaveCorrectIds
  ✓ DemoEmployees_DistributedAcrossCompanies
  ✓ RecordCounts_Deterministic

✅ DemoSafetyTests.cs (11 tests)
  ✓ DemoMode_DisabledByDefault
  ✓ ProductionEnvironment_BlocksSeeding
  ✓ ProductionEnvironment_AllowedWhenOptedIn
  ✓ SeedEnabled_RequiredForActualSeeding
  ✓ DryRun_AllowedEvenWhenSeedDisabled
  ✓ RealCustomerData_NeverTouched
  ✓ Cleanup_OnlyDeletesDemoRecords
  ✓ SeedVersion_PreventsRegressions
  ✓ TransactionRollback_OnFailure
  ✓ NoAutomaticSeeding_OnStartup
  ✓ NoDemoRecordsInProduction_ByDefault

✅ DemoIsolationTests.cs (12 tests)
  ✓ DemoCompanyA_CannotSeeDemoCompanyB
  ✓ QueryFilter_ScopesToCompanyId
  ✓ SuperAdmin_CanSeeCrossCompany
  ✓ DemoCompanies_IsolatedFromRealCustomers
  ✓ DemoCompanies_UseReservedIds
  ✓ AttendanceRecords_IsolatedByCompany
  ✓ Payroll_IsolatedByCompany
  ✓ Assets_IsolatedByCompany
  ✓ DemoUsersAssignedToCompanies
  ✓ Cleanup_RespectsDemoIsolation
  ✓ LeaveRequests_IsolatedByCompany
  ✓ AllDemoRecords_ShareSameIsDemo_Flag
```

**Existing Test Suite:**
```
✅ All existing tests continue to pass
   (no regressions)
```

**Summary Line:**
```
Test Run Successful. X total tests passed, 0 failed, 0 skipped
```

---

## STEP 5: Docker Build Verification

```bash
# List current images
docker images | grep ratanhr || echo "No existing RatanHR images"

# Build new Docker image
docker build -t ratanhr:demo-mode .

# Verify build succeeded
docker images | grep ratanhr
```

### Expected Output:
```
REPOSITORY  TAG         IMAGE ID      SIZE
ratanhr     demo-mode   <sha>         XXX MB
```

---

## STEP 6: Database Migration Verification

```bash
# List pending migrations
dotnet ef migrations list --project HRMS.Infrastructure --startup-project HRMS.API

# Should show: 20260819000001_AddIsDemoColumn (Pending)
```

```bash
# For development/test: apply migration
dotnet ef database update --project HRMS.Infrastructure --startup-project HRMS.API
```

### Expected Output:
```
Applying migration '20260819000001_AddIsDemoColumn'.
Done.
```

---

## STEP 7: Verify API Endpoints Exist

```bash
# Optional: Use dotnet to inspect endpoints
dotnet build --configuration Release

# Then inspect generated code or use reflection tools
# OR start the API and check Swagger if enabled
```

### Endpoints to verify in swagger/code:
```
GET  /api/admin/demo/seed/dry-run        → AdminDemoController.DryRunSeed()
POST /api/admin/demo/seed                 → AdminDemoController.Seed(confirm)
GET  /api/admin/demo/cleanup/dry-run      → AdminDemoController.DryRunCleanup()
DELETE /api/admin/demo/cleanup             → AdminDemoController.Cleanup(confirm)
GET  /api/admin/demo/validate             → AdminDemoController.Validate()
GET  /api/admin/demo/status               → AdminDemoController.GetStatus()
```

---

## ✅ PHASE 7 VERIFICATION CHECKLIST

- [ ] ApplicationDbContext.cs has DemoSeedTrackers DbSet
- [ ] ServiceExtensions.cs registers IDemoSeedService
- [ ] All using statements added to modified files
- [ ] `dotnet build --configuration Release` → ✅ 0 errors
- [ ] `dotnet test` → ✅ All 36+ demo tests pass
- [ ] `dotnet test` → ✅ All existing tests still pass
- [ ] `docker build` → ✅ Image builds successfully
- [ ] Migration listed as pending: ✅ 20260819000001_AddIsDemoColumn
- [ ] API endpoints verified in code: ✅ AdminDemoController exists

---

## 🎯 IF ANYTHING FAILS

### Build fails
- Check C# syntax in manual edits
- Verify file paths match your project structure
- Run `dotnet clean` and retry

### Tests fail
- Run single test: `dotnet test --filter "DemoSeedServiceTests.DryRun_DoesNotModifyDatabase"`
- Check test output for specific error
- Verify ApplicationDbContext DbSet added correctly

### Docker fails
- Ensure Docker daemon running: `docker ps`
- Check Dockerfile intact: `cat Dockerfile | head -20`
- Rebuild with: `docker build --no-cache -t ratanhr:demo-mode .`

---

## ✅ PHASE 7 COMPLETE WHEN:

```
✅ Build succeeds with 0 errors
✅ 36+ demo tests pass
✅ All existing tests still pass  
✅ Docker image builds
✅ Migration is pending (not yet applied)
```

---

**Next Phase:** Phase 8 - Functional verification (demo seed/cleanup)

**Time Spent:** 10 minutes  
**Total Progress:** 75% complete (8/10 phases)
