# Phase 3 Remediation Report

Date: 2026-08-08
Scope: resolve the four Phase 2 blockers — EF model/snapshot drift, backend build/test
execution, exact MySQL 8.4 / Redis 7.4-alpine runtime verification, and Playwright E2E
execution — or prove exactly why each cannot be verified from this environment.

**Environment note, stated up front:** this working environment has no `dotnet`, no
Docker, and no `bun`, and cannot install any of them (see the evidence under
`docs/evidence/phase-3-remediation/{ef,backend,docker,e2e}/*-attempt.txt` for the exact
commands run and exact errors received — real 404s from `apt-get`, a real
`npx playwright install` failure, not simulated). Nothing below claims a runtime result
that wasn't actually produced by running the command. Where a check could genuinely be
done without those tools (static source inspection, regex secret scan, YAML tag
inspection, ZIP integrity), it was done for real and is reported as such.

## 1. Archive extraction and inventory

Extracted to a temporary directory, original ZIP untouched. ZIP integrity passed.
Root correctly identified as `RatanHR-merged-release-candidate/`. Solution, all five
backend projects, the frontend project, all nine Compose files, the Dockerfile, the
Playwright config, and `global.json` were all located directly (not assumed) — see
`docs/evidence/phase-3-remediation/archive-inventory.txt`.

## 2. EF model/snapshot drift

**Could not run** `dotnet ef migrations list` or
`dotnet ef migrations has-pending-model-changes` — no dotnet SDK available or
installable here (evidence: `docs/evidence/phase-3-remediation/ef/ef-attempt.txt`).

Static source inspection was performed instead and found real, concrete drift: only
4 of 17 migrations have a matching `.Designer.cs` (a normal EF-generated migration
always has one), and the hand-maintained `ApplicationDbContextModelSnapshot_MySql.cs`
declares only 3 of the ~22 mapped columns on `payslips` (and omits the
`ux_payslips_employee_month_year` unique index that the `20260806000001` migration
creates) — the same sparse pattern repeats across most of its 81 entity blocks. Full
detail in `docs/evidence/phase-3-remediation/ef/static-analysis.md`.

The payslip-uniqueness migration itself **is** present on disk and looks correct
(guards against pre-existing duplicates by letting MySQL's `CREATE UNIQUE INDEX`
fail rather than silently deleting data).

Per the task's own constraint against hand-patching the snapshot without the EF
tool available to verify the result, this was **not** patched blind. Copy-ready next
steps are in `docs/evidence/phase-3-remediation/ef/external-runbook.md`.

**Status: UNVERIFIED — DOTNET ENVIRONMENT REQUIRED** (drift is confirmed by static
analysis; the authoritative CLI check still needs to run).

## 3. Backend build and test suite

**Could not run.** `apt-get install -y dotnet-sdk-8.0` was attempted and failed with
404s from both `archive.ubuntu.com` and `security.ubuntu.com` for every `dotnet8`
package (full output: `docs/evidence/phase-3-remediation/backend/dotnet-install-attempt.txt`).
No build, no test run, no TRX file — none of the 15 backend behaviors in the task
brief (health check, antivirus fail-closed, payroll duplicate-period, MFA, etc.) were
exercised.

**Status: BLOCKED — DOTNET SDK REQUIRED.** Runbook:
`docs/evidence/phase-3-remediation/backend/external-runbook.md`.

## 4. MySQL 8.4 / Redis 7.4-alpine

**Could not run.** No Docker daemon; `apt-get install -y docker.io` also failed with
404s (`docs/evidence/phase-3-remediation/docker/docker-install-attempt.txt`), and even
had it installed, this sandbox's egress allowlist excludes Docker Hub registry hosts,
so image pulls would fail regardless.

Static inspection of the Compose files did find one real, worth-flagging drift:
`Staging/docker-compose.staging.yml` pins `redis:7-alpine`, not the required
`redis:7.4-alpine` — the E2E and prod Compose files (`docker-compose.e2e.yml`,
`docker-compose.yml`, `docker-compose.prod.yml`) all use the exact required tags
correctly (the latter two additionally pin by `sha256` digest). The `Dockerfile`'s
three stages (`build`, `migrate`, `runtime`) exist and the `migrate` stage does copy
the three `sql-supplements/*.sql` files as required — confirmed by reading the
Dockerfile, not by building it.

**Status: BLOCKED — DOCKER ENVIRONMENT REQUIRED.** Runbook:
`docs/evidence/phase-3-remediation/docker/external-runbook.md`.

## 5. Playwright E2E

`HRMS.SPA.Source/.env.e2e.example` already exists, is well-documented, and contains
only placeholder credentials — no action needed there. `bun.lock` confirms bun is the
actual package manager (a stale comment in `playwright.config.ts` references `pnpm`;
harmless, flagged for cleanup).

**Could not run.** No bun in this sandbox and no reachable installer for it. As a
fallback, `npx playwright install --with-deps chromium firefox` was actually attempted
and failed for real (blocked `deb.nodesource.com` mirror; see
`docs/evidence/phase-3-remediation/e2e/e2e-attempt.txt`). No browsers were installed,
no specs ran.

**Status: BLOCKED — DOCKER/BROWSER ENVIRONMENT REQUIRED.** Runbook:
`docs/evidence/phase-3-remediation/e2e/external-runbook.md`.

## 6. Regression checks that genuinely could run here

- **Secret scan:** a regex sweep across tracked source found one hit, inspected and
  confirmed to be a runtime-generated RSA test fixture, not a real credential. No
  committed `.env`/connection strings/tokens found.
  **Status: VERIFIED clean** (static sweep — see
  `docs/evidence/phase-3-remediation/secrets/README.md` for scope/caveats).
- **Frontend typecheck/lint/Vitest/build and the `SslMode=None` removal:** per the
  brief, these were already genuinely verified in Phase 2 and are unchanged by this
  pass, so they were intentionally **not** re-run (the brief explicitly says not to
  touch already-verified frontend work absent a regression trigger, and nothing in
  this pass touched frontend source).
- **`.github/workflows/ci.yml`:** already statically verified per Phase 2; not
  re-inspected here since nothing changed in it.

## What changed in this pass

Only additive documentation and evidence:
- `docs/phase-3-remediation.md` (this file)
- `docs/phase-3-readiness.md`
- `docs/evidence/phase-3-remediation/**` (new evidence tree)

No source code, migration, Compose file, or Dockerfile was modified. No historical
evidence file was touched.
