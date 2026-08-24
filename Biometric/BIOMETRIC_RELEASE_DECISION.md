# Biometric Realtime Release Decision

**Status:** DEFERRED — not included in current production release.
**Decision date:** 2026-08-05
**Review date:** To be determined by product team.
**Owner:** Engineering lead + Product

---

## Decision

Realtime biometric device monitoring (the `/api/biometric/realtime` endpoint and the
`biometric-realtime.html` UI page) is **deferred** from the current production release.

The feature is **not production-ready** because:

1. The `RealtimeProvider` in `HRMS.Infrastructure/Biometric/RealtimeProvider.cs` returns
   stub behavior — it does not connect to real hardware.
2. The Realtime SDK has not been procured, integrated, or tested.
3. No staging hardware is available for acceptance testing.

---

## Current State

| Component | State |
|---|---|
| `/api/biometric/realtime` endpoint | Returns HTTP 501 (feature flag disabled) |
| `biometric-realtime.html` UI page | Hidden from sidebar navigation |
| `RealtimeProvider.cs` | Stub — returns empty/fake data |
| `Features:BiometricRealtime` flag | `false` (default) |

The endpoint is **not** removed — it is gated behind `Features:BiometricRealtime`.
Attempted use is logged as a structured warning with CompanyId and UserId for audit.

---

## How to Enable (when ready)

Before enabling this feature, the following must be complete:

- [ ] Realtime SDK procured and licensed
- [ ] `RealtimeProvider.cs` fully implemented (no stub methods remaining)
- [ ] Integration tested against real Realtime hardware in staging
- [ ] Acceptance criteria signed off by QA
- [ ] Sidebar entry in `sidebar-admin.html` re-enabled
- [ ] This document updated with target release version

When ready:
1. Set `Features:BiometricRealtime=true` in `appsettings.Production.json`
   (or via environment variable `Features__BiometricRealtime=true`).
2. Re-enable the sidebar entry in `HRMS.API/wwwroot/includes/sidebar-admin.html`.
3. Update this file with the release version and sign-off.

---

## Acceptance Criteria (for future release)

- Real Realtime hardware punches appear in the realtime endpoint response.
- Device offline/online state changes are reflected within 60 seconds.
- The endpoint returns correct data for multiple devices in the same company.
- The endpoint is correctly scoped to the caller's company (no cross-tenant data).
- Load test: 50 concurrent polling clients at 30-second intervals show no errors.

---

## Code, UI, and Documentation Agreement

| Layer | Status |
|---|---|
| API endpoint (`/api/biometric/realtime`) | 501 stub, flag-gated |
| UI sidebar link | Hidden |
| `RealtimeProvider.cs` | Stub, clearly documented as not implemented |
| This document | Explicitly records deferral |
| `BiometricCapabilitiesController` | Reports Realtime as `IsImplemented: false` |
