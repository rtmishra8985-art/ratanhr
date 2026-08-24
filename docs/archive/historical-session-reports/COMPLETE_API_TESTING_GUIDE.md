# 🧪 RatanHR DEMO MODE - COMPLETE API TESTING GUIDE

## 📋 Table of Contents
1. [Setup & Prerequisites](#setup--prerequisites)
2. [Getting JWT Token](#getting-jwt-token)
3. [API Endpoint Reference](#api-endpoint-reference)
4. [Complete Testing Flow](#complete-testing-flow)
5. [Troubleshooting](#troubleshooting)

---

## Setup & Prerequisites

### What You Need
- ✅ RatanHR API running locally (or accessible)
- ✅ SuperAdmin JWT token
- ✅ curl or Postman installed
- ✅ Database connection working
- ✅ Demo Mode enabled in appsettings.json

### Configuration Checklist
```
[ ] Database running and accessible
[ ] appsettings.json connection string correct
[ ] Demo Mode Enabled = true
[ ] Demo Mode SeedEnabled = true
[ ] Demo Mode AllowProduction = true (for localhost)
[ ] Application started: dotnet run --project HRMS.API
[ ] Application running on http://localhost:5000
```

---

## Getting JWT Token

### Method 1: Using the Application's Login Endpoint

**Step 1: Get a SuperAdmin JWT Token**

```bash
# PowerShell
$response = curl -X POST "http://localhost:5000/api/auth/login" `
  -H "Content-Type: application/json" `
  -d '{
    "email": "admin@ratanhr.local",
    "password": "Your_Admin_Password"
  }' | ConvertFrom-Json

$token = $response.data.token
Write-Host "Token: $token"
```

**Step 2: Store the Token**
```powershell
# Save for reuse in this session
$env:RATANHR_TOKEN = $token
```

### Method 2: Manual Token Generation (If you have JWT secret)

See your JWT configuration in appsettings.json:
```json
"Jwt": {
  "Secret": "your_jwt_secret_here",
  "Issuer": "RatanHR",
  "Audience": "RatanHR",
  "ExpiryMinutes": 60
}
```

---

## API Endpoint Reference

### Base URL
```
http://localhost:5000
```

### Demo Mode Endpoints

| Endpoint | Method | Purpose | Authorization |
|----------|--------|---------|---------------|
| `/api/admin/demo/validate` | GET | Validate demo prerequisites | SuperAdmin |
| `/api/admin/demo/seed/dry-run` | GET | Preview seed (no changes) | SuperAdmin |
| `/api/admin/demo/seed` | POST | Create demo data | SuperAdmin |
| `/api/admin/demo/cleanup/dry-run` | GET | Preview cleanup (no changes) | SuperAdmin |
| `/api/admin/demo/cleanup` | DELETE | Delete all demo data | SuperAdmin |
| `/api/admin/demo/status` | GET | Get seed status | SuperAdmin |

---

## Complete Testing Flow

### Phase 1: Validation (2 minutes)

#### Test 1.1: Validate Prerequisites

**Command:**
```bash
curl -X GET "http://localhost:5000/api/admin/demo/validate" \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json"
```

**Expected Response (200 OK):**
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
      "checkName": "Database Connectivity",
      "passed": true,
      "message": "Database is accessible"
    },
    {
      "checkName": "Production Safeguard",
      "passed": true,
      "message": "Non-production environment allows demo operations"
    }
  ]
}
```

**If Validation Fails:**
- ❌ "Demo mode is disabled" → Enable in appsettings.json
- ❌ "Database Connectivity Failed" → Check connection string
- ❌ "Production Safeguard Failed" → Set AllowProduction=true

---

### Phase 2: Dry-Run Testing (2 minutes)

#### Test 2.1: Preview Seed (No Data Changes)

**Command:**
```bash
curl -X GET "http://localhost:5000/api/admin/demo/seed/dry-run" \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json"
```

**Expected Response (200 OK):**
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
  "usersCreated": 15,
  "totalRecordsCreated": 92265,
  "estimatedExecutionTime": "45-60 seconds",
  "executedAt": "2026-08-19T14:30:00Z"
}
```

**Verification:**
- ✅ `wasDryRun: true` (no actual data created)
- ✅ Shows realistic numbers of records to be created
- ✅ No errors in response

#### Test 2.2: Verify No Data Created

**SQL Query:**
```sql
SELECT COUNT(*) as demo_companies FROM companies WHERE is_demo = true;
-- Should return: 0 (no data created yet)

SELECT COUNT(*) as demo_employees FROM employees WHERE is_demo = true;
-- Should return: 0 (no data created yet)
```

#### Test 2.3: Preview Cleanup (No Data Changes)

**Command:**
```bash
curl -X GET "http://localhost:5000/api/admin/demo/cleanup/dry-run" \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json"
```

**Expected Response (200 OK):**
```json
{
  "isSuccess": true,
  "wasDryRun": true,
  "message": "[DRY-RUN] Demo Cleanup Preview",
  "companiesDeleted": 0,
  "employeesDeleted": 0,
  "attendanceRecordsDeleted": 0,
  "leaveRequestsDeleted": 0,
  "assetsDeleted": 0,
  "candidatesDeleted": 0,
  "usersDeleted": 0,
  "totalRecordsDeleted": 0,
  "executedAt": "2026-08-19T14:31:00Z"
}
```

**Verification:**
- ✅ All deletion counts are 0 (no demo data exists yet)
- ✅ `wasDryRun: true` (no actual deletion)

---

### Phase 3: Live Data Creation (5 minutes)

#### Test 3.1: Create Demo Data

**Command:**
```bash
curl -X POST "http://localhost:5000/api/admin/demo/seed?confirm=true" \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json"
```

**Expected Response (200 OK):**
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
  "totalRecordsCreated": 92265,
  "executionTime": "52.3 seconds",
  "executedAt": "2026-08-19T14:32:15Z"
}
```

**Verification:**
- ✅ `wasDryRun: false` (data was actually created)
- ✅ `isSuccess: true` (seed completed successfully)
- ✅ All counts match dry-run preview

#### Test 3.2: Verify Data Created in Database

**SQL Query 1: Count demo companies**
```sql
SELECT COUNT(*) as demo_companies FROM companies WHERE is_demo = true;
-- Expected: 5
```

**SQL Query 2: List demo companies**
```sql
SELECT id, company_name, company_code FROM companies WHERE is_demo = true ORDER BY id;
-- Expected: 5 rows
-- DEMO-RH Demo Holdings, DEMO-NM (Northstar), DEMO-BC (BluePeak), etc.
```

**SQL Query 3: Count demo employees**
```sql
SELECT COUNT(*) as demo_employees FROM employees WHERE is_demo = true;
-- Expected: ~500
```

**SQL Query 4: Count demo employees by company**
```sql
SELECT company_id, COUNT(*) as count FROM employees 
WHERE is_demo = true AND company_id IN (1,2,3,4,5)
GROUP BY company_id;
-- Expected: ~100 employees per company
```

**SQL Query 5: Count attendance records**
```sql
SELECT COUNT(*) as attendance FROM web_attendances 
WHERE company_id IN (1,2,3,4,5);
-- Expected: ~45,000
```

---

### Phase 4: Isolation & Security Testing (5 minutes)

#### Test 4.1: Login as Demo User (Company 1)

**Command:**
```bash
curl -X POST "http://localhost:5000/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "demo1.user0@demo.ratanhr.local",
    "password": "Demo@10#2026"
  }'
