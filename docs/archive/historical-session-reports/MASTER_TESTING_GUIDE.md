# 🎯 RATANHR DEMO MODE - MASTER TESTING GUIDE

**START HERE - Complete testing in 30-45 minutes**

---

## 🚀 QUICK START (DO THIS FIRST)

### Step 1: Run Automated Setup (5 minutes)

Double-click or run in PowerShell:
```powershell
C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new\SETUP_AND_TEST.bat
```

**What it does:**
- ✅ Verifies prerequisites
- ✅ Builds project (Release)
- ✅ Runs all 36+ tests
- ✅ Creates API helper scripts
- ✅ Enables Demo Mode temporarily
- ✅ Reverts to production-safe defaults

**Expected Output:**
```
✓ dotnet found
✓ git found
✓ Project built successfully
✓ All tests passed
✓ Demo Mode setup complete
```

### Step 2: Enable Demo Mode for Testing (1 minute)

Edit `HRMS.API/appsettings.json`:

**Find this:**
```json
"DemoMode": {
  "Enabled": false,
  "SeedEnabled": false,
  "AllowProduction": false,
```

**Change to:**
```json
"DemoMode": {
  "Enabled": true,
  "SeedEnabled": true,
  "AllowProduction": true,
```

⚠️ **NOTE:** You'll revert this after testing.

### Step 3: Start Application (1 minute)

Open PowerShell in project directory:
```powershell
cd C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new
dotnet run --project HRMS.API
```

**Wait for:**
```
Now listening on: http://localhost:5000
```

---

## 🧪 COMPLETE TESTING FLOW (30-40 minutes)

### SECTION 1: VALIDATION (2 minutes)

#### 1.1 Get JWT Token

Login to get SuperAdmin token:
```powershell
$response = curl -X POST "http://localhost:5000/api/auth/login" `
  -H "Content-Type: application/json" `
  -d '{
    "email": "admin@ratanhr.local",
    "password": "Your_Password"
  }' | ConvertFrom-Json

$token = $response.data.token
Write-Host "Token: $token"
```

Save token for this session:
```powershell
$env:RATANHR_TOKEN = $token
```

#### 1.2 Validate Prerequisites

```powershell
curl -X GET "http://localhost:5000/api/admin/demo/validate" `
  -H "Authorization: Bearer $env:RATANHR_TOKEN"
```

**Expected:** `isValid: true` ✅

---

### SECTION 2: DRY-RUN TESTING (3 minutes)

#### 2.1 Preview Seed (No Data Changes)

```powershell
curl -X GET "http://localhost:5000/api/admin/demo/seed/dry-run" `
  -H "Authorization: Bearer $env:RATANHR_TOKEN" | ConvertFrom-Json | Format-Table
```

**Expected Output:**
```
wasDryRun companiesCreated employeesCreated attendanceRecordsCreated
True      5               500             90000
```

✅ **KEY:** `wasDryRun = True` means no database changes

#### 2.2 Verify No Data Created Yet

```powershell
# Open your database and run:
SELECT COUNT(*) as demo_companies FROM companies WHERE is_demo = true;
SELECT COUNT(*) as demo_employees FROM employees WHERE is_demo = true;
```

**Expected:** Both return 0 ✅

---

### SECTION 3: CREATE DEMO DATA (5 minutes)

#### 3.1 Create Demo Data

```powershell
curl -X POST "http://localhost:5000/api/admin/demo/seed?confirm=true" `
  -H "Authorization: Bearer $env:RATANHR_TOKEN" | ConvertFrom-Json | Format-Table
```

**Expected Output:**
```
wasDryRun isSuccess
False     True
```

✅ **KEY:** `wasDryRun = False` means data was created

#### 3.2 Wait for Completion
Seeding takes ~1-2 minutes. Watch the console for completion message.

#### 3.3 Verify Data Created

**In Database:**
```sql
SELECT COUNT(*) as demo_companies FROM companies WHERE is_demo = true;
-- Expected: 5

SELECT COUNT(*) as demo_employees FROM employees WHERE is_demo = true;
-- Expected: ~500

