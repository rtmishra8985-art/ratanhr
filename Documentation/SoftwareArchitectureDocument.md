# Software Architecture Document
**Project**: HRMS SaaS — ASP.NET Core 8 Clean Architecture  
**Version**: 2.0.0  
**Date**: July 2026

---

## 1. Introduction

### 1.1 Purpose
This document describes the architecture of the Human Resource Management System (HRMS) — a multi-tenant SaaS platform built on ASP.NET Core 8 Clean Architecture principles.

### 1.2 Scope
Covers: Domain, Application, Infrastructure, and API layers; database design; external integrations; deployment topology; security model; observability stack.

---

## 2. Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│                        Internet                              │
└────────────────────────┬─────────────────────────────────────┘
                         │ HTTPS :443
                    ┌────▼────────┐
                    │    Nginx    │  TLS termination, rate-limit,
                    │  1.27.0     │  static file serving
                    └────┬────────┘
                         │ HTTP :8080
                    ┌────▼────────┐
                    │  HRMS API   │  ASP.NET Core 8 (Kestrel)
                    │  Container  │  Clean Architecture
                    └──┬──────────┘
              ┌────────┘ │ └──────────┐
     ┌────────▼──┐  ┌────▼────┐  ┌───▼─────┐
     │PostgreSQL │  │  Redis  │  │  SMTP   │
     │  16.4     │  │   7.4   │  │ (email) │
     └───────────┘  └─────────┘  └─────────┘
```

### 2.1 Layer Responsibilities

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Domain | `HRMS.Domain` | Entities, value objects, domain exceptions — no framework dependencies |
| Application | `HRMS.Application` | Interfaces, DTOs, validators, use-case contracts |
| Infrastructure | `HRMS.Infrastructure` | EF Core, repositories, services, JWT, email, Redis, payroll engine |
| Presentation | `HRMS.API` | Controllers, middleware, DI composition root |

---

## 3. Clean Architecture Dependency Rule

```
HRMS.API  →  HRMS.Application  →  HRMS.Domain
          ↗
HRMS.Infrastructure  →  HRMS.Application
```

- Domain has **zero** external dependencies.
- Application depends only on Domain.
- Infrastructure implements Application interfaces.
- API wires everything via DI; no business logic lives here.

---

## 4. Multi-Tenancy Model

- **Tenant = Company**: every `Company` record is an isolated tenant.
- All tenant-scoped entities carry a `CompanyId` foreign key.
- Tenant isolation is enforced in every service method and repository query.
- `SuperAdmin` role has cross-tenant access for platform management.
- IDOR prevention: all queries verify `CompanyId` matches the authenticated user's context.

---

## 5. Authentication & Authorisation

- JWT Bearer tokens (HS256, 12-hour expiry).
- Refresh tokens stored in PostgreSQL, hashed, single-use rotation.
- Role hierarchy: `superadmin → admin → hr → employee`.
- Fine-grained `Permission` table for module-level access control.
- All password operations rate-limited (5 req/min/IP) via Redis sliding window.

---

## 6. Key Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| ORM | Entity Framework Core 8 | First-class PostgreSQL support, migrations, projections |
| Caching | Redis 7.4 | Distributed rate-limiting, optional response caching |
| Excel export | ClosedXML (< 10k rows) + OpenXmlWriter streaming (≥ 10k) | Memory efficiency at scale |
| Logging | Serilog + Seq | Structured logs, enriched with CorrelationId |
| Telemetry | OpenTelemetry SDK | Vendor-neutral tracing & metrics |
| Container | Docker multi-stage build | Small runtime image, non-root user |
| Migrations | Dedicated `migrate` init-container | Prevents race conditions in HA deployments |

---

## 7. Security Architecture

- TLS 1.2 + 1.3 (Nginx terminates)
- HSTS with `includeSubDomains`
- CSP nonce-based (no `unsafe-inline`)
- PII fields (NationalId, BankAccount, Aadhaar) AES-256 encrypted at rest
- BCrypt password hashing (work factor 12)
- Input validation via FluentValidation
- Correlation IDs on every request for audit traceability

---

## 8. Observability Stack

- **Tracing**: OpenTelemetry → Jaeger/Zipkin/OTLP
- **Metrics**: OpenTelemetry → Prometheus `/metrics`
- **Logs**: Serilog → Console + File + Seq (optional)
- **Health**: `/health` JSON endpoint (DB + email checks)
- **Custom meters**: payroll generation time, DB query latency, Redis latency

---

## 9. Performance Characteristics

- All report queries use `AsNoTracking` (read-only, no change tracking)
- Large exports (payroll register, salary register) use `OpenXmlWriter` streaming — O(batch) RAM not O(rows)
- Composite indexes on high-traffic columns (see `20260719000001_AddPerformanceIndexes`)
- Single-query JOIN projections replace separate employee dictionary loads
- Background service cleans expired refresh tokens hourly