```

**Expected Response (200 OK):**
```json
{
  "isSuccess": true,
  "data": {
    "user": {
      "id": 1,
      "email": "demo1.user0@demo.ratanhr.local",
      "fullName": "Demo HR User 0",
      "companyId": 1
    },
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }
}
```

**Verification:**
- ✅ Login successful
- ✅ User has companyId = 1

#### Test 4.2: Test Company 1 Data Isolation

**Command (Get Company 1 employees):**
```bash
curl -X GET "http://localhost:5000/api/employees?companyId=1" \
  -H "Authorization: Bearer $company1_token" \
  -H "Content-Type: application/json"
```

**Expected Response:**
```json
{
  "isSuccess": true,
  "data": {
    "employees": [
      { "id": 1, "fullName": "...", "companyId": 1 },
      { "id": 2, "fullName": "...", "companyId": 1 },
      ...
    ],
    "totalCount": 100
  }
}
```

#### Test 4.3: Verify Cannot Access Company 2 Data

**Command (Try to get Company 2 employees as Company 1 user):**
```bash
curl -X GET "http://localhost:5000/api/employees?companyId=2" \
  -H "Authorization: Bearer $company1_token" \
  -H "Content-Type: application/json"
```

**Expected Response (200 OK, but empty data):**
```json
{
  "isSuccess": true,
  "data": {
    "employees": [],
    "totalCount": 0
  }
}
```

**OR (403 Forbidden if strict authorization):**
```json
{
  "isSuccess": false,
  "message": "You do not have permission to access Company 2 data"
}
```

**Verification:**
- ✅ Company 1 user cannot see Company 2 data
- ✅ Isolation enforced at API level

---

### Phase 5: Cleanup (3 minutes)

#### Test 5.1: Preview Cleanup

**Command:**
```bash
curl -X GET "http://localhost:5000/api/admin/demo/cleanup/dry-run" \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json"
```

**Expected Response (200 OK):**
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
  "candidatesDeleted": 200,
  "usersDeleted": 15,
  "totalRecordsDeleted": 92265,
  "executedAt": "2026-08-19T14:40:00Z"
}
```

