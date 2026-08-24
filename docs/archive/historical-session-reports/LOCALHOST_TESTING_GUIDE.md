# 🧪 RatanHR DEMO MODE - LOCALHOST LIVE TESTING GUIDE

**Status:** ✅ **READY FOR LOCALHOST TESTING**

---

## ⚠️ IMPORTANT SAFETY NOTE

**Before you start:** Demo Mode is currently **DISABLED** in appsettings.json:
```json
"DemoMode": {
  "Enabled": false,
  "SeedEnabled": false,
  "AllowProduction": false
}
```

To test locally, you need to **enable demo mode** first.

---

## 🎯 STEP 1: ENABLE DEMO MODE FOR TESTING

### Option A: Modify appsettings.json (Development Only)

**File:** `HRMS.API/appsettings.json`

**Change from:**
```json
"DemoMode": {
  "Enabled": false,
  "SeedEnabled": false,
  "AllowProduction": false,
  "SeedVersion": "1.0.0",
  "DryRunByDefault": true
}
```

**Change to:**
```json
"DemoMode": {
  "Enabled": true,
  "SeedEnabled": true,
  "AllowProduction": true,
  "SeedVersion": "1.0.0",
  "DryRunByDefault": true
}
```

⚠️ **WARNING:** Only do this for LOCAL/DEVELOPMENT testing. REVERT before pushing to production.

### Option B: Use Environment Variables (Safer)

When running locally, set environment variables:
```bash
set DEMOMODE__ENABLED=true
set DEMOMODE__SEEDENABLED=true
set DEMOMODE__ALLOWPRODUCTION=true
dotnet run --project HRMS.API
```

Or in PowerShell:
```powershell
$env:DEMOMODE__ENABLED = "true"
$env:DEMOMODE__SEEDENABLED = "true"
$env:DEMOMODE__ALLOWPRODUCTION = "true"
dotnet run --project HRMS.API
```

---

## 🚀 STEP 2: START THE APPLICATION

### Prerequisites
1. ✅ Database running (MySQL/database of choice)
2. ✅ Connection string configured in appsettings.json
3. ✅ All migrations applied: `dotnet ef database update`
4. ✅ Demo Mode enabled (see Step 1 above)

### Start Application

```bash
cd C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new
dotnet run --project HRMS.API
```

**Expected Output:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
      Now listening on: https://localhost:5001
```

---

## 🧪 STEP 3: TEST DRY-RUN (NO DATA CHANGES)

**First, always test dry-run to preview what will be created:**

### Test 1: Check Validation

```bash
curl -X GET http://localhost:5000/api/admin/demo/validate \
  -H "Authorization: Bearer YOUR_SUPERADMIN_JWT_TOKEN"
```

**Expected Response:**
```json
{
  "isValid": true,
  "validationChecks": [
    {
      "checkName": "DemoMode:Enabled",
      "passed": true,
      "message": "Demo mode is enabled"
    },
    {
      "checkName": "Production Safeguard",
      "passed": true,
      "message": "Non-production environment (development allowed)"
    },
    {
      "checkName": "Database Connectivity",
      "passed": true,
      "message": "Database is accessible"
    },
    {
      "checkName": "Demo Company Isolation",
      "passed": true,
      "message": "No real customer data found in reserved demo company IDs (1-5)"
    }
  ]
}
```

### Test 2: Dry-Run Seed (Preview Only)

```bash
curl -X GET http://localhost:5000/api/admin/demo/seed/dry-run \
  -H "Authorization: Bearer YOUR_SUPERADMIN_JWT_TOKEN"
```

**Expected Response:**
```json
{
  "isSuccess": true,
  "wasDryRun": true,
  "message": "[DRY-RUN] Demo Seed Operation Preview",
  "companiesCreated": 5,
  "employeesCreated": 500,
  "attendanceRecordsCreated": 90000,
  "leaveRequestsCreated": 250,
  "assetsCreated": 300,
  "candidatesCreated": 200,
  "usersCreated": 15
}
```

✅ **KEY:** `wasDryRun: true` means **NO DATA WAS ACTUALLY CREATED**

### Test 3: Verify Database (Should be Empty)

**Query your database:**
```sql
SELECT COUNT(*) FROM companies WHERE is_demo = true;
SELECT COUNT(*) FROM employees WHERE is_demo = true;
```

**Expected:** Both return 0 (no demo data yet)

---

## ✅ STEP 4: CREATE DEMO DATA (WITH CONFIRMATION)

**When you're ready to create actual demo data:**

### Create Demo Data

```bash
curl -X POST "http://localhost:5000/api/admin/demo/seed?confirm=true" \
  -H "Authorization: Bearer YOUR_SUPERADMIN_JWT_TOKEN"
