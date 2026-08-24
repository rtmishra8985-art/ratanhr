# RatanHR — Phase 1 Blocker Clearance Report

**Evidence date:** 2026-08-07  
**Source:** `RatanHR-work-fixed-updated/` extracted from the uploaded archive  
**Scope:** Verification and evidence collection only. No production-code bug was fixed, no source file was deleted, no Git repository was fabricated, and no real secret was collected or committed.

The command logs under `evidence/` preserve stdout, stderr, and exit code for each attempted command. Disposable configuration values used during Compose validation were redacted from the evidence package.

## 1. Blocker-by-blocker table

| Blocker | Status | Fresh evidence |
|---|---|---|
| #1 .NET SDK, build, tests, dependencies | **PARTIALLY CLEARED** | .NET 8.0.416 is available; restore and Release build pass. Test run: 1,143 total, 1,091 passed, 51 failed, 1 skipped. No vulnerable packages; deprecated and outdated packages remain. |
| #2 Docker / Compose | **PARTIALLY CLEARED** | Docker 27.5.1 and Compose 2.36.0 available. Base, production, and E2E Compose configs validate. Override, replica, and backup files fail when evaluated standalone. SPA image target passes; .NET build, migrate, and runtime targets fail because Dockerfile SDK 8.0.303 does not satisfy `global.json` 8.0.416. |
| #3 MySQL / migrations | **STILL BLOCKED** | No host MySQL client and no MySQL module are available. Disposable MySQL/Redis containers start but MySQL becomes unhealthy and container exec is unavailable. EF migration listing and update cannot connect to a live database. |
| #4 Version control / provenance | **STILL BLOCKED** | The extracted archive has no `.git` directory, so history, blame, baseline diff, and obsolete-vs-current provenance cannot be verified. |
| #5 Frontend toolchain | **PARTIALLY CLEARED** | Bun install, typecheck, lint, unit tests, and production build pass. Playwright browser installation fails in the environment, and E2E is blocked by missing non-committed `.env.e2e`. |
| #6 CI workflows | **STILL BLOCKED** | `.github/workflows/` is absent. A proposed workflow is documented in section 12 only; no workflow was created. |
| #7 Legacy UI decision | **LIVE (static evidence)** | `Program.cs` enables static files and redirects `/` to `/login.html`; Docker preserves legacy `wwwroot` files while overlaying React output; production Nginx falls back to the API. Runtime confirmation was blocked by API startup failure. |

## 2. Full verbatim command output index

Each file below contains the command, complete captured stdout, complete captured stderr, and exit code:

### .NET and EF

| Command | Evidence |
|---|---|
| `dotnet --version` | `evidence/dotnet-version.txt` |
| `dotnet --info` | `evidence/dotnet-info.txt` |
| `dotnet restore HRMS.sln --locked-mode` | `evidence/dotnet-restore.txt` |
| `dotnet build HRMS.sln -c Release --no-restore` | `evidence/dotnet-build.txt` |
| `dotnet test HRMS.sln -c Release --no-build --settings coverlet.runsettings` | `evidence/dotnet-test.txt` |
| `dotnet list HRMS.sln package --vulnerable --include-transitive` | `evidence/dotnet-vulnerable.txt` |
| `dotnet list HRMS.sln package --deprecated` | `evidence/dotnet-deprecated.txt` |
| `dotnet list HRMS.sln package --outdated` | `evidence/dotnet-outdated.txt` |
| `dotnet tool restore` | `evidence/dotnet-tool-restore.txt` |
| `dotnet ef migrations list` | `evidence/ef-migrations.txt` |
| MySQL-connection `dotnet ef migrations list` | `evidence/ef-migrations-mysql.txt` |
| MySQL-connection `dotnet ef database update` | `evidence/migration-update.txt` |

### Docker and Compose

