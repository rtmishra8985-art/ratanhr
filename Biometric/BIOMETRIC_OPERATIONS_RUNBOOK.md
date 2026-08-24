# BIOMETRIC OPERATIONS RUNBOOK
**Project:** RatanHR HRMS  
**Version:** 2.0.0  
**Date:** 2026-08-01  
**Author:** Senior Production-Readiness Engineer  
**Audience:** System Administrators, HR Admins, On-Call Engineers

---

> **CURRENT STATUS:** Biometric live sync is DISABLED for this release per `BIOMETRIC_RELEASE_DECISION.md`.  
> This runbook documents operational procedures for when live sync is approved and enabled.
>
> **Vendor Selection:** `CLIENT DECISION REQUIRED` — client has not confirmed deployed hardware.  
> See `BIOMETRIC_VENDOR_VALIDATION.md` for vendor decision table.

---

## 1. Overview

The biometric subsystem pulls punch logs from hardware attendance terminals (fingerprint scanners, face recognition devices, access control panels) and creates or updates attendance records in the HRMS database.

**Key components:**

| Component | Description |
|---|---|
| `IBiometricProviderFactory` | Resolves the correct vendor protocol driver by name |
| `IBiometricSyncService` | Fetches logs from device, scopes to company, upserts `WebAttendance` |
| `IBiometricDeviceService` | Device CRUD, test-connection, settings management |
| `IBiometricCapabilityService` | Registry of `IsImplemented` flags; drives background service filtering |
| `BiometricHostedService` | Background timer (default 30 min); polls implemented vendors only |
| `BiometricLogCleanupService` | Background cleanup of raw biometric logs beyond `RetentionDays` |

**Implemented vendors (real protocol drivers):** ZKTeco, eSSL, Matrix, Suprema, Hikvision, Anviz  
**Stub vendors (do not poll):** Realtime

---

## 2. Enabling / Disabling Live Sync

### Enable Live Sync (after full validation only)

```bash
# Set via environment secret — NEVER hardcode in source or docker-compose files
Biometric__EnableLiveSync=true
Biometric__AllowedVendors=ZKTeco        # or comma-separated list of validated vendors
Biometric__SyncIntervalMinutes=30

# Restart the API after changing env vars
docker compose restart hrms_api

# Verify
curl https://api.hrms.yourdomain.com/api/biometric/settings \
  -H "Authorization: Bearer <ADMIN_TOKEN>" | jq .data.enableLiveSync
# Expected: true
```

**Pre-conditions before enabling:**
- [ ] All 14 hardware tests passed on a staging device (see `BIOMETRIC_VENDOR_VALIDATION.md` §4)
- [ ] Device credentials set in environment secrets (never in source)
- [ ] Engineering Lead sign-off obtained
- [ ] Client CTO sign-off obtained

### Disable Live Sync (emergency stop)

```bash
# Option 1: Environment variable + restart (persistent)
Biometric__EnableLiveSync=false
docker compose restart hrms_api

# Option 2: API call (immediate, resets on next restart unless env var also changed)
curl -X PUT https://api.hrms.yourdomain.com/api/biometric/settings \
  -H "Authorization: Bearer <SUPERADMIN_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"enableLiveSync": false}'

# Verify sync is disabled
curl https://api.hrms.yourdomain.com/api/biometric/settings \
  -H "Authorization: Bearer <ADMIN_TOKEN>" | jq .data.enableLiveSync
# Expected: false
```

---

## 3. Check Vendor Implementation Status

```bash
# List all registered vendors with IsImplemented status
curl https://api.hrms.yourdomain.com/api/biometric/capabilities \
  -H "Authorization: Bearer <ADMIN_TOKEN>" | jq .

# Check specific vendor
curl https://api.hrms.yourdomain.com/api/biometric/capabilities/ZKTeco \
  -H "Authorization: Bearer <ADMIN_TOKEN>" | jq .
```

**Expected response — implemented vendor:**
```json
{
  "vendorName": "ZKTeco",
  "isImplemented": true,
  "statusDescription": "Fully implemented via ZKLib binary TCP protocol (port 4370). Circuit breaker active. Tested against ZKTeco F18 / K40 / UA760.",
  "pendingIntegration": null
}
```

**Expected response — stub vendor (Realtime):**
```json
{
  "vendorName": "Realtime",
  "isImplemented": false,
  "statusDescription": "Stub — returns empty data. Not yet integrated.",
  "pendingIntegration": "Realtime Biometrics SDK or HTTP API. Contact: https://www.realtime.co.in/"
}
```

> **⚠️ Operator rule:** Do NOT enable live sync for any vendor where `isImplemented: false`. Stub providers return empty attendance data silently — no error, no indication, just zero records.

---

## 4. Device Health Check

