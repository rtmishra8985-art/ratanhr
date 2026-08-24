# PHASE 7 PROMPT
## End-to-End Integration Testing & Production Release Readiness

**Project:** RatanHR HRMS v1.0.4  
**Phase:** 7 (End-to-End Integration Testing)  
**Phase 6 Status:** ✅ **COMPLETE — ZERO BLOCKERS — APPROVED**  
**Phase 7 Status:** 🟢 **READY TO BEGIN**  
**Date Initiated:** 2026-08-12

---

## EXECUTIVE SUMMARY

**Phase 6 Closure: ✅ APPROVED**

All security controls verified. Multi-tenant isolation confirmed. Zero IDOR vulnerabilities. Production-ready code.

**Phase 7 Objective:** Real-world end-to-end testing with complete workflows, actual user journeys, and production scenario verification.

---

## PHASE 7 SCOPE

### Primary Goal

Verify that the entire RatanHR system works **end-to-end** in realistic production scenarios:

```
USER LOGIN
  ↓
JWT AUTHENTICATION
  ↓
MFA VERIFICATION (TOTP)
  ↓
TENANT CONTEXT INJECTION (CompanyId)
  ↓
EMPLOYEE DASHBOARD
  ↓
PAYROLL PROCESSING
  ↓
PAYSLIP GENERATION
  ↓
PAYSLIP PDF DOWNLOAD
  ↓
AUDIT LOGGING
  ↓
DATA PERSISTENCE (DATABASE)
  ↓
CROSS-TENANT ISOLATION VERIFICATION
```

### User Journey Testing

**Scenario 1: HR Admin Complete Workflow**
1. Login with MFA
2. Create/update employee
3. Generate payroll
4. Lock payroll period
5. Employee accesses own payslip
6. Download payslip PDF
7. Verify audit trail

**Scenario 2: Employee Self-Service Workflow**
1. Login
2. View personal profile
3. Access own payslips (cannot see others)
4. Download payslip PDF
5. Submit leave request
6. View leave balance

**Scenario 3: Multi-Company Isolation Workflow**
1. Company A admin logs in
2. Attempts to access Company B data
3. Request blocked (403)
4. Audit logged
5. Company B admin verifies same behavior

**Scenario 4: SuperAdmin Workflow**
1. SuperAdmin login
2. Cross-company visibility verified (intentional)
3. Can switch between companies
4. All data visible (no tenant filtering)
5. Audit trail complete

---

## 15 END-TO-END TEST CASES

### Test Case 1: Complete Login → MFA → Dashboard Flow

**Scenario:** New user first login with MFA setup

**Steps:**
1. User navigates to /login
2. Enters email: emp001@company-a.com, password: SecurePass123!
3. POST /api/auth/login → Receives temp token (MfaRequired=true)
4. User scans QR code, sets up TOTP in authenticator app
5. POST /api/auth/mfa/verify with 6-digit code
6. Receives full JWT (MfaVerified=true)
7. Browser stores JWT in HttpOnly cookie
8. User navigates to /dashboard
9. Dashboard loads → calls GET /api/employees/profile
10. Profile returned (authenticated)

**Expected Result:** ✅ PASS
- User authenticated
- MFA verified
- JWT valid (30 min expiry)
- Dashboard loads
- No cross-tenant data leakage

---

### Test Case 2: Payroll Generation → Calculation → Database Persistence

**Scenario:** HR Admin generates payroll for multiple employees

**Steps:**
1. HR Admin logs in
2. Navigates to Payroll → Generate Monthly
3. Selects Month: August 2026, Company: Company A
4. Selects employee list: EMP001, EMP002, EMP003
5. POST /api/payroll/process (BulkPayrollDto)
6. System calculates:
   - Attendance query (WebAttendance table)
   - Salary structure query (SalaryStructure table)
   - Bonus query (Bonuses table)
   - Payslip calculation (IndianPayrollCalculator)
   - Database insert (Payslips table)
   - Audit logging (AuditLog table)
7. Response: { Generated: 3, Skipped: 0, Failed: 0 }
8. Verify database: SELECT COUNT(*) FROM Payslips WHERE Month=8, Year=2026, CompanyId=1
9. Expected: 3 rows inserted

**Expected Result:** ✅ PASS
- All 3 payslips calculated correctly
- Database persistence confirmed
- Audit trail logged
- No calculation errors
- NetPay > 0 for all

---

### Test Case 3: Payslip PDF Generation & Download

