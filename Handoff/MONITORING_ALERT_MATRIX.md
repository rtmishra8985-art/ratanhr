# MONITORING ALERT MATRIX
**Project:** RatanHR HRMS  
**Version:** 2.0.0  
**Date:** 2026-08-01  
**Status:** TEMPLATE — Configure in Grafana Alerting / PagerDuty before go-live

---

## Alert Severity Definitions

| Severity | Response Time | Notification Channel | Action |
|---|---|---|---|
| **CRITICAL** | Immediate (24/7) | PagerDuty + SMS + Phone | Page on-call engineer; escalate if no ack in 10 min |
| **HIGH** | 15 minutes (24/7) | PagerDuty + Email | Assign to on-call; fix or mitigate within 1 hour |
| **MEDIUM** | 1 hour (business hours) | Email + Slack | Assign next business day if outside hours |
| **LOW** | Next business day | Email | Log in ticket tracker; investigate during next sprint |
| **INFO** | No action required | Dashboard only | Trend monitoring |

---

## 1. Availability Alerts

| Alert Name | Condition | Severity | Channel | Notes |
|---|---|---|---|---|
| `hrms_api_down` | `/api/healthz` returns non-200 for >1 min | CRITICAL | PagerDuty + SMS | API completely unavailable |
| `hrms_frontend_down` | Frontend HTTP probe fails for >2 min | CRITICAL | PagerDuty + SMS | Users cannot access application |
| `hrms_db_unhealthy` | DB health check component = `Unhealthy` for >1 min | CRITICAL | PagerDuty + SMS | Database connectivity lost |
| `hrms_redis_unhealthy` | Redis health check = `Unhealthy` for >2 min | HIGH | PagerDuty + Email | Session/cache degraded |
| `hrms_api_slow_start` | API restart takes >90 s | MEDIUM | Email | Investigate deployment or resource issue |

---

## 2. Error Rate Alerts

| Alert Name | Condition | Severity | Channel | Notes |
|---|---|---|---|---|
| `hrms_5xx_critical` | HTTP 5xx rate > 5% of requests over 5-min window | CRITICAL | PagerDuty + SMS | Major system failure |
| `hrms_5xx_high` | HTTP 5xx rate > 1% over 10-min window | HIGH | PagerDuty + Email | Elevated errors |
| `hrms_5xx_medium` | HTTP 5xx rate > 0.1% over 30-min window | MEDIUM | Email | Monitor trend |
| `hrms_4xx_spike` | HTTP 4xx rate > 20% over 5-min window | MEDIUM | Email | Possible bot/scraper or broken client |
| `hrms_auth_failures_spike` | `/api/auth/login` 401 rate > 50/min | HIGH | PagerDuty + Email | Possible brute-force attack |
| `hrms_rate_limit_spike` | HTTP 429 rate > 100/min | HIGH | Email | Rate limit policy may need tuning |

---

## 3. Performance Alerts

| Alert Name | Condition | Severity | Channel | Notes |
|---|---|---|---|---|
| `hrms_p95_latency_high` | p95 response time > 2 s over 5-min window | HIGH | Email | SLA at risk |
| `hrms_p95_latency_critical` | p95 response time > 5 s over 5-min window | CRITICAL | PagerDuty | SLA breached |
| `hrms_slow_endpoint` | Any single endpoint p95 > 3 s | MEDIUM | Email | Identify and optimise |
| `hrms_db_query_slow` | Any query > 2 s | MEDIUM | Email | Review EF queries / indexes |
| `hrms_db_connections_high` | Active MySQL connections > 150 | HIGH | Email | Connection pool exhaustion risk |
| `hrms_hangfire_queue_depth` | Hangfire job queue depth > 500 | MEDIUM | Email | Background job backlog |
| `hrms_hangfire_job_failed` | Failed Hangfire jobs > 10 in 1 hour | HIGH | Email | Email or background job failures |

---

## 4. Infrastructure Alerts

| Alert Name | Condition | Severity | Channel | Notes |
|---|---|---|---|---|
| `hrms_disk_high` | Disk usage > 80% | HIGH | Email | Schedule cleanup or expansion |
| `hrms_disk_critical` | Disk usage > 95% | CRITICAL | PagerDuty + SMS | Imminent disk full — service will fail |
| `hrms_cpu_high` | CPU > 80% sustained for 10 min | HIGH | Email | Scale up or investigate runaway process |
| `hrms_cpu_critical` | CPU > 95% sustained for 5 min | CRITICAL | PagerDuty | Likely process crash or attack |
| `hrms_memory_high` | Memory > 80% | HIGH | Email | Review for memory leaks |
| `hrms_memory_critical` | Memory > 95% | CRITICAL | PagerDuty + SMS | OOM kill risk |
| `hrms_container_restart` | Any container restarts > 3 times in 30 min | HIGH | Email | Crash loop; check logs |

---

## 5. Security Alerts