| Command | Evidence |
|---|---|
| `docker --version` | `evidence/docker-version.txt` |
| `docker compose version` | `evidence/docker-compose-version.txt` |
| `docker compose -f docker-compose.yml config` | `evidence/docker-compose-config.txt` |
| `docker compose -f docker-compose.override.yml config` | `evidence/docker-compose-override-config.txt` |
| `docker compose -f docker-compose.prod.yml config` | `evidence/docker-compose-prod-config.txt` |
| `docker compose -f docker-compose.e2e.yml config` | `evidence/docker-compose-e2e-config.txt` |
| `docker compose -f docker-compose.replica.yml config` | `evidence/docker-compose-replica-config.txt` |
| `docker compose -f docker-compose.backup.yml config` | `evidence/docker-compose-backup-config.txt` |
| `docker build --target spa-builder -t ratanhr-spa .` | `evidence/docker-build-spa-builder.txt` |
| `docker build --target build -t ratanhr-build .` | `evidence/docker-build-build.txt` |
| `docker build --target migrate -t ratanhr-migrate .` | `evidence/docker-build-migrate.txt` |
| `docker build --target runtime -t ratanhr-api .` | `evidence/docker-build-runtime.txt` |
| MySQL/Redis lifecycle, health, exec, migrate, cleanup | `evidence/mysql-verification.txt` |

### Frontend and runtime

| Command | Evidence |
|---|---|
| `bun --version` | `evidence/frontend-bun-version.txt` |
| `bun install --frozen-lockfile` | `evidence/frontend-install.txt` |
| `bun run typecheck` | `evidence/frontend-typecheck.txt` |
| `bun run lint` | `evidence/frontend-lint.txt` |
| `bun run test` | `evidence/frontend-test.txt` |
| `PORT=3000 BASE_PATH=/ NODE_ENV=production bun run build:ci` | `evidence/frontend-build.txt` |
| `bunx playwright install --with-deps chromium` | `evidence/frontend-playwright-install.txt` |
| `bun run e2e` | `evidence/frontend-e2e.txt` |
| API startup and patch reachability request | `evidence/runtime-reachability.txt` |
| `mysql --version` | `evidence/mysql-version.txt` |

## 3. Build results

### Backend

* `dotnet --version`: `8.0.416`, exit 0.
* `dotnet restore HRMS.sln --locked-mode`: succeeded, exit 0.
* `dotnet build HRMS.sln -c Release --no-restore`: succeeded, exit 0.
* Compiler warnings: **1**.
* Compiler errors: **0**.

The complete warning is:

```text
HRMS.Infrastructure/Biometric/ZKTecoProvider.cs(86,28): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread.
```

### Docker build

* `spa-builder`: passed.
* `build`: failed.
* `migrate`: failed.
* `runtime`: failed.