**Verification:**
- ✅ `wasDryRun: true` (no actual deletion)
- ✅ Shows all records that will be deleted

#### Test 5.2: Execute Cleanup

**Command:**
```bash
curl -X DELETE "http://localhost:5000/api/admin/demo/cleanup?confirm=true" \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json"
```

**Expected Response (200 OK):**
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
  "candidatesDeleted": 200,
  "usersDeleted": 15,
  "totalRecordsDeleted": 92265,
  "executionTime": "8.2 seconds",
  "executedAt": "2026-08-19T14:41:00Z"
}
```

**Verification:**
- ✅ `wasDryRun: false` (data was actually deleted)
- ✅ All counts match previous dry-run
- ✅ Deletion successful

#### Test 5.3: Verify Cleanup Successful

**SQL Query:**
```sql
SELECT COUNT(*) as remaining_demo_companies FROM companies WHERE is_demo = true;
-- Expected: 0 (all demo data deleted)

SELECT COUNT(*) as remaining_demo_employees FROM employees WHERE is_demo = true;
-- Expected: 0 (all demo data deleted)

SELECT COUNT(*) as preserved_real_companies FROM companies WHERE is_demo = false;
-- Expected: Same as before cleanup (real data untouched)
```

**Verification:**
- ✅ All demo records deleted
- ✅ Real customer data preserved

---

## Troubleshooting

### Issue 1: 401 Unauthorized
**Cause:** Invalid or expired JWT token  
**Solution:**
1. Get a new token with login endpoint
2. Verify token is being sent in Authorization header
3. Check token hasn't expired

### Issue 2: 403 Forbidden
**Cause:** User is not SuperAdmin  
**Solution:**
1. Use SuperAdmin credentials to login
2. Verify user has SuperAdmin role in database

### Issue 3: Demo mode is disabled
**Cause:** DemoMode:Enabled = false in appsettings.json  
**Solution:**
1. Edit appsettings.json
2. Change `"Enabled": false` to `"Enabled": true`
3. Restart application

### Issue 4: Seed requires confirm=true
**Cause:** Called without confirm=true parameter  
**Solution:**
1. Add `?confirm=true` to POST /api/admin/demo/seed URL
2. For example: `POST /api/admin/demo/seed?confirm=true`

### Issue 5: Database Connectivity Failed
**Cause:** Connection string incorrect or database not running  
**Solution:**
1. Verify database is running
2. Check connection string in appsettings.json
3. Test database connection with management tool

### Issue 6: Records not created
**Cause:** Seed returned success but no data visible  
**Solution:**
1. Verify seed was called with confirm=true
2. Check database for records with `is_demo = true`
3. Review application logs for errors

---

## PowerShell Helper Script

**Save as: demo-test.ps1**

```powershell
param(
    [string]$action = "help",
    [string]$token = ""
)

