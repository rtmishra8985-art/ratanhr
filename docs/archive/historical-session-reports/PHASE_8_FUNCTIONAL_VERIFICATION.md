# PHASE 8: FUNCTIONAL VERIFICATION - DEMO SEED/CLEANUP

**Objective:** Verify demo mode operations work end-to-end  
**Status:** Ready to execute after Phase 7 build passes  
**Effort:** ~15 minutes

---

## PREREQUISITE: Phase 7 Complete

✅ Build succeeds: `dotnet build --configuration Release`  
✅ All tests pass: `dotnet test`  
✅ Docker image builds: `docker build -t ratanhr:demo-mode .`

---

## STEP 1: Apply Database Migration

```bash
# Navigate to project
cd "C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new"

# Apply IsDemo schema migration
dotnet ef database update --project HRMS.Infrastructure --startup-project HRMS.API
```

### Expected Output:
```
Applying migration '20260819000001_AddIsDemoColumn'.
Done.
```

### Verify in Database:
```sql
-- Connect to hrms_db
-- Check IsDemo column exists on companies
DESCRIBE companies;
-- Should show: is_demo | tinyint(1) | NO | NULL | 0

-- Check indexes created
SHOW INDEXES FROM companies WHERE Key_name LIKE 'ix_%demo%';
```

---

## STEP 2: Start Application in Development

```bash
# Start ASP.NET Core app
dotnet run --project HRMS.API

# Expected: 
# info: Microsoft.Hosting.Lifetime
# info: Application started. Press Ctrl+C to exit.
# info: Hosting environment: Development
# info: Content root path: ...
```

### Verify API is responsive:
```bash
# In another terminal
curl http://localhost:5000/api/admin/demo/validate

# Expected response (JSON):
{
  "isValid": true,
  "checks": [
    {"checkName": "DemoMode:Enabled", "passed": true, ...},
    ...
  ],
  "failureReasons": []
}
```

---

## STEP 3: Dry-Run Demo Seed (Preview)

```bash
# Test dry-run endpoint
curl http://localhost:5000/api/admin/demo/seed/dry-run

# Expected response (JSON):
{
  "isSuccess": true,
  "wasDryRun": true,
  "message": "[DRY-RUN] Demo Seed Operation Preview",
  "companiesCreated": 5,
  "employeesCreated": 500,
  "attendanceRecordsCreated": 90000,
  "payslipsCreated": 6000,
  "totalRecordsCreated": 100000,
  ...
}
```

### Verify NO database changes:
```bash
# Check employee count
# In separate MySQL session:
SELECT COUNT(*) as employee_count FROM employees WHERE is_demo = true;
# Should return: 0 (no actual seeding happened)
```

---

## STEP 4: Actually Seed Demo Data

```bash
# Execute actual seed (requires confirm=true)
curl -X POST "http://localhost:5000/api/admin/demo/seed?confirm=true" \
  -H "Authorization: Bearer <SUPERADMIN_JWT_TOKEN>"

# Expected response (JSON):
{
  "isSuccess": true,
  "wasDryRun": false,
  "message": "Demo data successfully seeded (v1.0.0)",
  "companiesCreated": 5,
  "employeesCreated": 500,
  "attendanceRecordsCreated": 90000,
  "payslipsCreated": 6000,
  "totalRecordsCreated": 100000,
  "executedAt": "2026-08-19T...",
  ...
}
```

### Verify in Database:
```sql
-- Check companies created
SELECT COUNT(*) FROM companies WHERE is_demo = true;
-- Should return: 5

-- Check employees created
SELECT COUNT(*) FROM employees WHERE is_demo = true;
-- Should return: 500

-- Check attendance
SELECT COUNT(*) FROM web_attendances WHERE is_demo = true;
-- Should return: ~90000

-- Check payslips
SELECT COUNT(*) FROM payslips WHERE is_demo = true;
-- Should return: ~6000

-- Verify all are marked IsDemo
SELECT COUNT(*) FROM employees WHERE company_id >= 1 AND company_id <= 5 AND is_demo = false;
-- Should return: 0 (all demo employees marked)
```

---

## STEP 5: Verify Idempotency (Seed Again)

```bash
# Seed again with same version - should detect and skip
curl -X POST "http://localhost:5000/api/admin/demo/seed?confirm=true"

# Expected response:
{
  "isSuccess": true,
  "wasDryRun": true,
  "message": "Demo data already seeded (v1.0.0). No action taken.",
  ...
}
```

### Verify record counts unchanged:
```sql
SELECT COUNT(*) FROM companies WHERE is_demo = true;
-- Should still return: 5 (not 10)
```

