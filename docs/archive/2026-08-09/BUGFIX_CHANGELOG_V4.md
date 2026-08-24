> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# RatanHR — Bug-Fix Changelog V4
**Date:** 2026-07-20
**Scope:** 5 additional bugs found by a comprehensive sweep of controllers, services, and infrastructure not covered in previous rounds.
**Files changed:** 6 modified.

---

## 🟠 High Fixes

### BF4-1 — RecruitmentController: Resume upload bypasses all file-safety checks
**File:** `HRMS.API/Controllers/Recruitment/RecruitmentController.cs`

Both `CreateCandidate` and `UpdateCandidate` handled resume uploads using raw `System.IO.File.Create` with `Path.GetExtension(resume.FileName)` taken directly from the client. This bypassed the `FileStorageService` entirely, meaning:
- **No extension allow-list check** — any file type could be uploaded (`.exe`, `.php`, `.sh`, …)
- **No magic-byte (file-signature) validation** — an attacker could rename a malicious file to `.pdf`
- **No file-size limit** — disk exhaustion was possible
- **No server-generated filename sanitisation** — the GUID filename was present, but the extension came from the client

Every other upload in the codebase (employee photos, documents) correctly used `IFileStorageService.SaveAsync`. The recruitment module was simply missed.

**Fix:** `IFileStorageService` injected into `RecruitmentController`; both `CreateCandidate` and `UpdateCandidate` now call `_fileStorage.SaveAsync(resume, "resumes")` which enforces the same extension allow-list, size limit, and magic-byte validation as all other uploads. A `FileUploadValidationException` returns `400 Bad Request` with the validation message.

---

### BF4-2 — TimesheetService: `ApproveAsync`/`RejectAsync` have no company ownership check (IDOR)
**Files:** `HRMS.Infrastructure/Services/TimesheetService.cs` + `HRMS.API/Controllers/Timesheet/TimesheetController.cs`

Both service methods used `FindAsync(id)` — EF Core's primary-key lookup — with no company filter:

```csharp
var entry = await _db.TimesheetEntries.FindAsync(id)   // no company scope
    ?? throw new KeyNotFoundException(…);
```

A Company-A admin knowing (or guessing) sequential integer IDs could approve or reject Company-B employees' timesheets. Compare: the employee-facing `DeleteAsync` correctly used `FirstOrDefaultAsync(t => t.Id == id && t.EmployeeId == employeeId)`.

**Fix:**
- Service signatures changed to `ApproveAsync(int id, int approverUserId, int companyId, …)` and `RejectAsync(int id, int approverUserId, int companyId, …)`.
- Lookup changed to `FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId)`.
- Controller `Approve` and `Reject` actions now pass `CallerCompanyIdOrNull ?? -1` as the company scope.

---

## 🟡 Medium Fixes

### BF4-3 — TimesheetController: Three endpoints fall back to company `0`
**File:** `HRMS.API/Controllers/Timesheet/TimesheetController.cs`

Three endpoints (`GetMine`, `GetPending`, `Create`) all contained `CallerCompanyIdOrNull ?? 0`, the same defect class as BF2-05 (`AnalyticsController`) and BF3-C (`PayrollController.GetLocks`). For superadmins `CallerCompanyIdOrNull` is `null`, so `?? 0` silently passes company `0` to the service — no real company has that ID, so superadmins always receive empty results. For users with a malformed claim the same incorrect zero is passed.

**Fix:** All three changed to `CallerCompanyIdOrNull ?? -1` — the established safe sentinel.

---

### BF4-4 — PerformanceController: Employee can update any other employee's goal progress
**Files:** `HRMS.Infrastructure/Services/PerformanceService.cs` + `HRMS.API/Controllers/Performance/PerformanceController.cs`

`PATCH /api/performance/goals/{id}/progress` allows the `employee` role. The service call was:

```csharp
await _svc.UpdateGoalProgressAsync(id, dto.AchievedValue, CallerCompanyId);
```

`UpdateGoalProgressAsync` filtered only by `companyId` — any employee could update any other employee's goal progress within the same company simply by guessing or iterating over goal IDs. The analogous `SubmitSelfReviewAsync` correctly required both `companyId` and `employeeId`.

**Fix:**
- `UpdateGoalProgressAsync` gains an optional `string? callerEmployeeId` parameter.
- When non-null, the service asserts `g.EmployeeId == callerEmployeeId` before mutating.
- The controller passes `ActorEmployeeId` for `employee`-role callers and `null` for admins (unrestricted admin updates remain supported).

---

### BF4-5 — FileStorageService.Delete: Path traversal allows arbitrary file deletion
**File:** `HRMS.Infrastructure/FileStorage/FileStorageService.cs`

```csharp
var fullPath = Path.Combine(_uploadsRoot, "..", relativePath.TrimStart('/'));
```

The intended pattern was `uploads/ + .. + /uploads/sub/file` → `uploads/sub/file`. But `Path.Combine` with a `..` segment does not canonicalise the path. A crafted `relativePath` of `../../../etc/cron.d/backup` resolves outside the uploads directory. Any code path that calls `Delete` with a caller-influenced value could delete arbitrary files the application process has write access to.

Demonstrated: with `_uploadsRoot = /app/wwwroot/uploads` and `relativePath = "../../etc/passwd"`, the resolved path is `/app/etc/passwd`.

**Fix:** `Delete` now:
1. Resolves both `_uploadsRoot` and the candidate path to their canonical forms with `Path.GetFullPath`.
2. Asserts the result starts with `uploadsRootFull + Path.DirectorySeparatorChar`.
3. Silently returns (no exception, no information leak) if the check fails.

---

## Summary

| ID | Severity | File(s) | Issue | Status |
|---|---|---|---|---|
| BF4-1 | 🟠 High | `RecruitmentController.cs` | Resume upload bypasses extension allowlist, magic-byte check, and size limit | ✅ Fixed |
| BF4-2 | 🟠 High | `TimesheetService.cs` + `TimesheetController.cs` | `ApproveAsync`/`RejectAsync` use `FindAsync(id)` with no company scope (IDOR) | ✅ Fixed |
| BF4-3 | 🟡 Medium | `TimesheetController.cs` | Three endpoints fall back to company `0` via `CallerCompanyIdOrNull ?? 0` | ✅ Fixed |
| BF4-4 | 🟡 Medium | `PerformanceService.cs` + `PerformanceController.cs` | Employee can update any company peer's goal progress (missing ownership check) | ✅ Fixed |
| BF4-5 | 🟡 Medium | `FileStorageService.cs` | `Delete` method resolves paths with `..` — arbitrary file deletion via path traversal | ✅ Fixed |
