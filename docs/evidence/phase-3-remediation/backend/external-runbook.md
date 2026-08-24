# External Runbook — Backend Build & Test Suite

Run on a machine with .NET SDK 8.0.416 installed (this sandbox has none — see
`dotnet-install-attempt.txt`).

```bash
cd RatanHR-merged-release-candidate

dotnet --info

dotnet restore HRMS.sln --locked-mode

dotnet build HRMS.sln \
  --configuration Release \
  --no-restore \
  --no-incremental

dotnet test HRMS.sln \
  --configuration Release \
  --no-build \
  --logger "trx;LogFileName=phase-3-backend-tests.trx"
```

Confirm specifically (per task brief), then record pass/fail for each in
`docs/phase-3-readiness.md` with the TRX evidence path:

- All projects compile with zero errors
- API host starts and DI container validates (`ValidateOnBuild`/`ValidateScopes`)
- `GET /health` returns healthy
- Antivirus adapter resolves; clean uploads accepted; infected uploads rejected;
  scanner failure fails closed (see `HRMS.Tests` for the relevant fixtures, e.g.
  `Phase6SecurityAuditTests.cs` and any `*Antivirus*Tests.cs`)
- Payroll duplicate-period rejection and the new payslip unique-constraint behavior
  (requires the EF runbook to have been completed first — the migration must be
  applied to a real MySQL instance for the DB-level constraint test to be meaningful)
- Auth + MFA flows
- Redis-backed and MySQL-backed services (requires the Docker runbook's services
  running, or equivalent local MySQL 8.4 / Redis 7.4-alpine instances)
- Startup validation (fails fast on missing required config)

## If dotnet is unavailable on the target machine too

Install via the official channel (not apt — apt's `dotnet-sdk-8.0` package returned
404 from `security.ubuntu.com`/`archive.ubuntu.com` in this sandbox; a real machine
with unrestricted internet access should be able to reach
`https://dotnet.microsoft.com/download` or use the `dotnet-install.sh` script), then
re-run the block above.

Do not mark backend build/tests VERIFIED from anything other than a fresh run of the
commands above with the TRX file archived under
`docs/evidence/phase-3-remediation/backend/`.
