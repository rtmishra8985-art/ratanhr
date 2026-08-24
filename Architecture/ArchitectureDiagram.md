# Architecture Diagram
**HRMS v2.0.0** | ASP.NET Core 8 Clean Architecture

## Deployment Architecture

```
                            ┌─────────────────────────────────────────┐
                            │          Internet (Users/Clients)        │
                            └──────────────────┬──────────────────────┘
                                               │ HTTPS :443
                            ┌──────────────────▼──────────────────────┐
                            │          Nginx 1.27 (TLS termination)    │
                            │  • Let's Encrypt cert (Certbot)          │
                            │  • Rate limiting (nginx level)           │
                            │  • Static file serving (/uploads/)       │
                            │  • HTTP → HTTPS redirect                 │
                            └──────────────────┬──────────────────────┘
                                               │ HTTP :8080
                            ┌──────────────────▼──────────────────────┐
                            │          HRMS API (Kestrel)              │
                            │  ASP.NET Core 8 — Clean Architecture     │
                            │                                          │
                            │  ┌──────────────────────────────────┐   │
                            │  │ Middleware Pipeline               │   │
                            │  │ CorrelationId → ExceptionHandler  │   │
                            │  │ → CSP Nonce → Security Headers   │   │
                            │  │ → Auth → RateLimit → Controllers │   │
                            │  └──────────────────────────────────┘   │
                            │                                          │
                            │  ┌───────────┐  ┌───────────────────┐   │
                            │  │Controllers│  │  Background Jobs  │   │
                            │  └─────┬─────┘  │ TokenCleanupSvc   │   │
                            │        │        └───────────────────┘   │
                            │  ┌─────▼──────────────────────────┐    │
                            │  │     Application Layer           │    │
                            │  │  Interfaces │ DTOs │ Validators │    │
                            │  └─────┬──────────────────────────┘    │
                            │        │                                 │
                            │  ┌─────▼──────────────────────────┐    │
                            │  │     Infrastructure Layer        │    │
                            │  │  Repositories │ Services │ JWT  │    │
                            │  │  Payroll Engine │ Email │ Redis │    │
                            │  └─────┬──────────────────────────┘    │
                            └────────┼─────────────────────────────────┘
                                     │
              ┌──────────────────────┼────────────────────┐
              │                      │                     │
   ┌──────────▼──────┐  ┌────────────▼──────┐  ┌─────────▼──────┐
   │  PostgreSQL 16  │  │   Redis 7.4        │  │  SMTP Server   │
   │  (primary data) │  │  (rate limiting,   │  │  (email)       │
   │                 │  │   optional cache)  │  │                │
   └─────────────────┘  └───────────────────┘  └────────────────┘

              ┌──────────────────────────────────┐
              │       Observability Stack         │
              │  OpenTelemetry → Jaeger/Zipkin    │
              │  Prometheus ← /metrics            │
              │  Serilog → Console/File/Seq       │
              └──────────────────────────────────┘
```

## Clean Architecture Layers

```
┌─────────────────────────────────────────────────────┐
│                    HRMS.API                          │
│  Controllers │ Middleware │ Extensions │ Program.cs  │
└──────────────────────────┬──────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────┐
│              HRMS.Infrastructure                     │
│  Services │ Repositories │ Data │ JWT │ Email        │
│  Payroll │ Redis │ Telemetry │ Migrations            │
└──────────────────────────┬──────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────┐
│              HRMS.Application                        │
│  Interfaces │ DTOs │ Validators │ Common             │
└──────────────────────────┬──────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────┐
│                 HRMS.Domain                          │
│  Entities │ Value Objects │ Domain Exceptions        │
│  (Zero external dependencies)                        │
└─────────────────────────────────────────────────────┘
```

## Data Flow: Payroll Generation

```
HTTP POST /api/v1/payroll/generate
    │
    ▼ CorrelationId assigned (X-Correlation-ID: abc-123)
    │
    ▼ JWT validated → CompanyId extracted
    │
    ▼ PayrollController.Generate()
    │
    ▼ IPayrollService.GenerateBulkPayrollAsync()
    │    ├── PayrollRepository.GetPayslipsAsync()       → PostgreSQL
    │    ├── EmployeeRepository.GetByCompanyAsync()     → PostgreSQL  
    │    ├── IndianPayrollCalculator.Calculate()        → in-memory
    │    ├── HrmsMetrics.RecordPayrollGeneration()      → Prometheus
    │    └── AuditService.LogAsync()                    → PostgreSQL
    │
    ▼ ApiResponse<BulkPayrollResultDto>
    │
    ▼ HTTP 200 OK  (X-Correlation-ID: abc-123 echoed back)
```
