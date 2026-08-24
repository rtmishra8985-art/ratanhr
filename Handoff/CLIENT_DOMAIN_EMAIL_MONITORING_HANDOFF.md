# CLIENT DOMAIN, TLS, EMAIL & MONITORING HANDOFF
**Project:** RatanHR HRMS  
**Version:** 2.0.0  
**Date:** 2026-08-01  
**Prepared by:** Senior Production-Readiness Engineer  
**Status:** COMPLETE — Items marked CLIENT ACTION REQUIRED must be actioned by the client before go-live

---

## How to Read This Document

| Badge | Meaning |
|---|---|
| ✅ VERIFIED | Confirmed working in the codebase/infrastructure |
| 🔁 CLIENT ACTION REQUIRED | Client must supply credentials, DNS, or access |
| ⚠️ CONFIGURED (not live-tested) | Code/config is in place; requires a live domain to verify end-to-end |
| ❌ BLOCKED | Cannot proceed without a prerequisite action |

---

## 1. Domain and DNS

### 1.1 What Is Configured in the Codebase

| Item | Implementation | Status |
|---|---|---|
| Nginx HTTP → HTTPS redirect | `nginx/nginx.conf`: `listen 80; return 301 https://$host$request_uri;` | ✅ VERIFIED |
| Nginx HTTPS server block | `nginx/nginx.conf`: `listen 443 ssl http2; server_name ${DOMAIN_NAME}` | ✅ VERIFIED |
| `AllowedHosts` env-driven | `appsettings.json`: `"AllowedHosts": "*"` dev only; must be overridden via `AllowedHosts` env var | ✅ VERIFIED |
| `EnvironmentValidator` blocks `*` in production | API startup validation rejects `AllowedHosts=*` if `ASPNETCORE_ENVIRONMENT=Production` | ✅ VERIFIED |
| CORS origins env-driven | `appsettings.json`: `"AllowedOrigins": ""` — must be set via `Cors__AllowedOrigins` or `ALLOWED_ORIGINS` | ✅ VERIFIED |
| Nginx rate-limiting | `nginx/nginx.conf`: `limit_req_zone` for API (30 req/min) and auth (5 req/min) | ✅ VERIFIED |

### 1.2 DNS Records the Client Must Create

> **CLIENT ACTION REQUIRED** — The client must own and control the production domain. All records below must be created in the client's DNS provider (Cloudflare, AWS Route 53, GoDaddy, etc.). Replace `yourdomain.com` with the actual domain.

| Record Type | Name (Host) | Value / Target | TTL | Owner | Status |
|---|---|---|---|---|---|
| A | `hrms.yourdomain.com` | `<PRODUCTION_SERVER_IP>` | 300 | Client DNS | 🔁 CLIENT ACTION |
| A | `api.hrms.yourdomain.com` | `<PRODUCTION_SERVER_IP>` | 300 | Client DNS | 🔁 CLIENT ACTION |
| CNAME | `www.hrms.yourdomain.com` | `hrms.yourdomain.com` | 300 | Client DNS | 🔁 CLIENT ACTION |
| MX | `yourdomain.com` | `mail.yourdomain.com` (priority 10) | 3600 | Client DNS | 🔁 CLIENT ACTION |
| TXT | `yourdomain.com` | SPF record — see §3.2 | 3600 | Client DNS | 🔁 CLIENT ACTION |
| TXT | `_dmarc.yourdomain.com` | DMARC policy — see §3.4 | 3600 | Client DNS | 🔁 CLIENT ACTION |
| TXT | `hrms-mail._domainkey.yourdomain.com` | DKIM public key — see §3.3 | 3600 | Client DNS | 🔁 CLIENT ACTION |

**Verification (after DNS propagation — 15–60 minutes):**
```bash
dig hrms.yourdomain.com +short
nslookup hrms.yourdomain.com 8.8.8.8
# Both must return the production server IP
```

### 1.3 Environment Variables the Client Must Set

