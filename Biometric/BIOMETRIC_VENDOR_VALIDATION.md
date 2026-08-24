# BIOMETRIC VENDOR VALIDATION REPORT
**Project:** RatanHR HRMS  
**Version:** 2.0.0  
**Date:** 2026-08-01  
**Author:** Senior Production-Readiness Engineer  
**Validation Method:** Static code inspection of extracted source archive `ratanhr-fixed-ready-updated_1785582900635.zip`  
**Status:** ANALYSIS COMPLETE — Live hardware validation BLOCKED pending client device access

---

## ⚠️ Correction Notice

An earlier draft of this document incorrectly classified all 7 vendors as "PARTIAL / STUB — requires SDK". That assessment was based on controller comments and did not reflect the actual provider implementations in `HRMS.Infrastructure/Biometric/`. This document supersedes that draft with evidence-based findings from direct code inspection.

---

## Executive Summary

The RatanHR HRMS biometric subsystem is architecturally sound and contains real protocol implementations for 6 of 7 registered vendors. **No vendor has been validated against live hardware**, as no physical devices or approved simulators were available during this inspection. Live sync is correctly disabled by default (`Biometric:EnableLiveSync=false`).

**Vendor selection has NOT been made by the client.** A decision table is provided below. This document ends with `CLIENT DECISION REQUIRED`.

---

## 1. Architecture Overview

```
IBiometricCapabilityService         — Registry: IsImplemented per vendor
IBiometricProviderFactory           — Resolves provider by vendor name (case-insensitive)
IBiometricProvider                  — Per-vendor: FetchLogsAsync, SyncUsersAsync, GetDeviceStatusAsync
IBiometricSyncService               — Orchestrates sync: fetch → tenant-scope → upsert WebAttendance
BiometricHostedService              — Background timer: skips vendors where IsImplemented = false
IBiometricDeviceService             — Device CRUD, test, settings management
IBiometricCapabilityService         — Surfaces IsImplemented flags to UI + background service
```

**Key architectural controls verified:**

| Control | Evidence | Status |
|---|---|---|
| Unknown vendor → HTTP 501, not 500 | `BiometricProviderFactory.cs`: throws `NotSupportedException` → controller returns 501 | ✅ CORRECT |
| Stub vendors skipped by background service | `BiometricHostedService.cs` lines: filters `implementedVendors.Contains(...)` | ✅ CORRECT |
| Capability registry is immutable at runtime | `BiometricCapabilityService.cs`: `static readonly` lists | ✅ CORRECT |
| Tenant isolation in sync | `BiometricSyncService.cs`: loads `Employee.EmployeeCode WHERE CompanyId == companyId` before processing | ✅ CORRECT |
| Tenant isolation in repositories | `BiometricDeviceRepository.cs`, `BiometricLogRepository.cs`: all queries filter `CompanyId` | ✅ CORRECT |
| Day-level duplicate prevention | `BiometricSyncService.cs`: upserts by `EmployeeId + AttDate`; keeps earliest CheckIn, latest CheckOut | ✅ CORRECT |
| Circuit breaker on all implemented providers | All 6 providers: `MaxConsecutiveFailures=3`, `CircuitOpenDuration=60s` | ✅ CORRECT |
| Live sync disabled by default | `appsettings.json`: `"EnableRealtime": false`; staging `staging.env.template`: `Biometric__EnableLiveSync=false` | ✅ CORRECT |

---

## 2. Vendor-by-Vendor Assessment

### 2.1 ZKTeco

| Field | Finding |
|---|---|
| Source file | `HRMS.Infrastructure/Biometric/ZKTecoProvider.cs` |
| `IsImplemented` (BiometricCapabilityService) | `true` |
| Protocol | ZKLib binary TCP, default port 4370 |
| Communication method | Raw TCP socket; binary command/response protocol |

**Protocol implementation evidence:**
- Binary command constants defined: `CMD_CONNECT (1000)`, `CMD_EXIT (1001)`, `CMD_ACK_OK (2000)`, `CMD_ATTLOG_RRQ (13)`, `CMD_DATA (15)`, `CMD_PREPARE_DATA (16)`, `CMD_DATA_WRRQ (23)`, `CMD_FREE_DATA (32)`.
- Attendance record parsing: 40-byte fixed-width records (`ATT_RECORD_SIZE = 40`); bytes 8–13 are BCD-encoded `YY MM DD hh mm ss`; byte 15 = direction (0=CheckIn, 1=CheckOut).
- Circuit breaker: 3 consecutive TCP failures → open for 60 s → half-open on next call.
- Config: `ZKTECO_DEVICE_IP`, `ZKTECO_DEVICE_PORT` (default 4370), `ZKTECO_CONNECT_TIMEOUT_MS` (default 5000 ms).
- Tested models per capability registry: `ZKTeco F18 / K40 / UA760` (documented in `BiometricCapabilityService.cs`).

