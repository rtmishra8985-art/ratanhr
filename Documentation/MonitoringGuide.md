# Monitoring Guide
**HRMS v2.0.0** | OpenTelemetry + Prometheus + Serilog

---

## Overview

HRMS ships three telemetry pillars:

| Pillar | Tool | Endpoint / Sink |
|--------|------|-----------------|
| Traces | OpenTelemetry → Jaeger / Zipkin / OTLP | Configured via env vars |
| Metrics | OpenTelemetry → Prometheus | `GET /metrics` |
| Logs | Serilog → Console + File + Seq | Files in `/app/Logs/`, optional Seq |

---

## Correlation IDs

Every request receives an `X-Correlation-ID` header:
- Generated as a new GUID if not provided by the caller
- Propagated through all Serilog log entries for that request
- Echoed back in the response

```
Request:  GET /api/v1/employees  X-Correlation-ID: (none)
Response: 200 OK                 X-Correlation-ID: 3fa85f64-5717-4562-b3fc-2c963f66afa6

All log entries for this request:
  [INF] CorrelationId=3fa85f64-5717-4562-b3fc-2c963f66afa6 Getting employees for company 5
```

To search all logs for a request: `grep "3fa85f64" Logs/hrms-*.log`

---

## Prometheus Metrics

Available at `GET /metrics` (restricted to internal IPs by nginx).

### Built-in Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `http_server_request_duration_seconds` | Histogram | Request duration by route + status |
| `http_server_active_requests` | Gauge | Concurrent active requests |
| `process_cpu_seconds_total` | Counter | CPU usage |
| `process_working_set_bytes` | Gauge | Memory (working set) |
| `dotnet_gc_collections_total` | Counter | GC collections by generation |
| `dotnet_threadpool_threads_count` | Gauge | Thread pool thread count |

### Custom HRMS Metrics

| Metric | Description |
|--------|-------------|
| `hrms_payroll_generation_duration_ms` | Time to run payroll for a period |
| `hrms_payroll_generation_count` | Payroll runs (labelled `success=true/false`) |
| `hrms_payroll_error_count` | Failed payroll runs |
| `hrms_db_query_duration_ms` | DB query latency by operation + entity |
| `hrms_redis_operation_duration_ms` | Redis latency by operation |
| `hrms_report_generation_duration_ms` | Report generation time by type |
| `hrms_report_row_count` | Rows per report generation |

---

## Setting Up Prometheus + Grafana (Docker)

Add to `docker-compose.yml`:

```yaml
prometheus:
  image: prom/prometheus:v2.53.0
  volumes:
    - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml:ro
  ports:
    - "9090:9090"

grafana:
  image: grafana/grafana:11.0.0
  ports:
    - "3000:3000"
  volumes:
    - hrms_grafana:/var/lib/grafana
```

`monitoring/prometheus.yml`:
```yaml
scrape_configs:
  - job_name: hrms-api
    static_configs:
      - targets: ['api:8080']
    metrics_path: /metrics
    scrape_interval: 15s
```

---

## Setting Up Jaeger (Distributed Tracing)

```yaml
jaeger:
  image: jaegertracing/all-in-one:1.58
  ports:
    - "16686:16686"  # Jaeger UI
    - "4317:4317"    # OTLP gRPC
```

Set in `.env`:
```
OTEL_JAEGER_ENDPOINT=http://jaeger:4317
```

Access Jaeger UI: http://localhost:16686

---

## Log Files

Logs are written to `/app/Logs/hrms-YYYYMMDD.log` with 30-day retention.

Log format:
```
2026-07-19 10:30:00.000 +00:00 [INF] 3fa85f64 Getting employees for company 5
```

Fields: Timestamp | Level | CorrelationId | Message

---

## Health Check

```bash
curl https://api.yourcompany.com/health
```

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "database", "status": "Healthy",  "description": null },
    { "name": "email",    "status": "Degraded", "description": "SMTP host not configured" }
  ]
}
```

Status codes:
- `200 Healthy` — all checks passing
- `200 Degraded` — non-critical check failing (e.g. email)  
- `503 Unhealthy` — critical check failing (e.g. database down)

---

## Alerting Recommendations

| Alert | Condition | Severity |
|-------|-----------|---------|
| High error rate | `http_server_request_duration_seconds{status=~"5.."}` > 5% | Critical |
| API down | `up{job="hrms-api"} == 0` | Critical |
| High DB latency | `hrms_db_query_duration_ms p99` > 500ms | Warning |
| Payroll failures | `hrms_payroll_error_count` > 0 | Critical |
| Memory pressure | `process_working_set_bytes` > 1GB | Warning |
