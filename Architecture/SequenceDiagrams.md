# Sequence Diagrams
**HRMS v2.0.0**

---

## 1. Login Flow

```
Client          Nginx           API             PostgreSQL      Redis
  │               │               │                 │             │
  │─POST /login──►│               │                 │             │
  │               │─proxy─────────►│                 │             │
  │               │               │─rate limit?──────────────────►│
  │               │               │◄─(counter)────────────────────│
  │               │               │                 │             │
  │               │               │─find user────────────────────►│
  │               │              ◄│                 │             │
  │               │               │─verify BCrypt───►│(local CPU) │
  │               │               │                 │             │
  │               │               │─insert RefreshToken──────────►│
  │               │               │◄─────────────────────────────│
  │               │               │                 │             │
  │◄──200 {token}─│◄──────────────│                 │             │
  │ X-Correlation-ID: abc123       │                 │             │
```

---

## 2. Payroll Generation Flow

```
HR User       API              PayrollService    PostgreSQL    HrmsMetrics
  │             │                    │               │              │
  │─POST /gen──►│                    │               │              │
  │             │─AuthZ check────────►              │              │
  │             │─call GenerateAsync►                │              │
  │             │                    │─GetEmployees──►│             │
  │             │                    │◄──────────────│             │
  │             │                    │─GetPayslips───►│             │
  │             │                    │◄──────────────│             │
  │             │                    │─Check lock────►│             │
  │             │                    │◄──────────────│             │
  │             │                    │               │              │
  │             │                    │─Calculate()───►(CPU local)   │
  │             │                    │               │              │
  │             │                    │─Upsert payslips►│            │
  │             │                    │◄──────────────│             │
  │             │                    │─AuditLog──────►│             │
  │             │                    │◄──────────────│             │
  │             │                    │─RecordMetrics─────────────► │
  │             │◄───────────────────│               │              │
  │◄──200 result│                    │               │              │
```

---

## 3. JWT Refresh Flow

```
Client          API              JwtService      PostgreSQL
  │               │                 │                │
  │─POST /refresh►│                 │                │
  │               │─rate limit (5/min)               │
  │               │─find token hash─►                │
  │               │◄────────────────│                │
  │               │─verify expiry───►                │
  │               │─mark old as revoked──────────────►│
  │               │─create new token►                │
  │               │─store new hash──────────────────►│
  │               │◄────────────────────────────────│
  │◄──new tokens──│                 │                │
```

---

## 4. Streaming Excel Export Flow

```
HR User       API              StreamingReportSvc    PostgreSQL    OpenXmlWriter
  │             │                      │                 │              │
  │─GET /export►│                      │                 │              │
  │             │─call ExportAsync─────►               │              │
  │             │                      │─JOIN query──────►             │
  │             │                      │◄─streamed rows──│             │
  │             │                      │─WriteRow(r1)────────────────► │
  │             │                      │─WriteRow(r2)────────────────► │
  │             │                      │ ... (no buffering whole file)  │
  │             │                      │─FinishDocument─────────────► │
  │             │                      │◄──byte[]─────────────────────│
  │             │◄─────────────────────│                 │              │
  │◄──xlsx file─│                      │                 │              │
  │(no interim  │                      │                 │              │
  │ RAM spike)  │                      │                 │              │
```

---

## 5. Correlation ID Flow

```
Client          Nginx           CorrelationIdMW     Serilog       Controller
  │               │                    │               │              │
  │─GET /emp──────►│                   │               │              │
  │               │─proxy─────────────►│               │              │
  │               │                    │─gen UUID──────►              │
  │               │                    │─PushProperty─────────────── ►│
  │               │                    │ "CorrelationId=abc-123"       │
  │               │                    │─HttpContext.Items["CorrelId"] │
  │               │                    │─(all subsequent logs include  │
  │               │                    │  CorrelationId=abc-123)       │
  │               │                    │─call next─────────────────── ►│
  │               │                    │                 │             │─process
  │               │                    │                 │◄────────────│
  │               │                    │◄──────────────────────────────│
  │               │                    │─OnStarting: set response header
  │◄──200────────◄│◄──────────────────│               │              │
  │X-Correlation-ID: abc-123            │               │              │
```