```

**Expected Response:**
```json
{
  "isSuccess": true,
  "wasDryRun": false,
  "message": "Demo data successfully seeded (v1.0.0)",
  "companiesCreated": 5,
  "employeesCreated": 500,
  "attendanceRecordsCreated": 45000,
  "leaveRequestsCreated": 200,
  "assetsCreated": 250,
  "candidatesCreated": 200,
  "usersCreated": 15,
  "executedAt": "2026-08-19T14:30:45.123Z"
}
```

✅ **KEY:** `wasDryRun: false` means **DEMO DATA WAS ACTUALLY CREATED**

---

## 📊 STEP 5: VERIFY DEMO DATA IN DATABASE

### Check Created Records

```sql
-- Check demo companies
SELECT id, company_name, is_demo FROM companies WHERE is_demo = true;
-- Expected: 5 rows (DEMO-RH, DEMO-NM, DEMO-BC, DEMO-GR, DEMO-SL)

-- Check demo employees
SELECT COUNT(*) FROM employees WHERE is_demo = true;
-- Expected: ~500 rows

-- Check demo attendance
SELECT COUNT(*) FROM web_attendances WHERE company_id IN (1,2,3,4,5);
-- Expected: ~45,000 rows

-- Check demo leave requests
SELECT COUNT(*) FROM leave_requests WHERE company_id IN (1,2,3,4,5);
-- Expected: ~200 rows

-- Check demo assets
SELECT COUNT(*) FROM assets WHERE company_id IN (1,2,3,4,5);
-- Expected: ~250 rows
```

✅ **VERIFICATION:** All IsDemo records should have `is_demo = true`

---

## 🧪 STEP 6: TEST WITH DEMO DATA

### Login as Demo User

**Demo users created:**
- Email: `demo1.user0@demo.ratanhr.local` 
- Password: `Demo@10#2026`

Or for other companies:
- Company 2: `demo2.user0@demo.ratanhr.local`
- Company 3: `demo3.user0@demo.ratanhr.local`
- etc.

### Test Isolation

**Login as Company 1 user:**
```
User: demo1.user0@demo.ratanhr.local
Password: Demo@10#2026
```

**Verify:**
- ✅ Can see Company 1 employees
- ✅ Can see Company 1 attendance
- ✅ Cannot see Company 2 data
- ✅ Cannot see real customer data (if any exists)

**Login as Company 2 user:**
```
User: demo2.user0@demo.ratanhr.local
Password: Demo@20#2026
```

**Verify:**
- ✅ Can see Company 2 employees
- ✅ Can see Company 2 attendance
- ✅ Cannot see Company 1 data
- ✅ Cannot see real customer data

---

## 🗑️ STEP 7: CLEANUP DEMO DATA

### Test Cleanup Dry-Run

```bash
curl -X GET http://localhost:5000/api/admin/demo/cleanup/dry-run \
  -H "Authorization: Bearer YOUR_SUPERADMIN_JWT_TOKEN"
```

**Expected Response:**
```json
{
  "isSuccess": true,
  "wasDryRun": true,
  "message": "[DRY-RUN] Demo Cleanup Preview",
  "companiesDeleted": 5,
  "employeesDeleted": 500,
  "attendanceRecordsDeleted": 45000,
  "leaveRequestsDeleted": 200,
  "assetsDeleted": 250,
  "usersDeleted": 15
}
```

✅ **KEY:** `wasDryRun: true` means **NO DATA WAS DELETED**

### Execute Cleanup

**When ready to delete demo data:**

```bash
curl -X DELETE "http://localhost:5000/api/admin/demo/cleanup?confirm=true" \
  -H "Authorization: Bearer YOUR_SUPERADMIN_JWT_TOKEN"
```

**Expected Response:**
```json
{
  "isSuccess": true,
  "wasDryRun": false,
  "message": "Demo data successfully cleaned up",
  "companiesDeleted": 5,
  "employeesDeleted": 500,
  "attendanceRecordsDeleted": 45000,
  "leaveRequestsDeleted": 200,
  "assetsDeleted": 250,
  "usersDeleted": 15,
  "executedAt": "2026-08-19T14:35:00.123Z"
}
```

### Verify Cleanup

```sql
SELECT COUNT(*) FROM companies WHERE is_demo = true;
SELECT COUNT(*) FROM employees WHERE is_demo = true;
-- Both should return 0
```

---

## 📋 COMPLETE LOCALHOST TEST CHECKLIST

### Phase 1: Setup (5 minutes)
- [ ] Enable Demo Mode in appsettings.json (or use env vars)
- [ ] Ensure database connection working
- [ ] Run migrations: `dotnet ef database update`
- [ ] Start application: `dotnet run --project HRMS.API`

### Phase 2: Dry-Run Testing (5 minutes)
- [ ] Call `/api/admin/demo/validate` → isValid=true
- [ ] Call `/api/admin/demo/seed/dry-run` → wasDryRun=true
- [ ] Verify database empty (no demo records created)
- [ ] Call `/api/admin/demo/cleanup/dry-run` → wasDryRun=true