```bash
# Set via Replit Secrets / deployment environment — NEVER hardcode
DOMAIN_NAME=hrms.yourdomain.com
API_URL=https://hrms.yourdomain.com/api
APP_BASE_URL=https://hrms.yourdomain.com
AllowedHosts=hrms.yourdomain.com;api.hrms.yourdomain.com
ALLOWED_ORIGINS=https://hrms.yourdomain.com
```

### 1.4 Staging URL Reference

| Service | Staging URL |
|---|---|
| API | `http://localhost:8081` (local Docker staging only) |
| Frontend | `http://localhost:3001` |
| MySQL | `127.0.0.1:3307` |
| Redis | `127.0.0.1:6380` |
| Health check | `http://localhost:8081/api/healthz` |

---

## 2. TLS / HTTPS

### 2.1 Nginx TLS Configuration (Verified in Code)

| Setting | Value | Status |
|---|---|---|
| TLS protocols | `TLSv1.2 TLSv1.3` | ✅ VERIFIED — `nginx/nginx.conf` |
| Cipher suites | Mozilla Intermediate (ECDHE + GCM + CHACHA20) | ✅ VERIFIED |
| SSL session cache | `shared:SSL:10m` | ✅ VERIFIED |
| OCSP stapling | `ssl_stapling on; ssl_stapling_verify on` | ✅ VERIFIED |
| HTTP → HTTPS redirect | `listen 80; return 301 https://$host$request_uri` | ✅ VERIFIED |
| HSTS header | `Strict-Transport-Security: max-age=63072000; includeSubDomains; preload` | ✅ VERIFIED |
| X-Content-Type-Options | `nosniff` | ✅ VERIFIED |
| X-Frame-Options | `SAMEORIGIN` | ✅ VERIFIED |
| X-XSS-Protection | `1; mode=block` | ✅ VERIFIED |
| Referrer-Policy | `strict-origin-when-cross-origin` | ✅ VERIFIED |
| Permissions-Policy | `geolocation=(), microphone=(), camera=()` | ✅ VERIFIED |
| `server_tokens off` | Server version suppressed | ✅ VERIFIED |

### 2.2 Certificate Provisioning (Client Action)

> **CLIENT ACTION REQUIRED** — Production TLS certificate must be provisioned before go-live.

**Recommended: Let's Encrypt (free, auto-renewing)**
```bash
sudo apt-get install certbot python3-certbot-nginx

sudo certbot --nginx \
  -d hrms.yourdomain.com \
  -d api.hrms.yourdomain.com \
  --non-interactive \
  --agree-tos \
  --email it@yourdomain.com

# Test auto-renewal:
sudo certbot renew --dry-run
```

**Alternative: Client-supplied wildcard certificate**
1. Place cert chain: `/etc/letsencrypt/live/hrms.yourdomain.com/fullchain.pem`
2. Place private key: `/etc/letsencrypt/live/hrms.yourdomain.com/privkey.pem` (chmod 600)
3. Update `nginx/nginx.conf.template` `DOMAIN_NAME` variable if using custom path

### 2.3 TLS Verification Tests (Run After Certificate Provisioned)

```bash
# HTTP → HTTPS redirect
curl -I http://hrms.yourdomain.com
# Expected: HTTP/1.1 301 Moved Permanently | Location: https://...

# HTTPS responds
curl -I https://hrms.yourdomain.com
# Expected: HTTP/2 200 | strict-transport-security | x-content-type-options: nosniff

# API health over HTTPS
curl -I https://hrms.yourdomain.com/api/healthz
# Expected: HTTP/2 200

# TLS grade (requires curl 7.66+)
curl --tlsv1.2 -I https://hrms.yourdomain.com
# Expected: success (TLS 1.2 and 1.3 accepted; TLS 1.0/1.1 rejected)

# Certificate details
openssl s_client -connect hrms.yourdomain.com:443 -brief 2>/dev/null | head -5
# Expected: Certificate chain valid; issuer = Let's Encrypt or client CA
```

---

## 3. Email Configuration

### 3.1 Email Service Implementation (Verified in Code)