**Scenario:** Employee downloads payslip PDF

**Steps:**
1. Employee logs in
2. Navigates to Payslips
3. GET /api/payroll/payslips (authenticated, tenant-scoped)
4. Lists own payslips (Company A only)
5. Clicks "Download PDF" for payslip ID=100
6. GET /api/payroll/payslips/100/pdf
7. System:
   - Verifies JWT authentication
   - Verifies payslip belongs to caller's company
   - Enriches payslip with employee/company details
   - Generates PDF (iText/SAP Crystal Reports)
   - Returns binary file
8. Browser downloads file: payslip_100.pdf
9. Employee opens PDF → sees:
   - Employee name, ID, department
   - Salary components (basic, HRA, DA, etc.)
   - Deductions (PF, ESI, PT, TDS)
   - Net pay amount
   - Company logo and branding
   - Digital signature/watermark

**Expected Result:** ✅ PASS
- PDF generated successfully
- No PII leaked in filename/headers
- Company branding present
- All salary components correct
- Download size < 500KB (efficient PDF)

---

### Test Case 4: Cross-Tenant Isolation (Company A Cannot Access Company B)

**Scenario:** Company A admin attempts to access Company B payslips

**Steps:**
1. Company A admin logs in → JWT { companyId: 1, role: "Admin" }
2. Database has payslips for both companies:
   - Company A: Payslip ID 100-102
   - Company B: Payslip ID 200-202
3. Attempt 1: GET /api/payroll/payslips (no company filter in URL)
   - Expected: Returns only Company A payslips (0-3)
   - Company B payslips: NOT returned
4. Attempt 2: GET /api/payroll/payslips?companyId=2
   - Expected: Returns 0 rows (filtered by global query filter)
   - NOT returns Company B payslips
5. Attempt 3: GET /api/payroll/payslips/200 (Company B payslip)
   - Expected: 404 Not Found (or 0 rows)
   - NOT returns payslip details
6. Check audit log: No AUTHORIZATION_FAILED events (request silently filtered)

**Expected Result:** ✅ PASS
- Company A sees only own data
- Global query filter active
- No cross-tenant leakage
- Silent filtering (no errors in UI)

---

### Test Case 5: Employee Cannot See Other Employee's Payslip

**Scenario:** Employee EMP001 attempts to access EMP002's payslip

**Steps:**
1. Employee EMP001 logs in → JWT { sub: EMP001, companyId: 1 }
2. GET /api/payroll/payslips (returns own payslips only)
3. Payslips list: [Payslip(emp=EMP001, month=8, year=2026)]
4. Attempt: GET /api/payroll/payslips (ID for EMP002)
   - Even if EMP001 guesses the payslip ID
   - Expected: 404 Not Found
   - NOT returns EMP002's data
5. Verify controller: `[Authorize(Policy = "RequireMfaCompleted")]`
   - Employee request without MFA: 401 Unauthorized
6. Verify service layer: GetEmployeePayslipsAsync checks employeeId parameter

**Expected Result:** ✅ PASS
- Employee sees only own payslips
- Guessing ID doesn't bypass security
- Authorization policy enforced
- Service layer validates ownership

---

### Test Case 6: Refresh Token Rotation & MFA Bypass Prevention

**Scenario:** Token refresh after MFA is enabled

**Steps:**
1. User logs in without MFA (MfaEnabled=false)
   - Receives JWT + refresh token (MfaVerified=false)
2. Admin enables MFA on user's account
3. User attempts to use refresh token:
   - POST /api/auth/refresh with old refresh token
   - AuthService.RefreshTokenAsync checks:
     - Token exists? Yes
     - Token active (not revoked)? Yes
     - User exists? Yes
     - User.IsMfaEnabled? YES (newly enabled)
     - Token.MfaVerified? NO (old token, pre-MFA)
   - Decision: REVOKE token, return null
4. Refresh fails (401 Unauthorized)
5. User forced to login again → complete MFA flow
6. New JWT + refresh token issued (MfaVerified=true)

**Expected Result:** ✅ PASS
- Pre-MFA tokens invalidated when MFA enabled
- User forced to complete MFA
- New token carries MfaVerified=true
- No MFA bypass possible

---

### Test Case 7: Password Change Revokes All Sessions

**Scenario:** User changes password; old tokens invalidated

**Steps:**
1. User has 2 active sessions (laptop + mobile)
   - Laptop JWT: valid, refresh token: active
   - Mobile JWT: valid, refresh token: active
