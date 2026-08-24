# RatanHR HRMS — Fresh Isolated Validation Record

**Validation date:** 2026-08-02  
**Scope:** Exact uploaded source archive `ratanhr-final-readiness-source-20260802_1785653980526.zip`  
**Environment:** Disposable local Docker staging resources only  
**Production access:** None  
**Tester:** Replit validation  
**Decision:** **NO-GO — external release gates remain open**

## Evidence handling

This record contains no passwords, tokens, private keys, cookies, email bodies,
personal data, production connection strings, or client data. Temporary staging
values were generated locally, used only for the disposable run, and removed
during cleanup. The source baseline was not changed:

- `Database__AutoMigrate=false`
- `Biometric__EnableLiveSync=false`
- migration `20260801000001_AddCompanyIdToLeaveTypes` remains present

The dedicated `dotnet-ef` migration-image path was attempted and blocked by a
temporary NuGet network failure. To verify the actual EF migration set without
changing the source baseline, a one-shot container made from the already-built
API image was run with an ephemeral `Database__AutoMigrate=true` override against
the disposable database only. The API was then started again with the staging
compose baseline (`Database__AutoMigrate=false`) and the schema was queried.

## Source archive checks

| Check | Result | Observation |
|---|---|---|
| ZIP integrity | **PASS** | `unzip -tq` returned no errors |
| Exact archive SHA-256 | **RECORDED** | `78f54a0fe47559fd47d120f8591380981ab6dada5c65ff1dc4cee9ee1909adc7` |
| Archive file count | **RECORDED** | 1,262 paths |
| Committed environment/key candidates | **PASS** | No `.env*`, `.pem`, `.key`, `.p12`, `.pfx`, or `.crt` files found |
| Protected migration reference | **PASS** | `20260801000001_AddCompanyIdToLeaveTypes.cs` present |

## Staging configuration checks

The package's `scripts/validate-staging.sh` was run with generated
staging-only values. It passed Compose interpolation and verified:

- loopback-only host bindings in the documented staging Compose file;
- dedicated `hrms_staging_net`;
- `Database__AutoMigrate: "false"`;
- `Biometric__EnableLiveSync: "false"`;
- `Hangfire__UseInMemory: "false"`.

No production compose file, volume, credential, or database was used.

## Disposable database and migration checks

The disposable MySQL service was `mysql:8.0` and reported version `8.0.46`.
Network-level validation through a separate disposable MySQL client returned:

| Check | Result |
|---|---|
| MySQL connectivity | **PASS** |
| Server character set | `utf8mb4` |
| Server collation | `utf8mb4_unicode_ci` |
| Server timezone | `+05:30` |
| Selected database | `hrms_staging` |
| EF migration runner | **PASS — disposable application runner** |
| Migration history count | **PASS — 8 rows** |
| Protected migration row | **PASS — `20260801000001_AddCompanyIdToLeaveTypes`** |
| `leave_types.company_id` | **PASS — nullable `int`** |

Observed migration IDs, in order:

```text
20260726000001_MySqlInitialSchema
20260728000001_AddTimesheetsTable
20260728000002_AddPayslipStatusColumn
20260728000003_FixWebAttendanceTimeColumns
20260728000004_AddCheckConstraintsAndPayslipIndex
20260729120000_EncryptPiiFields
20260731000001_AddUserSoftDelete
20260801000001_AddCompanyIdToLeaveTypes
```

The dedicated migration image build still has a reproducibility dependency:
`dotnet tool install dotnet-ef --version 8.0.*` could not reach
`https://api.nuget.org/v3/index.json` in this runtime. This remains a
**BLOCKED** item for the prescribed manual migration-image procedure and must
be rerun when NuGet access is available.

## Runtime checks

After schema creation, the API was started with the staging baseline and
`Database__AutoMigrate=false`. The following returned HTTP 200:

- `/health`
- `/healthz`
- `/healthz/live`
- `/healthz/ready`

The staging frontend returned HTTP 200 and the MailHog API returned HTTP 200.
Redis network validation passed authenticated `PING` and a `SET`/`GET`
round-trip. MailHog returned an empty inbox (`[]`); no real email was sent.

Observed security headers on `/health`:

- `Strict-Transport-Security`
- `Content-Security-Policy`
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy`
- `Permissions-Policy`

Unauthenticated requests to representative protected API routes returned
HTTP 401. No approved role accounts were available, so role-specific 403,
login, logout, refresh rotation, MFA, CSRF, cookie, IDOR, and tenant/branch
tests were not performed.

## Environment limitation

The Docker runtime in this workspace rejected in-container health-check
processes with `OCI runtime exec failed ... setns process`. This caused the
Compose health status to report unhealthy/starting for some containers even
though MySQL logged “ready for connections,” Redis logged “Ready to accept
connections,” MailHog bound SMTP/HTTP, and independent network probes passed.
This limitation is recorded rather than treated as a service pass.

The first run also encountered the documented API host port already in use.
The stack was cleaned up, then rerun with alternate loopback-only host ports;
the source Compose contract was not modified.

## Cleanup

The disposable containers, volumes, network, generated key material, temporary
environment file, and validation Compose override were removed after the run.
No production resources were accessed or changed.

## Remaining release blockers

The following remain **PENDING/BLOCKED** and prevent production approval:

1. Approved staging-only SuperAdmin, Admin, and Employee accounts.
2. Authenticated login/session/security testing, including refresh rotation,
   expiry, logout, rate limiting, MFA if enabled, CSRF, and secure cookies.
3. Two sanitized tenants, multiple branches, and authenticated tenant/branch,
   RBAC, IDOR, export, download, and forbidden-mutation checks.
4. Authenticated HRMS workflow testing across employee, attendance, leave,
   payroll, payslip, reports, recruitment, performance, notifications,
   helpdesk, GPS, and biometric read-only scope.
5. SMTP success/failure/retry/recovery/duplicate/attachment evidence.
6. Authenticated Hangfire dashboard, job, retry, recovery, idempotency, and
   scope evidence.
7. Current backup/restore/rollback evidence with RPO, RTO, freshness,
   encryption, retention, and integrity results.
8. Monitoring/alert routing, DNS/TLS, SMTP sender records, secret ownership,
   escalation, and infrastructure-owner evidence.
9. Client UAT scenarios, defect retests, and explicit approvals.
10. Formal approval matrix completion and final release approval.
11. A successful rerun of the prescribed dedicated migration-image procedure
    after NuGet access is restored.

**Final decision: NO-GO.** The package is structurally complete and the fresh
isolated technical checks are substantially positive, but production must
remain blocked until the open staging, infrastructure, UAT, and approval gates
have durable evidence.

---

## Final exact-candidate continuation — 2026-08-02

The dedicated migration-image procedure was subsequently rerun successfully
against a disposable MySQL network after NuGet access became available. The
image built and executed; `__EFMigrationsHistory` contained 8 rows,
`20260801000001_AddCompanyIdToLeaveTypes` appeared exactly once,
`leave_types.company_id` was nullable, and the disposable schema reported
`utf8mb4/utf8mb4_unicode_ci`.

The corrected disposable SDK run also passed 934 backend tests. Frontend
typecheck, 76 tests, lint, production build, runtime-image build, source
safety scan, and staging Compose validation passed. A complete current
Compose runtime pass was not claimed because the documented API host port was
already occupied by the workspace preview; the attempted disposable stack was
cleaned up.

This continuation does not change the release decision. Authenticated
staging, client UAT, tenant/branch isolation, SMTP/Hangfire behavior,
production recovery controls, monitoring ownership, and formal approvals
remain blocked or pending.