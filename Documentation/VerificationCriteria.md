# Verification Criteria — Definition of VERIFIED
**HRMS v2.0.0** | Addresses Specification Gap #9

---

## The Problem

The audit report uses three statuses — **VERIFIED**, **PARTIALLY VERIFIED**, **NOT VERIFIED** — but never defines what each means. This creates three specific ambiguities:

1. Is a fix **VERIFIED** if the code compiles? If tests pass? If manual testing confirms it? If a second reviewer signs off?
2. Can a fix be **VERIFIED** if the spec says "add X" and the code adds "Y which is equivalent to X"?
3. What is the minimum evidence required to change status from NOT VERIFIED to VERIFIED?

This document provides the authoritative definitions. All audit reports produced for this codebase must use these definitions — retroactively and going forward.

---

## Status Definitions

### ✅ VERIFIED

A fix is **VERIFIED** when **all three** of the following conditions are met:

| Condition | Definition |
|-----------|-----------|
| **C1 — Implementation Match** | The code change implements what the specification prescribed, OR implements an equivalent fix that satisfies the same root-cause requirement. Equivalence must be explicitly documented in the audit note. |
| **C2 — Static Confirmation** | The fix is confirmed present in the codebase via direct code inspection (file path + line number cited in the audit report). The auditor must have read the relevant lines — not inferred from a diff or changelog. |
| **C3 — No Regression** | The fix does not introduce a new defect visible from static analysis. If the fix introduces a known trade-off, it must be noted but does not block VERIFIED status. |

### ⚠️ PARTIALLY VERIFIED

A fix is **PARTIALLY VERIFIED** when:

| Case | Description |
|------|-------------|
| **PV-A** | The fix is present in code (C1 + C2 met) but introduces a minor spec deviation that does not affect the security or correctness goal. Example: spec says "log a warning" but code throws an exception — same safety outcome, different mechanism. |
| **PV-B** | The fix addresses the symptom but not the root cause described by the auditor. Example: controller-level IDOR check added, but spec required the check to be pushed into the DB query. The immediate risk is reduced but the architectural requirement is unmet. |
| **PV-C** | The fix is correct but incomplete — some sub-items are implemented, others are not. The audit table must list each sub-item with its individual status. |
| **PV-D** | The fix requires runtime validation (cannot be confirmed by static analysis alone) and has not been tested in a live environment. Example: distributed lock timeout behaviour, Redis connection failover. |

### ❌ NOT VERIFIED

A fix is **NOT VERIFIED** when any of the following is true:

| Case | Description |
|------|-------------|
| **NV-A** | The prescribed code change is absent from the codebase (auditor searched and did not find it). |
| **NV-B** | The code exists but implements a different change that does not satisfy the root-cause requirement. |
| **NV-C** | The fix is in a commented-out block, a TODO, or a stub with no implementation. |
| **NV-D** | The migration, script, or configuration file was prescribed but the file does not exist. |
| **NV-E** | The auditor was unable to access or read the relevant file (access error, binary file, generated file not present). Auditor must document the reason. |

---

## Equivalence Rule

A fix may be marked **VERIFIED** even when it differs from the exact specification wording, **if and only if**:

1. The alternative implementation satisfies the **same root-cause requirement** (not just the surface symptom).
2. The equivalence is **explicitly noted** in the audit finding (format: `"Note: spec prescribed X; implementation uses Y — equivalent because Z"`).
3. The alternative does not introduce new risk that the prescribed fix would have avoided.

**Examples:**

| Spec Said | Code Does | Equivalent? | Reason |
|-----------|-----------|-------------|--------|
| "Use CsvHelper for streaming export" | Uses OpenXML streaming with row-by-row flush | ✅ Yes | Both achieve constant-memory streaming; library choice is implementation detail |
| "Log a warning for temp password" | Throws and returns 500 | ❌ No | Throwing breaks the user flow; the spec required the flow to continue with a warning |
| "Add `IDistributedLockService` in Application layer" | Adds `IPayrollBulkLockService` in Infrastructure layer | ❌ No | Clean Architecture boundary violation; not equivalent to the prescribed design |
| "Push IDOR check into DB query" | Adds post-fetch check in controller | ❌ No | Post-fetch check does not meet the root-cause requirement (db-level scoping) even though it prevents data leakage |