The failing .NET Docker stages use `Dockerfile:25` and `Dockerfile:57`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0.303-alpine3.20 AS build
FROM mcr.microsoft.com/dotnet/sdk:8.0.303-alpine3.20 AS migrate
```

The captured error states that SDK `8.0.416` is requested by `/src/global.json` while only SDK `8.0.303` is installed in the image. Full output is in the three Docker build evidence files.

### Frontend

* Bun version: `1.3.6`, exit 0.
* Frozen install: passed.
* Typecheck: passed.
* Lint: passed.
* Unit tests: 5 files passed, 82 tests passed.
* Production build: passed, exit 0.

The build emitted 7 Vite/source-map warnings, all with exit 0:

```text
src/components/ui/tooltip.tsx (2:0): Error when using sourcemap for reporting an error: Can't resolve original location of error.
src/components/ui/sidebar.tsx (2:0): Error when using sourcemap for reporting an error: Can't resolve original location of error.
src/components/ui/dropdown-menu.tsx (2:0): Error when using sourcemap for reporting an error: Can't resolve original location of error.
src/components/ui/progress.tsx (2:0): Error when using sourcemap for reporting an error: Can't resolve original location of error.
src/components/ui/select.tsx (2:0): Error when using sourcemap for reporting an error: Can't resolve original location of error.
src/components/ui/label.tsx (2:0): Error when using sourcemap for reporting an error: Can't resolve original location of error.
src/components/ui/sheet.tsx (2:0): Error when using sourcemap for reporting an error: Can't resolve original location of error.
```

## 4. Complete test results and every failure

The required backend test command produced this verbatim summary:

```text
Failed!  - Failed:    51, Passed:  1091, Skipped:     1, Total:  1143, Duration: 11 s - HRMS.Tests.dll (net8.0)
```

**Totals:** 1,143 total; 1,091 passed; 51 failed; 1 skipped.

Every failure name and its complete failure message, stack trace, and source location are preserved as individual blocks in `evidence/backend-test-failures.txt`; the unmodified full test output is `evidence/dotnet-test.txt`. The 51 failures are:

1. `HRMS.Tests.PayrollServiceTests.GeneratePayslip_DuplicateMonthYear_UpdatesExisting`
2. `HRMS.Tests.Payroll.OldRegimeTdsTests.T03_MiddleIncome_20PctSlab_CorrectTds`
3. `HRMS.Tests.UploadSecurityPhase2Tests.MalwareDetected_UploadIsRejected`
4. `HRMS.Tests.UploadSecurityPhase2Tests.MalwareScannerUnavailable_UploadIsRejected_FailClosed`
5. `HRMS.Tests.BackgroundJobPhase2Tests.EmailQueue_PermanentlyFails_AfterThreeRetries`
6. `HRMS.Tests.BackgroundJobPhase2Tests.EmailQueue_AlreadySentItems_AreNotReprocessed`
7. `HRMS.Tests.CsrfCorsPhase2Tests.Csrf_InvalidToken_MutationVerbs_AreRejected(method: "DELETE")`
8. `HRMS.Tests.CsrfCorsPhase2Tests.Csrf_InvalidToken_MutationVerbs_AreRejected(method: "POST")`
9. `HRMS.Tests.CsrfCorsPhase2Tests.Csrf_InvalidToken_MutationVerbs_AreRejected(method: "PATCH")`
10. `HRMS.Tests.CsrfCorsPhase2Tests.Csrf_InvalidToken_MutationVerbs_AreRejected(method: "PUT")`
11. `HRMS.Tests.CsrfCorsPhase2Tests.Csrf_MissingToken_AuthenticatedMutation_IsRejected`
12. `HRMS.Tests.Payroll.PayrollGenerateCrossTenantTests.Generate_SuperAdmin_CrossTenantAllowed`
13. `HRMS.Tests.Payroll.PayrollGenerateCrossTenantTests.Generate_LockedPeriod_Returns409`
14. `HRMS.Tests.Security.MfaBypassHttpTests.B2_FullJwt_WithCompanyId_ProtectedEndpointReturns200`
15. `HRMS.Tests.DockerfileValidationTests.Dockerfile_Uses_Database_Update_Not_Database_Migrate`
16. `HRMS.Tests.Security.EmployeeSelfControllerIdorIntegrationTests.GetMyProfile_SameTenant_ValidEmployeeId_Returns200`
17. `HRMS.Tests.Security.EmployeeSelfControllerIdorIntegrationTests.GetMyProfile_UnauthenticatedRequest_Returns401`
18. `HRMS.Tests.Security.EmployeeSelfControllerIdorIntegrationTests.GetMyProfile_CrossTenant_Manipulated_EmployeeId_Returns404`
19. `HRMS.Tests.Security.MfaHappyPathTests.A4_FullMfaFlow_LoginVerifyTotp_ProducesAuthenticatedSession`
20. `HRMS.Tests.RoleBasedAccessTests.ProfileEndpoint_AuthenticatedUser_Returns200`
21. `HRMS.Tests.RoleBasedAccessTests.HealthEndpoint_NoToken_Returns200(path: "/healthz/live")`
22. `HRMS.Tests.Payroll.PayrollEdgeCaseTests.BulkGeneratePayslips_ExceedingRepositoryLimit_PropagatesException`
23. `HRMS.Tests.RoleBasedAccessTests.HealthEndpoint_NoToken_Returns200(path: "/healthz")`
24. `HRMS.Tests.RoleBasedAccessTests.HealthEndpoint_NoToken_Returns200(path: "/health")`
25. `HRMS.Tests.RoleBasedAccessTests.HealthEndpoint_NoToken_Returns200(path: "/healthz/ready")`
26. `HRMS.Tests.RoleBasedAccessTests.Endpoint_EmployeeToken_Returns403(method: "POST", path: "/api/companies")`
27. `HRMS.Tests.RoleBasedAccessTests.Endpoint_EmployeeToken_Returns403(method: "DELETE", path: "/api/admin-users/some-id")`
28. `HRMS.Tests.RoleBasedAccessTests.Endpoint_EmployeeToken_Returns403(method: "POST", path: "/api/departments")`
29. `HRMS.Tests.RoleBasedAccessTests.Endpoint_EmployeeToken_Returns403(method: "POST", path: "/api/payroll/generate")`
30. `HRMS.Tests.RoleBasedAccessTests.Endpoint_EmployeeToken_Returns403(method: "POST", path: "/api/admin-users")`
31. `HRMS.Tests.RoleBasedAccessTests.Endpoint_NoToken_Returns401(method: "GET", path: "/api/payroll")`
32. `HRMS.Tests.RoleBasedAccessTests.Endpoint_NoToken_Returns401(method: "GET", path: "/api/leave")`
33. `HRMS.Tests.RoleBasedAccessTests.Endpoint_NoToken_Returns401(method: "GET", path: "/api/admin-users")`
34. `HRMS.Tests.RoleBasedAccessTests.Endpoint_NoToken_Returns401(method: "GET", path: "/api/employees")`
35. `HRMS.Tests.RoleBasedAccessTests.Endpoint_NoToken_Returns401(method: "GET", path: "/api/departments")`
36. `HRMS.Tests.RoleBasedAccessTests.Endpoint_NoToken_Returns401(method: "GET", path: "/api/reports/dashboard")`
37. `HRMS.Tests.RoleBasedAccessTests.CompanyEndpoint_SuperAdminToken_Succeeds`
38. `HRMS.Tests.RoleBasedAccessTests.PayrollGenerate_HrAdminToken_ReturnsNotForbidden`
39. `HRMS.Tests.RoleBasedAccessTests.Login_RateLimited_AfterThreshold_Returns429`
40. `HRMS.Tests.RoleBasedAccessTests.Swagger_NoBasicAuth_Returns401`
41. `HRMS.Tests.Phase5PayrollAuditTests.TC07_Calculator_TDS_HighIncome_TdsIs28059`
42. `HRMS.Tests.Phase5PayrollAuditTests.TC15_Service_GeneratePayslip_SamePeriodTwice_UpsertNotDuplicate`
43. `HRMS.Tests.Infrastructure.DockerEnvironmentValidationTests.EncryptionKey_AbsentInProduction_ThrowsButNotInDevelopment`
44. `HRMS.Tests.Infrastructure.DockerEnvironmentValidationTests.JwtPublicKeyPem_PresentInProduction_DoesNotThrow`
45. `HRMS.Tests.Infrastructure.DockerEnvironmentValidationTests.LegacyEnvironmentSecretNames_AreAcceptedForCompatibility`
46. `HRMS.Tests.StartupValidationTests.Validate_MissingRequiredSecret_Throws(missingKey: "JWT_PUBLIC_KEY_PEM")`
47. `HRMS.Tests.StartupValidationTests.Validate_MissingRequiredSecret_Throws(missingKey: "JWT_PRIVATE_KEY_PEM")`
48. `HRMS.Tests.StartupValidationTests.Validate_MissingRequiredSecret_Throws(missingKey: "ENCRYPTION_KEY")`
49. `HRMS.Tests.StartupValidationTests.Validate_HangfireUseInMemory_ThrowsOutsideDevelopment`
50. `HRMS.Tests.StartupValidationTests.Validate_NoRedisConfig_DoesNotThrowInTestEnvironment`
51. `HRMS.Tests.Security.MfaHappyPathTests.A1_LoginWithMfaUser_ReturnsMfaRequiredAndTempToken`

The complete failure messages are not summarized or substituted: see the numbered blocks in `evidence/backend-test-failures.txt`. Representative failure groups, without replacing the complete inventory, are:

* DI/test-host failures for `FileStorageService`, `IDbContextFactory<ApplicationDbContext>`, and webhook `ChannelReader`/`ChannelWriter`.
* Exact-result-type assertions expecting `ObjectResult` but receiving `UnauthorizedObjectResult`, `NotFoundObjectResult`, or `ConflictObjectResult`.
* Payroll duplicate-period, locked-period, and TDS assertion failures.
* Malware scanner, MFA, and email queue failures.
* Startup validation tests expecting legacy environment-variable names or reaching `ALLOWED_HOSTS` first.
* The newly added failing-first audit test `Validate_HangfireUseInMemory_ThrowsOutsideDevelopment`, which fails because production code currently does not independently reject `Hangfire:UseInMemory=true`.

The new test is the only application-test source change made during this audit. It intentionally remains failing, as required by the uploaded Phase 1 instructions; production code was not changed to make it pass.

## 5. Complete dependency vulnerability/deprecation/outdated report

### Vulnerable packages

`dotnet list HRMS.sln package --vulnerable --include-transitive` exited 0. All five projects reported no vulnerable packages from `https://api.nuget.org/v3/index.json`. Full output: `evidence/dotnet-vulnerable.txt`.

