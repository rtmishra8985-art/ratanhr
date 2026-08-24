# HRMS – Human Resource Management System

A full-stack HRMS built with **ASP.NET Core 8**, **MySQL 8.4**, Redis, and a React/Vite frontend.

> **Current release baseline:** MySQL 8.4 is the active database provider, JWT uses
> RS256 RSA keys, Redis-backed Hangfire is the production job store, and all production
> secrets are supplied through environment variables or an external secret manager.
> Any older historical notes retained in the repository are superseded by
> `RELEASE_GATE_CURRENT.md`; follow the current MySQL, RSA, and secret-manager
> configuration documented below.

---

---

## Frontend — Which One to Use?

This repository ships **two frontends**. Only one should be served in production.

| Frontend | Directory | Technology | Status |
|---|---|---|---|
| **React SPA** (recommended) | `HRMS.SPA.Source/` | Vite + React 18 + TypeScript + Tailwind | ✅ Active / primary |
| Legacy HTML | `HRMS.API/wwwroot/` | Bootstrap 5 + Vanilla JS | ⚠️ Maintenance mode — kept for reference |

### Production: React SPA

Build the React SPA from source. For the isolated staging compose stack, the
prebuilt Nginx context is `HRMS.SPA/`; refresh it from the generated
`HRMS.SPA.Source/dist/public/` output before starting the stack:

```bash
cd HRMS.SPA.Source
bun install --frozen-lockfile
PORT=3001 BASE_PATH=/ NODE_ENV=production bun run build
rm -rf ../HRMS.SPA/assets
cp -R dist/public/* ../HRMS.SPA/
```

The backend Dockerfile DOES build the React SPA: stage `spa-builder` runs
`bun run build:ci` and the runtime stage copies `dist/public` into `wwwroot`.
The separate staging frontend image exists only for standalone SPA hosting.

### Local Development: React SPA dev server

```bash
cd HRMS.SPA.Source
bun run dev        # starts Vite dev server on http://localhost:5173
# API calls are proxied to http://localhost:5000 via vite.config.local.ts
```

The legacy Bootstrap HTML files in `wwwroot/` will continue to serve until the React
`dist/` output overwrites them. To prevent confusion, run the build step above before
starting the API in development.

### Staging validation

Use only staging-only values copied from `Staging/staging.env.template`:

```bash
cp Staging/staging.env.template Staging/.env.staging
chmod 600 Staging/.env.staging
bash scripts/validate-staging.sh
```

After approved staging secrets and accounts are available, run the isolated
runtime checks with `--start`. The runner removes staging containers, volumes,
and the network on exit unless `--keep` is explicitly provided:

```bash
bash scripts/validate-staging.sh --start
```

This validates configuration, isolation, API health, MailHog, frontend loading,
and cleanup. It does not fabricate or infer authenticated role, tenant-isolation,
workflow, email-trigger, or Hangfire evidence.


## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 8 Web API (Clean Architecture) |
| ORM | Entity Framework Core 8 + Pomelo (MySQL) |
| Database | MySQL 8.4 |
| Auth | JWT Bearer (access token + rotating refresh token), account lockout, forced first-login password change |
| Frontend | Pure HTML5 + Bootstrap 5 + Vanilla JS |
| Excel | ClosedXML |
| Logging | Serilog |
| Docs | Swagger / OpenAPI (dev only) |
| Tests | xUnit + EF Core InMemory |
| Email | MailKit / SMTP (falls back to console logging if unconfigured) |
| Rate limiting | Redis-backed and shared across instances when `Redis:ConnectionString` is set; in-memory single-instance fallback otherwise |
| Reverse proxy | Nginx (TLS termination, HTTP→HTTPS redirect, HSTS, gzip, static file serving) |
| Audit logging | Immutable `AuditLog` table, written on security-significant and data-changing actions |
| Containers | Docker multi-stage build + docker-compose (API + MySQL + Redis + Nginx) |