| Alert Name | Condition | Severity | Channel | Notes |
|---|---|---|---|---|
| `hrms_brute_force` | >20 failed logins for same username in 5 min | CRITICAL | PagerDuty + SMS | Account under attack |
| `hrms_jwt_forgery_attempt` | JWT validation failures > 10/min | HIGH | PagerDuty + Email | Possible token forgery attempt |
| `hrms_idor_attempt` | Cross-tenant 403 responses > 5/min per IP | HIGH | PagerDuty + Email | IDOR probe detected |
| `hrms_unusual_export` | Bulk data export (>1000 records) outside business hours | MEDIUM | Email + Slack | Investigate for data exfiltration |
| `hrms_admin_action_flood` | Admin account performing >100 mutations/min | HIGH | Email | Compromised account possible |

---

## 6. Certificate & Domain Alerts

| Alert Name | Condition | Severity | Channel | Notes |
|---|---|---|---|---|
| `hrms_ssl_expiry_30d` | SSL cert expires in < 30 days | HIGH | Email | Trigger renewal |
| `hrms_ssl_expiry_7d` | SSL cert expires in < 7 days | CRITICAL | PagerDuty + SMS | Emergency renewal required |
| `hrms_ssl_expired` | SSL cert expired | CRITICAL | PagerDuty + SMS | Service broken for all HTTPS clients |
| `hrms_domain_expiry_30d` | Domain registration expires in < 30 days | HIGH | Email | 🔁 CLIENT ACTION: Renew domain |

---

## 7. Backup Alerts

| Alert Name | Condition | Severity | Channel | Notes |
|---|---|---|---|---|
| `hrms_backup_missing` | No successful backup in last 25 hours | HIGH | PagerDuty + Email | Daily backup job may have failed |
| `hrms_backup_failed` | Backup job exit code ≠ 0 | HIGH | PagerDuty + Email | Investigate backup script |
| `hrms_backup_size_anomaly` | Backup size < 50% of previous day | MEDIUM | Email | Possible data loss or truncation |

---

## 8. Business Metric Alerts

| Alert Name | Condition | Severity | Channel | Notes |
|---|---|---|---|---|
| `hrms_no_logins_8h` | Zero successful logins for 8 consecutive hours during business hours | MEDIUM | Email | May indicate auth system failure |
| `hrms_email_queue_stuck` | Email queue depth > 100 for > 1 hour | MEDIUM | Email | SMTP or Hangfire issue |

---

## Prometheus Alert Rules Reference

```yaml
# prometheus-alerts.yml — example rules
groups:
  - name: hrms-availability
    rules:
      - alert: HRMSApiDown
        expr: up{job="hrms-api"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "HRMS API is down"
          description: "The HRMS API health check has been failing for more than 1 minute."

      - alert: HRMSHighErrorRate
        expr: >
          rate(http_requests_total{job="hrms-api",status=~"5.."}[5m])
          / rate(http_requests_total{job="hrms-api"}[5m]) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High 5xx error rate on HRMS API"

      - alert: HRMSHighP95Latency
        expr: >
          histogram_quantile(0.95,
            rate(http_request_duration_seconds_bucket{job="hrms-api"}[5m])
          ) > 2
        for: 5m
        labels:
          severity: high
        annotations:
          summary: "HRMS API p95 latency > 2s"

      - alert: HRMSDiskHigh
        expr: (node_filesystem_size_bytes - node_filesystem_free_bytes)
              / node_filesystem_size_bytes > 0.80
        for: 5m
        labels:
          severity: high
        annotations:
          summary: "HRMS server disk usage > 80%"
```

---

## Alert Routing Configuration

> **CLIENT ACTION REQUIRED** — Configure routing in your alertmanager or PagerDuty.

```yaml
# alertmanager.yml example routing
route:
  receiver: hrms-ops-team
  group_by: [alertname, severity]
  routes:
    - match:
        severity: critical
      receiver: hrms-oncall-pagerduty
      repeat_interval: 5m
    - match:
        severity: high
      receiver: hrms-ops-email
      repeat_interval: 1h

receivers:
  - name: hrms-oncall-pagerduty
    pagerduty_configs:
      - service_key: <PAGERDUTY_SERVICE_KEY>   # Set via secret, not here
  - name: hrms-ops-email
    email_configs:
      - to: ops@yourdomain.com
  - name: hrms-ops-team
    email_configs:
      - to: ops@yourdomain.com
```

---

## Setup Checklist

- [ ] Prometheus scraping `/api/metrics` on 15s interval
- [ ] All alert rules loaded and active
- [ ] Grafana dashboards imported from `Documentation/grafana-dashboard.json`
- [ ] PagerDuty / OpsGenie integration tested with test alert
- [ ] Email alerts confirmed delivered to on-call inbox
- [ ] SSL certificate expiry alert tested
- [ ] Backup failure alert tested with intentional job failure
- [ ] Alert routing reviewed and approved by client IT manager