2. User logs in to web → changes password
   - POST /api/auth/change-password
   - New password: NewSecurePass456!@#
   - AuthService.ChangePasswordAsync:
     - Verifies old password
     - Validates new password (policy)
     - Updates PasswordHash
     - Queries all active refresh tokens (RevokedAt=null)
     - Sets RevokedAt=DateTime.UtcNow for all
3. Laptop refreshes JWT before expiry:
   - POST /api/auth/refresh
   - Refresh token is now revoked (RevokedAt != null)
   - RefreshTokenAsync returns null
   - Browser receives 401 Unauthorized
   - User logged out (must login again)
4. Mobile session also expires naturally
   - Cannot refresh (token revoked)
   - User logged out

**Expected Result:** ✅ PASS
- All active sessions revoked
- Users forced to re-login
- Old tokens unusable
- New login creates fresh session

---

### Test Case 8: Rate Limiting - Login Brute Force Protection

**Scenario:** Attacker attempts repeated login (brute force)

**Steps:**
1. Attacker has IP: 192.168.1.100
2. Redis rate limiter: ratelimit:login:192.168.1.100
3. Policy: 10 requests per 60 seconds
4. Attempt sequence:
   - Req 1-10: All accepted (POST /api/auth/login)
   - Req 11: HTTP 429 Too Many Requests
   - Req 12: HTTP 429 Too Many Requests
5. Verify response headers:
   - Retry-After: 60 (seconds until next request allowed)
6. After 60 seconds:
   - Rate limit resets
   - Req 1: Accepted again

**Expected Result:** ✅ PASS
- First 10 requests: 200 OK (or auth failure)
- Request 11+: 429 Too Many Requests
- Retry-After header present
- IP-based throttling active
- Protects against brute force

---

### Test Case 9: Account Lockout After Failed Attempts

**Scenario:** User fails login 5 times; account locked

**Steps:**
1. User EMP001: Max failed attempts = 5
2. Attempt 1: Wrong password → user.FailedLoginAttempts=1
3. Attempt 2: Wrong password → user.FailedLoginAttempts=2
4. Attempt 3: Wrong password → user.FailedLoginAttempts=3
5. Attempt 4: Wrong password → user.FailedLoginAttempts=4
6. Attempt 5: Wrong password → user.FailedLoginAttempts=5
   - System: FailedLoginAttempts >= 5
   - Action: Set user.LockoutUntil = DateTime.UtcNow.AddMinutes(15)
   - Response: "Account locked. Try again in 15 minutes."
   - Audit logged: ACCOUNT_LOCKED
7. Attempt 6 (immediate): LockoutUntil > DateTime.UtcNow
   - Response: "Account temporarily locked. Try again in X minutes."
8. Wait 15 minutes → LockoutUntil expires
9. Attempt 7: LockoutUntil <= DateTime.UtcNow
   - Account unlocked
   - Login succeeds
   - user.FailedLoginAttempts = 0
   - user.LockoutUntil = null

**Expected Result:** ✅ PASS
- Account locked after 5 failed attempts
- Lockout duration: 15 minutes
- Cannot login during lockout
- Attempts during lockout logged
- Automatic unlock after duration

---

### Test Case 10: CSRF Token Protection

**Scenario:** Mutation request without CSRF token

**Steps:**
1. User logs in
2. GET /api/auth/csrf
   - Response: { requestToken: "abc123xyz..." }
   - Browser cookie: XSRF-TOKEN=def456uvw... (HttpOnly)
3. POST /api/payroll/process (legitimate request)
   - Header: X-XSRF-TOKEN: abc123xyz... (from response body)
   - Cookie: XSRF-TOKEN=def456uvw... (automatic)
   - Framework validates: CookieToken ↔ RequestToken match
   - Result: ✅ VALID → Request accepted
4. Malicious POST /api/payroll/process (CSRF attack)
   - No X-XSRF-TOKEN header (attacker cannot read HttpOnly cookie)
   - Cookie: XSRF-TOKEN=def456uvw... (sent automatically)
   - Framework validates: CookieToken (present) vs RequestToken (missing)
   - Result: ❌ INVALID → Request rejected (400 Bad Request)

**Expected Result:** ✅ PASS
- Legitimate requests with token: Accepted
- Requests without token: Rejected
- Token verified (cookie ↔ header match)
- CSRF attack prevented

---

### Test Case 11: Audit Trail - All Operations Logged

**Scenario:** Complete audit trail for payroll operation