$baseUrl = "http://localhost:5000/api/admin/demo"

switch ($action) {
    "validate" {
        Write-Host "Testing /validate..." -ForegroundColor Cyan
        curl -X GET "$baseUrl/validate" `
            -H "Authorization: Bearer $token" `
            -H "Content-Type: application/json" | ConvertFrom-Json | Format-Table
    }
    "seed-dry" {
        Write-Host "Testing /seed/dry-run..." -ForegroundColor Cyan
        curl -X GET "$baseUrl/seed/dry-run" `
            -H "Authorization: Bearer $token" `
            -H "Content-Type: application/json" | ConvertFrom-Json | Format-Table
    }
    "seed" {
        Write-Host "Creating demo data..." -ForegroundColor Yellow
        curl -X POST "$baseUrl/seed?confirm=true" `
            -H "Authorization: Bearer $token" `
            -H "Content-Type: application/json" | ConvertFrom-Json | Format-Table
    }
    "cleanup-dry" {
        Write-Host "Testing /cleanup/dry-run..." -ForegroundColor Cyan
        curl -X GET "$baseUrl/cleanup/dry-run" `
            -H "Authorization: Bearer $token" `
            -H "Content-Type: application/json" | ConvertFrom-Json | Format-Table
    }
    "cleanup" {
        Write-Host "Deleting demo data..." -ForegroundColor Yellow
        curl -X DELETE "$baseUrl/cleanup?confirm=true" `
            -H "Authorization: Bearer $token" `
            -H "Content-Type: application/json" | ConvertFrom-Json | Format-Table
    }
    default {
        Write-Host "Usage: ./demo-test.ps1 -action [validate|seed-dry|seed|cleanup-dry|cleanup] -token [JWT_TOKEN]"
        Write-Host ""
        Write-Host "Example: ./demo-test.ps1 -action seed-dry -token 'eyJhbGciOi...'"
    }
}
```

**Usage:**
```powershell
$token = "your_jwt_token_here"
./demo-test.ps1 -action validate -token $token
./demo-test.ps1 -action seed-dry -token $token
./demo-test.ps1 -action seed -token $token
./demo-test.ps1 -action cleanup-dry -token $token
./demo-test.ps1 -action cleanup -token $token
```

---

## Complete Testing Checklist

```
[ ] Phase 1: Validation
  [ ] GET /validate returns isValid=true
  [ ] All validation checks pass
  
[ ] Phase 2: Dry-Run
  [ ] GET /seed/dry-run returns wasDryRun=true
  [ ] Database shows 0 demo records
  [ ] GET /cleanup/dry-run returns wasDryRun=true
  
[ ] Phase 3: Live Creation
  [ ] POST /seed?confirm=true returns wasDryRun=false
  [ ] 5 demo companies created
  [ ] ~500 demo employees created
  [ ] ~45,000 attendance records created
  [ ] All records marked is_demo=true
  
[ ] Phase 4: Isolation & Security
  [ ] Company 1 user login successful
  [ ] Company 1 user sees only Company 1 data
  [ ] Company 1 user cannot see Company 2 data
  [ ] Company 2 user sees only Company 2 data
  [ ] Real customer data not affected
  
[ ] Phase 5: Cleanup
  [ ] GET /cleanup/dry-run shows all records to delete
  [ ] DELETE /cleanup?confirm=true deletes all demo data
  [ ] Database shows 0 demo records after cleanup
  [ ] Real customer data preserved
  
[ ] Final Verification
  [ ] All tests passed
  [ ] No errors encountered
  [ ] Performance acceptable
  [ ] Ready for production deployment
```

---

**Status: ✅ Complete API Testing Guide Ready**
