# Biometric Integration — Formal Scope Deferral

**Status:** DEFERRED — full biometric synchronization excluded from current production release.  
**Decision date:** 2026-08-06  
**Review date:** To be determined by product team.  
**Owner:** Engineering lead + Product  
**Related document:** `BIOMETRIC_RELEASE_DECISION.md` (covers realtime-specific deferral)

---

## Scope of this document

`BIOMETRIC_RELEASE_DECISION.md` records the deferral of the **realtime** biometric
endpoint specifically (`/api/biometric/realtime`, `RealtimeProvider.cs`).

This document records the deferral of **the full biometric synchronization feature**,
covering all biometric provider integration work that requires vendor hardware or
third-party SDK procurement that has not yet occurred.

---

## Components and their current state

| Component | Location | State |
|---|---|---|
| Biometric provider abstraction | `HRMS.Application/Interfaces/IBiometricProvider.cs` | Implemented — interface only |
| ZKTeco provider | `HRMS.Infrastructure/Biometric/ZKTecoProvider.cs` | Attendance-log import and device status implemented; roster/user synchronization deferred |
| Realtime provider | `HRMS.Infrastructure/Biometric/RealtimeProvider.cs` | Stub — SDK not procured |
| `BiometricHostedService` | `HRMS.Infrastructure/BackgroundServices/BiometricHostedService.cs` | Implemented — loops but uses stub providers |
| `/api/biometric/realtime` endpoint | `HRMS.API/Controllers/Attendance/BiometricController.cs` | HTTP 501, flag-gated (`Features:BiometricRealtime=false`) |
| `/api/biometric/capabilities` | `BiometricCapabilitiesController.cs` | Returns `IsImplemented: false` for all realtime capabilities |
| `biometric-realtime.html` | `HRMS.API/wwwroot` | Hidden from sidebar navigation |
| Biometric sync UI | `HRMS.SPA.Source` | Not surfaced as operational |

---

## Why this is deferred

1. **No vendor hardware available in staging.** Real biometric devices (ZKTeco, BioTime,
   or equivalent) are client-supplied infrastructure. They are not available in the
   development or staging environment.

2. **Vendor SDK not procured.** The realtime attendance-data SDK requires a license
   agreement that is outside the developer's scope.

3. **No mock can substitute.** Per Rule 9 of the mandatory rules, test doubles must not
   "falsely prove production integration." Returning fake attendance records would
   violate this rule. The existing stub providers are documented as stubs; they do not
   claim to be production integrations.

4. **Acceptance criteria cannot be verified.** End-to-end biometric acceptance (real
   hardware punches, device offline/online state, multi-device scope, load testing)
   requires client-owned physical infrastructure.

---

## Current safe state

- All biometric endpoints that could return fake data are either:
  - Gated behind `Features:BiometricRealtime` (default: `false`), or
  - Return HTTP 501 with a clear documented response.
- `BiometricCapabilitiesController` explicitly reports `IsImplemented: false` for all
  realtime capabilities, so frontend and integrations know the feature is unavailable.
- The frontend does not present biometric synchronization as operational.
- `BiometricHostedService` runs but only processes devices whose provider is in the
  "supported and enabled" state — stubs are excluded from the active sync cycle.
- All unsupported operations return a clear `501 Not Implemented` response or throw
  `NotSupportedException`; they are not reported as successful zero-count syncs.

---

## Tests covering the current safe state

- `HRMS.Tests/BiometricServiceTests.cs` — verifies provider-selector behavior and that
  unsupported providers are excluded.
- `HRMS.Tests/BackgroundServiceTests.cs` — verifies `BiometricHostedService` cycle
  behavior and failure logging.
- The capabilities endpoint is covered by integration smoke tests.

---

## Acceptance criteria for a future release (NOT met for current release)

- [ ] Vendor hardware procured and available in staging.
- [ ] SDK licensed and integrated into `ZKTecoProvider.cs` or `RealtimeProvider.cs`
      with no stub methods remaining.
- [ ] Integration tested against real hardware: device punches appear in sync results.
- [ ] Device offline/online state changes reflected within 60 seconds.
- [ ] Multi-company isolation verified (no cross-tenant data in sync results).
- [ ] Load test: 50 concurrent polling clients at 30-second intervals with no errors.
- [ ] Acceptance criteria signed off by QA and client.
- [ ] This document updated with target release version, test evidence, and sign-off.

---

## How to enable (when ready)

1. Implement `ZkTecoProvider.cs` / `RealtimeProvider.cs` fully — remove all stub returns.
2. Set `Features:BiometricRealtime=true` in `appsettings.Production.json`
   (or environment variable `Features__BiometricRealtime=true`).
3. Re-enable the sidebar entry in `HRMS.API/wwwroot/includes/sidebar-admin.html`.
4. Update this document and `BIOMETRIC_RELEASE_DECISION.md` with release version and
   evidence of completed acceptance criteria.

---

## Release impact

**Current release is not affected.** The biometric feature is safely disabled. No
functionality that users depend on in the current release relies on biometric hardware.
This deferral does not block any other Phase 2 developer blocker.

**MANUAL VERIFICATION REQUIRED** when biometric hardware and SDK are procured:  
Run the full biometric acceptance suite against real hardware in staging before any
`Features:BiometricRealtime=true` configuration reaches production.