**Gaps requiring live device validation:**
- No TCP connect test confirming CMD_ACK_OK receipt.
- No end-to-end attendance pull against real hardware.
- No timezone test (device UTC offset vs stored UTC).
- No malformed-record recovery test.
- Requires `ZKTECO_DEVICE_IP` set in environment secret before first run.

**Classification: `STAGING ONLY`** — Code is implemented and has real protocol logic. Live device validation is BLOCKED pending client hardware access.

---

### 2.2 eSSL

| Field | Finding |
|---|---|
| Source file | `HRMS.Infrastructure/Biometric/EsslProvider.cs` |
| `IsImplemented` (BiometricCapabilityService) | `true` |
| Protocol | eSSL PUSH/cdata HTTP REST, default port 8080 |
| Communication method | HTTP GET to device's built-in web server |

**Implementation evidence:**
- Polls device at `GET /iclock/getrequest?options=att&Stamp=<from_ISO>`.
- Parses `C/Att\t<uid>\t<datetime YYYY-MM-DD HH:mm:ss>\t<status>` line format.
- User sync via cdata POST to `/iclock/cdata?SN=HRMS_SYNC`.
- Circuit breaker pattern identical to ZKTeco.
- Config: `ESSL_DEVICE_IP` and `ESSL_DEVICE_PORT` supplied through Replit Secrets/environment variables; the provider has no device-address fallback. Port is typically `8080`.

**Gaps requiring live device validation:**
- PUSH vs PULL direction: the code polls the device, but eSSL devices are typically configured in PUSH mode (device pushes punches to a receiver). The server-side PUSH receiver endpoint has not been verified in the controller.
- No confirmed test against real eSSL hardware.

**Classification: `STAGING ONLY`** — Code is implemented. Live device validation BLOCKED.

---

### 2.3 Matrix

| Field | Finding |
|---|---|
| Source file | `HRMS.Infrastructure/Biometric/MatrixProvider.cs` |
| `IsImplemented` (BiometricCapabilityService) | `true` |
| Protocol | Matrix COSEC REST API v2.x, default port 4050 |
| Communication method | HTTP REST with HTTP Basic auth |

**Implementation evidence:**
- HTTP Basic auth via `Authorization: Basic <base64>` header.
- Config: `MATRIX_DEVICE_IP`, `MATRIX_DEVICE_PORT` (default 4050), `MATRIX_USERNAME`, `MATRIX_PASSWORD`.
- Circuit breaker present.
- BiometricCapabilityService note: "API documentation: https://www.matrixcomsec.com/cosec-developer/".

**Gaps:** Requires client to confirm Matrix COSEC REST API version and device model. No live device test.

**Classification: `STAGING ONLY`** — Code is implemented. Live device validation BLOCKED.

---

### 2.4 Suprema

| Field | Finding |
|---|---|
| Source file | `HRMS.Infrastructure/Biometric/SupremaProvider.cs` |
| `IsImplemented` (BiometricCapabilityService) | `true` |
| Protocol | BioStar2 REST API v2 |
| Communication method | HTTP REST with session-token authentication |

**Implementation evidence:**
- Session authentication: `POST /api/login` → `User-Session` token; token cached with expiry, refreshed on expiry.
- Circuit breaker present.
- Config: `SUPREMA_BIOSTAR_URL`, `SUPREMA_USERNAME`, `SUPREMA_PASSWORD`.
- API reference documented: `https://bs2api.biostar2.com/`.

**Gaps:** Requires BioStar2 server license. No live device test.

**Classification: `STAGING ONLY`** — Code is implemented. Live device validation BLOCKED.

---

### 2.5 Hikvision

| Field | Finding |
|---|---|
| Source file | `HRMS.Infrastructure/Biometric/HikvisionProvider.cs` |
| `IsImplemented` (BiometricCapabilityService) | `true` |
| Protocol | Hikvision ISAPI HTTP, port 80/443 |
| Communication method | HTTP REST with Digest authentication |

**Implementation evidence:**
- Digest auth for ISAPI endpoints.
- Config: `HIKVISION_DEVICE_IP`, `HIKVISION_DEVICE_PORT` (default 80), `HIKVISION_USERNAME`, `HIKVISION_PASSWORD`.
- Circuit breaker present.

**Gaps:** Requires Hikvision ISAPI license/access. No live device test.

**Classification: `STAGING ONLY`** — Code is implemented. Live device validation BLOCKED.

---

### 2.6 Anviz

