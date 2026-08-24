# Bugfix Changelog — Audit V8
**HRMS v2.0.0** | Date: 2026-07-31

---

## Summary

Single file changed as a result of the post-V7 static audit pass.

---

## FIX — AssetsController: explicit company-claim guard (AUDIT-V7 finding)

**File:** `HRMS.API/Controllers/AssetsController.cs`

**Severity:** Low (code hygiene — not directly exploitable)

### Problem

`AssetsController` used `BaseController.CompanyId` directly in all 11 action methods.
`BaseController.CompanyId` returns `-1` when the `companyId` JWT claim is absent or
unparseable (e.g. a SuperAdmin token issued without a tenant claim, or a malformed token
that passed RS256 signature validation but was missing the claim).

Because `IAssetService` methods accept `int companyId` (not `int?`), passing `-1` would
reach the service layer silently. The EF Core HasQueryFilter on `CompanyId` would then
return an empty result set (no company has `Id = -1`) rather than surfacing an error.
This produced confusing, misleading empty responses instead of an explicit failure.

### Fix

Added a private `TryGetCompanyId(out int companyId)` helper that wraps
`BaseController.CompanyId` and returns `false` when the value is `-1`.

Every action method now calls `TryGetCompanyId` as its first guard:

```csharp
if (!TryGetCompanyId(out var cid))
    return Forbid();
```

`IAssetService` signatures are intentionally left as `int` (not `int?`) because the
asset module has no SuperAdmin cross-tenant use case. A SuperAdmin must impersonate a
specific tenant before accessing assets.

### Behaviour change

| Scenario | Before fix | After fix |
|---|---|---|
| Normal authenticated user (companyId claim present) | Worked correctly | Unchanged |
| SuperAdmin with no companyId claim | Silently returned empty body (200 OK `{ items: [] }`) | `403 Forbidden` |
| Token with unparseable companyId claim | Silently returned empty body | `403 Forbidden` |

### No interface changes

`IAssetService` and `AssetService` are unchanged.

---

## No other changes

The audit found no IDOR vulnerabilities, no genuine N+1 query patterns, and no
missing `[Authorize]` attributes on any controller. All three pre-launch evidence
documents (PenetrationTestReport.md, LoadTestResults.md, DRDrillReport.md) were
assessed as credible. The sole open item from the pen-test report (tester credential
placeholders CRT-XXXX / OSCP-YYYY) is a documentation gap, not a code issue — the
original signed PDF from the testing firm should be retained as the source of record.
