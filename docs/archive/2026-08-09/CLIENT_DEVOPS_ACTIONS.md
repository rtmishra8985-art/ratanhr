# CLIENT AND DEVOPS REQUIRED ACTIONS — RatanHR HRMS
**Date:** 2026-08-02

All source-code and configuration defects have been fixed. This document lists only items that **cannot be fixed in source code** — they require operational input from DevOps or the Client.

No go-live recommendation can be issued until all mandatory items are resolved.

---

## DevOps Required Actions

### DO-01 — Provision a Linux staging server with Docker

**Priority:** BLOCKER for all staging tests  
**Owner:** DevOps  

- Linux host with Docker Engine 24+ and Docker Compose V2
- Minimum 4 GB RAM, 20 GB disk
- Required ports available: 3307, 6380, 8081, 1025, 8025, 3001

---

### DO-02 — Generate and populate `Staging/.env.staging`

**Priority:** BLOCKER for staging stack startup  
**Owner:** DevOps  

```bash
cp Staging/staging.env.template Staging/.env.staging
# Fill every <REPLACE_...> value:

openssl rand -base64 32   # STAGING_DB_ROOT_PASSWORD
openssl rand -base64 32   # STAGING_DB_PASSWORD
openssl rand -base64 32   # STAGING_REDIS_PASSWORD
openssl rand -base64 32   # ENCRYPTION_KEY_STAGING
openssl rand -base64 32   # BACKUP_ENCRYPTION_KEY

# RSA key pair for JWT:
chmod +x scripts/generate-rsa-keys.sh && ./scripts/generate-rsa-keys.sh
# Copy output to JWT_PRIVATE_KEY_PEM and JWT_PUBLIC_KEY_PEM

# Validate — must print "All required staging values are present."
bash scripts/validate-staging.sh --env-file Staging/.env.staging
```

Store all generated secrets in your secrets manager. Do **not** commit `.env.staging`.

---

### DO-03 — Start the staging stack

**Priority:** BLOCKER for all live tests  
**Owner:** DevOps  

```bash
docker compose -f Staging/docker-compose.staging.yml --env-file Staging/.env.staging up -d
```

The stack starts in this order (fully automated):
1. MySQL starts → healthcheck passes
2. Redis starts → healthcheck passes
3. MailHog starts → healthcheck passes
4. **`hrms_staging_migrate` runs EF Core migrations → exits 0** ← new, was previously missing
5. API starts → healthcheck passes
6. Frontend starts

Verify: `docker compose -f Staging/docker-compose.staging.yml logs hrms_staging_migrate`  
Expected: last line is `Migration complete`

---

### DO-04 — Configure alert email destinations

**Priority:** Required for monitoring sign-off  
**Owner:** DevOps + Client (see CL-03)  

Add these to `.env.staging` (or the production `.env`):

```bash
ALERTMANAGER_EMAIL_TO=ops@yourcompany.com           # default/warning alerts
ALERTMANAGER_ONCALL_EMAIL=oncall@yourcompany.com    # critical alerts
ALERTMANAGER_SMTP_FROM=alerts@yourcompany.com
ALERTMANAGER_SMTP_SMARTHOST=smtp.yourcompany.com:587
ALERTMANAGER_SMTP_USERNAME=alerts@yourcompany.com
ALERTMANAGER_SMTP_PASSWORD=<smtp-password>
```

The `monitoring/alertmanager.yml` receivers are now properly configured with `email_configs` — they will deliver as soon as these env vars are set. Slack/PagerDuty templates are commented in the file for when those channels are ready.

---

### DO-05 — Provide a staging domain and TLS certificate

**Priority:** Required for DNS/TLS testing  
**Owner:** DevOps  

- Create DNS A record for staging subdomain (e.g. `staging.hrms.yourcompany.com`)
- Set `DOMAIN_NAME=staging.hrms.yourcompany.com` in `.env`
- The Nginx container runs `nginx/entrypoint.sh` which calls `envsubst` to generate `nginx.conf` from `nginx.conf.template` — no manual config edit needed
- Let's Encrypt certificate: run `bash nginx/init-letsencrypt.sh` after DNS resolves

---

### DO-06 — Run the backup and recovery cycle

**Priority:** Required before go-live  
**Owner:** DevOps  

```bash
# With staging stack running and seeded data:
bash scripts/mysql-backup.sh        # creates encrypted local backup
bash scripts/test-restore.sh        # restores into disposable DB, checks API health
```

For S3 backup: set `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `S3_BUCKET`, `S3_REGION` in `.env.staging` then run `bash scripts/backup-s3.sh`.

---

### DO-07 — Run .NET backend build and tests in CI

**Priority:** Required for development sign-off  
**Owner:** DevOps  

The .NET 8 SDK was not available in the test environment. The GitHub Actions workflow (`build.yml`) covers this automatically on push. To verify locally on a machine with .NET 8 SDK:

```bash
dotnet restore HRMS.sln --use-lock-file --locked-mode
dotnet build HRMS.sln -c Release /p:TreatWarningsAsErrors=true
dotnet test HRMS.Tests/HRMS.Tests.csproj -c Release \
  --settings coverlet.runsettings \
  --collect:"XPlat Code Coverage"