---

## Project Structure

```
HRMS/
├── HRMS.Domain/            # Entities (User, Employee, Company, Attendance, Payroll, Leave, AuditLog…)
├── HRMS.Application/       # Interfaces, DTOs, ApiResponse (no service implementations)
├── HRMS.Infrastructure/    # ApplicationDbContext, ALL 57 service implementations, JWT, FileStorage, AES encryption
│   ├── Migrations/         # EF Core migrations
│   ├── Payroll/            # IndianPayrollCalculator (PF, ESI, Professional Tax, TDS)
│   ├── Redis/              # Redis connection helpers (distributed cache / Hangfire storage)
│   └── BackgroundServices/ # TokenCleanupService (hosted service, runs every 24h)
├── HRMS.API/               # ASP.NET Core Web API entry point
│   ├── Controllers/        # Auth, Employee, Company, Attendance, Payroll, Leave, Reports, Audit, …
│   ├── Extensions/         # DI wiring (ServiceExtensions)
│   ├── Middleware/         # Global exception handler
│   └── wwwroot/            # Static frontend files (HTML + CSS + JS) + uploads/
├── HRMS.Tests/             # xUnit tests for auth, leave, JWT
├── nginx/                  # nginx.conf (reverse proxy / TLS termination) + ssl/ (cert mount point)
├── Dockerfile
├── docker-compose.yml      # API + MySQL + Redis + Nginx
├── Staging/                # isolated staging compose, template, and checklists
└── .gitignore              # excludes local environment and secret files
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- MySQL 8.4 (or Docker, see below)
- (Optional) Visual Studio 2022 / VS Code / JetBrains Rider

---

## Quick Start (local, without Docker)

### 1. Configure development settings

For local development, use `HRMS.API/appsettings.Development.json`,
environment variables, or `dotnet user-secrets`. For isolated staging, use
`Staging/staging.env.template` and the validation runner documented above.
Never commit an environment file or private key:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=hrms_db;User ID=hrms;Password=CHANGE_ME;AllowPublicKeyRetrieval=True;SslMode=Required"
  },
  "Jwt": { "PrivateKeyPem": "<RSA private PEM>", "PublicKeyPem": "<RSA public PEM>", "Issuer": "HRMS.API", "Audience": "HRMS.Client", "ExpiresInMinutes": 30 },
  "Security": { "EncryptionKey": "<base64-encoded 32-byte key>" },
  "Cors": { "AllowedOrigins": "" }
}
```

> **Never reuse the dev placeholders in production.** `appsettings.Production.json` ships with
> placeholders on purpose — the app throws on startup if the RSA key pair, database connection,
> encryption key, CORS origins, compliance settings, Redis, or host allowlist are not supplied
> through secure environment variables in a non-Development environment.
>
> Generate an RSA JWT key pair with `scripts/generate-rsa-keys.sh` and an
> encryption key with `openssl rand -base64 32`. Use separate values per
> environment.

### 2. Create the MySQL database

```sql
CREATE DATABASE hrms_db;
```

### 3. Apply migrations & run the API

```bash
cd HRMS.API
dotnet run
```

In Development, `Database:AutoMigrate` defaults to `true`. The app automatically:
1. Applies EF Core migrations on startup
2. Seeds the default **SuperAdmin** account and **5 default leave types** on first run

In any other environment, set `Database:AutoMigrate=false` and run migrations explicitly:

```bash
dotnet ef database update --project HRMS.Infrastructure --startup-project HRMS.API
```

### 4. Open the frontend

Navigate to **http://localhost:5000** — you will be redirected to the login page.

---

## Quick Start (Docker)

```bash
# For isolated staging, use only staging resources and values:
cp Staging/staging.env.template Staging/.env.staging
chmod 600 Staging/.env.staging
bash scripts/validate-staging.sh --env-file Staging/.env.staging --start

# The validator checks the isolated API, frontend, MailHog, health endpoints,
# and cleanup. It does not replace authenticated UAT or client approval.
```

