# GitHub Actions — Required Secrets & Variables Setup

Before your first push to GitHub, configure the following in:
**GitHub repo → Settings → Secrets and variables → Actions**

---

## 🔴 Required Secrets (production deploys will fail without these)

Go to **Secrets → Repository secrets** and add:

| Secret Name | Description | How to Generate |
|---|---|---|
| `JWT_KEY` | JWT signing key (min 32 bytes base64-decoded) | `openssl rand -base64 48` |
| `ENCRYPTION_KEY` | AES-256 PII encryption key (exactly 32 bytes base64-decoded) | `openssl rand -base64 32` |
| `MYSQL_PASSWORD` | Production database password | `openssl rand -base64 24` |
| `REDIS_PASSWORD` | Redis password | `openssl rand -base64 24` |
| `EMAIL_PASSWORD` | SMTP password for your mail provider | From your mail provider |

> ⚠️ **Never use the same values as the CI test credentials** in `test.yml`.
> Those are fixed test values for the CI sandbox only — not for production.

---

## 🟡 Required Variables (for E2E tests on staging)

Go to **Variables → Repository variables** and add:

| Variable Name | Description | Example |
|---|---|---|
| `E2E_BASE_URL` | Your staging deployment URL | `https://staging.ratanhr.com` |

Then add these **Secrets** for E2E login:

| Secret Name | Description |
|---|---|
| `E2E_ADMIN_EMAIL` | Admin account email used by Playwright |
| `E2E_ADMIN_PASSWORD` | Admin account password used by Playwright |

> If `E2E_BASE_URL` is not set, the Playwright E2E job is automatically skipped.
> This is safe — it means you haven't set up a staging environment yet.

---

## 🟢 Optional Secrets (monitoring integrations)

| Secret Name | Description |
|---|---|
| `SENTRY_DSN` | Sentry error tracking DSN |
| `SEQ_URL` | Seq structured log server URL |
| `OTLP_ENDPOINT` | OpenTelemetry collector endpoint |

---

## Quick checklist before going live

- [ ] All 🔴 secrets added to GitHub Actions
- [ ] `PAYROLL_DEFAULT_STATE` set correctly in your `.env` (use `Other` unless India deployment)
- [ ] `.env` file created on your server from `.env.example` — never committed to git
- [ ] `scripts/generate-secrets.sh` run to produce your production `.env` values
- [ ] `E2E_BASE_URL` set once your staging server is running