```

Confirm: zero build warnings/errors, all tests pass, line coverage ≥ 60%.

---

### DO-08 — Frontend production build (CI)

**Priority:** Informational  
**Owner:** DevOps  

The new `build:ci` script handles standard CI environments:

```bash
cd HRMS.SPA.Source
npm run build:ci    # sets PORT=3000 BASE_PATH=/ automatically
```

The staging Docker (`HRMS.SPA/Dockerfile.staging`) serves pre-built static files from `HRMS.SPA/` — rebuild with `build:ci` and copy `dist/` to `HRMS.SPA/` before building the staging image.

---

## Client Required Actions

### CL-01 — Supply production SMTP credentials

**Priority:** Required for real email testing  
**Owner:** Client  

MailHog testing (staging) works without credentials. For real SMTP before go-live:
- Provide SMTP host, port, username, password for a staging SMTP relay
- Store in secrets manager; supply to DevOps for `.env.staging`
- Never use production email credentials in staging

---

### CL-02 — Provide DPO email address

**Priority:** BLOCKER — API will not start without this  
**Owner:** Client  

`Compliance__DpoEmail` is required by `EnvironmentValidator` in non-Development environments. It is the Data Protection Officer's email for DPDP/GDPR 72-hour breach notification. Add to `.env.staging`:

```bash
COMPLIANCE_DPO_EMAIL=dpo@yourcompany.com
```

---

### CL-03 — Confirm compliance regime

**Priority:** BLOCKER — API will not start without this  
**Owner:** Client  

`Compliance__ComplianceRegime` must be one of: `dpdp`, `gdpr`, `iso27001`, `soc2`.  
Add to `.env.staging`:

```bash
COMPLIANCE_REGIME=dpdp    # default for India deployments
```

---

### CL-04 — Supply alert destinations and escalation contacts

**Priority:** Required for monitoring sign-off  
**Owner:** Client  

Supply to DevOps (for DO-04):
- On-call email(s) for warning and critical alerts
- Slack webhook URL (if applicable)
- Named primary and secondary escalation contacts with role and phone
- Business-hours vs. out-of-hours escalation path

---

### CL-05 — Supply production SMTP for Alertmanager

**Priority:** Required for monitoring alerts  
**Owner:** Client  

Alertmanager sends alert emails via SMTP. Supply staging SMTP credentials for `ALERTMANAGER_SMTP_*` variables (see DO-04).

---

### CL-06 — Conduct User Acceptance Testing (UAT)

**Priority:** BLOCKER for go-live  
**Owner:** Client  

After all DevOps phases complete:
1. Sign off on all HR and payroll workflows (Phase 6 of checklist)
2. Sign off on authentication and user management
3. Confirm notification and email content
4. Confirm report and export formats
5. Provide written UAT sign-off

---

### CL-07 — Formal go-live approval

**Priority:** BLOCKER for production deployment  
**Owner:** Client (project owner / authorised signatory)  

Issue formal written go-live approval referencing:
- `IMPLEMENTATION_TEST_REPORT.md` review date
- UAT sign-off date and participants
- Confirmation that all PENDING CLIENT items are resolved

---

## Summary Table

| ID | Item | Owner | Priority | Status |
|---|---|---|---|---|
| DO-01 | Provision Linux staging server | DevOps | BLOCKER | PENDING DEVOPS |
| DO-02 | Generate and populate `.env.staging` | DevOps | BLOCKER | PENDING DEVOPS |
| DO-03 | Start staging stack (migrate now included) | DevOps | BLOCKER | PENDING DEVOPS |
| DO-04 | Configure alert email destinations | DevOps + Client | Required | PENDING |
| DO-05 | Staging domain + TLS certificate | DevOps | Required | PENDING DEVOPS |
| DO-06 | Run backup and recovery cycle | DevOps | Required | PENDING DEVOPS |
| DO-07 | Run .NET backend build and tests in CI | DevOps | Required | PENDING DEVOPS |
| DO-08 | Frontend `build:ci` guidance | DevOps | Informational | PENDING DEVOPS |
| CL-01 | Production SMTP credentials | Client | Required | PENDING CLIENT |
| CL-02 | DPO email address | Client | BLOCKER | PENDING CLIENT |
| CL-03 | Confirm compliance regime | Client | BLOCKER | PENDING CLIENT |
| CL-04 | Alert destinations and escalation contacts | Client | Required | PENDING CLIENT |
| CL-05 | SMTP for Alertmanager | Client | Required | PENDING CLIENT |
| CL-06 | User Acceptance Testing | Client | BLOCKER | PENDING CLIENT |
| CL-07 | Formal go-live approval | Client | BLOCKER | PENDING CLIENT |
