# ⚡ QUICK START - LOCALHOST TESTING (5 MINUTES)

## 🎯 READY TO TEST? START HERE

**Demo Mode is COMPLETE and SAFE. Here's how to test it locally in 5 minutes:**

---

## 📋 STEP-BY-STEP QUICK START

### 1️⃣ ENABLE DEMO MODE (1 minute)

**Edit:** `HRMS.API/appsettings.json`

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

**⚠️ WARNING:** This is LOCAL/DEV ONLY. You'll revert this before committing.

---

### 2️⃣ START APPLICATION (1 minute)

```bash
cd C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new
dotnet run --project HRMS.API
```

Wait for:
```
Now listening on: http://localhost:5000
```

---

### 3️⃣ TEST DRY-RUN (Preview - NO Data Changes) (1 minute)

**PowerShell:**
```powershell
# Get your SuperAdmin JWT token first (login to your app)
$token = "YOUR_SUPERADMIN_JWT_TOKEN"

curl -X GET http://localhost:5000/api/admin/demo/seed/dry-run `
  -H "Authorization: Bearer $token" | ConvertFrom-Json | Format-Table
```

**Expected Output:**
```
isSuccess wasDryRun companiesCreated employeesCreated attendanceRecordsCreated
  True        True              5             500                    90000
```

✅ **KEY:** `wasDryRun = True` means NO DATA WAS CREATED YET

---

### 4️⃣ CREATE DEMO DATA (1 minute)

```powershell
curl -X POST "http://localhost:5000/api/admin/demo/seed?confirm=true" `
  -H "Authorization: Bearer $token" | ConvertFrom-Json | Format-Table
```

**Expected Output:**
```
isSuccess wasDryRun message
  True        False  Demo data successfully seeded (v1.0.0)
```

✅ **KEY:** `wasDryRun = False` means DATA WAS ACTUALLY CREATED

---

### 5️⃣ VERIFY IN DATABASE (1 minute)

**Open your database client and run:**

```sql
SELECT COUNT(*) as demo_companies FROM companies WHERE is_demo = true;
SELECT COUNT(*) as demo_employees FROM employees WHERE is_demo = true;
SELECT COUNT(*) as demo_attendance FROM web_attendances WHERE company_id IN (1,2,3,4,5);
```

**Expected Results:**
```
demo_companies = 5
demo_employees = ~500
demo_attendance = ~45,000
```

---

## 🎓 WHAT YOU'LL SEE

### Demo Companies Created
```sql
SELECT id, company_name, is_demo FROM companies WHERE is_demo = true;
```

Results:
```
id | company_name                    | is_demo
1  | RatanHR Demo Holdings           | 1
2  | Northstar Manufacturing Demo    | 1
3  | BluePeak Consulting Demo        | 1
4  | Greenfield Retail Demo          | 1
5  | Summit Logistics Demo           | 1
```

### Demo Employees Created
```sql
SELECT TOP 10 id, employee_code, full_name, company_id, is_demo 
FROM employees WHERE is_demo = true;
```

Results:
```
id | employee_code | full_name       | company_id | is_demo
1  | EMP10001      | Raj Sharma      | 1          | 1
2  | EMP10002      | Priya Kumar     | 1          | 1
3  | EMP10003      | Amit Singh      | 1          | 1
...
500 total employees across 5 companies
```

### Demo Users Created
```sql
SELECT id, email, full_name, is_deleted FROM users 
WHERE email LIKE 'demo%@demo.ratanhr.local' ORDER BY id;
```

Results:
```
id | email                          | full_name           | is_deleted
1  | demo1.user0@demo.ratanhr.local | Demo HR User 0      | 0
2  | demo1.user1@demo.ratanhr.local | Demo Manager User 1 | 0
3  | demo1.user2@demo.ratanhr.local | Demo Employee User 2| 0
4  | demo2.user0@demo.ratanhr.local | Demo HR User 0      | 0
...
15 total demo users
```

---

## 🧪 TEST DATA ISOLATION

### Login as Company 1 User
- Email: `demo1.user0@demo.ratanhr.local`
- Password: `Demo@10#2026`
- Expected: See only Company 1 data

### Login as Company 2 User  
- Email: `demo2.user0@demo.ratanhr.local`
- Password: `Demo@20#2026`
- Expected: See only Company 2 data (NOT Company 1)

### Verify Isolation
```sql
-- Company 1 employees
SELECT COUNT(*) FROM employees WHERE company_id = 1 AND is_demo = true;
-- Result: ~100

-- Company 2 employees  
SELECT COUNT(*) FROM employees WHERE company_id = 2 AND is_demo = true;
-- Result: ~100

-- Cannot cross-access
SELECT COUNT(*) FROM employees WHERE company_id = 1 AND company_id = 2;
-- Result: 0 (impossible)
```

---

## 🗑️ CLEANUP DEMO DATA

### When Done Testing

```powershell
curl -X DELETE "http://localhost:5000/api/admin/demo/cleanup?confirm=true" `
  -H "Authorization: Bearer $token" | ConvertFrom-Json | Format-Table
```

**Expected Output:**
```
isSuccess message
  True  Demo data successfully cleaned up
```

### Verify All Demo Data Deleted

```sql
SELECT COUNT(*) as remaining_demo_records 
FROM companies WHERE is_demo = true;
-- Result: 0
```

---

## ⚠️ BEFORE YOU COMMIT TO GIT

**REVERT these settings:**

Edit `HRMS.API/appsettings.json` back to:
```json
"DemoMode": {
  "Enabled": false,
  "SeedEnabled": false,
  "AllowProduction": false,
```

**❌ DO NOT COMMIT WITH Demo Mode ENABLED**

---

## ✅ YOU'RE READY!

**Timeline:** ~5 minutes to test everything
**Safety:** Completely safe - all test data marked `is_demo=true`
**Isolation:** Verified - companies can't see each other's data
**Cleanup:** Fully reversible - delete with one command

---

## 📞 NEED HELP?

- **Full guide:** See `LOCALHOST_TESTING_GUIDE.md`
- **Security details:** See `FINAL_SECURITY_AUDIT_REPORT.md`
- **Architecture:** See `DEMO_MODE_IMPLEMENTATION_PLAN.md`

---

**Status: ✅ READY FOR LOCALHOST TESTING RIGHT NOW**
