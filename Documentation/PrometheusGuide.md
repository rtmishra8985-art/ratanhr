# Prometheus Guide
**HRMS v2.0.0**

---

## Metrics Endpoint

```
GET /metrics
```

Restricted to internal IPs in nginx:
```nginx
location /metrics {
    allow 10.0.0.0/8;
    allow 172.16.0.0/12;
    allow 192.168.0.0/16;
    deny all;
    proxy_pass http://api:8080/metrics;
}
```

---

## Prometheus Scrape Configuration

```yaml
# prometheus.yml
scrape_configs:
  - job_name: hrms-api
    static_configs:
      - targets: ['api:8080']
    metrics_path: /metrics
    scrape_interval: 15s
    scrape_timeout: 10s
    relabel_configs:
      - target_label: environment
        replacement: production
```

---

## Key Metrics Reference

### HTTP Metrics (ASP.NET Core Instrumentation)

```promql
# Request rate (per second, 5-min window)
rate(http_server_request_duration_seconds_count[5m])

# Error rate (5xx)
rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m])

# p95 response time
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m]))

# Active requests
http_server_active_requests
```

### HRMS Custom Metrics

```promql
# Payroll generation p95 time (ms)
histogram_quantile(0.95, hrms_payroll_generation_duration_ms_bucket)

# Payroll error rate
rate(hrms_payroll_error_count_total[1h])

# Database query p99 (by entity)
histogram_quantile(0.99, hrms_db_query_duration_ms_bucket{db_entity="Employee"})

# Report generation time (salary register)
histogram_quantile(0.9, hrms_report_generation_duration_ms_bucket{report_type="SalaryRegister"})

# Report row counts (avg rows per export)
rate(hrms_report_row_count_sum[1h]) / rate(hrms_report_row_count_count[1h])
```

### .NET Runtime Metrics

```promql
# GC pressure (gen 2 collections per minute)
rate(dotnet_gc_collections_total{generation="gen2"}[1m])

# Memory (working set MB)
process_working_set_bytes / 1024 / 1024

# Thread pool queue depth
dotnet_threadpool_queue_length
```

---

## Recommended Dashboards (Grafana)

### Import pre-built dashboards:

1. **ASP.NET Core** — Dashboard ID: 19924
2. **.NET Runtime** — Dashboard ID: 13978  
3. **PostgreSQL** — Dashboard ID: 9628

### Custom HRMS Dashboard Panels

```json
// Panel: Payroll Generation Duration
{
  "title": "Payroll Generation (p95, ms)",
  "type": "gauge",
  "targets": [{
    "expr": "histogram_quantile(0.95, hrms_payroll_generation_duration_ms_bucket)"
  }],
  "thresholds": [
    {"value": 5000,  "color": "green"},
    {"value": 15000, "color": "yellow"},
    {"value": 30000, "color": "red"}
  ]
}
```

---

## Alert Rules

```yaml
# alerts.yml
groups:
  - name: hrms-alerts
    rules:
      - alert: HRMSDown
        expr: up{job="hrms-api"} == 0
        for: 1m
        annotations:
          summary: "HRMS API is down"

      - alert: HighErrorRate
        expr: |
          rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m])
          / rate(http_server_request_duration_seconds_count[5m]) > 0.05
        for: 2m
        annotations:
          summary: "Error rate > 5%"

      - alert: SlowResponseTime
        expr: histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m])) > 2
        for: 5m
        annotations:
          summary: "p95 response time > 2s"

      - alert: HighMemory
        expr: process_working_set_bytes > 1073741824
        for: 5m
        annotations:
          summary: "API memory > 1GB"

      - alert: PayrollErrors
        expr: increase(hrms_payroll_error_count_total[1h]) > 0
        annotations:
          summary: "Payroll generation error detected"
```
