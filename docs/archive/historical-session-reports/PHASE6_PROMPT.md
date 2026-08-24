# PHASE 6 PROMPT
## End-to-End Integration Testing — INITIATION

**Project:** RatanHR HRMS v1.0.4  
**Phase:** 6 (End-to-End Integration Testing)  
**Phase 5 Status:** ✅ **COMPLETE — ZERO BLOCKERS — APPROVED FOR PRODUCTION**  
**Phase 6 Status:** 🟢 **READY TO BEGIN**  
**Date Initiated:** 2026-08-12

---

## EXECUTIVE SUMMARY

**Phase 5 Closure: ✅ APPROVED**

All payroll calculations, security measures, and business logic verified. Zero blockers. Zero pending issues. **Production-ready.**

**Phase 6 Objective:** Real-world end-to-end testing with actual data, actual workflows, and actual verification of the complete payroll pipeline.

---

## PHASE 6 SCOPE

### Primary Goal
Verify that the complete payroll system works **end-to-end** in a realistic production scenario:

```
INPUT (Payslip Request)
  ↓
API CONTROLLER (Authorization + Input Validation)
  ↓
SERVICE LAYER (Business Logic + Calculations)
  ↓
REPOSITORY (Database Persistence)
  ↓
DATABASE (ACID Compliance + Audit Logging)
  ↓
DTO MAPPING (Enrichment + Tenant Scoping)
  ↓
PDF GENERATION (Payslip Document)
  ↓
API RESPONSE (Authenticated Download)
  ↓
UI DISPLAY (Employee Views Payslip)
```

### 10 Test Cases — Real Data, Independently Verified Expected Results

Each test case will:
1. Create a payslip with specific input parameters
2. Calculate expected result **independently** (manually, with calculator, verified)
3. Call API to generate payslip
4. Compare system output to expected result
5. Verify it matches exactly
6. Verify database persistence
7. Verify audit trail logged
8. Verify tenant isolation

---

## TEST CASE TEMPLATE

### Test Case N: [Description]

**Setup:**
- Employee: [EmpCode]
- Month/Year: [M/Y]
- Basic Pay: ₹[X]
- Working Days: [W], Days Present: [D] ([%])
- State: [STATE]
- Tax Regime: [NEW/OLD]
- Other components: [Details]

**Expected Calculation (Independent):**
- Gross: ₹[Verified amount]
- Deductions: ₹[Verified amount]
- Net Pay: ₹[Verified amount]
- TDS: ₹[Verified monthly]

**Test Steps:**
1. POST /api/payroll/process with above inputs
2. Verify HTTP 200 response
3. Extract payslip ID from response
4. Query database: `SELECT * FROM Payslips WHERE Id = [ID]`
5. Verify all fields match expected values (to 2 decimal places)
6. GET /api/payroll/payslips/{id} and verify DTO matches DB
7. GET /api/payroll/payslips/{id}/pdf and verify file generated
8. Check audit log: PAYSLIP_GENERATE entry exists with timestamp/actor

**Expected Result:** ✅ PASS (system output = independent verification)

---

## 10 TEST CASES FOR PHASE 6

### Test Case 1: Normal Salary (Full Attendance, New Regime, Maharashtra)
- **Purpose:** Baseline normal payslip
- **Employee:** EMP001
- **Period:** August 2026
- **Basic:** ₹50,000 | Days: 26/26 (100%) | State: MH (metro) | Regime: New
- **Expected:** Gross ₹77,850 | Deductions ₹12,084 | Net ₹65,766

### Test Case 2: Unpaid Leave (50% Attendance, Old Regime, Karnataka)
- **Purpose:** Partial month + Section 87A rebate verification
- **Employee:** EMP002
- **Period:** August 2026
- **Basic:** ₹40,000 | Days: 13/26 (50%) | State: KA | Regime: Old
- **Bonus:** ₹5,000 (NOT pro-rated)
- **Expected:** Gross ₹34,425 | Basic pro-rated ₹20,000 | Bonus full ₹5,000

### Test Case 3: Overtime + Bonus (New Regime, Tamil Nadu)
- **Purpose:** Overtime pro-ration + bonus verification
- **Employee:** EMP003
- **Period:** August 2026
- **Basic:** ₹35,000 | Days: 26/26 | OT Pay: ₹2,000 | Bonus: ₹10,000 | State: TN
- **Expected:** Gross ₹61,000 | OT included | Bonus full amount

### Test Case 4: Decimal Precision (Fractional Salary)
- **Purpose:** Verify RoundTo2(AwayFromZero) precision
- **Employee:** EMP004
- **Period:** August 2026
- **Basic:** ₹33,333.33 | Days: 20/26 (76.92%)
- **Expected:** Basic ₹25,641.02 | All decimals ± 0.01 only