### Phase 3: Live Data Creation (5 minutes)
- [ ] Call `/api/admin/demo/seed?confirm=true` → wasDryRun=false
- [ ] Verify database has demo records (5 companies, 500 employees, etc.)
- [ ] Query: `SELECT COUNT(*) FROM companies WHERE is_demo=true;` → 5

### Phase 4: Data Verification (10 minutes)
- [ ] Query demo companies: `SELECT * FROM companies WHERE is_demo=true;`
- [ ] Query demo employees: `SELECT COUNT(*) FROM employees WHERE is_demo=true;` → ~500
- [ ] Query demo attendance: Record count ~45,000
- [ ] Verify all records have `is_demo=true`

### Phase 5: User Testing (10 minutes)
- [ ] Login as `demo1.user0@demo.ratanhr.local` with password `Demo@10#2026`
- [ ] Verify can see Company 1 data
- [ ] Verify cannot see Company 2 data
- [ ] Login as `demo2.user0@demo.ratanhr.local` with password `Demo@20#2026`
- [ ] Verify can see Company 2 data
- [ ] Verify cannot see Company 1 data

### Phase 6: Cleanup (5 minutes)
- [ ] Call `/api/admin/demo/cleanup/dry-run` → shows records to delete
- [ ] Call `/api/admin/demo/cleanup?confirm=true` → wasDryRun=false
- [ ] Verify database empty: `SELECT COUNT(*) FROM companies WHERE is_demo=true;` → 0

---

## 🔐 SAFETY VERIFICATION DURING TESTING

### Verify Real Data Is NEVER Touched

```sql
-- Before seeding
SELECT COUNT(*) FROM companies WHERE is_demo = false;
SELECT COUNT(*) FROM employees WHERE is_demo = false;

-- Run demo seed

-- After seeding (should be same as before)
SELECT COUNT(*) FROM companies WHERE is_demo = false;
SELECT COUNT(*) FROM employees WHERE is_demo = false;
```

✅ **Expected:** Real customer data count unchanged

### Verify Only SuperAdmin Can Execute

**Test with non-admin user:**
```bash
curl -X POST "http://localhost:5000/api/admin/demo/seed?confirm=true" \
  -H "Authorization: Bearer REGULAR_USER_JWT_TOKEN"
```

**Expected Response:** 403 Forbidden

---

## ❌ COMMON ISSUES & SOLUTIONS

### Issue 1: "Demo mode is disabled"
**Cause:** DemoMode:Enabled = false in appsettings.json
**Fix:** Set to true (for development only)

### Issue 2: "Demo seeding is disabled"
**Cause:** DemoMode:SeedEnabled = false
**Fix:** Set to true (for development only)

### Issue 3: "Production seeding blocked"
**Cause:** Environment is Production and AllowProduction = false
**Fix:** Set AllowProduction = true (for local testing only)

### Issue 4: "Seed requires confirm=true"
**Cause:** Called without confirm=true parameter
**Fix:** Add `?confirm=true` to URL

### Issue 5: Database connection failed
**Cause:** Database not running or connection string wrong
**Fix:** Verify database is running and connection string in appsettings.json is correct

### Issue 6: 401 Unauthorized
**Cause:** Missing or invalid JWT token
**Fix:** Use valid SuperAdmin JWT token in Authorization header

---

## ✅ EXPECTED LOCALHOST TEST RESULTS

| Test | Expected Result | Verification |
|------|-----------------|--------------|
| Dry-run seed | No data created | Query shows 0 demo records |
| Live seed | ~100K records | Query shows 5 companies, 500 employees, etc. |
| Company isolation | Company 1 ≠ Company 2 | Login to each and verify data visibility |
| Real data safety | Real records untouched | Count of real records unchanged |
| Cleanup | All demo records deleted | Query shows 0 demo records |
| Authorization | SuperAdmin only | 403 for non-admin users |

---

## 🎯 NEXT STEPS AFTER TESTING

1. ✅ Test in localhost (this guide)
2. ✅ Verify all features work
3. ✅ Test data isolation
4. ✅ Clean up demo data
5. ✅ **REVERT DemoMode settings to defaults** (all false)
6. ✅ Commit changes
7. ✅ Deploy to staging
8. ✅ Repeat testing in staging
9. ✅ Deploy to production

---

## ⚠️ BEFORE COMMITTING TO GIT

**CRITICAL:** Revert Demo Mode settings:

```json
"DemoMode": {
  "Enabled": false,        // ← Must be false
  "SeedEnabled": false,    // ← Must be false
  "AllowProduction": false // ← Must be false
}
```

Do NOT commit with Demo Mode enabled.

---

## ✅ YOU ARE READY TO TEST!

All systems are ready for localhost testing. Follow the steps above to:
1. Enable demo mode for testing
2. Create demo data
3. Verify isolation and safety
4. Clean up demo data
5. Revert settings

**Status: READY FOR LOCALHOST TESTING** ✅