| Component | Implementation | Status |
|---|---|---|
| SMTP sender | `HRMS.Infrastructure/Services/EmailService.cs` — MailKit | ✅ VERIFIED |
| Async queue | `EmailQueueService.cs` + `EmailQueueWorker.cs` | ✅ VERIFIED |
| Health check | `EmailHealthCheck.cs` — wired into `/api/healthz` | ✅ VERIFIED |
| Queue schema | `Migrations/20260720000005_AddEmailQueue.cs` | ✅ VERIFIED |
| Empty host → log-only mode | Configured in `appsettings.json` (safe default) | ✅ VERIFIED |
| Port 587 + `UseSsl=false` → STARTTLS | Correctly configured for STARTTLS (not implicit TLS) | ✅ VERIFIED |
| **DO NOT** use `UseSsl=true` on port 587 | Documented in `appsettings.json` comment | ✅ VERIFIED |

### 3.2 SMTP Configuration the Client Must Provide

> **CLIENT ACTION REQUIRED** — Set via Replit Secrets / environment variables only. Never hardcode.

| Setting | Env Var | Notes |
|---|---|---|
| SMTP host | `Email__Host` | e.g. `smtp.sendgrid.net`, `email-smtp.eu-west-1.amazonaws.com` |
| SMTP port | `Email__Port` | `587` for STARTTLS (recommended), `465` for implicit TLS |
| UseSsl | `Email__UseSsl` | `false` for port 587 (STARTTLS), `true` for port 465 |
| Username | `Email__Username` | SMTP auth username |
| Password | `Email__Password` | **Secret — use `requestSecrets` flow** |
| From address | `Email__FromAddress` | `noreply@yourdomain.com` |
| From name | `Email__FromName` | `RatanHR` (or client brand name) |
| App base URL | `Email__AppBaseUrl` | `https://hrms.yourdomain.com` (for email links) |

**Recommended SMTP providers:** AWS SES, SendGrid, Postmark, Mailgun, Zoho Mail  
**Staging / test:** MailHog (bundled in `docker-compose.staging.yml` as `hrms_staging_mailhog`) — no credentials required; web inbox at `http://127.0.0.1:8025`. See `Staging/staging.env.template` for defaults.

### 3.3 SPF Record

Authorizes your email provider to send on behalf of your domain.

```
Type: TXT
Name: yourdomain.com
Value: "v=spf1 include:<PROVIDER_INCLUDE> -all"
# Examples:
#   SendGrid:  include:sendgrid.net
#   AWS SES:   include:amazonses.com
#   Mailgun:   include:mailgun.org
```

> **CLIENT ACTION REQUIRED** — Add TXT record at DNS provider. Replace `<PROVIDER_INCLUDE>` with your actual SMTP provider's SPF include string.

### 3.4 DKIM

Signs outgoing emails to prove they originate from your domain.

1. Generate DKIM key pair at your email provider dashboard
2. Add DNS TXT record:
```
Type: TXT
Name: hrms-mail._domainkey.yourdomain.com
Value: "v=DKIM1; k=rsa; p=<DKIM_PUBLIC_KEY>"
```

> **CLIENT ACTION REQUIRED** — Generate DKIM keys at your email provider and add DNS record.

### 3.5 DMARC

Instructs receiving mail servers how to handle messages failing SPF/DKIM. Start with `p=none` for monitoring, upgrade to `p=quarantine` after 30 days of clean reports.

```
Type: TXT
Name: _dmarc.yourdomain.com
Value: "v=DMARC1; p=none; pct=100; rua=mailto:dmarc-reports@yourdomain.com; ruf=mailto:dmarc-failures@yourdomain.com; sp=none; aspf=r; adkim=r"
```

> **CLIENT ACTION REQUIRED** — Add TXT record. After 30 days of clean DMARC reports, change `p=none` to `p=quarantine`.

### 3.6 Email Verification Tests

```bash
# Verify SPF
dig TXT yourdomain.com | grep spf

# Verify DKIM
dig TXT hrms-mail._domainkey.yourdomain.com

# Verify DMARC
dig TXT _dmarc.yourdomain.com

# Send test email via API (superadmin token required)
curl -X POST https://hrms.yourdomain.com/api/email/test \
  -H "Authorization: Bearer <SUPERADMIN_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"to": "test@yourdomain.com"}'
# Expected: 200 OK; email appears in inbox within 60 seconds
```