### Test Case 5: ESI Conditional (Gross ≤ ₹21,000)
- **Purpose:** Verify ESI only applies when gross ≤ ₹21,000
- **Employee:** EMP005
- **Period:** August 2026
- **Basic:** ₹15,000 | Days: 26/26 | State: MH (metro) | Regime: New
- **Expected:** Gross ≈ ₹23,250 | ESI ₹0 (gross exceeds cap)

### Test Case 6: PF Ceiling (₹15,000 Cap)
- **Purpose:** Verify PF employee contribution capped at ₹15,000
- **Employee:** EMP006
- **Period:** August 2026
- **Basic:** ₹150,000 | Days: 26/26 | State: KA | Regime: Old
- **Expected:** PF Employee ₹1,800 (capped) | PF Employer ₹1,800 (capped)

### Test Case 7: Professional Tax (11-State Verification)
- **Purpose:** Verify PT correctly applies by state; multiple state slab coverage
- **Employee:** EMP007
- **Period:** August 2026 (run 11 payslips, one per state)
- **Basic:** ₹40,000 (adjustable) | Gross ≈ ₹60,000
- **Expected:** PT varies by state (Maharashtra ₹200, Tamil Nadu ₹208, etc.)

### Test Case 8: TDS Old Regime + Section 80C (₹1,50,000 Cap)
- **Purpose:** Verify old regime TDS with 80C deduction cap + 87A rebate
- **Employee:** EMP008
- **Period:** August 2026
- **Basic:** ₹30,000 | Days: 26/26 | State: Delhi | Regime: Old
- **80C Deduction:** ₹1,50,000 (LIC, PPF, etc.)
- **Expected:** Taxable income significantly reduced; 87A may apply

### Test Case 9: Arrears (NOT Pro-Rated)
- **Purpose:** Verify arrears added in full (not pro-rated like bonus)
- **Employee:** EMP009
- **Period:** August 2026
- **Basic:** ₹40,000 | Days: 26/26 | Arrears: ₹20,000 (from prior period)
- **Expected:** Arrears ₹20,000 added to gross in full (not reduced by attendance)

### Test Case 10: Cross-Tenant Isolation (Security)
- **Purpose:** Verify Company A admin cannot see Company B payslips
- **Setup:**
  - Company A: EMP010 (CompanyId=1)
  - Company B: EMP010 (CompanyId=2)
- **Test:**
  - Login as Company A admin (token companyId=1)
  - Request payslips for Company B (companyId=2)
  - Expected: HTTP 403 Forbidden OR 0 rows returned
  - Verify audit log shows authorization failure

---

## PHASE 6 TEST EXECUTION PLAN

### Week 1: Test Infrastructure Setup

- [ ] Create test database with 10 employees + salary structures
- [ ] Create test user accounts (Company A admin, Company B admin, Employees)
- [ ] Generate JWT tokens with correct company claims
- [ ] Set up Postman collection with all API endpoints
- [ ] Set up logging to capture API requests/responses
- [ ] Prepare independent calculation spreadsheet for expected values

### Week 1-2: Test Case Execution

- [ ] **Test Case 1:** Run, verify, document result
- [ ] **Test Case 2:** Run, verify, document result
- [ ] **Test Case 3:** Run, verify, document result
- [ ] **Test Case 4:** Run, verify, document result
- [ ] **Test Case 5:** Run, verify, document result
- [ ] **Test Case 6:** Run, verify, document result
- [ ] **Test Case 7:** Run, verify, document result
- [ ] **Test Case 8:** Run, verify, document result
- [ ] **Test Case 9:** Run, verify, document result
- [ ] **Test Case 10:** Run, verify, document result

### Week 2: Verification & Validation

- [ ] Compare system output to independent calculations (all 10 cases)
- [ ] Verify database persistence (check PAYSLIPS table for all 10 records)
- [ ] Verify audit trail (check AUDITLOGS table for all 10 PAYSLIP_GENERATE entries)
- [ ] Verify PDF generation (download all 10 PDFs, open and inspect)
- [ ] Verify tenant isolation (confirm cross-tenant access denied)
- [ ] Verify authorization (confirm unauthorized users get 403)

### Week 2-3: Edge Cases & Stress Testing

- [ ] Bulk payroll: 500 employees, verify no undercalculation
- [ ] Duplicate prevention: Generate payslip twice, verify rejection without overwrite flag
- [ ] Overwrite: Generate with overwrite=true, verify recalculation allowed + audit logged
- [ ] Transaction rollback: Inject error mid-generation, verify no partial saves
- [ ] Performance: Query 10,000 payslips paged, measure response time
- [ ] Concurrency: Generate 5 payslips simultaneously, verify no race conditions

