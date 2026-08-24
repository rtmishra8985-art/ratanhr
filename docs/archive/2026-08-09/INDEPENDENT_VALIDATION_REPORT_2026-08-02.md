# RatanHR HRMS — Independent Validation Report

**Review date:** 2026-08-02  
**Scope:** Uploaded HRMS source archive and validation brief  
**Safety boundary:** Disposable local review only. No production credentials, databases, infrastructure, migrations, or external backup targets were used.

## Executive summary

| Validation item | Result |
|---|---|
| Docker Compose configuration | **PASS after one confirmed defect fix** |
| Backend restore/build/tests | **PASS after two confirmed source/test fixes** |

The staging Compose file contained invalid YAML at the required MySQL password interpolation. The error message included an unquoted colon, so Compose failed before variable interpolation. The interpolation was quoted in this corrected review copy and all Compose validation checks then passed.

The standalone `docker-compose.backup.yml` file is intentionally an overlay, not a complete Compose project. It fails when run alone because it relies on the `hrms_internal` network declared by the main file; the documented combined command passed.

## Source identity

- Uploaded archive: `HRMS_STAGING_VALIDATED_2026-08-02_1785676149878.zip`
- Archive integrity: PASS (`unzip -tq`)
- SHA-256: `78f54a0fe47559fd47d120f8591380981ab6dada5c65ff1dc4cee9ee1909adc7`
- Validation copy: `validation-output/ratanhr-source`

Historical reports in the archive were treated as documentation only. Their PASS claims were not reused as fresh evidence.

## Fresh Compose validation

All checks used disposable placeholder values supplied through a temporary environment file outside the source tree. No placeholder values were written into the repository or printed in the report.

| Check | Result |
|---|---|
| `docker compose --env-file <temporary> -f docker-compose.yml config --quiet` | **PASS** |
| Main file + `docker-compose.override.yml` | **PASS** |
| Main file + documented `docker-compose.backup.yml` overlay | **PASS** |
| Main file with `--profile offsite` | **PASS** |
| Main file + `docker-compose.replica.yml` | **PASS** |
| `Staging/docker-compose.staging.yml` before correction | **FAIL** — YAML line 41 |
| Corrected staging Compose file | **PASS** |
| Corrected staging Compose file from its own directory | **PASS** |

The dependency graph was also inspected. The main API waits for healthy MySQL, healthy Redis, successful migration completion, and healthy ClamAV. The migration job waits for the successful backfill job, and backfill waits for healthy MySQL.

Referenced bind-mounted files and Dockerfile project files were checked and were present, including:

- database initialization SQL;
- Nginx configuration and entrypoint;
- Prometheus, Alertmanager, and Grafana configuration;
- backup scripts;
- staging initialization SQL;
- the solution and all five referenced .NET projects.

## Confirmed defect and correction

### DEFECT-01 — invalid staging YAML interpolation

**Location:** `Staging/docker-compose.staging.yml`, line 41 in the uploaded source.

**Original form:**

```yaml
MYSQL_ROOT_PASSWORD: ${STAGING_DB_ROOT_PASSWORD:?STAGING_DB_ROOT_PASSWORD must be set. Generate: openssl rand -base64 32}
```

The colon in `Generate:` was parsed as YAML syntax because the scalar was unquoted.

**Corrected form:**

```yaml
MYSQL_ROOT_PASSWORD: "${STAGING_DB_ROOT_PASSWORD:?STAGING_DB_ROOT_PASSWORD must be set. Generate: openssl rand -base64 32}"
```

**Regression result:** The corrected staging Compose file passed `docker compose config --quiet` with isolated validation values.

## Backend validation

The project targets `net8.0` and has no `global.json`. Validation used the SDK version specified by the Docker build instructions: `mcr.microsoft.com/dotnet/sdk:8.0.416`.

The following commands were run together in one disposable container from the repository root:

```bash
dotnet restore HRMS.sln --locked-mode
dotnet build HRMS.sln --configuration Release --warnaserror --no-restore
dotnet test HRMS.Tests/HRMS.Tests.csproj \
  --no-build \
  --no-restore \
  --configuration Release
```

Results:

- Locked restore: **PASS**
- Release build: **PASS**, 0 warnings, 0 errors
- Backend tests: **PASS**, 934 passed, 0 failed, 0 skipped
- SDK: `.NET SDK 8.0.416`
- Docker: `27.5.1`

### Backend fixes required

1. `HRMS.Infrastructure/Services/StubServices.cs`
   - Updated `StubPayrollService.GetPayslipAsync` to implement the interface’s optional `companyId` parameter.
2. `HRMS.Tests/IDORExtendedTests.cs`
   - Updated Moq expressions to pass tenant scope explicitly.
   - Preserved unrestricted `null` scope for superadmin.
   - Modeled the database-level tenant filter as `null` for the cross-tenant case.

These changes were required for the current source to compile and for the security-focused tests to reflect the controller’s tenant-scoping behavior.

## Checks not performed

The following require a running disposable staging stack and were intentionally not executed during this configuration-only review:

- database migration execution or rollback;
- API, MySQL, Redis, ClamAV, Nginx, monitoring, and MailHog runtime health;
- authenticated workflow, authorization, tenant-isolation, and IDOR checks;
- backup creation, encryption, upload, and restore;
- production deployment or production data access.

## Deliverables

- This report: `INDEPENDENT_VALIDATION_REPORT_2026-08-02.md`
- Corrected source review copy: the `validation-output/ratanhr-source` directory
- Packaged production-candidate source archive: `HRMS_PRODUCTION_CANDIDATE_FINAL_2026-08-02.zip`