---

## 4. Monitoring and Alerting

### 4.1 Observability Stack (Verified in Code)

| Pillar | Tool | Config / Evidence | Status |
|---|---|---|---|
| Metrics | OpenTelemetry → Prometheus | `appsettings.json`: `OpenTelemetry.OtlpEndpoint`; `GET /metrics` endpoint | ✅ VERIFIED |
| Traces | OpenTelemetry → Jaeger / Zipkin / OTLP | `appsettings.json`: `JaegerEndpoint`, `ZipkinEndpoint`, `OtlpEndpoint` | ✅ VERIFIED |
| Structured logs | Serilog → Console + File + optional Seq | `appsettings.json`: `Serilog` section; file sink in `Logs/` | ✅ VERIFIED |
| Correlation IDs | `X-Correlation-ID` header, auto-generated if absent | Nginx passthrough configured | ✅ VERIFIED |
| Health checks | `/api/healthz`, `/api/healthz/live`, `/api/healthz/ready` | Wired at startup; includes DB + Redis + Email | ✅ VERIFIED |
| Prometheus endpoint | `GET /metrics` | Restricted to RFC-1918 IPs in nginx config | ✅ VERIFIED |
| Prometheus + Grafana | `docker-compose.yml` services | Pre-configured with alertmanager | ✅ VERIFIED |

### 4.2 Custom HRMS Prometheus Metrics

These metrics are exposed at `GET /metrics` and are ready for Grafana dashboards:

| Metric | Description |
|---|---|
| `hrms_payroll_generation_duration_ms` | Payroll run duration |
| `hrms_payroll_generation_count` | Payroll runs (labelled `success=true/false`) |
| `hrms_db_query_duration_ms` | DB query latency by operation |
| `hrms_redis_operation_duration_ms` | Redis latency by operation |
| `hrms_report_generation_duration_ms` | Report generation time |
| `http_server_request_duration_seconds` | HTTP request duration by route + status |

### 4.3 Prometheus Scrape Configuration

```yaml
# prometheus.yml scrape config for HRMS
scrape_configs:
  - job_name: hrms-api
    static_configs:
      - targets: ['hrms-api:8080']
    metrics_path: /metrics
    scrape_interval: 15s
```

### 4.4 Alert Matrix Summary

For full alert definitions, thresholds, and Alertmanager routing, see `Handoff/MONITORING_ALERT_MATRIX.md`. Key categories:

| Category | Key Alerts | Severity |
|---|---|---|
| Availability | API down, DB unhealthy, Redis unhealthy | CRITICAL |
| Error rates | 5xx > 5% (5 min), auth failures > 50/min | CRITICAL / HIGH |
| Performance | p95 > 2 s, p95 > 5 s | HIGH / CRITICAL |
| Infrastructure | Disk > 80%, CPU > 80%, memory > 80% | HIGH |
| Security | Brute-force > 20 fails/user/5 min, JWT forgery | CRITICAL |

### 4.5 Grafana Setup (Client Action)

> **CLIENT ACTION REQUIRED**

```bash
# Via docker-compose.yml (pre-configured):
docker compose up -d prometheus grafana alertmanager

# Or standalone:
docker run -d \
  --name grafana \
  -p 3000:3000 \
  -e GF_SECURITY_ADMIN_PASSWORD=<GRAFANA_ADMIN_PASSWORD> \
  grafana/grafana-oss:11.2.0

# Import dashboard from Documentation/grafana-dashboard.json
```

Set these environment variables for Grafana:
```bash
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=<SECRET>    # Set via Replit Secrets
```

### 4.6 Monitoring Setup Checklist