| Field | Finding |
|---|---|
| Source file | `HRMS.Infrastructure/Biometric/AnvizProvider.cs` |
| `IsImplemented` (BiometricCapabilityService) | `true` |
| Protocol | Anviz CrossChex HTTP API, default port 8080 |
| Communication method | HTTP REST with API key (token-based auth) |

**Implementation evidence:**
- Token-based: `Authorization: <API_KEY>` on every request.
- Config: `ANVIZ_DEVICE_IP` (default `192.168.1.204`), `ANVIZ_DEVICE_PORT` (default 8080), `ANVIZ_API_KEY`.
- Circuit breaker present.
- API reference: `https://www.anviz.com/developer/`.

**Gaps:** Requires client to provide Anviz device IP and API key. No live device test.

**Classification: `STAGING ONLY`** — Code is implemented. Live device validation BLOCKED.

---

### 2.7 Realtime

| Field | Finding |
|---|---|
| Source file | `HRMS.Infrastructure/Biometric/RealtimeProvider.cs` |
| `IsImplemented` (BiometricCapabilityService) | `false` |
| Protocol | None — stub only |
| Communication method | None |

**Implementation evidence:**
- `FetchLogsAsync` returns `Array.Empty<BiometricPunchLog>()` unconditionally.
- `SyncUsersAsync` returns `0` unconditionally.
- `GetDeviceStatusAsync` returns `IsOnline: false` with an explicit stub message.
- Feature-flagged: `Biometric:EnableRealtime=false` (default). Enabling the flag only adds a warning log; it does not invoke any SDK.
- Code comment explicitly: "Replace the method bodies below with real SDK/HTTP calls."
- BiometricHostedService skips this vendor because `IsImplemented = false`.

**Classification: `STUB / NOT IMPLEMENTED`** — No SDK integration. Will not produce attendance data. Must NOT be enabled for any sync operation.

---

## 3. Vendor Decision Table

> **eSSL SELECTED** — The client confirmed eSSL as the deployed vendor on 2026-08-01.

| # | Vendor | Code Status | Live-Test Status | Integration Complexity | Notes |
|---|---|---|---|---|---|
| 1 | **ZKTeco** | ✅ Implemented (binary TCP) | ⛔ BLOCKED — needs device | Low | Most complete implementation; BCD protocol parsing verified in code; tested models: F18/K40/UA760 |
| 2 | **eSSL** | ✅ Implemented (HTTP REST) | ⛔ BLOCKED — device IP/port/credentials not provided | Low-Medium | Validate PUSH vs PULL mode with the client's device config |
| 3 | **Matrix** | ✅ Implemented (COSEC REST) | ⛔ BLOCKED — needs device | Medium | Requires COSEC REST API v2.x |
| 4 | **Suprema** | ✅ Implemented (BioStar2) | ⛔ BLOCKED — needs device | Medium | Requires BioStar2 server license |
| 5 | **Hikvision** | ✅ Implemented (ISAPI HTTP) | ⛔ BLOCKED — needs device | Medium | Digest auth; widely deployed |
| 6 | **Anviz** | ✅ Implemented (CrossChex) | ⛔ BLOCKED — needs device | Low-Medium | Requires device API key |
| 7 | **Realtime** | ❌ STUB | ❌ Not applicable | High | No SDK integration; cannot be used |

---

## 4. Tests That Require Live Hardware (Applicable to All 6 Implemented Vendors)

Once the client selects a vendor and provides a staging device, the following tests must all PASS before live sync is approved:

| # | Test | Method | Expected Result |
|---|---|---|---|
| T1 | Device connectivity | Connect from API server to device IP:PORT | TCP handshake succeeds; CMD_ACK_OK (ZKTeco) or HTTP 200 |
| T2 | Authentication | Authenticate with device credentials | Session established; no auth error |
| T3 | Device registration | Register device in HRMS UI | Device appears with correct status |
| T4 | Employee mapping | Map employee EmployeeCode to device user ID | Sync correctly assigns punch to employee |
| T5 | Attendance pull | Pull punch logs for a test day | Records appear in `WebAttendance` table |
| T6 | Duplicate punch prevention | Submit same punch twice | Only one attendance record created |
| T7 | Check-In/Check-Out logic | Submit CheckIn then CheckOut | Single `WebAttendance` row with correct times |
| T8 | Earliest-punch-wins (CheckIn) | Submit two CheckIn punches | Earliest time retained |
| T9 | Latest-punch-wins (CheckOut) | Submit two CheckOut punches | Latest time retained |
| T10 | Timezone handling | Set device to known TZ offset; verify UTC storage | `PunchedAt` stored as UTC; local display correct |
| T11 | Tenant isolation | Two companies, one device each | No cross-tenant attendance records |
| T12 | Circuit breaker activation | Disconnect device; trigger 3 sync cycles | Circuit opens; warning logged; no crash |
| T13 | Circuit breaker recovery | Reconnect device | Circuit resets; sync resumes |
| T14 | Audit log completeness | Run sync; check audit trail | Sync events appear in BiometricSyncHistory |

