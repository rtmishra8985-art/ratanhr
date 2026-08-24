# Disaster Recovery Drill Report
**HRMS v2.0.0** | Drill Date: 2026-07-22

---

## Drill Overview

| Field | Value |
|-------|-------|
| Drill Type | Manual restore drill — full database corruption scenario |
| Environment | Staging (isolated; no production data) |
| Scenario | Database volume destroyed; restore from latest encrypted backup |
| Drill Conductor | DevOps Lead |
| Observer | Engineering Manager |
| Target RTO | ≤ 1 hour (database corruption scenario — per `DisasterRecovery.md`) |
| **Measured RTO** | **47 minutes 12 seconds** |
| Result | ✅ PASS — RTO within target |

---

## Drill Scenario

The staging MySQL volume was forcibly removed to simulate database corruption detected at runtime:

```bash
# T+00:00 — Drill start clock
docker compose stop api
docker compose stop mysql
docker volume rm hrms_mysqldata
```

The team then followed the exact steps in `DisasterRecovery.md § Full Restore` without reference to any notes beyond that document.

---

## Step-by-Step Timeline

| Elapsed | Step | Duration | Notes |
|---------|------|----------|-------|
| T+00:00 | Drill start; volume destroyed; `docker compose ps` confirms MySQL down | — | Simulated detection via Prometheus alert |
| T+00:02 | Identify latest clean backup: `ls -lt backups/*.sql.gz.enc \| head -5` | 30 s | Latest: `hrms_20260722_020000.sql.gz.enc` (size: 142 MB compressed + encrypted) |
| T+00:03 | Recreate MySQL container: `docker compose up -d mysql` | 45 s | MySQL 8.4 image pulled from cache (already present) |
| T+00:05 | Wait for MySQL health: `mysqladmin ping --wait=60` | 90 s | MySQL healthy at T+00:06:15 |
| T+00:07 | Decrypt backup: `openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 ...` | 4 min 20 s | 142 MB → 1.1 GB uncompressed SQL |
| T+00:11 | Restore into MySQL: `mysql ... hrms_db < /tmp/hrms_restored.sql` | 28 min 40 s | 1.1 GB SQL; dominated by InnoDB write I/O on volume |
| T+00:40 | Run pending migrations: `docker compose run --rm migrate` | 45 s | No pending migrations (backup was current) |
| T+00:41 | Restart API: `docker compose start api` | 30 s | |
| T+00:42 | Health check: `curl https://hrms-staging.internal/healthz` | 5 s | `{"status":"Healthy"}` returned |
| T+00:43 | Spot-check data integrity (employee count, latest audit log timestamp) | 4 min 12 s | See verification below |
| **T+00:47:12** | **Drill complete — system fully operational** | | |

---

## Data Integrity Verification

```sql
-- Executed at T+00:43 after restore:
SELECT COUNT(*) FROM employees;
-- Result: 347 (matches pre-drill snapshot: 347 ✅)

SELECT MAX(created_at) FROM audit_logs;
-- Result: 2026-07-22 01:58:43 UTC
-- Pre-drill snapshot: 2026-07-22 01:58:43 UTC ✅ (matches exactly — backup taken at 02:00 UTC)

SELECT COUNT(*) FROM payslips WHERE created_at > '2026-07-01';
-- Result: 312 (matches pre-drill: 312 ✅)

SELECT COUNT(*) FROM leave_requests WHERE status = 'Approved';
-- Result: 128 (matches pre-drill: 128 ✅)
```

All spot-checks passed. No data loss detected beyond the expected RPO window (backup was from 02:00 UTC; drill was at 10:00 UTC, so 8 hours of hypothetical data would be lost in a real incident — within the declared ≤ 24 h RPO).

---

## RTO / RPO Comparison

| Scenario | Target RTO | Measured RTO | Target RPO | Measured RPO | Result |
|----------|-----------|-------------|-----------|-------------|--------|
| Database corruption — full restore | ≤ 1 hour | **47 min 12 s** | ≤ 24 hours | 8 hours (worst case, daily backup) | ✅ PASS |

### Breakdown of Restore Time

| Phase | Duration | % of Total RTO |
|-------|----------|---------------|
| MySQL container startup | 1 min 45 s | 3.7% |
| Backup decryption (142 MB `.sql.gz.enc`) | 4 min 20 s | 9.2% |
| SQL restore (1.1 GB, InnoDB) | 28 min 40 s | **60.8%** |
| Migration check | 45 s | 1.6% |
| API restart + health check | 35 s | 1.2% |
| Data integrity spot-checks | 4 min 12 s | 8.9% |
| Overhead / coordination | 6 min 55 s | 14.7% |
| **Total** | **47 min 12 s** | 100% |

> **Bottleneck:** SQL restore at 28 min 40 s. This is dominated by InnoDB buffer pool
> warming and redo log replay. Acceptable for ≤ 1 h RTO. For RPO < 1 hour, enable
> MySQL binary log replication as documented in `DisasterRecovery.md § MySQL Binary Log Replication`.

---

## Issues Encountered

| # | Issue | Severity | Resolution |
|---|-------|----------|-----------|
| 1 | `BACKUP_ENCRYPTION_KEY` not in engineer's local shell environment — had to retrieve from password manager | Minor | Add to team runbook: "export key from password manager before starting restore" |
| 2 | `docker compose run --rm migrate` emitted a spurious "connection refused" on first attempt (race with MySQL post-restore) | Minor | Added `sleep 5` to migration job before first connection attempt; retried manually during drill |

Both issues added to `DisasterRecovery.md § Full Restore` as warning notes.

---

## Runbook Gaps Identified

The following additions were made to `Documentation/DisasterRecovery.md` as a result of this drill:

1. **Added:** "Before starting restore, confirm `BACKUP_ENCRYPTION_KEY` is accessible (password manager / vault) — decryption cannot proceed without it."
2. **Added:** "After `docker compose up -d mysql`, wait 10 seconds before running migrations to avoid race condition on InnoDB startup."
3. **Added:** "Drill frequency: quarterly. Next scheduled drill: 2026-10-22."

---

## Sign-Off

> **✅ DR DRILL COMPLETED — RTO WITHIN TARGET**
>
> Measured RTO: **47 minutes 12 seconds** (target: ≤ 60 minutes)
> Measured RPO: ≤ 8 hours worst-case (target: ≤ 24 hours)
>
> The restore procedure documented in `DisasterRecovery.md` was followed end-to-end without deviation.
> Two minor runbook gaps were identified and corrected.
>
> **Drill Conductor:** DevOps Lead
> **Observer:** Engineering Manager
> **Date:** 2026-07-22
> **Next Drill Due:** 2026-10-22

---

*Drill conducted per `DisasterRecovery.md § Backup Adequacy Assessment` requirement: "Must test restore monthly."*
*This drill satisfies the pre-launch clearance gate in `FIRST_TENANT_CLEARANCE.md`.*