**Steps:**
1. HR Admin logs in
2. POST /api/payroll/process (generates payroll)
3. Check AuditLog table:
   - Event: PAYROLL_GENERATE (or BULK_PAYROLL)
   - EntityType: Payslip
   - EntityId: 100, 101, 102, ...
   - ActorId: 123 (admin's user ID)
   - ActorName: "John Admin"
   - Action: "Generated"
   - Timestamp: 2026-08-12 10:15:30
   - CompanyId: 1
   - Details: "Generated: 3 payslips, Skipped: 0, Failed: 0"
   - OldValues: null
   - NewValues: { BasicPay: 50000, Gross: 77850, Net: 65766 }
   - IsSuccess: true
4. Employee accesses payslip:
   - Event: PAYSLIP_VIEW (or similar)
   - ActorId: Employee ID
   - EntityId: 100
   - CompanyId: 1
5. Employee downloads PDF:
   - Event: PAYSLIP_PDF_DOWNLOAD
   - EntityId: 100
   - IpAddress: 192.168.1.50
   - UserAgent: "Mozilla/5.0..."

**Expected Result:** ✅ PASS
- All mutations logged
- Actor information captured
- Timestamp accurate
- CompanyId scoped
- No PII in audit log (salaries redacted if logged as objects)

---

### Test Case 12: Payroll Duplicate Prevention

**Scenario:** Attempt to generate payroll twice for same period

**Steps:**
1. HR Admin generates August 2026 payroll
   - POST /api/payroll/process { month: 8, year: 2026 }
   - Response: { Generated: 3 }
   - Payslips inserted: ID 100, 101, 102
2. Immediately generate again:
   - POST /api/payroll/process { month: 8, year: 2026 }
   - Service checks: Payslip exists for (EMP001, 8, 2026)?
   - Yes → NetPay=65766 > 0 → Already calculated
   - No Overwrite flag
   - Result: SKIP or ERROR
   - Response: { Generated: 0, Skipped: 3 }
3. Generate with Overwrite=true:
   - POST /api/payroll/process { month: 8, year: 2026, overwrite: true }
   - Service allows recalculation
   - Old payslips updated (not deleted)
   - Response: { Generated: 3 }
   - Audit logged: PAYSLIP_GENERATE with "overwrite=true"

**Expected Result:** ✅ PASS
- Duplicate generation prevented (by default)
- Overwrite flag allows recalculation
- Audit trail records overwrites
- No orphaned records

---

### Test Case 13: Performance - Payslip List with 10,000 Records

**Scenario:** Query performance with large dataset

**Steps:**
1. Database seeded with 10,000 payslips (40 companies × 250 months/company)
2. HR Admin queries payslips:
   - GET /api/payroll/payslips?page=1&pageSize=20&sortBy=createdDate
3. Measure:
   - Response time: < 500ms target
   - Database queries: Should be 1-2 (paged query + employee enrichment)
   - Memory footprint: < 50MB
4. Response includes:
   - 20 payslips (paginated)
   - Enriched with employee/company data (no N+1)
   - TotalCount: 10000
   - Sorted by CreatedDate DESC
5. Navigate to page 500 (last page):
   - GET /api/payroll/payslips?page=500&pageSize=20
   - Skip: 9980, Take: 20
   - Still < 500ms (SQL can handle OFFSET/LIMIT efficiently)

**Expected Result:** ✅ PASS
- Response time < 500ms
- Paging works efficiently
- No N+1 queries
- Memory usage acceptable
- Sorting applied at DB level

---

### Test Case 14: Concurrency - Simultaneous Payroll Generation

**Scenario:** Two HR admins generate payroll simultaneously (different months)

**Steps:**
1. Thread 1 (Admin A): POST /api/payroll/process { month: 7, year: 2026 }
2. Thread 2 (Admin B): POST /api/payroll/process { month: 8, year: 2026 }
3. Both queries execute concurrently:
   - Attendance pre-load (Thread 1 for July, Thread 2 for August)
   - Salary structure query
   - Payslip calculation
   - Database insert
4. Both transactions commit successfully
5. Verify database:
   - July payslips: 3 rows (EMP001, EMP002, EMP003)
   - August payslips: 3 rows (same employees)
   - Total: 6 rows
   - No duplicates
   - No data corruption

**Expected Result:** ✅ PASS
- Concurrent requests don't interfere
- Each uses separate transaction
- Both complete successfully
- Data integrity maintained
- No race conditions

---

### Test Case 15: Error Handling - Missing Salary Structure

**Scenario:** Generate payroll for employee with no salary structure

**Steps:**
1. Employee EMP099 has no SalaryStructure record
2. HR Admin generates payroll including EMP099:
   - POST /api/payroll/process { month: 8, year: 2026, employeeIds: [..., "EMP099"] }
3. System processes:
   - EMP001, EMP002: Success (have salary structures)
   - EMP099: Salary lookup returns null
   - Service: "No active salary structure"
   - Action: Generate payslip with zero earnings
   - Log error: "EMP099: no active salary structure"
4. Response: { Generated: 2, Skipped: 0, Failed: 1, Errors: ["EMP099: no active salary structure"] }
5. Payslip for EMP099:
   - BasicPay: 0
   - HRA: 0
   - Gross: 0
   - NetPay: 0
   - Status: INCOMPLETE or WARNING

**Expected Result:** ✅ PASS
- Error handled gracefully
- Payroll continues for other employees
- Error logged in response
- Incomplete payslips visible (admins know to fix)
- No system crash

---

## SUCCESS CRITERIA

Phase 7 is **PASS** if:

✅ All 15 test cases pass (complete workflows from login to persistence)  
✅ No uncaught exceptions  
✅ Response times acceptable (< 500ms for typical requests)  
✅ Database consistency verified (no orphaned records)  
✅ Audit trail complete (all operations logged)  
✅ Tenant isolation enforced (cross-company access blocked)  
✅ PDF generation works (valid files, correct data)  
✅ Concurrent requests handled correctly (no race conditions)  
✅ Error handling graceful (no cascading failures)  
✅ UI/API responsive (no hangs or timeouts)  
✅ Zero security vulnerabilities discovered  
✅ User experience smooth (no confusing errors)  

---

## FAILURE CRITERIA

Phase 7 is **FAIL** if:

❌ Any test case fails (incomplete workflow)  
❌ Cross-tenant data leakage (Company A sees Company B data)  
❌ Authentication bypassed (user gets in without proper auth)  
❌ MFA bypass (user skips MFA somehow)  
❌ Performance unacceptable (response > 1 second)  
❌ Database inconsistency (orphaned records, missing audit)  
❌ PDF generation fails  
❌ Concurrent requests corrupt data  
❌ Unhandled exceptions visible to users  
❌ Security vulnerability discovered  

---

## PHASE 7 EXECUTION PLAN

### Week 1: Test Setup & Execution

- [ ] Prepare test database with 5 companies, 100 employees
- [ ] Create test user accounts (5 admins, 20 employees)
- [ ] Generate sample payroll data
- [ ] Execute Test Cases 1-5 (login, auth, payroll, IDOR)
- [ ] Document results

### Week 1-2: Advanced Testing

- [ ] Execute Test Cases 6-10 (token rotation, lockout, CSRF, audit)
- [ ] Execute Test Cases 11-15 (performance, concurrency, error handling)
- [ ] Performance benchmarking (response times, memory)
- [ ] Stress testing (100+ concurrent users)

### Week 2: Validation & Sign-Off

- [ ] Review all test results
- [ ] Verify audit trails
- [ ] Security spot-checks
- [ ] Generate Phase 7 report
- [ ] Final sign-off

---

## DELIVERABLES (Phase 7)

1. **Phase7_Test_Results.md** (20+ pages)
   - Test case results (pass/fail)
   - Screenshot evidence
   - Performance metrics
   - Error logs (if any)

2. **Phase7_Audit_Report.md** (10+ pages)
   - Database audit (record counts, consistency)
   - Audit trail verification
   - Tenant isolation confirmation

3. **Phase7_Performance_Report.md** (5+ pages)
   - Response times
   - Database query analysis
   - Memory profiling
   - Concurrency test results

4. **Phase7_Final_Sign_Off.md** (3+ pages)
   - Executive summary
   - Pass/Fail verdict
   - Production readiness confirmation
   - Release approval

---

## PHASE 7 KICKOFF

**When:** Immediately after Phase 6 completion  
**Status:** 🟢 **READY TO BEGIN**  
**Prerequisites:** All Phase 6 items complete ✅

---

## NEXT COMMAND

**Reply "START PHASE 7" to begin End-to-End Integration Testing with Test Case 1 execution.**

---

**Document:** PHASE7_PROMPT.md  
**Status:** 🟢 READY  
**Authority:** Gordon (Docker AI)  
**Date:** 2026-08-12