For a production deployment, inject the required MySQL, Redis, RSA JWT,
encryption, CORS, host-allowlist, SMTP, and TLS settings through the deployment
secret manager. Do not use the staging compose file or staging credentials for
production.

### Production Docker deployment order

The root `docker-compose.yml` is the production stack. The following variables
are required before startup:

| Variable | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | MySQL connection string used by the API and migration job |
| `Jwt__PrivateKeyPem` / `Jwt__PublicKeyPem` | RSA key pair used for RS256 access tokens |
| `Security__EncryptionKey` | Base64-encoded 32-byte AES-256 key |
| `MYSQL_PASSWORD` / `MYSQL_ROOT_PASSWORD` | Credentials for the bundled MySQL service |
| `REDIS_PASSWORD` | Redis authentication |
| `GRAFANA_ADMIN_PASSWORD` | Grafana administrator password |
| `Email__Host` / `Email__Username` / `Email__Password` | SMTP delivery settings |
| `DOMAIN_NAME` / `AllowedHosts` / `DPO_EMAIL` | Public host and compliance settings |
| `BACKUP_ENCRYPTION_KEY` | Encryption key for scheduled database backups |

For a fresh deployment, `scripts/generate-secrets.sh` creates the local
database, Redis, RSA, AES, Grafana, and backup values. Replace its domain,
SMTP, CORS, and compliance placeholders before starting. The generated PEM
values use literal `\n` separators, which is the format expected by Docker
Compose and normalized by the API.

Apply the database changes in this order:

```bash
# Start MySQL and wait for its health check.
docker compose up -d mysql

# The backfill is safe on a new database and repairs existing employees with
# no company before EF Core migrations run.
docker compose run --rm backfill

# Apply the dedicated, one-shot migration image.
docker compose run --rm migrate

# Start the remaining services.
docker compose up -d
```

`docker compose up -d` also respects this order because `migrate` waits for
`backfill`, and the API waits for successful migrations. The optional off-site
backup profile still requires S3 credentials, but those credentials are not
needed for the normal stack.

---

## Default Credentials

| Portal | Email | Password |
|---|---|---|
| SuperAdmin | `superadmin@hrms.com` | *(see SeedAsync stdout on first run — no hardcoded default exists)* |

> **SuperAdmin password:** There is no hardcoded default password. On the first run, `SeedAsync` prints
> the one-time generated password to stdout. Copy it immediately — it is never stored or logged in
> plaintext again. Use it to log in and change the password via Profile → Change Password before
> doing anything else.
>
> Employee accounts are auto-created when you register an employee. A cryptographically random
> 12-character temporary password is generated and returned **once** in the API response of
> `POST /api/employees` (it is never stored or logged in plaintext). The employee is forced to
> set a new password via `change-password.html` on first login.

---

## Running Tests

```bash
dotnet test
```

To see verbose output with individual test names:

```bash
dotnet test --logger "console;verbosity=normal"
```

To collect code coverage (requires `coverlet.collector`, already in the test project):

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Test Coverage Summary

**Total: 65+ passing tests** across 7 test files:

| Test File | Tests | What it covers |
|---|---|---|
| `AuthServiceTests.cs` | 8 | Login success/failure, wrong portal, non-existent email, account lockout, refresh-token rotation, garbage refresh token |
| `JwtServiceTests.cs` | 2 | Token generation → validation round-trip, garbage token rejection |
| `JwtTokenClaimsTests.cs` | 8 | Email/role/employeeId/companyId claims, expiry window, cross-key rejection, tampered payload |
| `LeaveServiceTests.cs` | 4 | Within-quota apply, quota exceeded, approval updates balance, overlapping date rejection |
| `PayrollServiceTests.cs` | 9 | Payslip generation, net-pay calculation, duplicate month upsert, deletion, not-found deletion, auto-calculate PF/HRA slabs, get-by-id |
| `EmployeeAuthorizationTests.cs` | 18 | Cross-tenant IDOR: Documents, Exit, Promotions, Salary, Bonus controllers — admin scoped, superadmin unrestricted |
| `EncryptionServiceTests.cs` | 12 | AES-256-GCM round-trip (Aadhaar, PAN, account, Unicode), idempotency, null/empty, prefix tagging, short/empty key rejection, legacy plaintext tolerance |
| `PasswordHashingTests.cs` | 6 | Hash+verify, wrong password, salt uniqueness, plaintext not stored, empty password, special characters |
| `ApiResponseTests.cs` | 6 | Ok/Fail factories (generic + non-generic), data propagation, error list population |
| `StartupValidationTests.cs` | 6 | JWT key missing/too-short/valid, encryption key missing/wrong-length/valid |

---

## Security Implementation

### 1. JWT Security (Production-Grade)

| Requirement | Implementation |
|---|---|
| Key type | RSA key pair required at startup; private key signs and public key validates |
| Startup guard | `EnvironmentValidator.Validate()` runs before DI registration and rejects missing or malformed PEM keys |
| Algorithm | RS256 |
| Key storage | `Jwt__PrivateKeyPem` and `Jwt__PublicKeyPem` environment variables; never hardcoded |
| Token lifetime | Configurable via `Jwt:ExpiresInMinutes` (default 30 min); zero clock skew on validation |
| Refresh tokens | SHA-256 hashed before DB storage; rotated on each use; old token immediately revoked |

**Generate a JWT key pair:**
```bash
./scripts/generate-rsa-keys.sh
```
Set the resulting `JWT_PRIVATE_KEY_PEM` and `JWT_PUBLIC_KEY_PEM` values through
the deployment secret manager. Never commit the private key.

---

### 2. AES-256 Data Encryption

Sensitive PII is encrypted at the application layer before it reaches the database, using **AES-256-GCM** (authenticated encryption — provides both confidentiality and tamper detection).

| Field | Entity | Encrypted |
|---|---|---|
| Aadhaar Number | `Employee` | ✅ |
| PAN Number | `Employee` | ✅ |
| Bank Account Number | `Employee` | ✅ |
| UAN | `Employee` | ✅ |
| IFSC Code | `Employee` | ✅ |

**How it works:**
- `AesEncryptionService` (AES-256-GCM) encrypts values before EF Core writes them to MySQL.
- Ciphertext is prefixed `enc:v1:` to identify encrypted rows and support future key rotation.
- Decryption happens automatically on read — plaintext is only materialized in application memory.
- Legacy plaintext rows are tolerated on read (no crash during migration window).
- Values are never written to logs.

> ⚠️ **Security rule:** Never commit a real encryption key to source control. `appsettings.Development.json` ships with an empty `EncryptionKey` value on purpose — always supply the key through environment variables or User Secrets.

#### Generating a secure AES-256 key

The key must be a Base64-encoded value that decodes to **exactly 32 bytes**:

```bash
# Linux / macOS / Git Bash / WSL
openssl rand -base64 32

# PowerShell (Windows)
[Convert]::ToBase64String((1..32 | ForEach-Object { [byte](Get-Random -Maximum 256) }))
```

The output looks like: `K7mXn9vQ2rPwY5sL0jHdFgBiNuCqA3eT8oZhWmIkRVE=`

#### Development setup

Use **ASP.NET Core User Secrets** so the key never touches the repository:

```bash
cd HRMS.API
dotnet user-secrets init
dotnet user-secrets set "Security:EncryptionKey" "<output of openssl rand -base64 32>"
```

Verify it is stored:
```bash
dotnet user-secrets list
```

Alternatively, set it as a shell environment variable before `dotnet run`:

```bash
export ENCRYPTION_KEY="<output of openssl rand -base64 32>"
dotnet run
```

> **Important:** In Development, a missing `Security:EncryptionKey` produces a startup warning (not an error) so developers can run the app without PII encryption enabled during initial setup. PII columns will store plaintext until a key is configured.

#### Production deployment

Set the `Security__EncryptionKey` environment variable through your deployment platform:

**Docker / Docker Compose:**
```bash
# In a secret manager (never commit this value):
Security__EncryptionKey=<output of openssl rand -base64 32>
```

**Kubernetes:**
```bash
kubectl create secret generic hrms-secrets \
  --from-literal=Security__EncryptionKey="<key>" \
  --from-file=Jwt__PrivateKeyPem=staging-private.pem \
  --from-file=Jwt__PublicKeyPem=staging-public.pem
```
Reference it in your Deployment manifest as an `envFrom.secretRef`.

**Azure / AWS / GCP managed secrets:** Store the key in Azure Key Vault, AWS Secrets Manager, or GCP Secret Manager and inject it as an environment variable at container start. Never bake it into container images or config files.

In Production, a missing or incorrectly sized key **halts the application at startup** with a clear error message before serving any requests.

#### Encryption key rotation

To rotate to a new key without data loss:

1. Generate a new key: `openssl rand -base64 32`
2. Write a one-time migration script that:
   a. Reads every encrypted row using the **current key** (AES-256-GCM decrypt).
   b. Re-encrypts each value with the **new key** (AES-256-GCM encrypt — a new nonce is generated automatically).
   c. Writes the new ciphertext back to the database within a transaction.
3. Deploy the new key alongside the migration (update `ENCRYPTION_KEY` in your secrets store).
4. Run the migration script on the live database.
5. Revoke the old key from all secrets stores.

> **Do not** simply swap the environment variable without re-encrypting existing rows — the service will fail to decrypt old ciphertext with the new key.

**Startup guard:** `ServiceExtensions.AddEncryptionService()` validates the key at startup. In Production, a missing or incorrectly sized key halts the application immediately with a diagnostic error.

---

### 3. Environment Validation

`EnvironmentValidator.Validate()` (called at the very top of `Program.cs`, before any service registration) checks:

| Variable | Requirement |
|---|---|
| `ConnectionStrings:DefaultConnection` | Must be present |
| `Jwt:Key` | Must be present and ≥ 32 characters |
| `Jwt:Issuer` | Must be present |
| `Jwt:Audience` | Must be present |
| `Security:EncryptionKey` | Must be present and decode to exactly 32 bytes (Production only; warning in Development) |

If any check fails, the application exits with a bullet-point error message listing every missing/invalid value. It **never** starts in a partially configured state.

---

### 4. Additional Security Hardening

| Feature | Detail |
|---|---|
| Password hashing | BCrypt with random salt; work factor ≥ 10; plaintext never stored or logged |
| Account lockout | 5 failed login attempts → 15-minute lockout; counter reset on success |
| No email enumeration | Forgot-password endpoint always returns the same response regardless of whether the email exists |
| Refresh token storage | SHA-256 hash of the raw token stored; raw token only returned to the client once |
| One-time password reset | Reset tokens are single-use (marked `UsedAt` on first use); expire after 30 minutes |
| Forced first-login reset | New employee accounts set `MustChangePassword = true`; token carries the flag |
| Rate limiting | Login and password-reset endpoints rate-limited (Redis-backed for HA; in-memory fallback) |
| HTTPS enforcement | `RequireHttpsMetadata = true` in non-Development; Nginx terminates TLS and enforces HSTS |
| CORS | Configured via `ALLOWED_ORIGINS` env var; defaults to `[]` (deny-all) in Production |
| Audit trail | Immutable `AuditLog` table records login attempts, lockouts, CRUD operations with actor and IP |
| File upload | Extension allowlist and max-size enforcement via `FileUploadOptions`; files stored outside wwwroot |

