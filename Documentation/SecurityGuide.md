# Security Guide
**HRMS v5.3** *(updated 2026-07-26 — MySQL 8.4 migration; corrected JWT algorithm from HS256 → RS256)*

---

## Authentication

### JWT Tokens
- **Algorithm: RS256** (RSA-2048 asymmetric signing — upgraded from HS256 in v5.0)
- **Access token expiry: 30 minutes** (was 8–12 hours before v5.0)
- Refresh tokens: 7-day expiry, single-use rotation, stored hashed in MySQL
- Token delivery: HttpOnly + Secure + SameSite=Strict cookies (XSS-safe)
- Token revocation: refresh token invalidation on logout; access tokens expire naturally

### Key Pair Setup
Generate the RSA-2048 key pair before first deployment:
```bash
chmod +x scripts/generate-rsa-keys.sh && ./scripts/generate-rsa-keys.sh
```
This writes `JWT_PRIVATE_KEY_PEM` and `JWT_PUBLIC_KEY_PEM` to your `.env`.

- `JWT_PRIVATE_KEY_PEM` — signs tokens; must never leave the API server
- `JWT_PUBLIC_KEY_PEM` — verifies tokens; safe to share with downstream services

### Password Policy
- Minimum 8 characters enforced by FluentValidation
- BCrypt hashing with work factor 12
- `MustChangePassword` flag forces reset on first login
- Password reset via time-limited email token (1-hour TTL)

---

## MFA (TOTP)

- TOTP via RFC 6238 (Google Authenticator / Authy compatible)
- Setup: `POST /api/mfa/setup` → scan QR → `POST /api/mfa/confirm`
- Login step: `POST /api/mfa/verify` after successful password auth
- Disable: `POST /api/mfa/disable` (requires current password)

---

## Rate Limiting

| Policy | Limit | Applies To |
|--------|-------|------------|
| `login` | 10 req/min/IP | Login, forgot-password |
| `sensitive` | 5 req/min/IP | Refresh token, change-password, password-reset |
| `api` | 120 req/min/IP | All other endpoints |

Rate limits are enforced by Redis (distributed, multi-instance safe).

**Redis failure behaviour (FIX BLOCKER-2):** `RedisDistributedRateLimiter` applies a
policy-specific fail strategy when Redis is unavailable:

| Policy | Redis-down behaviour | Rationale |
|--------|----------------------|-----------|
| `login` | **Fail closed** — request rejected (429) | Brute-force protection must hold even during Redis outage |
| `sensitive` | **Fail closed** — request rejected (429) | Same — credential operations must not bypass rate limiting |
| `api` | Fail open — request allowed | Availability prioritised for non-auth endpoints; nginx-layer limit (30 req/min) remains active regardless |

This behaviour is implemented in `HRMS.Infrastructure/Redis/RedisDistributedRateLimiter.cs`:
```csharp
var failClosed = _policyName is "login" or "sensitive";
return new Lease(!failClosed);   // false = rejected for login/sensitive
```

The nginx rate-limit zones (5 req/min auth, 30 req/min API — see `nginx/nginx.conf.template`)
apply independently of Redis and provide a permanent second layer for all endpoints.

---

## PII Encryption

The following fields are AES-256-GCM encrypted at rest in MySQL:

| Field | Entity |
|-------|--------|
| AadhaarNumber | Employee |
| PanNumber | Employee |
| BankAccountNumber | Employee |
| IFSC | Employee |

- Key: `ENCRYPTION_KEY` env var (must decode to exactly 32 bytes — generate with `openssl rand -base64 32`)
- Format: `enc:v1:<base64(nonce + tag + ciphertext)>` — versioned prefix allows key rotation
- Logs: PII fields are masked via Serilog destructuring policies and never appear in log sinks

---

## Transport Security

- TLS 1.2 + 1.3 enforced (TLS 1.0/1.1 disabled in nginx config)
- HSTS: `max-age=63072000; includeSubDomains; preload`
- Certificate: Let's Encrypt via Certbot (auto-renewed every 60 days)
- OCSP stapling enabled

---

## Security Headers

Every response includes:

```
Content-Security-Policy: default-src 'self'; script-src 'self' 'nonce-{random}'; ...
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: camera=(), microphone=(), geolocation=()
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

CSP uses per-request nonces (no `unsafe-inline`) injected by `CspNonceMiddleware`.

---

## Multi-Tenant Isolation

- All database queries are scoped to the authenticated user's `CompanyId` via EF Core global query filters
- Cross-company IDOR prevented in every service method and tested by dedicated xUnit suites
- SuperAdmin is the only role with cross-tenant access (explicit JWT role claim check)
- Tenant context is derived from JWT claims — never from query parameters or request body

---

## Input Validation

- FluentValidation on all DTO inputs (scoped per-request)
- `[ApiController]` returns automatic HTTP 400 on model state errors
- File uploads: allowed extensions enforced + max size (10 MB default) + ClamAV antivirus scan
- SQL injection: not possible — all queries use EF Core parameterised LINQ

---

## Secrets Management

| Secret | How to Set | Never |
|--------|-----------|-------|
| `JWT_PRIVATE_KEY_PEM` | `.env` / Docker secret | Hardcode in code or commit to git |
| `JWT_PUBLIC_KEY_PEM` | `.env` / Docker secret | — |
| `ENCRYPTION_KEY` | `.env` / Docker secret | Commit to git or log |
| `MYSQL_PASSWORD` | `.env` / Docker secret | Expose via API |
| `REDIS_PASSWORD` | `.env` / Docker secret | Log |
| `REPLICATION_PASS` | `.env` / Docker secret | Commit to git |

`EnvironmentValidator.Validate()` runs at startup and blocks the application if required secrets are missing.

---

## Audit Logging

All data-modifying operations are written to the `audit_logs` table:
- Entity name, action (Create / Update / Delete)
- Before/after values (JSON)
- User ID, IP address, timestamp, correlation ID

Retention: 36 months (enforced by `AuditLogRetentionService` Hangfire job, daily at 03:00 UTC).
Indexes: `(entity_name, created_at)` and `(user_id, created_at)` for fast queries.

---

## SAST & Dependency Scanning

- **Semgrep** (p/csharp + p/owasp-top-ten + p/secrets) runs on every PR — findings **block merges**
- **TruffleHog** scans full git history for leaked secrets on every PR
- **Dependabot** raises PRs for vulnerable dependencies weekly

---

## Security Checklist (pre go-live)

### Secrets & Keys
- [ ] Generate RSA key pair: `scripts/generate-rsa-keys.sh`
- [ ] Set `ENCRYPTION_KEY` (32-byte base64): `openssl rand -base64 32`
- [ ] Set `MYSQL_PASSWORD`, `MYSQL_ROOT_PASSWORD`, `REDIS_PASSWORD` (all different, strong)
- [ ] Set `BACKUP_ENCRYPTION_KEY`: `openssl rand -base64 48`
- [ ] Set `GRAFANA_ADMIN_PASSWORD`: `openssl rand -base64 32`
- [ ] Change the generated SuperAdmin password immediately after first login

### Network & CORS
- [ ] Configure `ALLOWED_ORIGINS` to your frontend domain (never leave blank)
- [ ] Set `AllowedHosts` to `app.yourdomain.com;api.yourdomain.com` (no wildcard)
- [ ] Verify SSL certificate is issued and auto-renewing (`certbot renew --dry-run`)
- [ ] Confirm `/metrics` is restricted to monitoring subnet in nginx

### Compliance
- [ ] Set `DPO_EMAIL` to the Data Protection Officer's email address (required for DPDP/GDPR breach notification within 72 h)
- [ ] Set `COMPLIANCE_REGIME` to `dpdp` (India) or `gdpr` (EU) or `iso27001` / `soc2`
- [ ] Startup validator will block launch if either is missing — verify with `docker compose logs api | grep -i compliance`

### Webhook SSRF Hardening
- [ ] Set `WEBHOOK_ALLOWED_DOMAIN_SUFFIXES` to a comma-separated list of permitted target domains (e.g. `hooks.slack.com,hooks.teams.com,yourdomain.com`)
- [ ] Leave blank only if you need to allow arbitrary public HTTPS endpoints (IP blocklist still applies)
- [ ] Test: attempt to register a webhook to `http://169.254.169.254/latest/meta-data` — must return 400

### Email
- [ ] Configure SMTP credentials (required for password reset and leave-decision email flows)
- [ ] Verify `GET /healthz` returns `Healthy` for database and email

### Backups
- [ ] Run the HIGH-8 payslip backfill SQL before applying migrations (see `Documentation/DataMigrationValidation.md`)
- [ ] Run `scripts/test-restore.sh` manually to verify local backup recoverability
- [ ] For off-site backup: set `S3_BUCKET`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_DEFAULT_REGION` in `.env`, then start with `docker compose --profile offsite up -d offsite-backup`
- [ ] Verify off-site upload: `docker compose logs offsite-backup | grep "✅ Upload verified"`
- [ ] Confirm remote retention policy is working: remote backups older than `S3_RETAIN_DAYS` (default 90 d) are pruned