### Week 3: End-to-End Workflow

- [ ] **Workflow 1:** Employee logs in → Views own payslip → Downloads PDF
- [ ] **Workflow 2:** HR Admin logs in → Generates payroll for 100 employees → Locks period
- [ ] **Workflow 3:** SuperAdmin logs in → Views all companies' payslips → Generates across tenants
- [ ] **Workflow 4:** Audit trail inspection: Verify complete history of all operations

---

## SUCCESS CRITERIA

Phase 6 is PASS if:

✅ All 10 test cases pass (system output = expected calculation)  
✅ Database persistence verified for all records  
✅ Audit trail logged for all operations  
✅ PDF generation works (all 10 PDFs valid)  
✅ Tenant isolation enforced (cross-tenant access denied)  
✅ Authorization working (unauthorized users get 403)  
✅ Bulk payroll scales to 500+ employees  
✅ Duplicate prevention works (second generation rejected/overwrite allows)  
✅ Transaction rollback works (no partial saves on failure)  
✅ Performance acceptable (paged queries <500ms for 10k records)  
✅ Zero data loss (compare DB counts to API response)  
✅ Zero rounding errors (all decimals ± 0.01)  
✅ Zero cross-tenant leaks (Company A never sees Company B data)  

---

## FAILURE CRITERIA

Phase 6 is FAIL if:

❌ Any test case output ≠ expected calculation  
❌ Duplicate payslips created without authorization  
❌ Cross-tenant data leakage detected  
❌ Authorization bypassed  
❌ Decimal rounding errors > 0.01  
❌ Audit trail incomplete or missing  
❌ Transaction rollback doesn't work (partial saves detected)  
❌ Performance unacceptable (queries > 1s)  

---

## DELIVERABLES (Phase 6 End)

1. **Phase 6 Test Report** (15+ pages)
   - 10 test cases with detailed results
   - Screenshots/logs for each test
   - Comparison of system output vs. expected calculation
   - Pass/Fail verdict for each test

2. **Database Verification Report**
   - SQL queries showing all 10 payslips persisted correctly
   - Audit log entries for all operations
   - Cross-tenant isolation verification

3. **Performance Report**
   - Bulk payroll execution time (500+ employees)
   - Paged query response time (10,000 records)
   - Concurrent operation stress test results

4. **Security Audit Report**
   - Authorization enforcement verification
   - IDOR prevention confirmed
   - Tenant isolation confirmed

5. **Final Sign-Off Document**
   - Phase 6 status (✅ PASS or ❌ FAIL)
   - Any issues found + resolution status
   - Recommendation: Approved for release or blockers identified

---

## PHASE 6 KICKOFF

**Ready to Begin:** 🟢 YES  
**Prerequisites Met:** ✅ YES  
**Phase 5 Approved:** ✅ YES  
**Zero Blockers:** ✅ YES  

**Next Command:** Begin Phase 6 test execution with Test Case 1

---

## QUESTIONS FOR PHASE 6 START

Before starting Phase 6 testing, confirm:

1. ✅ Test database with 10 employees ready? (YES / NO)
2. ✅ Salary structures configured for each employee? (YES / NO)
3. ✅ JWT tokens generated with correct company claims? (YES / NO)
4. ✅ Postman collection set up? (YES / NO)
5. ✅ Expected calculations independently verified? (YES / NO)
6. ✅ PDF viewer available? (YES / NO)
7. ✅ Database query tool available? (YES / NO)
8. ✅ Ready to start Test Case 1? (YES / NO)

---

**Document:** PHASE6_PROMPT.md  
**Status:** 🟢 READY  
**Authority:** Gordon (Docker AI / HRMS Audit)  
**Date:** 2026-08-12  

---

## PROJECT STATUS SUMMARY

| Phase | Status | Completion | Blockers | Next Action |
|---|---|---|---|---|
| Phase 1: Architecture | ✅ COMPLETE | 100% | 0 | N/A |
| Phase 2: Build & Tests | ✅ COMPLETE | 100% | 0 | N/A |
| Phase 3: Database | ✅ COMPLETE | 100% | 0 | N/A |
| Phase 4: API & Controllers | ✅ COMPLETE | 100% | 0 | N/A |
| Phase 5: Payroll Audit | ✅ COMPLETE | 100% | 0 | APPROVED ✅ |
| Phase 6: E2E Testing | 🟢 READY | 0% | 0 | **START NOW** |
| **TOTAL** | **85% COMPLETE** | | **ZERO BLOCKERS** | **Phase 6 Start** |

---

**When Ready: Reply "START PHASE 6" to begin Test Case 1 execution**