```bash
# Check device health for a specific vendor
curl https://api.hrms.yourdomain.com/api/biometric/status/ZKTeco \
  -H "Authorization: Bearer <ADMIN_TOKEN>" | jq .

# Dashboard — all device statuses for this company
curl https://api.hrms.yourdomain.com/api/biometric/dashboard \
  -H "Authorization: Bearer <ADMIN_TOKEN>" | jq .

# Realtime status (for live monitoring — poll every 30 s)
curl https://api.hrms.yourdomain.com/api/biometric/realtime \
  -H "Authorization: Bearer <ADMIN_TOKEN>" | jq .
```

**HTTP 501 response** means the vendor is not registered or sync is disabled — not a server error:
```json
{
  "success": false,
  "message": "Biometric hardware integration is not yet available in this release."
}
```

**Circuit breaker open** log signature to watch for:
```
[WRN] [ZKTeco] Circuit breaker OPEN — skipping connect to <HOST>:<PORT>
```
This means 3 consecutive TCP failures occurred. The device is likely offline or unreachable. The circuit will half-open after 60 seconds and retry.

---

## 5. Triggering a Manual Sync

```bash
# Trigger manual sync for a date range
curl -X POST https://api.hrms.yourdomain.com/api/biometric/sync \
  -H "Authorization: Bearer <ADMIN_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "vendorName": "ZKTeco",
    "from": "2026-07-01T00:00:00Z",
    "to":   "2026-07-31T23:59:59Z"
  }'

# Check sync history
curl https://api.hrms.yourdomain.com/api/biometric/sync-history \
  -H "Authorization: Bearer <ADMIN_TOKEN>" | jq .
```

**Sync behaviour:**
- Check-In / Unknown punches: creates `WebAttendance` row if absent; retains **earliest** CheckIn if a row exists.
- Check-Out punches: updates **latest** CheckOut on existing row; silently skipped if no check-in row exists.
- Unknown employee codes (not in `Employees` table for this company) are silently skipped and logged at Debug level.

---

## 6. Reviewing Sync History

```bash
# Latest sync runs for this company
curl https://api.hrms.yourdomain.com/api/biometric/sync-history \
  -H "Authorization: Bearer <ADMIN_TOKEN>" | jq .

# Database query (admin — MySQL)
SELECT vendor_name, company_id, started_at, completed_at, records_synced, status, error_message
FROM BiometricSyncHistory
WHERE company_id = @CompanyId
ORDER BY started_at DESC
LIMIT 20;
```

**Healthy sync history entry:**
```json
{
  "vendorName": "ZKTeco",
  "startedAt": "2026-08-01T07:00:00Z",
  "completedAt": "2026-08-01T07:00:12Z",
  "recordsSynced": 47,
  "status": "Success",
  "errorMessage": null
}
```

**Alert condition:** `recordsSynced = 0` on a working day. Could mean:
1. Device offline (check health endpoint and circuit breaker log).
2. Employees not mapped (EmployeeCode ≠ device UserID).
3. Date range too narrow (check the sync trigger parameters).
4. Provider is a stub (check `isImplemented`).

---

## 7. Troubleshooting

### Device Showing as Offline

```bash
# 1. Confirm device IP is reachable from API server
ping <DEVICE_IP>
nc -zv <DEVICE_IP> 4370        # ZKTeco: port 4370
curl -I http://<DEVICE_IP>:8080  # eSSL/Anviz: port 8080
curl -I http://<DEVICE_IP>:4050  # Matrix: port 4050

# 2. Check circuit breaker log
docker logs hrms_api 2>&1 | grep "Circuit breaker"

# 3. Confirm env vars are set
docker exec hrms_api printenv | grep -E "ZKTECO_|ESSL_|MATRIX_|SUPREMA_|HIKVISION_|ANVIZ_"
# Should show IP and port for the selected vendor
# WARNING: do not log or commit these values
```

### Sync Produces 0 Records (Device Online)

```bash
# Check if employee codes are mapped to device user IDs
SELECT e.EmployeeCode, e.FirstName, e.LastName, e.BiometricId
FROM Employees e
WHERE e.CompanyId = @CompanyId AND e.IsActive = 1 AND e.BiometricId IS NULL;
# Non-zero rows = employees not enrolled on device

# Check raw biometric log table
SELECT * FROM BiometricLogs
WHERE company_id = @CompanyId
ORDER BY created_at DESC LIMIT 50;
```

### Duplicate Attendance Records

Duplicate detection operates at the **day level** (EmployeeId + AttDate). If you see two `WebAttendance` rows for the same employee and date, it indicates one of:
1. A direct-entry attendance record was created manually and a biometric sync record was also created.
2. The sync was triggered twice with overlapping date ranges in an earlier version.

```sql
-- Find duplicates
SELECT employee_id, att_date, COUNT(*) AS cnt
FROM WebAttendances
WHERE company_id = @CompanyId
GROUP BY employee_id, att_date
HAVING cnt > 1;
```

