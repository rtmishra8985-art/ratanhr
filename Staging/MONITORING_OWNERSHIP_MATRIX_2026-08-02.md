# RatanHR HRMS — Monitoring and Recovery Ownership Matrix

**Date:** 2026-08-02  
**Environment:** Release-readiness review  
**Production incidents created:** None  
**Approval state:** **PENDING — no owner or destination was supplied**

This matrix records the required operating model without inventing names,
contacts, alert destinations, escalation commitments, or approvals. `TBD`
values must be completed by the infrastructure and support owners before
production approval.

| Area | Signal / threshold | Primary owner role | Backup owner role | Alert destination | First response | Escalation | Recovery action | Status |
|---|---|---|---|---|---|---|---|---|
| Database | Availability; query latency and pool saturation thresholds defined in monitoring rules | TBD — DBA/SRE | TBD | TBD | TBD | TBD | Validate connectivity, failover/restore runbook, and schema state | PENDING |
| Backups and restore | Backup failure; freshness/retention breach | TBD — DBA/SRE | TBD | TBD | TBD | TBD | Stop release, investigate backup job, run disposable restore test | PENDING |
| Redis | Availability and memory pressure | TBD — SRE | TBD | TBD | TBD | TBD | Restart/reconnect, verify persistence policy, inspect dependent jobs | PENDING |
| Hangfire | Failed jobs, retry exhaustion, worker loss | TBD — application operations | TBD | TBD | TBD | TBD | Inspect authenticated dashboard, retry safely, verify idempotency | PENDING |
| API | Liveness/readiness failure; HTTP 5xx rate above configured threshold | TBD — backend operations | TBD | TBD | TBD | TBD | Check logs/metrics, restart or roll back using approved runbook | PENDING |
| Frontend | Availability and API dependency failures | TBD — frontend operations | TBD | TBD | TBD | TBD | Verify safe error/loading state and restore dependency | PENDING |
| Email delivery | Delivery failure, retry exhaustion, invalid-recipient failures | TBD — notifications/operations | TBD | TBD | TBD | TBD | Inspect staging sink/provider, retry according to policy, prevent duplicates | PENDING |
| Authentication/security | JWT/login failure spike, rate-limit events, security events | TBD — security owner | TBD | TBD | TBD | TBD | Review sanitized security telemetry and follow incident procedure | PENDING |
| Monitoring platform | Prometheus/Alertmanager availability and rule evaluation | TBD — observability owner | TBD | TBD | TBD | TBD | Restore monitoring service and verify alert delivery | PENDING |
| Disk/memory/container health | Disk >85% warning/>95% critical; memory/container restart thresholds | TBD — SRE | TBD | TBD | TBD | TBD | Free capacity, scale/restart safely, preserve evidence | PENDING |
| Incident response | Severity-based escalation and client communication | TBD — incident manager | TBD | TBD | TBD | TBD | Open approved incident record and execute communications plan | PENDING |
| Rollback | Failed release, migration incompatibility, or health regression | TBD — release/operations owner | TBD | TBD | TBD | TBD | Follow migration rollback and application rollback runbooks | PENDING |

## Configuration review

The supplied Prometheus and Alertmanager files define useful signal categories
for API availability, database availability, error rate, latency, resource
pressure, authentication failures, and connection-pool saturation. They also
contain production labels and placeholder/no-op notification receivers. File
presence and rule text are not evidence that production scraping, alert
delivery, ownership, or escalation is configured.

## Required evidence before approval

1. Named primary and backup owner roles for every row.
2. Approved alert destinations and escalation times.
3. Controlled staging alert tests for health, database, Redis, Hangfire,
   authentication, HTTP errors, backup, email, disk, memory, and restart
   conditions.
4. Evidence of alert receipt and recovery/resolve notifications without
   exposing message bodies, credentials, or personal data.
5. Current DNS, TLS, SMTP sender, secret ownership, backup, rollback, and
   support evidence from the infrastructure owner.

**Overall status: PENDING — monitoring ownership and alert delivery are not
ready for production sign-off.**

---

## Final authoritative readiness addendum — 2026-08-02

No named owner, backup owner, alert destination, escalation commitment,
controlled alert receipt, or infrastructure approval was available in the
current review. Monitoring and ownership remain **PENDING**. See
`Staging/FINAL_READINESS_ADDENDUM_2026-08-02.md`; overall decision:
**NOT READY FOR RELEASE**.

## Final-task execution addendum — 2026-08-02

No operational owner, backup owner, alert destination, response/escalation
commitment, or controlled staging alert evidence was supplied during the
final-task execution. All ownership and monitoring rows remain **PENDING**.