---

## 4.1 eSSL Validation Result

**Selected vendor:** eSSL
**Protocol:** HTTP REST / PUSH cdata
**Required connection values:** `ESSL_DEVICE_IP`, `ESSL_DEVICE_PORT`, plus any device authentication values required by the deployed model, supplied only through Replit Secrets/environment variables.

The secure request for the eSSL staging device connection values was declined. No device-specific values were available in the environment, so no network, authentication, sync, or failure-mode test was executed.

| Test | Result | Evidence |
|---|---|---|
| T1 Device connectivity | **BLOCKED** | eSSL IP/port not available |
| T2 Authentication | **BLOCKED** | Device credentials/configuration not available |
| T3 Device registration | **BLOCKED** | Requires reachable staging device |
| T4 Employee mapping | **BLOCKED** | Requires device and test employee |
| T5 Attendance pull | **BLOCKED** | Requires reachable eSSL device |
| T6 Duplicate punch prevention | **BLOCKED** | Requires live/approved simulator data |
| T7 Check-In/Check-Out logic | **BLOCKED** | Requires live/approved simulator data |
| T8 Earliest-punch-wins | **BLOCKED** | Requires live/approved simulator data |
| T9 Latest-punch-wins | **BLOCKED** | Requires live/approved simulator data |
| T10 Timezone handling | **BLOCKED** | Device timezone cannot be inspected |
| T11 Tenant isolation | **BLOCKED** | Requires end-to-end sync data |
| T12 Circuit breaker activation | **BLOCKED** | Requires controlled device disconnect |
| T13 Circuit breaker recovery | **BLOCKED** | Requires controlled device reconnect |
| T14 Audit log completeness | **BLOCKED** | Requires completed sync run |

**Gate decision:** 0 PASS, 0 FAIL, 14 BLOCKED. Keep staging and production live sync disabled.

## 5. Background Service Validation (BiometricHostedService)

| Check | Evidence | Status |
|---|---|---|
| Skips Realtime (stub) at startup | `BiometricHostedService.cs`: logs "Stub providers (skipped):" at startup | ✅ Code verified |
| Per-company settings respected | Service iterates `BiometricSettings` where `AutoSyncEnabled = true` | ✅ Code verified |
| Escalating back-off on DB failure | `BaseRetryDelay = 1 min`, `MaxRetryDelay = 60 min`, `AlertAfterConsecutiveFailures = 10` | ✅ Code verified |
| Per-device error isolation | Each device sync in try/catch; one failure does not stop other devices | ✅ Code verified |
| Live-device polling tested | Requires running environment + device | ⛔ BLOCKED — CLIENT ACTION |

---

## 6. Data Privacy and Retention

| Area | Finding | Status |
|---|---|---|
| Raw biometric log retention | `BiometricSettings.RetentionDays` configurable per company | ✅ Configurable |
| Cleanup service | `BiometricLogCleanupService.cs` — scheduled cleanup of raw logs beyond retention window | ✅ Implemented |
| PII in biometric data | `BiometricLog` stores `UserId` (EmployeeCode), punch timestamp, direction only — no fingerprint templates | ✅ Minimal PII |
| Finger/face templates stored | Not stored in HRMS DB — remain on device only | ✅ Correct |
| DPDP/GDPR biometric consent | No explicit consent gate in code — **CLIENT ACTION REQUIRED** to confirm legal basis for biometric processing in their jurisdiction | ⚠️ CLIENT ACTION |

---

## 7. Summary

| Vendor | Classification |
|---|---|
| ZKTeco | **STAGING ONLY** — implemented, hardware validation blocked |
| eSSL | **STAGING ONLY** — implemented, hardware validation blocked |
| Matrix | **STAGING ONLY** — implemented, hardware validation blocked |
| Suprema | **STAGING ONLY** — implemented, hardware validation blocked |
| Hikvision | **STAGING ONLY** — implemented, hardware validation blocked |
| Anviz | **STAGING ONLY** — implemented, hardware validation blocked |
| Realtime | **STUB / NOT IMPLEMENTED** — no SDK, returns empty data |

**Overall gate: `eSSL SELECTED — HARDWARE VALIDATION BLOCKED`**
The client has identified eSSL. The client must still provide staging device access through secure environment values and confirm the legal basis for biometric data processing before eSSL can advance beyond STAGING ONLY.

**See `BIOMETRIC_RELEASE_DECISION.md` for the formal go/no-go recommendation.**
