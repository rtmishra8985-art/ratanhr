# Upload Validation Coverage — solution-wide sweep (2026-08-12, updated)

## Current verification gate status — 2026-08-12

**Status: PASS.** The upload endpoint integration matrix, authorization runtime audit, and full
test suite executed successfully against the current source tree. No upload test failed, and the
rejected-file tests verified that rejected files were not persisted.

The Nix environment's normal SDK wrapper supplied VSTest with a malformed child `dotnet` path.
The tests were launched with a temporary non-store .NET host root and
`--no-build --no-restore`; this changes only the test launcher environment, not application or
test behavior.

| Check | Result | Evidence |
|---|---|---|
| `UploadEndpointIntegrationTestsRemaining` | **PASS** | 24 passed, 0 failed, 0 skipped; `evidence/upload-audit-run/upload-filter-2026-08-12.txt`. |
| `AuthorizationEndpointRuntimeAuditTests` | **PASS** | 1 passed, 0 failed, 0 skipped; `evidence/upload-audit-run/authorization-filter-2026-08-12.txt`. |
| Full `dotnet test HRMS.Tests` | **PASS** | 1,239 passed, 0 failed, 1 skipped, 1,240 total; `evidence/upload-audit-run/full-suite-2026-08-12.txt`. |
| Solution build | **PASS** | Existing verification completed with 0 warnings and 0 errors before the no-build runtime runs. |

Raw terminal result:

```text
Test Run Successful.
Total tests: 24
     Passed: 24

Test Run Successful.
Total tests: 1240
     Passed: 1239
    Skipped: 1
```

The one skipped test is the intentional live Swagger parity check:
`SwaggerParityTests.LiveSwagger_MatchesControllerApiExplorerInventory`. It requires
`HRMS_SWAGGER_BASE_URL` to point to a running API and is not an upload-validation failure.

## Historical 2026-08-11 blocked assessment (superseded)

The following source-analysis-only assessment is retained for audit history. Its
`BLOCKED`/`NOT EXECUTED` labels describe the 2026-08-11 environment and do not describe the
current verification status.

This update was produced in a sandboxed environment with no `dotnet`, `mysql`, or `docker`
binary, and an egress allow-list that does not include any Microsoft .NET distribution domain
(`dot.net`, `dotnetcli.azureedge.net`, `download.visualstudio.microsoft.com` all returned
`403 host_not_allowed`). None of the required commands could be run at that time.

Everything below the historical heading reflects the prior source-level analysis and edits.

Every `IFormFile` that can enter the solution was enumerated (`rg -l "IFormFile"` across all
projects, plus all `[FromForm]` / `Request.Form.Files` / minimal-API endpoints) and mapped to an
`UploadProfile`. A signature/extension/MIME/size mismatch returns **HTTP 400** with the validator's
message — either directly from the controller, or via `UploadValidationException` →
`ExceptionMiddleware` → 400.

## Coverage matrix

| Endpoint / entry point | File(s) | Profile | Enforcement | Status |
|---|---|---|---|---|
| `POST /api/attendance/upload-excel` | `AttendanceController` | `Spreadsheet` | Controller gate | Already done |
| `POST /api/profile/photo` | `ProfileController` → `AuthService` | `Image` | Controller gate + `FileStorageService` | Already done |
| `POST /api/logo/{companyId}` | `LogoController` → `CompanyService` | `Image` | Controller gate + `FileStorageService` | Already done |
| `POST /api/employee-documents` | `EmployeeDocumentController` → `EmployeeDocumentService` | `Document` | Controller gate + `FileStorageService` | Already done |
| `POST /api/companies/{id}/logo` | `CompanyController.UploadLogo` | `Image` | Controller gate + `FileStorageService` | **Fixed this pass** — replaced ad-hoc 2 MB + Content-Type + extension checks; **SVG dropped** (stored-XSS vector when served inline) |
| `POST /api/appreciation` | `AppreciationController.Upload` → `AppreciationService` | `Image` (optional file) | Controller gate + `FileStorageService` | **Fixed this pass** — no magic-byte gate before; DTO allow-list narrowed to match |
| `POST /api/expenses` (line-item receipts) | `ExpenseController.Create` → `ExpenseService` | `Document` (optional) | Controller gate + explicit profile on `SaveAsync` | **Fixed this pass** |
| `POST /api/expenses/legacy` | `ExpenseController.SubmitLegacy` → `ExpenseService` | `Document` (optional) | Controller gate + explicit profile on `SaveAsync` | **Fixed this pass** |
| `POST/PUT /api/recruitment/candidates` | `RecruitmentController` | `Resume` | Controller `try/catch` → 400 | Already done |
| `POST/PUT /api/employees` (`Request.Form.Files`) | `EmployeeController`, `EmployeeSelfController` → `EmployeeService` | `Document` / `Image` per field | `FileStorageService` (explicit profile per field) | Already done |

### Entry points confirmed to need no change

* **Minimal APIs** — `Program.cs` exposes only `GET /api/auth/csrf` and `GET /`; neither accepts a body.
* **Biometric** (`/Biometric`) and **SPA** (`HRMS.SPA`, `HRMS.SPA.Source`) — no `IFormFile` anywhere;
  the SPA posts multipart to the API controllers above, which are all gated server-side.
* **Import controllers** — the only bulk import is the attendance Excel upload, already covered.
* `AntiVirusScanFilter` — takes `IFormFile` but is a defence-in-depth scan filter, not a bypass path.

## Dead code removed

* `HRMS.Infrastructure.Security.MimeValidator` (in `FileUploadOptions.cs`) — **deleted**. It had zero
  remaining callers and failed *open* (`unknown MIME → allow through`). All paths now use
  `UploadValidator`, which fails closed. `rg "MimeValidator|IsValidMime|MatchesAnySignature"` returns
  only explanatory comments.
* Ad-hoc MIME/size/extension checks in `CompanyController.UploadLogo` — removed with the fix above.
  No other hand-rolled `ContentType` / `file.Length >` checks remain in any controller.

> **This pass's changes were reasoned through by reading the exact source of every code path
> touched (controller → service → UploadValidator/FileStorageService → EF query translation),
> not compiled or executed.** Status stays BLOCKED. Next step: run the exact commands below on a
> machine with a real .NET 8 SDK (Claude Code pointed at your local checkout is the established
> path for this) and update this file's status to PASS only if they all succeed:
>
> ```
> dotnet restore HRMS.sln
> dotnet build HRMS.sln -warnaserror:CS0168,CS0219,CS8019
> dotnet test HRMS.Tests
> dotnet test HRMS.Tests --filter "FullyQualifiedName~UploadEndpointIntegrationTests"
> dotnet test HRMS.Tests --filter "FullyQualifiedName~N1RegressionTests"
> dotnet test HRMS.Tests --filter "FullyQualifiedName~PayrollAtomicityTests"
> dotnet test HRMS.Tests --filter "FullyQualifiedName~UploadSizeLimitTests"
> ```