SELECT COUNT(*) as demo_attendance FROM web_attendances WHERE company_id IN (1,2,3,4,5);
-- Expected: ~45,000
```

✅ All should match dry-run preview

---

### SECTION 4: DATA VERIFICATION (3 minutes)

#### 4.1 View Demo Companies

```sql
SELECT id, company_name, is_demo FROM companies WHERE is_demo = true ORDER BY id;
```

**Expected Results:**
```
id | company_name                    | is_demo
1  | RatanHR Demo Holdings           | 1
2  | Northstar Manufacturing Demo    | 1
3  | BluePeak Consulting Demo        | 1
4  | Greenfield Retail Demo          | 1
5  | Summit Logistics Demo           | 1
```

#### 4.2 View Demo Employees Sample

```sql
SELECT TOP 10 id, employee_code, full_name, company_id, is_demo 
FROM employees WHERE is_demo = true ORDER BY id;
```

#### 4.3 View Demo Users

```sql
SELECT id, email, company_id, created_at FROM users 
WHERE email LIKE 'demo%@demo.ratanhr.local' 
ORDER BY company_id LIMIT 5;
```

---

### SECTION 5: ISOLATION TESTING (10 minutes)

#### 5.1 Login as Company 1 User

```powershell
$c1_response = curl -X POST "http://localhost:5000/api/auth/login" `
  -H "Content-Type: application/json" `
  -d '{
    "email": "demo1.user0@demo.ratanhr.local",
    "password": "Demo@10#2026"
  }' | ConvertFrom-Json

$c1_token = $c1_response.data.token
Write-Host "Company 1 Token: $c1_token"
```

#### 5.2 Verify Company 1 User Sees Company 1 Data Only

```powershell
curl -X GET "http://localhost:5000/api/employees?companyId=1" `
  -H "Authorization: Bearer $c1_token" | ConvertFrom-Json | Format-Table
```

**Expected:** See Company 1 employees ✅

#### 5.3 Verify Company 1 User CANNOT See Company 2 Data

```powershell
curl -X GET "http://localhost:5000/api/employees?companyId=2" `
  -H "Authorization: Bearer $c1_token" | ConvertFrom-Json | Format-Table
```

**Expected:** Empty list or 403 error ✅ (Isolation working)

#### 5.4 Login as Company 2 User

```powershell
$c2_response = curl -X POST "http://localhost:5000/api/auth/login" `
  -H "Content-Type: application/json" `
  -d '{
    "email": "demo2.user0@demo.ratanhr.local",
    "password": "Demo@20#2026"
  }' | ConvertFrom-Json

$c2_token = $c2_response.data.token
```

#### 5.5 Verify Company 2 User Sees Company 2 Data Only

```powershell
curl -X GET "http://localhost:5000/api/employees?companyId=2" `
  -H "Authorization: Bearer $c2_token" | ConvertFrom-Json | Format-Table
```

**Expected:** See Company 2 employees ✅

#### 5.6 Verify Company 2 User CANNOT See Company 1 Data

```powershell
curl -X GET "http://localhost:5000/api/employees?companyId=1" `
  -H "Authorization: Bearer $c2_token" | ConvertFrom-Json | Format-Table
```

**Expected:** Empty list or 403 error ✅ (Cross-company isolation confirmed)

---

### SECTION 6: CLEANUP TESTING (5 minutes)

#### 6.1 Preview Cleanup (No Deletion)

Use your SuperAdmin token:
```powershell
curl -X GET "http://localhost:5000/api/admin/demo/cleanup/dry-run" `
  -H "Authorization: Bearer $env:RATANHR_TOKEN" | ConvertFrom-Json | Format-Table
```

**Expected Output:**
```
wasDryRun companiesDeleted employeesDeleted attendanceRecordsDeleted
True      5               500             45000
```

✅ **KEY:** `wasDryRun = True` means no actual deletion

#### 6.2 Execute Cleanup

```powershell
curl -X DELETE "http://localhost:5000/api/admin/demo/cleanup?confirm=true" `
  -H "Authorization: Bearer $env:RATANHR_TOKEN" | ConvertFrom-Json | Format-Table
