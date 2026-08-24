# RatanHR Current Release Gate

**Verification date:** 2026-08-01  
**Source:** Uploaded `ratanhr-fixed-v6-complete-source-updated.zip`, verified in an isolated working copy  
**Verdict:** **NOT READY FOR CLIENT PRODUCTION RELEASE**

This report is the current evidence summary. It supersedes older reports that claim
release readiness without evidence from this source snapshot. No production database,
customer data, DNS, certificates, credentials, or external services were changed.

## Verified in this environment

| Gate | Result | Evidence |
|---|---|---|
| Backend dependency restore | PASS | `dotnet restore HRMS.sln --use-lock-file --locked-mode` |
| Backend Release build | PASS | `dotnet build HRMS.sln --configuration Release --no-restore --no-incremental`; 0 warnings, 0 errors |
| Backend tests | PASS | `dotnet test HRMS.Tests/HRMS.Tests.csproj --configuration Release --no-restore`; 930 passed, 0 failed, 0 skipped |
| Frontend dependencies | PASS | `bun install --frozen-lockfile` |
| Frontend typecheck | PASS | `bun run typecheck` |
| Frontend lint | PASS | `bun run lint`; zero errors and warnings |
| Frontend unit tests | PASS | 4 files, 76 tests passed |
| Frontend production build | PASS | `PORT=3000 BASE_PATH=/ NODE_ENV=production bun run build` |
| Compose interpolation | PASS | `docker compose config --quiet` with command-only validation values for all required base-stack variables |
| Secret scan | PASS | No private-key material, cloud access-key pattern, or API-token pattern found in source/config files; placeholders only |
| Production Hangfire storage | PASS at code level | `Hangfire.Redis.StackExchange`; in-memory storage is blocked outside Development |
| MFA refresh-token controls | PASS at code level | Pre-MFA refresh tokens are revoked/rejected; TOTP verification issues an `MfaVerified=true` refresh token |
| Attendance dead-code IDOR path | PASS at code level | Unguarded `UpdateWebAttendanceStatusAsync` is absent from the application interface and service |

The frontend build emits non-fatal sourcemap-location notices for several UI files,
but exits successfully and produces the production bundle.

## Release blockers and client-owned gates

1. **No client staging environment was supplied.** MySQL/Redis connectivity, migrations,
   health endpoints, login/MFA, refresh rotation, rate limiting, cross-tenant access
   tests, and background-job persistence still require an isolated staging deployment.
2. **Production domain, DNS, TLS certificates, SMTP, DPO contact, and monitoring
   destinations are unknown.** The client must supply and authorize these values.
3. **Backup restore evidence is outstanding.** A non-production restore drill must use
   an encrypted backup, start the API against the restored database, verify health and
   representative read-only workflows, and record duration.
4. **Biometric vendors are intentionally not integrated.** The seven provider classes
   return HTTP 501. This must be accepted in the signed scope or implemented before
   promising live biometric synchronization.
5. **Off-site backups are optional and not validated here.** If the offsite Compose
   profile is enabled, the client must authorize an S3-compatible provider and test
   upload, retention, and restore.

## Secure configuration requirements

Use `.env.example` only as a checklist. Put real values in Replit Secrets, an external
secret manager, or the deployment platform's protected environment store. Do not place
real values in `.env.example`, Kubernetes templates, source code, logs, or release ZIPs.

`AllowedHosts` is now a deployment placeholder and the production validator rejects
wildcards and placeholder hostnames. Set it to the client-owned semicolon-separated
host list before starting production.

## Required next verification

Run the following only against an isolated staging database and non-production
credentials:

```bash
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d mysql redis
docker compose --env-file .env run --rm migrate
docker compose --env-file .env up -d api
curl -fsS http://localhost:8080/health
dotnet test HRMS.Tests/HRMS.Tests.csproj --configuration Release
```

Do not run migration, restore, or destructive data commands against production without
explicit approval and a tested rollback/backup plan.