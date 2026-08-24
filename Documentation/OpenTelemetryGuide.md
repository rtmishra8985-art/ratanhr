# OpenTelemetry Guide
**HRMS v2.0.0**

---

## What Is Instrumented

| Signal | Source | Details |
|--------|--------|---------|
| Traces | ASP.NET Core | Request duration, route, status, exception |
| Traces | Entity Framework Core | SQL statement, duration, table |
| Traces | Redis | Command, key, latency |
| Traces | HttpClient | Outbound HTTP calls |
| Metrics | ASP.NET Core | Request duration histogram, active requests |
| Metrics | .NET Runtime | GC, ThreadPool, memory, CPU |
| Metrics | Process | CPU time, working set |
| Metrics | Custom: HRMS.Payroll | Payroll generation duration, count, errors |
| Metrics | Custom: HRMS.Database | DB query latency by entity/operation |

---

## Configuration

Set in `.env` or environment variables:

```bash
# Service identity (appears in all telemetry)
OpenTelemetry__ServiceName=hrms-api
OpenTelemetry__ServiceVersion=2.0.0

# Jaeger (gRPC OTLP)
OpenTelemetry__JaegerEndpoint=http://jaeger:4317

# Zipkin
OpenTelemetry__ZipkinEndpoint=http://zipkin:9411/api/v2/spans

# OTLP (generic — Grafana, New Relic, Datadog, Honeycomb, etc.)
OpenTelemetry__OtlpEndpoint=http://otel-collector:4318
```

Leave endpoints blank to disable individual exporters.

---

## Prometheus Metrics Endpoint

Exposed at `GET /metrics` (Prometheus format):

```bash
# From inside the Docker network:
curl http://api:8080/metrics

# From the host (via nginx internal restriction):
curl http://localhost/metrics  # will be 403 unless you're on an allowed IP
```

Add to `prometheus.yml`:
```yaml
scrape_configs:
  - job_name: hrms
    static_configs:
      - targets: ['api:8080']
    metrics_path: /metrics
```

---

## Custom Spans (Tracing)

Use `HrmsMetrics.ActivitySource` to create custom spans in service code:

```csharp
using var activity = HrmsMetrics.ActivitySource.StartActivity("GeneratePayroll");
activity?.SetTag("company_id", companyId);
activity?.SetTag("employee_count", count);

// ... do work ...

activity?.SetStatus(ActivityStatusCode.Ok);
```

---

## Custom Metrics

Inject `HrmsMetrics` and record business metrics:

```csharp
public class PayrollService
{
    private readonly HrmsMetrics _metrics;

    public async Task GenerateAsync(int companyId, int month, int year)
    {
        var sw = Stopwatch.StartNew();
        var success = false;
        try
        {
            // ... payroll logic ...
            success = true;
        }
        finally
        {
            _metrics.RecordPayrollGeneration(sw.Elapsed.TotalMilliseconds, employeeCount, success);
        }
    }
}
```

---

## Viewing Traces in Jaeger

1. Navigate to http://localhost:16686
2. Select service: `hrms-api`
3. Search by:
   - Operation name (e.g., `POST /api/v1/payroll/generate`)
   - Tag: `CorrelationId=<id>` 
   - Duration: filter slow requests > 500ms

---

## Recommended Alert Rules (Prometheus)

```yaml
groups:
  - name: hrms
    rules:
      - alert: HighErrorRate
        expr: rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m]) > 0.05
        for: 2m
        labels:
          severity: critical

      - alert: SlowPayrollGeneration
        expr: histogram_quantile(0.99, hrms_payroll_generation_duration_ms_bucket) > 30000
        for: 1m
        labels:
          severity: warning

      - alert: DatabaseLatencyHigh
        expr: histogram_quantile(0.95, hrms_db_query_duration_ms_bucket) > 1000
        for: 5m
        labels:
          severity: warning
```