---

## API Documentation

Swagger UI is available in Development only, at:

```
http://localhost:5000/swagger
```

Click **Authorize** → paste your JWT token (from the login response) as `Bearer <token>`.

---

## API Endpoints Summary

### Authentication
| Method | URL | Description |
|---|---|---|
| POST | `/api/auth/login` | Login (employee / admin / superadmin); rate-limited |
| POST | `/api/auth/refresh` | Exchange a refresh token for a new access + refresh token pair |
| POST | `/api/auth/logout` | Revoke a refresh token |
| POST | `/api/auth/forgot-password` | Request password reset (no email enumeration; rate-limited) |
| POST | `/api/auth/reset-password` | Reset password using a one-time token |
| POST | `/api/auth/change-password` | Change own password (authenticated) |

### Employees
| Method | URL | Description |
|---|---|---|
| POST | `/api/employees` | Register employee (multipart/form-data); returns temp password once |
| GET | `/api/employees` | List all employees |
| GET | `/api/employees/{id}` | Get employee detail (company-scoped for non-superadmins) |
| PUT | `/api/employees/{id}` | Update employee |
| PATCH | `/api/employees/{id}/status` | Activate / deactivate |
| DELETE | `/api/employees/{id}` | Delete (superadmin only) |
| GET | `/api/my/profile` | Employee self – view profile |
| PUT | `/api/my/profile` | Employee self – update profile |

### Companies
| Method | URL | Description |
|---|---|---|
| POST | `/api/companies` | Create company |
| GET | `/api/companies` | List all companies |
| GET | `/api/companies/{id}` | Company detail |
| PUT | `/api/companies/{id}` | Update company |
| POST | `/api/companies/{id}/logo` | Upload company logo |
| DELETE | `/api/companies/{id}` | Delete (superadmin only) |

### Attendance (Web)
| Method | URL | Description |
|---|---|---|
| POST | `/api/attendance/web/check-in` | Employee check-in |
| POST | `/api/attendance/web/check-out/{id}` | Employee check-out |
| GET | `/api/attendance/web` | Admin – view records |
| PATCH | `/api/attendance/web/{id}/status` | Admin – update status |
| GET | `/api/attendance/web/my` | Employee – own records |

### Attendance (Excel Upload)
| Method | URL | Description |
|---|---|---|
| POST | `/api/attendance/excel/upload` | Upload Excel file |
| GET | `/api/attendance/excel` | View uploaded records |

**Excel format**: Columns → `EmployeeId | Date (YYYY-MM-DD) | Status (Present/Absent/Half Day) | HoursWorked`

### Payroll
| Method | URL | Description |
|---|---|---|
| POST | `/api/payroll/generate` | Generate / update payslip |
| GET | `/api/payroll` | All payslips (filter: month, year, employeeId) |
| GET | `/api/payroll/{id}` | Single payslip |
| GET | `/api/payroll/my` | Employee – own payslips |
| DELETE | `/api/payroll/{id}` | Delete payslip |

### Leave Management
| Method | URL | Description |
|---|---|---|
| GET | `/api/leave/types` | List leave types |
| POST | `/api/leave/types` | Create leave type (admin/superadmin) |
| POST | `/api/leave/apply` | Employee – apply for leave |
| GET | `/api/leave/my` | Employee – own leave requests |
| GET | `/api/leave/my/balance` | Employee – own leave balance by type |
| POST | `/api/leave/my/{id}/cancel` | Employee – cancel a pending request |
| GET | `/api/leave` | Admin – list all requests (filter by status) |
| POST | `/api/leave/{id}/decision` | Admin – approve / reject a request |