- [ ] Prometheus scraping `/metrics` on 15 s interval
- [ ] Alert rules loaded (from `monitoring/alertmanager/` in the source)
- [ ] Grafana dashboards imported from `Documentation/grafana-dashboard.json`
- [ ] PagerDuty / OpsGenie integration tested with test alert
- [ ] Email alerts confirmed delivered to on-call inbox
- [ ] SSL certificate expiry alert active (renew > 30 days before expiry)
- [ ] Backup failure alert tested
- [ ] Alert routing reviewed and approved by client IT manager
- [ ] See `Handoff/MONITORING_ALERT_MATRIX.md` for complete alert inventory

---

## 5. Nginx Internal Service URLs

Based on `nginx/nginx.conf`, the following routing is implemented:

| Path | Target | Rate Limit | Notes |
|---|---|---|---|
| `/health` | `api:8080/health` | None | Health probe — no rate limit |
| `/metrics` | `api:8080/metrics` | RFC-1918 IPs only | Prometheus scrape |
| `/api/auth/login` etc. | `api:8080` | Auth zone (5 req/min) | Strict rate limiting |
| `/uploads/` | Nginx static | None | Direct static file serving |
| `/swagger` | `api:8080` | RFC-1918 IPs only | Internal access only |
| `/` (catch-all) | `api:8080` | API zone (30 req/min) | General API proxy |

---

## 6. Pre-Go-Live Checklist

| # | Item | Owner | Status |
|---|---|---|---|
| D1 | Production domain DNS A records created | Client | 🔁 CLIENT ACTION |
| D2 | DNS propagation verified (dig / nslookup) | Client / DevOps | 🔁 CLIENT ACTION |
| D3 | TLS certificate provisioned (Let's Encrypt or client cert) | Client / DevOps | 🔁 CLIENT ACTION |
| D4 | HTTP → HTTPS redirect verified | DevOps | 🔁 After D3 |
| D5 | HSTS header present in response | DevOps | 🔁 After D3 |
| D6 | `AllowedHosts` env var set to production hostname(s) | DevOps | 🔁 CLIENT ACTION |
| D7 | `ALLOWED_ORIGINS` / `Cors__AllowedOrigins` set | DevOps | 🔁 CLIENT ACTION |
| E1 | SMTP credentials set via Replit Secrets | Client | 🔁 CLIENT ACTION |
| E2 | SPF DNS record published | Client | 🔁 CLIENT ACTION |
| E3 | DKIM key generated and DNS record published | Client | 🔁 CLIENT ACTION |
| E4 | DMARC DNS record published (start with `p=none`) | Client | 🔁 CLIENT ACTION |
| E5 | Test email delivered end-to-end | DevOps | 🔁 After E1–E4 |
| M1 | Prometheus scraping `/metrics` | Client / DevOps | 🔁 CLIENT ACTION |
| M2 | Grafana dashboards imported | Client / DevOps | 🔁 CLIENT ACTION |
| M3 | Alert routing configured (PagerDuty / email) | Client / DevOps | 🔁 CLIENT ACTION |
| M4 | Uptime monitor (external HTTP probe) configured | Client | 🔁 CLIENT ACTION |
| M5 | On-call contacts confirmed in `CLIENT_OPERATIONS_CONTACTS.md` | Client | 🔁 CLIENT ACTION |
| G1 | Production secrets rotated (all differ from staging) | DevOps | 🔁 CLIENT ACTION |
| G2 | Client UAT completed and signed off | Client | 🔁 CLIENT ACTION |
| G3 | Client `CLIENT_OPERATIONS_CONTACTS.md` completed | Client | 🔁 CLIENT ACTION |

---

## Handoff Contacts

| Role | Contact |
|---|---|
| RatanHR Engineering | support@ratanhr.com |
| Client IT (primary) | **See CLIENT_OPERATIONS_CONTACTS.md — CLIENT ACTION REQUIRED** |
| Client IT (escalation) | **See CLIENT_OPERATIONS_CONTACTS.md — CLIENT ACTION REQUIRED** |

**See also:**
- `Handoff/CLIENT_OPERATIONS_CONTACTS.md` — full escalation path template (client to complete)
- `Handoff/MONITORING_ALERT_MATRIX.md` — full alert inventory and Prometheus YAML rules
- `Documentation/MonitoringGuide.md` — observability setup guide
- `Documentation/SecurityGuide.md` — security configuration reference