### Deprecated packages

Full output: `evidence/dotnet-deprecated.txt`.

| Project | Package | Requested | Resolved | Reason | Alternative |
|---|---|---:|---:|---|---|
| `HRMS.API` | `FluentValidation.AspNetCore` | 11.3.0 | 11.3.0 | Legacy | — |
| `HRMS.Tests` | `xunit` | 2.9.0 | 2.9.0 | Legacy | `xunit.v3 >= 0.0.0` |

### Outdated packages

The complete untruncated output, including requested, resolved, latest, and source-not-found values, is in `evidence/dotnet-outdated.txt`. The scan exited 0 and found:

* `HRMS.Application`: 7 outdated packages.
* `HRMS.Infrastructure`: 18 outdated packages.
* `HRMS.API`: 27 outdated entries, including 3 packages whose latest value is `Not found at the sources`.
* `HRMS.Tests`: 13 outdated packages.
* `HRMS.Domain`: no updates.

The three prerelease packages reported as not found at the configured source are:

* `OpenTelemetry.Exporter.Prometheus.AspNetCore` — requested/resolved `1.17.0-beta.1`.
* `OpenTelemetry.Instrumentation.EntityFrameworkCore` — requested/resolved `1.17.0-beta.1`.
* `OpenTelemetry.Instrumentation.StackExchangeRedis` — requested/resolved `1.17.0-beta.1`.