---

## STEP 6: Test Cleanup Dry-Run

```bash
# Preview cleanup
curl http://localhost:5000/api/admin/demo/cleanup/dry-run

# Expected response (JSON):
{
  "isSuccess": true,
  "wasDryRun": true,
  "message": "[DRY-RUN] Demo Cleanup Preview",
  "companiesDeleted": 5,
  "employeesDeleted": 500,
  "attendanceRecordsDeleted": 90000,
  "payslipsDeleted": 6000,
  "totalRecordsDeleted": 100000,
  ...
}
```

### Verify NO deletion occurred:
```sql
SELECT COUNT(*) FROM companies WHERE is_demo = true;
-- Should still return: 5 (not deleted yet)
```

---

## STEP 7: Execute Cleanup

```bash
# Execute actual cleanup (requires confirm=true)
curl -X DELETE "http://localhost:5000/api/admin/demo/cleanup?confirm=true" \
  -H "Authorization: Bearer <SUPERADMIN_JWT_TOKEN>"

# Expected response (JSON):
{
  "isSuccess": true,
  "wasDryRun": false,
  "message": "Demo data successfully cleaned up",
  "companiesDeleted": 5,
  "employeesDeleted": 500,
  "attendanceRecordsDeleted": 90000,
  "payslipsDeleted": 6000,
  "totalRecordsDeleted": 100000,
  ...
}
```

### Verify all demo data deleted:
```sql
SELECT COUNT(*) FROM companies WHERE is_demo = true;
-- Should return: 0

SELECT COUNT(*) FROM employees WHERE is_demo = true;
-- Should return: 0

SELECT COUNT(*) FROM web_attendances WHERE is_demo = true;
-- Should return: 0
```

---

## STEP 8: Verify Multi-Company Isolation

### After seeding, test isolation:

```sql
-- Demo Company 1 employees
SELECT COUNT(*) FROM employees WHERE company_id = 1 AND is_demo = true;
-- Should return: ~100

-- Demo Company 2 employees  
SELECT COUNT(*) FROM employees WHERE company_id = 2 AND is_demo = true;
-- Should return: ~100

-- Verify they're different employees
SELECT COUNT(*) FROM employees WHERE company_id = 1 AND is_demo = true
INTERSECT
SELECT COUNT(*) FROM employees WHERE company_id = 2 AND is_demo = true;
-- Should return: 0 (no overlapping IDs)
```

---

## STEP 9: Security Verification

### Verify production safeguard:

```bash
# Set environment to Production
export ASPNETCORE_ENVIRONMENT=Production

# Restart app
dotnet run --project HRMS.API

# Try to seed - should fail
curl -X POST "http://localhost:5000/api/admin/demo/seed?confirm=true"

# Expected response (403 Forbidden or validation error):
{
  "success": false,
  "message": "Production seeding blocked by default..."
}
```

### Verify DemoMode configuration:
```bash
# Check appsettings.json
cat HRMS.API/appsettings.json | grep -A 5 "DemoMode"

# Should show:
# "DemoMode": {
#   "Enabled": false,
#   "SeedEnabled": false,
#   "AllowProduction": false,
#   ...
# }
```

---

## ✅ PHASE 8 VERIFICATION CHECKLIST

- [ ] Migration applied: `dotnet ef database update`
- [ ] API responds: GET /api/admin/demo/validate returns valid
- [ ] Dry-run works: No database changes after dry-run
- [ ] Actual seed works: 100K+ records created
- [ ] All records marked: `is_demo = true`
- [ ] Idempotency verified: Same version doesn't duplicate
- [ ] Cleanup dry-run works: Correct counts shown
- [ ] Cleanup works: All demo records deleted
- [ ] Multi-company isolation: Companies separate
- [ ] Production blocked: Demo seed fails in Production environment

---

## 🎯 IF ANYTHING FAILS

### API returns 401 Unauthorized
- Generate valid JWT token with SuperAdmin role
- Include: `Authorization: Bearer <token>`

### Demo seed fails with "already seeded"
- This is correct behavior (idempotency)
- Try cleanup then seed again, or increment SeedVersion

### Cleanup fails with "confirm not set"
- Requirement: `?confirm=true` in query parameter
- Retry: `curl -X DELETE "...?confirm=true"`

### Database migration fails
- Check MySQL is running and hrms_db exists
- Verify connection string in appsettings
- Run: `dotnet ef migrations list` to see pending

---

**Next Phase:** Phase 9 - Final documentation  
**Time Spent:** 15 minutes  
**Total Progress:** 80% complete (8/10 phases)