If duplicates are found: **disable sync immediately**, escalate to Engineering, and do not re-enable until root cause is identified.

### Cross-Tenant Data Leakage

```sql
-- Verify a sample sync scoped to the correct company
-- All WebAttendance rows linked to employees of company @CompanyId
SELECT wa.*, e.CompanyId
FROM WebAttendances wa
JOIN Employees e ON wa.EmployeeId = e.EmployeeCode
WHERE e.CompanyId != @ExpectedCompanyId
LIMIT 10;
-- Expected: 0 rows
```

If non-zero rows are returned: **this is a security incident.** Disable sync immediately and escalate to the Engineering Lead and CTO.

---

## 8. Device Credentials — Security Rules

1. **Never** hardcode device IP, port, username, password, or API key in source code or docker-compose files.
2. Always set via Replit Secrets or OS environment variables:
   ```bash
   ZKTECO_DEVICE_IP=<value>        # set in secret manager
   ZKTECO_DEVICE_PORT=4370
   ESSL_DEVICE_IP=<value>
   ANVIZ_API_KEY=<value>
   # ... etc.
   ```
3. Device credentials must be rotated after any personnel change on the operations team.
4. Device network access should be restricted by firewall: only the API server's IP should reach device ports.

---

## 9. Employee–Device Mapping

### Verify Employee Is Mapped

```bash
GET /api/employees/{id}
# Check biometricId field — must match the ID enrolled on the device
```

### Update Employee Biometric ID (Admin)

```bash
PUT /api/employees/{id}
Content-Type: application/json
{ "biometricId": "<DEVICE_ENROLLED_ID>" }
```

### Bulk-Check Unmapped Employees

```sql
SELECT e.Id, e.EmployeeCode, e.FirstName, e.LastName, e.BiometricId
FROM Employees e
WHERE e.BiometricId IS NULL
  AND e.IsActive = 1
  AND e.CompanyId = @CompanyId;
```

Unmapped employees will have their punches silently skipped during sync. Resolve before enabling live sync.

---

## 10. Vendor-Specific Connection Details

| Vendor | Default Port | Auth Method | Key Env Vars |
|---|---|---|---|
| ZKTeco | 4370 (TCP) | Binary handshake (CMD_CONNECT) | `ZKTECO_DEVICE_IP`, `ZKTECO_DEVICE_PORT` |
| eSSL | 8080 (HTTP) | None (device HTTP server, open by default) | `ESSL_DEVICE_IP`, `ESSL_DEVICE_PORT` |
| Matrix | 4050 (HTTP) | HTTP Basic auth | `MATRIX_DEVICE_IP`, `MATRIX_DEVICE_PORT`, `MATRIX_USERNAME`, `MATRIX_PASSWORD` |
| Suprema | 80/443 (HTTP) | BioStar2 session token | `SUPREMA_BIOSTAR_URL`, `SUPREMA_USERNAME`, `SUPREMA_PASSWORD` |
| Hikvision | 80/443 (HTTP) | HTTP Digest auth | `HIKVISION_DEVICE_IP`, `HIKVISION_DEVICE_PORT`, `HIKVISION_USERNAME`, `HIKVISION_PASSWORD` |
| Anviz | 8080 (HTTP) | API key header | `ANVIZ_DEVICE_IP`, `ANVIZ_DEVICE_PORT`, `ANVIZ_API_KEY` |
| Realtime | N/A | N/A — STUB | `Biometric:EnableRealtime=false` — **do not change** |

---

## 11. Security Checklist for Biometric Operations

- [ ] Only Admin / SuperAdmin roles can access biometric endpoints
- [ ] Device IP addresses and credentials stored in environment secrets only — not in version control
- [ ] `Biometric__EnableLiveSync=false` confirmed in staging env file
- [ ] `Biometric:EnableRealtime=false` confirmed in appsettings
- [ ] Circuit breaker thresholds reviewed for production load
- [ ] Biometric sync scoped to company (tenant isolation verified before first production run)
- [ ] Audit log (BiometricSyncHistory) reviewed after any configuration change
- [ ] No production sync enabled without explicit Engineering Lead + Client CTO sign-off

---

## 12. Escalation

| Scenario | Immediate Action | Escalation |
|---|---|---|
| Device offline > 2 hours | Notify HR Admin + IT Support | L2 |
| Sync producing 0 records unexpectedly | Check mapping + device health | L2 |
| Duplicate attendance records | **Disable sync immediately** | Engineering — L3 |
| Cross-tenant data leakage | **Disable sync immediately — security incident** | CTO — L4 |
| SDK / API key expired | Contact vendor | RatanHR Engineering — L5 |
| Payroll impacted by incorrect sync data | Disable sync; revert affected records | Engineering + HR — L3 |

**Engineering escalation:** RatanHR Support — support@ratanhr.com  
**Client IT escalation:** See `Handoff/CLIENT_OPERATIONS_CONTACTS.md`