Notable requested → resolved → latest entries include:

```text
Microsoft.AspNetCore.Authentication.JwtBearer  8.0.6  → 8.0.6  → 10.0.10
Microsoft.IdentityModel.Tokens                   8.14.0 → 8.14.0 → 8.22.0
Pomelo.EntityFrameworkCore.MySql                 8.0.2  → 8.0.2  → 9.0.0
Hangfire.AspNetCore                               1.8.14 → 1.8.14 → 1.8.24
Hangfire.Redis.StackExchange                     1.9.3  → 1.9.3  → 1.12.0
Sentry.AspNetCore                                 5.0.0  → 5.0.0  → 6.8.0
Swashbuckle.AspNetCore                            6.7.3  → 6.7.3  → 10.2.3
Serilog.AspNetCore                                8.0.1  → 8.0.1  → 10.0.0
Microsoft.Extensions.Http.Resilience              8.9.1  → 8.9.1  → 10.8.0
coverlet.collector                                6.0.2  → 6.0.2  → 10.0.1
Microsoft.NET.Test.Sdk                            17.11.1 → 17.11.1 → 18.8.1
```

## 6. Migration and schema verification

### Repository-static facts

* `HRMS.Infrastructure/Migrations/MySql/` contains **15 migration classes**.
* There are **20 `.cs` files** in that directory when designer files and `ApplicationDbContextModelSnapshot_MySql.cs` are included.
* `ApplicationDbContextModelSnapshot_MySql.cs` exists.
* `db_indexes_fix.sql`, `db_performance.sql`, and `db_softdelete_fix.sql` exist and are mounted by `docker-compose.prod.yml` lines 98–100.
* `docker-compose.prod.yml` documents the supplementary SQL execution through the migrate service. This is source evidence, not proof of application in a live database.

### Runtime attempts

