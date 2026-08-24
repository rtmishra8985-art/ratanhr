> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# Test Coverage Report — HRMS v2.0.0
**Date**: July 19, 2026

---

## Summary

| Metric | Value |
|--------|-------|
| Total test files | 40+ |
| Total test methods | 200+ |
| Layers covered | Application, Infrastructure, Security, IDOR |
| CI enforcement | GitHub Actions `build-and-test` job |

---

## Test Categories

### Authentication Tests
- `AuthServiceTests.cs` — login, logout, token validation
- `JwtServiceTests.cs` — token generation, claims, expiry

### Security Tests
- `IDORTests.cs` — basic cross-tenant access prevention
- `IDORExtendedTests.cs` — extended IDOR across all modules
- `IDORNewControllersTests.cs` — IDOR in newer controller methods
- `ReportControllerIDORTests.cs` — cross-tenant report access
- `BonusDeductionSecurityTests.cs` — financial data access control
- `EncryptionServiceTests.cs` — AES-256 encrypt/decrypt round-trip

### Payroll Tests
- `PayrollServiceTests.cs` — calculation correctness (PF, ESI, TDS)
- `BulkPayrollTests.cs` — multi-employee generation
- `SalaryStructureTests.cs` — structure validation
- `PayslipGenerationTests.cs` — payslip output format

### Leave Tests
- `LeaveServiceTests.cs` — leave request workflow
- `LeaveBalanceTests.cs` — balance calculation
- `LeaveBalanceAdjustmentTests.cs` — manual adjustments
- `LeaveApprovalWorkflowTests.cs` — approval chain

### Attendance Tests
- `AttendanceServiceTests.cs` — check-in/out logic
- `AttendanceCalculationTests.cs` — hours worked, status
- `BackDatedAttendanceTests.cs` — back-dating validation

### Boundary Tests
- `BoundaryTests.cs` — null inputs, empty lists, zero values
- Edge cases: zero-salary employee, 0-day leave, salary on day of joining

---

## CI Pipeline Integration

Tests are enforced by GitHub Actions:

```yaml
- name: Run tests
  run: dotnet test HRMS.Tests/HRMS.Tests.csproj
       --logger "trx;LogFileName=test-results.trx"
       -- RunConfiguration.FailFast=true
```

- **FailFast=true**: stops on first failure — fast feedback
- **TRX results**: uploaded as artifact, published as PR check
- **Build with /warnaserror**: test warnings are also treated as errors

---

## Running Locally

```bash
# Full suite
dotnet test HRMS.Tests/HRMS.Tests.csproj

# With coverage
dotnet test HRMS.Tests/HRMS.Tests.csproj \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=opencover \
  /p:Threshold=80

# Specific test class
dotnet test --filter "ClassName=IDORTests"

# Specific test method
dotnet test --filter "MethodName=GetEmployee_WhenDifferentCompany_Returns403"
```

---

## Coverage Goals

| Module | Target | Status |
|--------|--------|--------|
| Auth | 90% | ✅ |
| Payroll calculations | 95% | ✅ |
| IDOR prevention | 100% | ✅ |
| Leave workflow | 85% | ✅ |
| Attendance | 80% | ✅ |
| Reports | 70% | ✅ |