### Reports & Dashboard
| Method | URL | Description |
|---|---|---|
| GET | `/api/reports/attendance` | Attendance report |
| GET | `/api/reports/employees` | Employee report |
| GET | `/api/payroll` | Payroll report (filter by month/year/employeeId) |
| GET | `/api/dashboard/admin` | Admin dashboard stats |
| GET | `/api/dashboard/superadmin` | SuperAdmin dashboard stats |
| GET | `/api/dashboard/employee` | Employee dashboard |

### Appreciation
| Method | URL | Description |
|---|---|---|
| POST | `/api/appreciation` | Upload appreciation (file + message) |
| GET | `/api/appreciation` | Admin – all appreciations |
| GET | `/api/appreciation/my` | Employee – own |

### Admin Users & Permissions
| Method | URL | Description |
|---|---|---|
| GET | `/api/admin-users` | List admin users |
| POST | `/api/admin-users` | Create admin user |
| PATCH | `/api/admin-users/{id}/status` | Activate / deactivate |
| DELETE | `/api/admin-users/{id}` | Delete |
| GET | `/api/permissions` | List all role permissions |
| POST | `/api/permissions` | Save role permissions |

### Super Admins
| Method | URL | Description |
|---|---|---|
| GET | `/api/superadmins` | List super admins |
| POST | `/api/superadmins` | Create super admin |
| PATCH | `/api/superadmins/{id}/status` | Toggle status |

### Audit
| Method | URL | Description |
|---|---|---|
| GET | `/api/audit` | Admin/superadmin – query audit events, optionally filtered by `userId` |

---

## File Upload Directories

Uploaded files are saved under `wwwroot/uploads/`, validated against `FileUpload` config
(allowed extensions + max size) and given server-generated filenames:

| Subfolder | Content |
|---|---|
| `identity/` | Aadhaar, PAN, identity documents |
| `edu/` | Educational certificates |
| `photo/` | Passport photos |
| `experience/` | Experience letters |
| `appreciation/` | Appreciation certificates |
| `logo/` | Company logos |

---

## User Roles

| Role | Portal | Access |
|---|---|---|
| `superadmin` | SuperAdmin | Full access to all companies and settings |
| `admin` | Admin | Company-scoped access; can have sub-roles (HR, Manager, Owner…) |
| `employee` | Employee | Self-service only |

---

## What Was Updated In This Pass

**Infrastructure & hardening added this pass:**
- **Real email delivery** — `EmailService.cs` (MailKit/SMTP) sends the password-reset link and
  the new-employee welcome email. If `Email:Host` is left blank the app falls back to logging
  the message instead of sending it, so local/dev setups still work without an SMTP server.
- **Distributed rate limiting** — `RedisRateLimiter.cs` shares login/forgot-password rate-limit
  counters across all API instances via Redis when `Redis:ConnectionString` is set. Falls back
  to the previous in-memory, per-instance limiter when Redis isn't configured.
- **Nginx reverse proxy** — new `nginx/` folder with TLS termination, HTTP→HTTPS redirect, HSTS,
  gzip, and direct static-file serving for `wwwroot/uploads`. The API container is no longer
  published directly to the host in `docker-compose.yml`; all traffic goes through Nginx.
- **Audit logging** — new `AuditLog` entity + `AuditService`, written on security-significant and
  data-changing actions (e.g. `LOGIN_SUCCESS`, `LOGIN_FAILURE`, `EMPLOYEE_CREATE`), capturing the
  real client IP (read from the `X-Real-IP` / `X-Forwarded-For` headers Nginx sets). Exposed to
  admins/superadmins via `GET /api/audit`.
- **Statutory payroll engine** — `IndianPayrollCalculator.cs`, wired into `PayrollService`,
  computes PF (employee + employer, EPFO-ceiling capped), ESI, Maharashtra Professional Tax
  slabs, and TDS under the new tax regime (FY25-26), with a human-readable calculation note
  per line item on the payslip. Professional Tax slabs are Maharashtra-specific — recompute for
  other states before using this in a different jurisdiction.