* Host `mysql --version`: command not found, exit 127. No MySQL module was available.
* `dotnet tool restore`: passed.
* `dotnet ef migrations list`: failed to connect to MySQL and also reported model changes since the last migration.
* A direct MySQL-connection migration list failed because no live MySQL server was reachable.
* `dotnet ef database update` failed for the same unavailable-database reason.
* Compose MySQL and Redis containers were started with disposable values. MySQL became unhealthy before migration; MySQL exec returned `ERROR 2002 (HY000): Can't connect to local MySQL server through socket`, and Redis exec returned the environment error `OCI runtime exec failed ... unknown`.
* No `__EFMigrationsHistory` query succeeded.
* No live comparison between EF migration output and database history succeeded.
* No live model-drift or snapshot-against-database check succeeded.
* No claim is made that any migration or supplementary SQL file was applied.

Full runtime output is in `evidence/mysql-verification.txt`, `evidence/ef-migrations.txt`, `evidence/ef-migrations-mysql.txt`, and `evidence/migration-update.txt`.

## 7. Docker and Compose results

| Command/file | Result | Evidence |
|---|---|---|
| `docker --version` | Pass — 27.5.1 | `docker-version.txt` |
| `docker compose version` | Pass — 2.36.0 | `docker-compose-version.txt` |
| `docker-compose.yml config` | Pass with disposable values | `docker-compose-config.txt` |
| `docker-compose.prod.yml config` | Pass with disposable values | `docker-compose-prod-config.txt` |
| `docker-compose.e2e.yml config` | Pass with disposable values | `docker-compose-e2e-config.txt` |
| `docker-compose.override.yml config` alone | Fail — override fragment has no base image/build context | `docker-compose-override-config.txt` |
| `docker-compose.replica.yml config` alone | Fail — empty compose file | `docker-compose-replica-config.txt` |
| `docker-compose.backup.yml config` alone | Fail — required AWS variables | `docker-compose-backup-config.txt` |
| `spa-builder` target | Pass | `docker-build-spa-builder.txt` |
| `build` target | Fail — SDK 8.0.303 vs required 8.0.416 | `docker-build-build.txt` |
| `migrate` target | Fail — inherited SDK mismatch | `docker-build-migrate.txt` |
| `runtime` target | Fail — inherited SDK mismatch | `docker-build-runtime.txt` |

No Dockerfile, Compose file, image tag, or production configuration was changed during this audit.

## 8. Frontend and E2E results

Frontend checks passed:

* `bun install --frozen-lockfile`
* `bun run typecheck`
* `bun run lint`
* `bun run test` — 82/82 tests passed
* `PORT=3000 BASE_PATH=/ NODE_ENV=production bun run build:ci`

E2E was not completed:

* `bunx playwright install --with-deps chromium` exited 1 because the environment rejects the package-manager/browser installation path and directs system dependency installation elsewhere.
* `bun run e2e` exited 1 before tests because `HRMS.SPA.Source/.env.e2e` is absent.
* No real staging credentials were requested, created, printed, or committed.

The exact Playwright and E2E errors are in `evidence/frontend-playwright-install.txt` and `evidence/frontend-e2e.txt`.

## 9. Blocker #7 legacy UI verdict with file:line evidence

**Final verdict: LIVE** — based on static application and deployment evidence. Runtime confirmation against the RatanHR API was blocked by application startup failure.

### ASP.NET API behavior

* `HRMS.API/Program.cs:492–493` registers `HtmlNonceInjectionMiddleware` and `CspNonceMiddleware`.
* `HRMS.API/Program.cs:551` calls `app.UseStaticFiles()`.
* `HRMS.API/Program.cs:652` maps `/` to `Results.Redirect("/login.html")`.
* No `UseDefaultFiles` call was found.
* No `MapFallback` call was found.
* The root route therefore explicitly selects the legacy `login.html`; it is not an inferred SPA fallback.

### Docker copy order and coexistence

* `Dockerfile:80` copies the API publish output, including the existing API `wwwroot`.
* `Dockerfile:81` overlays React output into `./wwwroot`.
* The overlay does not delete non-colliding legacy HTML or `includes/` files. Legacy HTML and React output therefore coexist.

### Nginx

