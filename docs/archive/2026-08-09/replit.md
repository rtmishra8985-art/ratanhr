# HRMS Pro — Project Overview

## Architecture

**Full-stack Human Resource Management System** built on:

| Layer | Technology |
|-------|-----------|
| Backend API | ASP.NET Core 8 Web API |
| ORM | Entity Framework Core 8 + MySQL (Pomelo) |
| Auth | JWT Bearer + TOTP MFA (Otp.NET) |
| Cache | IMemoryCache (in-process) + Redis (distributed, optional) |
| Search | EF.Functions.Like with utf8mb4_unicode_ci collation |
| File upload | Local filesystem + magic-byte validation |
| Frontend | React 18 + Vite + TanStack Query v5 + wouter |
| UI | shadcn/ui + Radix UI + Tailwind CSS |
| Forms | React Hook Form + Zod |
| Testing | xUnit + Moq + EF InMemory (backend), Playwright + Vitest (frontend) |

## Project Structure

```
HRMS.Domain/          — Domain entities (no EF, no ASP.NET references)
HRMS.Application/     — DTOs, interfaces, validators, common utilities
HRMS.Infrastructure/  — EF Core context, service implementations, migrations
HRMS.API/             — ASP.NET Core controllers, middleware, DI wiring
HRMS.Tests/           — xUnit unit tests (35+ files)
HRMS.SPA.Source/      — React 18 + Vite SPA
  src/pages/          — Page-level components
  src/components/     — Shared layout + UI components
  src/hooks/          — Custom React hooks
  src/locales/        — i18n JSON (en, hi)
  e2e/                — Playwright end-to-end specs
scripts/              — staging validator, migrations, MySQL backup, secret generation
k8s/                  — Kubernetes manifests
```

## Modules

| Module | Backend | Frontend |
|--------|---------|----------|
| Auth (JWT + MFA) | ✅ | ✅ |
| Employees | ✅ | ✅ |
| Attendance | ✅ | ✅ |
| Leave | ✅ | ✅ |
| Payroll | ✅ | ✅ |
| Recruitment | ✅ | ✅ |
| Performance | ✅ | ✅ |
| Assets | ✅ | ✅ |
| Helpdesk | ✅ | ✅ |
| Training & LMS | ✅ | ✅ |
| Expense Claims | ✅ | ✅ |
| Travel Requests | ✅ | ✅ |
| Onboarding | ✅ | ✅ |
| Reports | ✅ | ✅ |
| Org Chart | — | ✅ |
| Webhooks | ✅ | — |

## Running Locally (Docker Compose)

```bash
# 1. Copy the isolated staging environment template
cp Staging/staging.env.template Staging/.env.staging
# Fill every placeholder with staging-only values. Never reuse production keys.

# 2. Validate the isolated staging configuration
bash scripts/validate-staging.sh --env-file Staging/.env.staging

# 3. After approved staging access is available, start and validate the
# isolated staging services. The runner cleans them up on exit by default.
bash scripts/validate-staging.sh --env-file Staging/.env.staging --start

# 4. Access the isolated staging services
#   API:      http://127.0.0.1:8081
#   Frontend: http://127.0.0.1:3001
#   MailHog:  http://127.0.0.1:8025
#   Health:   http://127.0.0.1:8081/healthz
```

## Running Tests

```bash
# Backend unit tests
dotnet test HRMS.Tests/HRMS.Tests.csproj --logger "console;verbosity=minimal"

# Frontend unit tests
cd HRMS.SPA.Source && pnpm test

# E2E tests (requires running app)
cd HRMS.SPA.Source && pnpm e2e
```

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `STAGING_DB_PASSWORD` | Yes for staging | Isolated staging MySQL password |
| `STAGING_REDIS_PASSWORD` | Yes for staging | Isolated staging Redis password |
| `JWT_PRIVATE_KEY_PEM` | Yes | Staging-only RSA private key used for signing |
| `JWT_PUBLIC_KEY_PEM` | Yes | Matching RSA public key used for validation |
| `ENCRYPTION_KEY_STAGING` | Yes | Base64-encoded staging encryption key |
| `SUPERADMIN_INITIAL_PASSWORD` | Yes for staging | Initial staging-only SuperAdmin password |
| `SMTP_HOST` | No | Defaults to the bundled staging MailHog sink |

Generate staging values from `Staging/staging.env.template`. RSA JWT keys can
be generated with `scripts/generate-rsa-keys.sh`; never commit the resulting
private key or environment file.

## User Preferences

- Prefer explicit, strongly-typed code over implicit conventions
- All new modules must follow the Domain → Application → Infrastructure → API layering
- Frontend pages use React Hook Form + Zod for all user inputs
- API responses use `ApiResponse<T>` wrapper with `.data`, `.message`, `.success`
- Do not use `any` types; use `unknown` + type guards instead
- English-language codebase with inline comments for non-obvious decisions