- **Background token cleanup** — `TokenCleanupService.cs`, a hosted service that purges expired
  refresh tokens older than 30 days once every 24 hours.

**Backend fixes:**
- `JwtService.cs` — JWT expiry now reads `Jwt:ExpiresInHours` from configuration instead of being hardcoded to 12 hours
- `AuthService.cs` — `ExpiresAt` in the login/refresh response likewise uses the configured value
- `EmployeeService.cs` — `GenerateEmployeeId()` now uses `RandomNumberGenerator` (cryptographically random, thread-safe) instead of `new Random()`
- `Program.cs` — Added startup DB seeding: SuperAdmin account + 5 default leave types seeded on first run; added `Content-Security-Policy` and `X-XSS-Protection` security headers

**Frontend fixes:**
- `js/api.js` — Added automatic silent access-token refresh on 401 (deduplicates concurrent refresh calls); added 429 rate-limit handling with friendly message; improved `apiFetch` to handle non-JSON error responses; added `logout()` helper that calls the API to revoke the refresh token before clearing session; added `populateTopnav()` helper; removed `requireAuth()` dependency on `hrms_user` JSON blob (now uses `hrms_role` directly)
- `leave-admin.html` — **Created** (was referenced in sidebar but missing): admin leave management page with leave type creation, request list with status filter, and approve/reject decision modal
- `reports-payroll.html` — **Created**: payroll report page with month/year/employee filters, summary totals, and print support
- `admin-dashboard.html` — Replaced all `{{template_vars}}` with real JavaScript API calls; sidebar loaded from include; logout calls API
- `emp-dashboard.html` — Same as above for employee portal; added My Leave quick link card
- `emp-payslip.html` — Replaced template vars with real API calls (`HrmsApi.myPayslips()`), added summary stat cards, sidebar loaded from include
- `leave.html` — Fixed logout (now calls API); sidebar loaded from include with active link highlight; added end-before-start date validation; improved cancel flow with error feedback
- `login.html` — Removed default credential hint from the visible UI; clears any stale session on page load; added `autocomplete` attributes
- `includes/sidebar-admin.html` — Added **Payroll Report** link under Reports section; corrected leave management link to `leave-admin.html`
- `includes/sidebar-employee.html` — Added **My Payslip** section; leave section now appears between Attendance and Documents

**Explicitly out of scope / deferred (flag before relying on in production):**
- Professional Tax is Maharashtra-slab only — other states' PT slabs are not implemented
- True multi-tenant database isolation (still company-scoped rows in a shared schema, not
  separate schemas/databases per tenant)
- Billing / subscription management
- Old tax regime TDS calculation (only the new regime, FY25-26, is implemented)

---

## Production Readiness Checklist

1. Set a strong `Jwt:Key` (≥ 32 random chars) via environment variables — `openssl rand -base64 48`
2. Set `Security:EncryptionKey` (32-byte base64) via environment variables — `openssl rand -base64 32`
3. Set `Cors:AllowedOrigins` to your actual frontend origin(s)
4. Provide a real TLS certificate + key at `nginx/ssl/cert.pem` and `nginx/ssl/key.pem`
   (Let's Encrypt/Certbot or your CA) — the API sits behind Nginx, which terminates TLS
5. Set `Database:AutoMigrate=false` and apply migrations as a separate deploy step
6. Set `Email:Host`/`Email:Username`/`Email:Password` (or the `EMAIL_*` env vars) to a real
   SMTP provider so password-reset and welcome emails actually send, instead of only logging
7. Set `Redis:ConnectionString` (or `REDIS_PASSWORD` for the bundled Redis container) if you
   plan to run more than one API instance, so rate limiting is consistent across replicas
8. Change the default SuperAdmin password immediately after first login
9. Periodically review `/api/audit` (or export `AuditLog` to your SIEM) and decide on a
   data-retention policy for that table — it is append-only and never auto-deleted

---

## License

Proprietary – [Your Company Name]