---

## Evidence Requirements by Status

| Status | Required Evidence in Audit Report |
|--------|----------------------------------|
| ✅ VERIFIED | File path + line number(s) + quoted or paraphrased code snippet confirming the change |
| ⚠️ PARTIALLY VERIFIED | File path + line number(s) for what IS present; explicit list of what is MISSING; PV category (A/B/C/D) |
| ❌ NOT VERIFIED | Search terms used + confirmation that no match was found; or file path + line showing the incorrect/absent implementation; NV category (A–E) |

---

## Cannot-Verify Cases (Runtime-Only Fixes)

Some fixes cannot be verified by static code review alone. These must be explicitly marked with the `⚠️ RUNTIME-ONLY` flag alongside their PARTIALLY VERIFIED status:

| Fix Type | Why Static Review Cannot Verify | Required Verification Method |
|----------|--------------------------------|------------------------------|
| Redis distributed lock timeout | Lock TTL is correct in code, but actual behaviour depends on Redis version, network latency, clock skew | Integration test with a live Redis instance |
| ClamAV file scan integration | File passed to ClamAV correctly in code, but scan result handling needs live ClamAV daemon | End-to-end test with EICAR test file |
| Email delivery on password reset | SMTP config correct in code; actual delivery depends on provider | Functional test with a live SMTP relay |
| Rate-limit enforcement under load | Rate-limit config correct; Redis race condition under concurrent requests cannot be detected statically | k6 load test at declared limit |
| EF Core global query filter on null CompanyId | Filter expression is correct; behaviour when CompanyId is null (bypass risk) requires runtime test | Integration test with NULL CompanyId in JWT |

---

## Retroactive Application to the Enterprise Audit Report

The `HRMS_ENTERPRISE_AUDIT_REPORT.md` was produced without these definitions. The following corrections apply retroactively:

| Finding | Original Status | Corrected Status | Reason |
|---------|----------------|-----------------|--------|
| HIGH-3 Redis lock architecture (IPayrollBulkLockService in Infrastructure) | ✅ VERIFIED (with architecture note) | ⚠️ PARTIALLY VERIFIED (PV-A) | Architecture boundary violation is a spec deviation; equivalence rule does NOT apply because the spec's requirement was a design constraint, not just an outcome |
| HIGH-2 Leave IDOR (post-fetch check vs DB-query check) | ⚠️ PARTIALLY VERIFIED | ⚠️ PARTIALLY VERIFIED (PV-B) | Confirmed — post-fetch does not meet root-cause requirement; status unchanged but category now explicit |
| MED-12 Dockerfile --locked-mode (comment present, flag absent) | ❌ NOT VERIFIED | ❌ NOT VERIFIED (NV-C) | Flag is in a comment — qualifies as NV-C (stub/TODO with no implementation) |
| CRIT-1 Employee.CompanyId nullable (migration not found) | ⚠️ PARTIALLY VERIFIED | ❌ NOT VERIFIED (NV-A) | The core requirement (migration, NOT NULL constraint, FK) is completely absent; the passing sub-items (global query filter, ICompanyOwned) are insufficient for PARTIALLY VERIFIED when the primary deliverable is missing |

---

## Second-Reviewer Sign-Off

For **CRITICAL** and **HIGH** findings, VERIFIED status requires a second reviewer to independently confirm:

| Finding Severity | Sign-Off Requirement |
|-----------------|---------------------|
| CRITICAL | Second reviewer must independently locate and confirm the fix (separate session, no sharing of line numbers) |
| HIGH | Second reviewer must review the auditor's cited evidence and confirm it satisfies the definition above |
| MEDIUM / LOW | Single auditor sign-off is sufficient; second reviewer recommended but not required |

Second-reviewer sign-offs are recorded in the audit report as:
```
**Reviewer 2 Confirmation:** [Name] — [Date] — [CONFIRMED / DISPUTED]
[Dispute reason if applicable]
```

---

*Verification criteria approved: 2026-07-24. These definitions supersede any prior implicit criteria used in audit reports for this codebase.*