```

**Expected Output:**
```
wasDryRun isSuccess message
False     True      Demo data successfully cleaned up
```

✅ **KEY:** `wasDryRun = False` means data was deleted

#### 6.3 Verify Cleanup Successful

```sql
SELECT COUNT(*) as remaining_demo_companies FROM companies WHERE is_demo = true;
SELECT COUNT(*) as remaining_demo_employees FROM employees WHERE is_demo = true;
```

**Expected:** Both return 0 ✅

---

### SECTION 7: FINAL VERIFICATION (2 minutes)

#### 7.1 Verify Real Data Untouched

```sql
SELECT COUNT(*) as real_companies FROM companies WHERE is_demo = false;
SELECT COUNT(*) as real_employees FROM employees WHERE is_demo = false;
```

**Expected:** Same counts as before seeding ✅

#### 7.2 Revert Demo Mode Settings

Edit `HRMS.API/appsettings.json`:

Change back to:
```json
"DemoMode": {
  "Enabled": false,
  "SeedEnabled": false,
  "AllowProduction": false,
```

⚠️ **IMPORTANT:** Do this before committing

---

## 📊 TESTING CHECKLIST

Print this and check off as you go:

```
SECTION 1: VALIDATION
  [ ] Run SETUP_AND_TEST.bat successfully
  [ ] Build completed with 0 errors
  [ ] All 36+ tests passed
  [ ] Got SuperAdmin JWT token
  [ ] Validation endpoint returned isValid=true

SECTION 2: DRY-RUN
  [ ] Seed dry-run returned wasDryRun=true
  [ ] Showed expected record counts
  [ ] Database has 0 demo records after dry-run
  [ ] Cleanup dry-run returned wasDryRun=true

SECTION 3: SEED
  [ ] POST seed?confirm=true returned wasDryRun=false
  [ ] Seeding completed successfully
  [ ] Expected execution time reasonable

SECTION 4: DATA VERIFICATION
  [ ] 5 demo companies created
  [ ] ~500 demo employees created
  [ ] ~45,000 attendance records created
  [ ] All records marked is_demo=true

SECTION 5: ISOLATION
  [ ] Company 1 user logged in successfully
  [ ] Company 1 user sees Company 1 data
  [ ] Company 1 user CANNOT see Company 2 data
  [ ] Company 2 user logged in successfully
  [ ] Company 2 user sees Company 2 data
  [ ] Company 2 user CANNOT see Company 1 data
  [ ] Isolation verified: Companies 1 ≠ 2

SECTION 6: CLEANUP
  [ ] Cleanup dry-run showed correct counts
  [ ] DELETE cleanup?confirm=true executed
  [ ] All 5 companies deleted
  [ ] All ~500 employees deleted
  [ ] All ~45,000 attendance records deleted

SECTION 7: FINAL
  [ ] 0 demo records remain in database
  [ ] Real customer data preserved
  [ ] Reverted Demo Mode settings to defaults
  [ ] No errors encountered

OVERALL: ALL TESTS PASSED ✅
```

---

## 🆘 TROUBLESHOOTING

| Problem | Solution |
|---------|----------|
| Build fails | Run `dotnet clean` first |
| Tests fail | Check database connection |
| 401 Unauthorized | Get new JWT token |
| Demo Mode disabled | Enable in appsettings.json |
| Database empty | Verify seeding completed |
| Isolation failing | Check query filters in code |
| Cleanup failing | Verify IsDemo column exists |

---

## 📚 DETAILED GUIDES

For more information, see:
- `COMPLETE_API_TESTING_GUIDE.md` - Full API reference
- `TEST_DATA_VERIFICATION_QUERIES.sql` - All verification queries
- `COMPLETE_READINESS_CHECKLIST.md` - Complete checklist
- `FINAL_SECURITY_AUDIT_REPORT.md` - Security details

---

## ⏱️ TIMELINE

| Phase | Duration | Status |
|-------|----------|--------|
| Setup | 5 min | ⏱️ |
| Enable Demo Mode | 1 min | ⏱️ |
| Start App | 1 min | ⏱️ |
| Validation | 2 min | ⏱️ |
| Dry-Run | 3 min | ⏱️ |
| Seed Data | 5 min | ⏱️ |
| Verify | 3 min | ⏱️ |
| Isolation | 10 min | ⏱️ |
| Cleanup | 5 min | ⏱️ |
| Final Checks | 2 min | ⏱️ |
| **TOTAL** | **~37 min** | **✅** |

---

## ✅ SUCCESS CRITERIA

You'll know everything is working when:

- ✅ Build completes with 0 errors
- ✅ All 36+ tests pass
- ✅ Validation endpoint succeeds
- ✅ Dry-run seed shows ~100K records estimated
- ✅ Live seed creates exactly that many records
- ✅ All records marked `is_demo=true`
- ✅ Company 1 user sees only Company 1 data
- ✅ Company 2 user sees only Company 2 data
- ✅ Cross-company access blocked
- ✅ Cleanup removes all demo data
- ✅ Real data completely untouched

---

## 🎯 NEXT STEPS AFTER TESTING

1. ✅ All tests pass locally
2. ✅ Revert Demo Mode settings
3. ✅ Commit changes
4. ✅ Deploy to staging
5. ✅ Run tests in staging
6. ✅ Deploy to production
7. ✅ Monitor for issues

---

## ✅ YOU ARE READY

Everything is set up and ready for complete testing.

**Start with:** Run `SETUP_AND_TEST.bat`  
**Then:** Follow the flow above  
**Time:** 30-45 minutes total  
**Result:** Complete validation that system is production-ready

---

**Status: ✅ READY TO START TESTING NOW**