* Production `nginx/nginx.conf:195–201` proxies `/api/` and auth endpoints to the API.
* Production `nginx/nginx.conf:236–242` uses `try_files $uri @spa_fallback`; the named fallback proxies to `hrms_api`.
* Staging `HRMS.SPA/nginx.staging.conf:9` uses `try_files $uri $uri/ /index.html`, which is SPA routing and differs from production.
* The production Compose layout does not mount React output into Nginx's `/usr/share/nginx/html`; the API fallback is therefore the effective application route for the documented layout.

### Legacy auth, CSRF, and CSP

* Legacy pages use `HRMS.API/wwwroot/js/api.js` for credentialed requests, XSRF cookie seeding, and `X-XSRF-TOKEN` mutation headers.
* Some legacy pages contain older direct `fetch` and local/session storage token logic.
* `Program.cs:492–493` provides CSP nonce middleware coverage before `UseStaticFiles`.
* Runtime HTTP verification of auth, CSRF, and nonce injection was not possible because the API failed DI validation before binding.

## 10. EnvironmentValidator findings and test coverage

Static validator behavior is in `HRMS.API/Security/EnvironmentValidator.cs`:

| Requested case | Static behavior | Existing/new coverage |
|---|---|---|
| `AllowedHosts="*"` | Rejected outside Development at lines 49–52 and 84–89 | Covered by `StartupValidationTests` and `Phase6SecurityAuditTests`; relevant tests still have unrelated setup failures in the full run |
| `Hangfire:UseInMemory=true` outside Development | **Not independently rejected**; validator checks `Hangfire:UseRedis` at lines 144–155 | Added failing-first `Validate_HangfireUseInMemory_ThrowsOutsideDevelopment`; it fails with “No exception was thrown” |
| Empty `Jwt:PrivateKeyPem` | Rejected by `RequireNonEmpty` at lines 41 and 171–182 | Covered; some legacy-name assertion cases fail because the message reports the hierarchical key |
| Empty `Jwt:PublicKeyPem` | Rejected by `RequireNonEmpty` at line 42 | Covered through startup/config tests; full test run contains related failures |
| Empty `Security:EncryptionKey` | Rejected by `RequireNonEmpty` at line 43 | Covered; full test run contains related failures |
| Missing Redis configuration | Rejected outside Development/Test when `Hangfire:UseRedis` is missing/false or its connection string is empty at lines 144–166 | Production missing/empty Redis cases exist; Test case currently fails first on missing `ALLOWED_HOSTS` |
| Production vs Development | Host and Redis checks are skipped in Development; Redis is also skipped for explicit Test/IntegrationTest at lines 49–58 and 191–197 | Covered in `StartupValidationTests`; the Test fixture is inconsistent with current `ALLOWED_HOSTS` behavior |

The only source change made for this coverage audit is the intentionally failing-first test in `HRMS.Tests/StartupValidationTests.cs`. Production behavior was not changed.

## 11. `sidebar-admin.html.patch` reachability

### Static evidence

* File exists at `HRMS.API/wwwroot/includes/sidebar-admin.html.patch`.
* Size is 706 bytes.
* It contains patch instructions and is not an HTML page.
* `app.UseStaticFiles()` is enabled.
* No static deny rule for `includes/` was found.
* Build static-asset metadata includes `includes/sidebar-admin.html.patch`.

### Runtime request

The exact request attempted through the available local proxy was:

```text
GET http://localhost:80/api/includes/sidebar-admin.html.patch
```

Observed response:

```text
HTTP/1.1 404 Not Found
Content-Type: text/html; charset=utf-8
Content-Length: 176
Response size: 176 bytes
Patch contents returned: no
```

This request reached the workspace proxy/API service rather than a running RatanHR API. It is not valid runtime proof that the RatanHR static file is unreachable. The RatanHR API could not start because of the DI errors recorded in `evidence/runtime-reachability.txt`. Therefore the final finding is: **static evidence indicates the file is web-reachable if the RatanHR API serves this `wwwroot`; runtime confirmation remains blocked**.

## 12. CI workflow status and proposed diff

`.github/workflows/` does not exist. No CI workflow was created.

The following is a proposed diff only and requires owner approval. It intentionally does not enable E2E until staging configuration and browser installation are available:

```diff
diff --git a/.github/workflows/validation.yml b/.github/workflows/validation.yml
new file mode 100644
--- /dev/null
+++ b/.github/workflows/validation.yml
@@
+name: validation
+
+on:
+  pull_request:
+  push:
+    branches: [main]
+
+jobs:
+  validate:
+    runs-on: ubuntu-latest
+    steps:
+      - uses: actions/checkout@v4
+      - uses: actions/setup-dotnet@v4
+        with:
+          dotnet-version: 8.0.416
+      - uses: oven-sh/setup-bun@v2
+        with:
+          bun-version: 1.3.6
+      - run: dotnet restore HRMS.sln --locked-mode
+      - run: dotnet build HRMS.sln -c Release --no-restore
+      - run: dotnet test HRMS.sln -c Release --no-build --settings coverlet.runsettings
+      - working-directory: HRMS.SPA.Source
+        run: |
+          bun install --frozen-lockfile
+          bun run typecheck
+          bun run lint
+          bun run test
+          PORT=3000 BASE_PATH=/ NODE_ENV=production bun run build:ci
+      - run: semgrep scan --config auto --error --exclude-from=.semgrepignore
+      - run: dotnet list HRMS.sln package --vulnerable --include-transitive
+      - run: bash scripts/verify-docker-digests.sh
+      - run: docker build --target spa-builder -t ratanhr-spa .
+      - run: docker build --target build -t ratanhr-build .
```

## 13. Candidate-only deletion list

No files were deleted. Candidates only:

1. `HRMS.API/wwwroot/includes/sidebar-admin.html.patch` — patch instructions under a static web root; remove only after owner confirms the deferred biometric navigation decision.
2. Legacy `HRMS.API/wwwroot/*.html` files — **do not delete yet**; `/login.html` is the selected root entry point and other legacy pages reference one another.
3. `docker-compose.replica.yml` — empty standalone file; provenance is unavailable.
4. Historical SQL/documentation names referenced by `SUPPLEMENTARY_SQL_EXECUTION_ORDER.md` but absent from the current archive — absence cannot be distinguished from completed cleanup without Git history.

## 14. Anything that could not be installed or run

* MySQL client: `mysql` command not found; no MySQL module was available.
* Live MySQL migration verification: blocked by unavailable/unhealthy MySQL container and unavailable container exec.
* `__EFMigrationsHistory` query: not run successfully.
* EF migration-to-database comparison: not possible without live database.
* Migration update: failed to connect to the database.
* RatanHR API runtime: failed before binding because DI could not resolve `FileStorageService`, `IDbContextFactory<ApplicationDbContext>`, `ChannelWriter<WebhookJob>`, and `ChannelReader<WebhookJob>`.
* Patch HTTP confirmation against the RatanHR API: blocked by the API startup failure; the proxy 404 is not treated as RatanHR runtime evidence.
* Playwright browser installation: blocked by the environment's package-installation policy.
* Frontend E2E: blocked by missing non-committed `.env.e2e`; no real credentials were requested or created.
* Git provenance: impossible because the archive contains no `.git` directory.
* CI workflow: not created, per the audit instruction.

## 15. Final Phase 1 status

The evidence package is complete for all commands and checks that could run in this environment. The phase remains partially cleared because backend tests fail, Docker .NET targets do not match the pinned SDK, live MySQL/migration verification is unavailable, provenance is absent, E2E is blocked, and CI is not present.

PHASE 1 BLOCKER STATUS:
PARTIALLY CLEARED

REMAINING BLOCKERS:
- 51 backend test failures, including the intentionally failing-first `Hangfire:UseInMemory` validator coverage test.
- Docker .NET image SDK mismatch: 8.0.303 versus `global.json` 8.0.416.
- MySQL client/live database verification is unavailable; MySQL/Redis container execution is unreliable in this environment.
- Live migration/history/snapshot verification is incomplete.
- No Git history is present in the archive.
- Playwright E2E requires browser installation and a non-committed `.env.e2e`.
- No CI workflow exists; the workflow above is only a proposed diff.
- EF tooling reports model changes since the last migration.
- `sidebar-admin.html.patch` remains a static disclosure candidate pending a running RatanHR HTTP confirmation and owner decision.

READY FOR PHASE 2 (Build & Dependency Audit): NO