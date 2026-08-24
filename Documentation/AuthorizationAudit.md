# Authorization Audit — HRMS.API/Controllers

_Generated 2026-08-11 as part of the RatanHR security audit (item 9 / authorization sweep)._
_Updated 2026-08-12 with runtime evidence from the current source tree._

## 2026-08-12 runtime verification — PASS

**Status: PASS.** The runtime endpoint metadata audit executed successfully against the current
source tree. It found no anonymous endpoint outside the exact approved allow-list, and the
full test suite completed with no failures.

The Nix environment's normal SDK wrapper supplied VSTest with a malformed child `dotnet` path.
Tests were therefore launched with a temporary non-store .NET host root and
`--no-build --no-restore`; this changes only the test launcher environment, not application or
test behavior.

| Check | Result | Evidence |
|---|---|---|
| `AuthorizationEndpointRuntimeAuditTests` | **PASS** | 1 passed, 0 failed, 0 skipped; `evidence/upload-audit-run/authorization-filter-2026-08-12.txt`. |
| Full `dotnet test HRMS.Tests` | **PASS** | 1,239 passed, 0 failed, 1 skipped, 1,240 total; `evidence/upload-audit-run/full-suite-2026-08-12.txt`. |
| Solution build | **PASS** | Existing verification completed with 0 warnings and 0 errors before the no-build runtime runs. |

Raw terminal result:

```text
Test Run Successful.
Total tests: 1
     Passed: 1

Test Run Successful.
Total tests: 1240
     Passed: 1239
    Skipped: 1
```

The one skipped test is the intentional live Swagger parity check, which requires
`HRMS_SWAGGER_BASE_URL` to point to a running API. The test report identifies it as
`SwaggerParityTests.LiveSwagger_MatchesControllerApiExplorerInventory`; it is not a failed
authorization test.

## Historical 2026-08-11 blocked assessment (superseded)

This pass ran in a sandboxed environment with no `dotnet` binary and an egress allow-list that
excludes every Microsoft .NET distribution domain (confirmed via direct `curl`, each returning
`403 host_not_allowed`). None of the commands in the table below (`dotnet restore`, `dotnet build`,
`dotnet test`) were re-executed. No authorization logic (`[Authorize]` attributes, policies,
fallback policy, IDOR guards) was touched in this pass — the changes made were:

1. **New file** `HRMS.Tests/Security/UploadEndpointIntegrationTests.cs` — live multipart HTTP
   tests for `CompanyController.UploadLogo` (including the required SVG-rejection/no-persist
   regression), `LogoController.Upload`, `ProfileController.UploadPicture`, and
   `AttendanceController.UploadExcel`. These tests assert `401`/`403`/`404` on the existing
   authorization/IDOR guards as part of exercising the full HTTP pipeline, but do not add,
   remove, or weaken any `[Authorize]` attribute, the `RequireMfaCompleted` policy, or the
   global fallback policy (`SetFallbackPolicy(... .RequireAuthenticatedUser())` in `Program.cs`,
   unchanged).
2. `HRMS.Infrastructure/Services/PayrollService.cs` — two SQLite-decimal-`Sum`-translation fixes
   (client-side sum after a single bulk fetch, still one query, no N+1). No authorization impact.
3. `HRMS.Tests/UploadSizeLimitTests.cs` — one test's setup corrected to actually exercise the
   30 MB scenario its name and assertion describe. No production code change, no authorization
   impact.

**None of this was compiled in the 2026-08-11 pass.** The runtime authorization endpoint audit
was not re-run at that time. This historical blocked status is superseded by the
2026-08-12 runtime result above.

The action required by that historical pass has now been completed; the current evidence above
must be used instead of the historical numbers below.

## Historical verification gate status (superseded by 2026-08-12 evidence)

**Historical status: BLOCKED** (authorization verification passed after two audit-caused
regressions were corrected; mandatory endpoint-level upload verification was incomplete at that
time).

Verification executed on 2026-08-11 with .NET SDK **8.0.416** on Linux:

| Check | Result | Evidence |
|---|---|---|
| `dotnet restore HRMS.sln` | **VERIFIED** | Exit code 0; all five projects restored. |
| `dotnet build HRMS.sln -warnaserror:CS0168,CS0219,CS8019` | **VERIFIED** | Exit code 0; 0 warnings, 0 errors. |
| `dotnet test HRMS.Tests` | **FAILED** | Exit code 1; 1,191 passed, 4 failed, 1 skipped, 1,196 total. |
| `PasswordPolicyTests` | **VERIFIED** | 29 passed, 0 failed, 0 skipped. |
| `UploadValidatorTests` | **VERIFIED** | 23 passed, 0 failed, 0 skipped. |
| Upload validation integration group | **VERIFIED** | 3 passed, 0 failed, 0 skipped. |
| Runtime authorization / fallback / endpoint metadata audit | **FAILED, then corrected** | The first run found `/metrics` and `/api/auth/csrf` outside the exact anonymous allow-list; this pass removes their anonymous metadata. MFA verify correctly combines controller authorization metadata with an action-level anonymous override. |
| Runtime role/health/MFA security groups | **VERIFIED** | RoleBasedAccessTests: 20 passed; HealthCheckIntegrationTests: 14 passed; MFA groups: 4 passed. |
| SVG rejection | **BLOCKED** | No endpoint-level multipart regression test exists for `CompanyController.UploadLogo`; helper coverage rejects `.svg`, but persistence/HTTP 400/no-persist evidence was not executed. |

The four full-suite failures are **PRE-EXISTING / unrelated to this authorization audit** in the tested source state: three SQLite decimal-aggregate/payroll test incompatibilities and one upload-size assertion expecting a message value that the current profile path does not include. They remain visible and prevent a PASS classification.

### Environment

| Item | Verification environment |
|---|---|
| OS | Linux x64 |
| .NET SDK/runtime | SDK 8.0.416; .NET 8 runtime |
| Database | No live database used; runtime test fixtures replace the application database with EF Core in-memory storage. |
| Redis | No live Redis used; runtime test fixtures replace distributed services for the executed security groups. |

## Method

Every `.cs` file under `HRMS.API/Controllers` was parsed and every public action carrying an
`[Http*]` verb attribute was enumerated — **363 actions across 56 controllers**. For each action the
effective authorization is the *most specific* of: the action attribute, the controller attribute,
and the global fallback policy.

## Runtime endpoint evidence

The runtime test enumerated `EndpointDataSource` rather than relying on a controller method list. The initial execution exposed two audit-caused anonymous endpoints:

| Endpoint | HTTP Method | Anonymous | Authorization Metadata | Rate Limiter | Allow-listed | Result |
|---|---|---:|---:|---|---:|---|
| `/api/auth/csrf` | GET | True | False | api | False | **FAILED, corrected** |
| `/metrics` | GET | True | False | api | False | **FAILED, corrected** |
| `/api/auth/login` | POST | True | False | login | True | PASS |
| `/api/auth/refresh` | POST | True | False | sensitive | True | PASS |
| `/api/auth/logout` | POST | True | False | login | True | PASS |
| `/api/auth/forgot-password` | POST | True | False | login | True | PASS |
| `/api/auth/reset-password` | POST | True | False | sensitive | True | PASS |
| `/api/auth/mfa/verify` | POST | True | True (controller metadata; action override) | sensitive | True | PASS |
| `/health`, `/healthz`, `/healthz/ready`, `/healthz/live` | GET / * | True | False | api | True | PASS |

The runtime test also verified that the configured fallback policy requires an authenticated user and that `GET /api/employees` without a token returns **401 Unauthorized**.

## Baseline: the global fallback policy

`Program.cs` sets a fallback policy requiring an authenticated user, so **an action with no
`[Authorize]` and no `[AllowAnonymous]` anywhere in its chain is still protected** — it is
*deny-by-default*, not open. Consequently "no attribute" is recorded below as **Inherited** and is
justified whenever the controller carries a class-level `[Authorize]`, or (absent that) whenever the
fallback alone is the intended protection level.

Two class-level policies are in use:

| Attribute | Meaning |
|---|---|
| `[Authorize]` | Any authenticated user. |
| `[Authorize(Policy = "RequireMfaCompleted")]` | Authenticated **and** the `mfa` claim proves the TOTP step completed. |
| `[Authorize(Roles = ...)]` | Role-gated on top of authentication (`AppRoles` constants). |

## Anonymous endpoint allow-list

The agreed allow-list is exactly: **login, refresh, forgot/reset password, MFA challenge, health**.
The audit found **6 `[AllowAnonymous]` actions**, all inside
`AuthController` / `MfaController`, and **no `[AllowAnonymous]` at controller-class level anywhere**.
Every one falls inside the allow-list; **no unjustified `[AllowAnonymous]` was found in a controller.**

| Endpoint | Route | Rate limiter | Justified? |
|---|---|---|---|
| `AuthController.Login` | `POST /api/auth/login` | `login` — 10 req/min per IP | Yes — credential entry point; no session can exist yet. |
| `AuthController.Refresh` | `POST /api/auth/refresh` | `sensitive` — 5 req/min per IP | Yes — runs precisely when the access token has expired. Refresh token is read from the HttpOnly cookie only (no body fallback). |
| `AuthController.Logout` | `POST /api/auth/logout` | `login` — 10 req/min per IP (**added by this audit**; was inheriting the 120/min `api` default) | Yes — must work after token expiry so stale cookies can be cleared. Acts only on the caller’s own cookie; constant response. |
| `AuthController.ForgotPassword` | `POST /api/auth/forgot-password` | `login` — 10 req/min per IP | Yes — the user cannot authenticate by definition. Response is deliberately non-enumerable. |
| `AuthController.ResetPassword` | `POST /api/auth/reset-password` | `sensitive` — 5 req/min per IP | Yes — authorised by the one-time emailed token, not by a session. |
| `MfaController.Verify` | `POST /api/auth/mfa/verify` | `sensitive` — 5 req/min per IP | Yes — MFA challenge; the caller holds only a temp token, not a full JWT. |

### Non-controller endpoint policy (`Program.cs`)

| Endpoint | Rate limiter | Justified? |
|---|---|---|
| `GET /metrics` (Prometheus) | `api` — 120 req/min per IP | **Protected by fallback policy**; internal network restriction remains defence-in-depth. |
| `GET /health`, `/healthz`, `/healthz/ready`, `/healthz/live` | `api` — 120 req/min per IP | Yes — on the agreed allow-list. K8s/LB probes are unauthenticated by design. |
| `GET /api/auth/csrf` | `api` — 120 req/min per IP | **Protected by fallback policy**; callers obtain the token after authentication. |
| `GET /` | `api` (via default) | Protected by fallback policy. |

> Note on limiter precedence: `MapControllers()` attaches `api` **only** where the endpoint declares
> no policy of its own, so `[EnableRateLimiting("login"/"sensitive")]` on the anonymous auth actions
> is not silently overridden.

## Actions taken

1. **`AuthController.Logout`** — kept `[AllowAnonymous]` (scrutinised in detail; see the rationale
   comment now in the source) but added `[EnableRateLimiting("login")]`. It was the only anonymous
   endpoint relying on the permissive 120 req/min `api` default.
2. No other authorization attribute was added, removed or weakened: every remaining action is either
   explicitly `[Authorize...]`-decorated or protected by the class-level attribute / global fallback.

## Full action inventory


### `AssetsController`

`HRMS.API/Controllers/AssetsController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| AssetsController | `GetAssets` | `GET /api/assets` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AssetsController | `GetAsset` | `GET /api/assets/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AssetsController | `CreateAsset` | `POST /api/assets` | `[Authorize(Roles = AppRoles.HrAdminAndAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AssetsController | `UpdateAsset` | `PUT /api/assets/{id:int}` | `[Authorize(Roles = AppRoles.HrAdminAndAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AssetsController | `DeleteAsset` | `DELETE /api/assets/{id:int}` | `[Authorize(Roles = AppRoles.HrAdminAndAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AssetsController | `AssignAsset` | `POST /api/assets/{id:int}/assign` | `[Authorize(Roles = AppRoles.HrAdminAndAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AssetsController | `ReturnAsset` | `POST /api/assets/{id:int}/return` | `[Authorize(Roles = AppRoles.HrAdminAndAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AssetsController | `GetAssetHistory` | `GET /api/assets/{id:int}/history` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AssetsController | `GetSummary` | `GET /api/assets/summary` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AssetsController | `GetCategories` | `GET /api/assets/categories` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AssetsController | `CreateCategory` | `POST /api/assets/categories` | `[Authorize(Roles = AppRoles.HrAdminAndAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `AuthController`

`HRMS.API/Controllers/Authentication/AuthController.cs` — controller-level: (none — global fallback applies)

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| AuthController | `Login` | `POST /api/auth/login` | `[AllowAnonymous]` | Yes — credential entry point; no session can exist yet. | None — verified against the allow-list. |
| AuthController | `Refresh` | `POST /api/auth/refresh` | `[AllowAnonymous]` | Yes — runs precisely when the access token has expired. Refresh token is read from the HttpOnly cookie only (no body fallback). | None — verified against the allow-list. |
| AuthController | `Logout` | `POST /api/auth/logout` | `[AllowAnonymous]` | Yes — must work after token expiry so stale cookies can be cleared. Acts only on the caller’s own cookie; constant response. | Added `[EnableRateLimiting("login")]` + rationale comment. |
| AuthController | `ForgotPassword` | `POST /api/auth/forgot-password` | `[AllowAnonymous]` | Yes — the user cannot authenticate by definition. Response is deliberately non-enumerable. | None — verified against the allow-list. |
| AuthController | `ResetPassword` | `POST /api/auth/reset-password` | `[AllowAnonymous]` | Yes — authorised by the one-time emailed token, not by a session. | None — verified against the allow-list. |
| AuthController | `ChangePassword` | `POST /api/auth/change-password` | `[Authorize]` | Yes — explicit gate, narrower than the controller default. | None. |

### `MfaController`

`HRMS.API/Controllers/Authentication/MfaController.cs` — controller-level: `[Authorize]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| MfaController | `Setup` | `POST /api/auth/mfa/setup` | `[Authorize]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| MfaController | `Confirm` | `POST /api/auth/mfa/confirm` | `[Authorize]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| MfaController | `Verify` | `POST /api/auth/mfa/verify` | `[AllowAnonymous]` | Yes — MFA challenge; the caller holds only a temp token, not a full JWT. | None — verified against the allow-list. |
| MfaController | `Disable` | `DELETE /api/auth/mfa` | `[Authorize]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `ProfileController`

`HRMS.API/Controllers/Authentication/ProfileController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| ProfileController | `GetProfile` | `GET /api/profile` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| ProfileController | `UpdateProfile` | `PUT /api/profile` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| ProfileController | `UploadPicture` | `POST /api/profile/picture` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `PerformanceController`

`HRMS.API/Controllers/Performance/PerformanceController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| PerformanceController | `GetDashboard` | `GET /api/performance/dashboard` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `ListCycles` | `GET /api/performance/cycles` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `CreateCycle` | `POST /api/performance/cycles` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `UpdateCycle` | `PUT /api/performance/cycles/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `DeleteCycle` | `DELETE /api/performance/cycles/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `ListGoals` | `GET /api/performance/goals` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `MyGoals` | `GET /api/performance/goals/my` | `[Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `CreateGoal` | `POST /api/performance/goals` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `UpdateGoal` | `PUT /api/performance/goals/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `UpdateGoalProgress` | `PATCH /api/performance/goals/{id:int}/progress` | `[Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `DeleteGoal` | `DELETE /api/performance/goals/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `ListReviews` | `GET /api/performance/reviews` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `MyReviews` | `GET /api/performance/reviews/my` | `[Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `GetReview` | `GET /api/performance/reviews/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `CreateReview` | `POST /api/performance/reviews` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `SubmitSelfReview` | `POST /api/performance/reviews/{id:int}/self` | `[Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `SubmitManagerReview` | `POST /api/performance/reviews/{id:int}/manager` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `FinalizeReview` | `POST /api/performance/reviews/{id:int}/finalize` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `ListFeedback` | `GET /api/performance/feedback` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `MyFeedback` | `GET /api/performance/feedback/my` | `[Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PerformanceController | `SubmitFeedback` | `POST /api/performance/feedback` | `[Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `CompanyBranchController`

`HRMS.API/Controllers/Companies/CompanyBranchController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| CompanyBranchController | `GetAll` | `GET /api/companies/{companyId:int}/branches` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| CompanyBranchController | `GetById` | `GET /api/companies/{companyId:int}/branches/{branchId:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| CompanyBranchController | `Create` | `POST /api/companies/{companyId:int}/branches` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| CompanyBranchController | `Update` | `PUT /api/companies/{companyId:int}/branches/{branchId:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| CompanyBranchController | `Delete` | `DELETE /api/companies/{companyId:int}/branches/{branchId:int}` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `CompanyController`

`HRMS.API/Controllers/Companies/CompanyController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| CompanyController | `Create` | `POST /api/companies` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| CompanyController | `GetAll` | `GET /api/companies` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| CompanyController | `GetById` | `GET /api/companies/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| CompanyController | `Update` | `PUT /api/companies/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| CompanyController | `UploadLogo` | `POST /api/companies/{id:int}/logo` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| CompanyController | `Delete` | `DELETE /api/companies/{id:int}` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `CompanySettingsController`

`HRMS.API/Controllers/Companies/CompanySettingsController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| CompanySettingsController | `Get` | `GET /api/companies/{companyId:int}/settings` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| CompanySettingsController | `Upsert` | `PUT /api/companies/{companyId:int}/settings` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `OnboardingController`

`HRMS.API/Controllers/Onboarding/OnboardingController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| OnboardingController | `GetTemplates` | `GET /api/onboarding/templates` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| OnboardingController | `CreateTemplate` | `POST /api/onboarding/templates` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| OnboardingController | `UpdateTemplate` | `PUT /api/onboarding/templates/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| OnboardingController | `DeleteTemplate` | `DELETE /api/onboarding/templates/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| OnboardingController | `Assign` | `POST /api/onboarding/assign` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| OnboardingController | `GetMyRecord` | `GET /api/onboarding/my` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| OnboardingController | `MarkStepComplete` | `PATCH /api/onboarding/records/{recordId:int}/complete-step` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `TravelController`

`HRMS.API/Controllers/Travel/TravelController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| TravelController | `Dashboard` | `GET /api/travel/dashboard` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| TravelController | `GetAll` | `GET /api/travel` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| TravelController | `Report` | `GET /api/travel/report` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| TravelController | `GetMy` | `GET /api/travel/my` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TravelController | `GetById` | `GET /api/travel/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TravelController | `Create` | `POST /api/travel` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TravelController | `Update` | `PUT /api/travel/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TravelController | `Submit` | `PATCH /api/travel/{id:int}/submit` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TravelController | `PatchUpdate` | `PATCH /api/travel/{id:int}/update` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TravelController | `Cancel` | `PATCH /api/travel/{id:int}/cancel` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TravelController | `Decide` | `PATCH /api/travel/{id:int}/decide` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| TravelController | `Delete` | `DELETE /api/travel/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `GeoFenceController`

`HRMS.API/Controllers/GPS/GeoFenceController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| GeoFenceController | `GetAll` | `GET /api/geofences` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| GeoFenceController | `GetById` | `GET /api/geofences/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| GeoFenceController | `Create` | `POST /api/geofences` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| GeoFenceController | `Update` | `PUT /api/geofences/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| GeoFenceController | `Delete` | `DELETE /api/geofences/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| GeoFenceController | `Toggle` | `PATCH /api/geofences/{id:int}/toggle` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `GpsAttendanceController`

`HRMS.API/Controllers/GPS/GpsAttendanceController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| GpsAttendanceController | `Validate` | `POST /api/gps/validate` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| GpsAttendanceController | `CheckIn` | `POST /api/gps/checkin/{webAttendanceId:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| GpsAttendanceController | `CheckOut` | `POST /api/gps/checkout/{webAttendanceId:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| GpsAttendanceController | `Dashboard` | `GET /api/gps/dashboard` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| GpsAttendanceController | `Logs` | `GET /api/gps/logs` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| GpsAttendanceController | `OutsideRadius` | `GET /api/gps/outside-radius` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `AttendanceReportController`

`HRMS.API/Controllers/Reports/AttendanceReportController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| AttendanceReportController | `Monthly` | `GET /api/reports/attendance/monthly` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AttendanceReportController | `Daily` | `GET /api/reports/attendance/daily` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AttendanceReportController | `Export` | `GET /api/reports/attendance/export` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AttendanceReportController | `ExportStream` | `GET /api/reports/attendance/export/stream` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `DashboardReportController`

`HRMS.API/Controllers/Reports/DashboardReportController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| DashboardReportController | `GetDashboard` | `GET /api/reports/dashboard` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| DashboardReportController | `GetKpis` | `GET /api/reports/dashboard/api/reports/kpis` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `EmployeeReportController`

`HRMS.API/Controllers/Reports/EmployeeReportController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| EmployeeReportController | `Summary` | `GET /api/reports/employees/summary` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeReportController | `Export` | `GET /api/reports/employees/export` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeReportController | `ExportStream` | `GET /api/reports/employees/export/stream` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `LeaveReportController`

`HRMS.API/Controllers/Reports/LeaveReportController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| LeaveReportController | `Monthly` | `GET /api/reports/leave/monthly` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| LeaveReportController | `Export` | `GET /api/reports/leave/export` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| LeaveReportController | `ExportStream` | `GET /api/reports/leave/export/stream` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `PayrollReportController`

`HRMS.API/Controllers/Reports/PayrollReportController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| PayrollReportController | `Monthly` | `GET /api/reports/payroll/monthly` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| PayrollReportController | `Export` | `GET /api/reports/payroll/export` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| PayrollReportController | `ExportStream` | `GET /api/reports/payroll/export/stream` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `ReportController`

`HRMS.API/Controllers/Reports/ReportController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| ReportController | `AttendanceReport` | `GET /api/reports/attendance` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| ReportController | `EmployeeReport` | `GET /api/reports/employees` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `SalaryRegisterController`

`HRMS.API/Controllers/Reports/SalaryRegisterController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| SalaryRegisterController | `Get` | `GET /api/reports/salary-register` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalaryRegisterController | `Export` | `GET /api/reports/salary-register/export` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalaryRegisterController | `ExportStream` | `GET /api/reports/salary-register/export/stream` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `AttendanceController`

`HRMS.API/Controllers/Attendance/AttendanceController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| AttendanceController | `CheckIn` | `POST /api/attendance/web/check-in` | `[Authorize(Roles = AppRoles.Employee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AttendanceController | `CheckOut` | `POST /api/attendance/web/check-out/{attendanceId:int}` | `[Authorize(Roles = AppRoles.Employee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AttendanceController | `DeleteAttendance` | `DELETE /api/attendance/web/{attendanceId:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AttendanceController | `EditAttendance` | `PATCH /api/attendance/web/{attendanceId:int}/edit` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AttendanceController | `UpdateStatus` | `PATCH /api/attendance/web/{attendanceId:int}/status` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AttendanceController | `GetWebAttendance` | `GET /api/attendance/web` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AttendanceController | `GetMyAttendance` | `GET /api/attendance/web/my` | `[Authorize(Roles = AppRoles.Employee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AttendanceController | `UploadExcel` | `POST /api/attendance/excel/upload` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AttendanceController | `GetExcelAttendance` | `GET /api/attendance/excel` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `BiometricCapabilitiesController`

`HRMS.API/Controllers/Attendance/BiometricCapabilitiesController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| BiometricCapabilitiesController | `GetCapabilities` | `GET /api/biometric/capabilities` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BiometricCapabilitiesController | `GetByVendor` | `GET /api/biometric/capabilities/{vendorName}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `BiometricController`

`HRMS.API/Controllers/Attendance/BiometricController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| BiometricController | `GetProviders` | `GET /api/biometric/providers` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BiometricController | `GetVendors` | `GET /api/biometric/vendors` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BiometricController | `GetStatus` | `GET /api/biometric/status/{vendor}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BiometricController | `Sync` | `POST /api/biometric/sync` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BiometricController | `GetSettings` | `GET /api/biometric/settings` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BiometricController | `UpdateSettings` | `PUT /api/biometric/settings` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BiometricController | `GetDashboard` | `GET /api/biometric/dashboard` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BiometricController | `GetRealtime` | `GET /api/biometric/realtime` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `ShiftController`

`HRMS.API/Controllers/Attendance/ShiftController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| ShiftController | `GetAll` | `GET /api/shifts` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| ShiftController | `Create` | `POST /api/shifts` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| ShiftController | `Update` | `PUT /api/shifts/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| ShiftController | `Delete` | `DELETE /api/shifts/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `SalesController`

`HRMS.API/Controllers/Sales/SalesController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| SalesController | `GetDashboard` | `GET /api/sales/dashboard` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `ListLeads` | `GET /api/sales/leads` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `GetLead` | `GET /api/sales/leads/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `CreateLead` | `POST /api/sales/leads` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `UpdateLead` | `PUT /api/sales/leads/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `UpdateLeadStatus` | `PATCH /api/sales/leads/{id:int}/status` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `DeleteLead` | `DELETE /api/sales/leads/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `ListCustomers` | `GET /api/sales/customers` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `GetCustomer` | `GET /api/sales/customers/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `CreateCustomer` | `POST /api/sales/customers` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `ConvertLeadToCustomer` | `POST /api/sales/leads/{leadId:int}/convert` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `UpdateCustomer` | `PUT /api/sales/customers/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `DeleteCustomer` | `DELETE /api/sales/customers/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `ListFollowUps` | `GET /api/sales/followups` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `CreateFollowUp` | `POST /api/sales/followups` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `UpdateFollowUp` | `PUT /api/sales/followups/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `DeleteFollowUp` | `DELETE /api/sales/followups/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `ListMeetings` | `GET /api/sales/meetings` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `GetMeeting` | `GET /api/sales/meetings/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `CreateMeeting` | `POST /api/sales/meetings` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `UpdateMeeting` | `PUT /api/sales/meetings/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `DeleteMeeting` | `DELETE /api/sales/meetings/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `ListVisits` | `GET /api/sales/visits` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `CheckIn` | `POST /api/sales/visits/checkin` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `CheckOut` | `PATCH /api/sales/visits/{id:int}/checkout` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `DeleteVisit` | `DELETE /api/sales/visits/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `ListTasks` | `GET /api/sales/tasks` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `CreateTask` | `POST /api/sales/tasks` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `UpdateTask` | `PUT /api/sales/tasks/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `UpdateTaskStatus` | `PATCH /api/sales/tasks/{id:int}/status` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `DeleteTask` | `DELETE /api/sales/tasks/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `ListQuotations` | `GET /api/sales/quotations` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `GetQuotation` | `GET /api/sales/quotations/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `CreateQuotation` | `POST /api/sales/quotations` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `UpdateQuotation` | `PUT /api/sales/quotations/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `UpdateQuotationStatus` | `PATCH /api/sales/quotations/{id:int}/status` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `DeleteQuotation` | `DELETE /api/sales/quotations/{id:int}` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `LeadReport` | `GET /api/sales/reports/leads` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `ConversionReport` | `GET /api/sales/reports/conversion` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `PerformanceReport` | `GET /api/sales/reports/performance` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `VisitReport` | `GET /api/sales/reports/visits` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `RevenueReport` | `GET /api/sales/reports/revenue` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `PipelineReport` | `GET /api/sales/reports/pipeline` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `AssignLead` | `POST /api/sales/leads/{id:int}/assign` | `[Authorize(Roles = AppRoles.AdminSuperAdminSalesManagers)]` | Yes — explicit gate, narrower than the controller default. | None. |
| SalesController | `ReassignLead` | `POST /api/sales/leads/{id:int}/reassign` | `[Authorize(Roles = AppRoles.AdminSuperAdminSalesManagers)]` | Yes — explicit gate, narrower than the controller default. | None. |
| SalesController | `BulkAssignLeads` | `POST /api/sales/leads/bulk-assign` | `[Authorize(Roles = AppRoles.AdminSuperAdminSalesManagers)]` | Yes — explicit gate, narrower than the controller default. | None. |
| SalesController | `GetAssignmentHistory` | `GET /api/sales/leads/{id:int}/assignment-history` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `MyAssignedLeads` | `GET /api/sales/leads/my-leads` | `[Authorize(Roles = AppRoles.AdminSuperAdminSales)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalesController | `UnassignedLeads` | `GET /api/sales/leads/unassigned` | `[Authorize(Roles = AppRoles.AdminSuperAdminSalesManagers)]` | Yes — explicit gate, narrower than the controller default. | None. |
| SalesController | `TeamLeads` | `GET /api/sales/leads/team-leads` | `[Authorize(Roles = AppRoles.AdminSuperAdminSalesManagers)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `NotificationController`

`HRMS.API/Controllers/Notifications/NotificationController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| NotificationController | `GetAll` | `GET /api/notifications` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| NotificationController | `UnreadCount` | `GET /api/notifications/count` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| NotificationController | `MarkRead` | `POST /api/notifications/{id:int}/read` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| NotificationController | `MarkAllRead` | `POST /api/notifications/read-all` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| NotificationController | `Delete` | `DELETE /api/notifications/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `RecruitmentController`

`HRMS.API/Controllers/Recruitment/RecruitmentController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| RecruitmentController | `GetDashboard` | `GET /api/recruitment/dashboard` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `ListRequisitions` | `GET /api/recruitment/requisitions` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `CreateRequisition` | `POST /api/recruitment/requisitions` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `GetRequisition` | `GET /api/recruitment/requisitions/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `UpdateRequisition` | `PUT /api/recruitment/requisitions/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `UpdateRequisitionStatus` | `PATCH /api/recruitment/requisitions/{id:int}/status` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `DeleteRequisition` | `DELETE /api/recruitment/requisitions/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `ListCandidates` | `GET /api/recruitment/candidates` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `CreateCandidate` | `POST /api/recruitment/candidates` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `GetCandidate` | `GET /api/recruitment/candidates/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `UpdateCandidate` | `PUT /api/recruitment/candidates/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `UpdateCandidateStatus` | `PATCH /api/recruitment/candidates/{id:int}/status` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `DeleteCandidate` | `DELETE /api/recruitment/candidates/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `ListInterviews` | `GET /api/recruitment/interviews` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `ScheduleInterview` | `POST /api/recruitment/interviews` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `UpdateInterview` | `PUT /api/recruitment/interviews/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `SubmitFeedback` | `POST /api/recruitment/interviews/{id:int}/feedback` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `DeleteInterview` | `DELETE /api/recruitment/interviews/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `ListOffers` | `GET /api/recruitment/offers` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `GetOffer` | `GET /api/recruitment/offers/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `CreateOffer` | `POST /api/recruitment/offers` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `ApproveOffer` | `POST /api/recruitment/offers/{id:int}/approve` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RecruitmentController | `UpdateOfferStatus` | `PATCH /api/recruitment/offers/{id:int}/status` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `AnalyticsController`

`HRMS.API/Controllers/Analytics/AnalyticsController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| AnalyticsController | `Headcount` | `GET /api/analytics/headcount` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AnalyticsController | `Attendance` | `GET /api/analytics/attendance` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AnalyticsController | `Payroll` | `GET /api/analytics/payroll` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AnalyticsController | `Turnover` | `GET /api/analytics/turnover` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `HelpdeskController`

`HRMS.API/Controllers/Helpdesk/HelpdeskController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| HelpdeskController | `GetTickets` | `GET /api/helpdesk/tickets` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| HelpdeskController | `GetTicket` | `GET /api/helpdesk/tickets/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| HelpdeskController | `CreateTicket` | `POST /api/helpdesk/tickets` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| HelpdeskController | `UpdateTicket` | `PUT /api/helpdesk/tickets/{id:int}` | `[Authorize(Roles = AppRoles.HrAdminAdminSupport)]` | Yes — explicit gate, narrower than the controller default. | None. |
| HelpdeskController | `AssignTicket` | `PATCH /api/helpdesk/tickets/{id:int}/assign` | `[Authorize(Roles = AppRoles.HrAdminAdminSupport)]` | Yes — explicit gate, narrower than the controller default. | None. |
| HelpdeskController | `GetComments` | `GET /api/helpdesk/tickets/{id:int}/comments` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| HelpdeskController | `AddComment` | `POST /api/helpdesk/tickets/{id:int}/comments` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| HelpdeskController | `GetSummary` | `GET /api/helpdesk/summary` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| HelpdeskController | `GetCategories` | `GET /api/helpdesk/categories` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| HelpdeskController | `CreateCategory` | `POST /api/helpdesk/categories` | `[Authorize(Roles = AppRoles.HrAdminAndAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| HelpdeskController | `DeleteTicket` | `DELETE /api/helpdesk/tickets/{id:int}` | `[Authorize(Roles = AppRoles.HrAdminAndAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `WebhookController`

`HRMS.API/Controllers/Webhooks/WebhookController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| WebhookController | `List` | `GET /api/webhooks` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| WebhookController | `Register` | `POST /api/webhooks` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| WebhookController | `Delete` | `DELETE /api/webhooks/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| WebhookController | `GetEventTypes` | `GET /api/webhooks/events` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `BonusController`

`HRMS.API/Controllers/Payroll/BonusController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| BonusController | `GetAll` | `GET /api/bonuses` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BonusController | `GetById` | `GET /api/bonuses/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BonusController | `Create` | `POST /api/bonuses` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BonusController | `Update` | `PUT /api/bonuses/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| BonusController | `Delete` | `DELETE /api/bonuses/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `DeductionController`

`HRMS.API/Controllers/Payroll/DeductionController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| DeductionController | `GetAll` | `GET /api/deductions` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| DeductionController | `GetById` | `GET /api/deductions/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| DeductionController | `Create` | `POST /api/deductions` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| DeductionController | `Update` | `PUT /api/deductions/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| DeductionController | `Delete` | `DELETE /api/deductions/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `PayrollController`

`HRMS.API/Controllers/Payroll/PayrollController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| PayrollController | `Calculate` | `POST /api/payroll/calculate` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PayrollController | `Generate` | `POST /api/payroll/generate` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PayrollController | `BulkGenerate` | `POST /api/payroll/bulk-generate` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PayrollController | `LockPeriod` | `POST /api/payroll/lock` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PayrollController | `UnlockPeriod` | `POST /api/payroll/unlock` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PayrollController | `GetLocks` | `GET /api/payroll/locks` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PayrollController | `GetAll` | `GET /api/payroll` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PayrollController | `GetById` | `GET /api/payroll/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| PayrollController | `GetMyPayslips` | `GET /api/payroll/my` | `[Authorize(Roles = AppRoles.Employee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PayrollController | `Delete` | `DELETE /api/payroll/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `PayslipController`

`HRMS.API/Controllers/Payroll/PayslipController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| PayslipController | `QueuePdfGeneration` | `POST /api/payslip/{payslipId:int}/pdf` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| PayslipController | `GetPdfStatus` | `GET /api/payslip/{payslipId:int}/pdf/status/{token}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| PayslipController | `DownloadPdf` | `GET /api/payslip/{payslipId:int}/pdf/download/{token}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `SalaryController`

`HRMS.API/Controllers/Payroll/SalaryController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| SalaryController | `GetActive` | `GET /api/salary/{employeeId}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalaryController | `GetHistory` | `GET /api/salary/{employeeId}/history` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SalaryController | `Upsert` | `POST /api/salary/{employeeId}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `TrainingController`

`HRMS.API/Controllers/Training/TrainingController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| TrainingController | `GetAll` | `GET /api/training` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TrainingController | `GetById` | `GET /api/training/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TrainingController | `Create` | `POST /api/training` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| TrainingController | `Update` | `PUT /api/training/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| TrainingController | `Delete` | `DELETE /api/training/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| TrainingController | `Enroll` | `POST /api/training/{id:int}/enroll` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TrainingController | `GetMyEnrollments` | `GET /api/training/enrollments/my` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TrainingController | `MarkComplete` | `PATCH /api/training/enrollments/{enrollmentId:int}/complete` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `LogoController`

`HRMS.API/Controllers/Logo/LogoController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| LogoController | `Upload` | `POST /api/logo/{companyId:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `EmployeeController`

`HRMS.API/Controllers/Employees/EmployeeController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| EmployeeController | `Create` | `POST /api/employees` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeController | `GetAll` | `GET /api/employees` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeController | `GetById` | `GET /api/employees/{employeeId}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeController | `Update` | `PUT /api/employees/{employeeId}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeController | `UpdateStatus` | `PATCH /api/employees/{employeeId}/status` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeController | `Delete` | `DELETE /api/employees/{employeeId}` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| EmployeeController | `GetPii` | `GET /api/employees/{employeeId}/pii` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `EmployeeDocumentController`

`HRMS.API/Controllers/Employees/EmployeeDocumentController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| EmployeeDocumentController | `GetAll` | `GET /api/employees/{employeeId}/documents` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeDocumentController | `Upload` | `POST /api/employees/{employeeId}/documents` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeDocumentController | `Verify` | `PATCH /api/employees/{employeeId}/documents/{docId:int}/verify` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeDocumentController | `Delete` | `DELETE /api/employees/{employeeId}/documents/{docId:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeDocumentController | `Download` | `GET /api/employees/{employeeId}/documents/{docId:int}/download` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `EmployeeExitController`

`HRMS.API/Controllers/Employees/EmployeeExitController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| EmployeeExitController | `Get` | `GET /api/employees/{employeeId}/exit` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeExitController | `Initiate` | `POST /api/employees/{employeeId}/exit` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeExitController | `Complete` | `PATCH /api/employees/{employeeId}/exit/{exitId:int}/complete` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `EmployeePromotionController`

`HRMS.API/Controllers/Employees/EmployeePromotionController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| EmployeePromotionController | `GetAll` | `GET /api/employees/{employeeId}/promotions` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeePromotionController | `Create` | `POST /api/employees/{employeeId}/promotions` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeePromotionController | `Delete` | `DELETE /api/employees/{employeeId}/promotions/{promotionId:int}` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `EmployeeSelfController`

`HRMS.API/Controllers/Employees/EmployeeSelfController.cs` — controller-level: `[Authorize(Roles = AppRoles.Employee)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| EmployeeSelfController | `GetMyProfile` | `GET /api/my/profile` | `[Authorize(Roles = AppRoles.Employee)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeSelfController | `UpdateMyProfile` | `PUT /api/my/profile` | `[Authorize(Roles = AppRoles.Employee)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `EmployeeTransferController`

`HRMS.API/Controllers/Employees/EmployeeTransferController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| EmployeeTransferController | `GetAll` | `GET /api/employees/{employeeId}/transfers` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeTransferController | `Create` | `POST /api/employees/{employeeId}/transfers` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmployeeTransferController | `Approve` | `PATCH /api/employees/{employeeId}/transfers/{transferId:int}/approve` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| EmployeeTransferController | `Reject` | `PATCH /api/employees/{employeeId}/transfers/{transferId:int}/reject` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `DepartmentController`

`HRMS.API/Controllers/Organisation/DepartmentController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| DepartmentController | `GetDepartments` | `GET /api/organisation/departments` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| DepartmentController | `GetDepartment` | `GET /api/organisation/departments/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| DepartmentController | `CreateDepartment` | `POST /api/organisation/departments` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| DepartmentController | `UpdateDepartment` | `PUT /api/organisation/departments/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| DepartmentController | `DeleteDepartment` | `DELETE /api/organisation/departments/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| DepartmentController | `GetDesignations` | `GET /api/organisation/designations` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| DepartmentController | `GetDesignation` | `GET /api/organisation/designations/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| DepartmentController | `CreateDesignation` | `POST /api/organisation/designations` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| DepartmentController | `UpdateDesignation` | `PUT /api/organisation/designations/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| DepartmentController | `DeleteDesignation` | `DELETE /api/organisation/designations/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `HolidayController`

`HRMS.API/Controllers/Organisation/HolidayController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| HolidayController | `GetAll` | `GET /api/holidays` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| HolidayController | `GetById` | `GET /api/holidays/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| HolidayController | `Create` | `POST /api/holidays` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| HolidayController | `Update` | `PUT /api/holidays/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| HolidayController | `Delete` | `DELETE /api/holidays/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `DashboardController`

`HRMS.API/Controllers/Dashboard/DashboardController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| DashboardController | `AdminStats` | `GET /api/dashboard/admin` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| DashboardController | `SuperAdminStats` | `GET /api/dashboard/superadmin` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| DashboardController | `EmployeeStats` | `GET /api/dashboard/employee` | `[Authorize(Roles = AppRoles.Employee)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `ExpenseController`

`HRMS.API/Controllers/Expense/ExpenseController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| ExpenseController | `Dashboard` | `GET /api/expenses/dashboard` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| ExpenseController | `GetAll` | `GET /api/expenses` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| ExpenseController | `Report` | `GET /api/expenses/report` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| ExpenseController | `GetMy` | `GET /api/expenses/my` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| ExpenseController | `GetById` | `GET /api/expenses/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| ExpenseController | `Create` | `POST /api/expenses` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| ExpenseController | `Submit` | `PATCH /api/expenses/{id:int}/submit` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| ExpenseController | `Decide` | `PATCH /api/expenses/{id:int}/decide` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| ExpenseController | `Delete` | `DELETE /api/expenses/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| ExpenseController | `SubmitLegacy` | `POST /api/expenses/legacy` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `SuperAdminController`

`HRMS.API/Controllers/SuperAdmins/SuperAdminController.cs` — controller-level: `[Authorize(Roles = AppRoles.SuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| SuperAdminController | `GetAll` | `GET /api/superadmins` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SuperAdminController | `Create` | `POST /api/superadmins` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| SuperAdminController | `UpdateStatus` | `PATCH /api/superadmins/{id:int}/status` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `EmailQueueController`

`HRMS.API/Controllers/Email/EmailQueueController.cs` — controller-level: `[Authorize(Roles = AppRoles.SuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| EmailQueueController | `List` | `GET /api/email-queue` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| EmailQueueController | `Retry` | `POST /api/email-queue/{id:int}/retry` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `AuditController`

`HRMS.API/Controllers/Audit/AuditController.cs` — controller-level: `[Authorize(Roles = AppRoles.SuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| AuditController | `Get` | `GET /api/audit` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `LoginHistoryController`

`HRMS.API/Controllers/Audit/LoginHistoryController.cs` — controller-level: `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| LoginHistoryController | `Get` | `GET /api/login-history` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `LeaveController`

`HRMS.API/Controllers/Leave/LeaveController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| LeaveController | `GetTypes` | `GET /api/leave/types` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| LeaveController | `CreateType` | `POST /api/leave/types` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `UpdateType` | `PUT /api/leave/types/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `DeleteType` | `DELETE /api/leave/types/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `Apply` | `POST /api/leave/apply` | `[Authorize(Roles = AppRoles.Employee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `MyRequests` | `GET /api/leave/my` | `[Authorize(Roles = AppRoles.Employee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `MyBalance` | `GET /api/leave/my/balance` | `[Authorize(Roles = AppRoles.Employee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `Cancel` | `POST /api/leave/my/{id}/cancel` | `[Authorize(Roles = AppRoles.Employee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `GetAll` | `GET /api/leave` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `GetById` | `GET /api/leave/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `Decide` | `POST /api/leave/{id:int}/decision` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `AdjustBalance` | `POST /api/leave/balance/adjust` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `GetAdjustments` | `GET /api/leave/balance/adjustments/{employeeId}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| LeaveController | `CarryForward` | `POST /api/leave/carry-forward` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `AdminUserController`

`HRMS.API/Controllers/AdminUsers/AdminUserController.cs` — controller-level: `[Authorize(Roles = AppRoles.SuperAdminAndAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| AdminUserController | `GetAll` | `GET /api/admin-users` | `[Authorize(Roles = AppRoles.SuperAdminAndAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AdminUserController | `GetById` | `GET /api/admin-users/{id:int}` | `[Authorize(Roles = AppRoles.SuperAdminAndAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| AdminUserController | `Create` | `POST /api/admin-users` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AdminUserController | `Update` | `PUT /api/admin-users/{id:int}` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AdminUserController | `UpdateStatus` | `PATCH /api/admin-users/{id:int}/status` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AdminUserController | `Delete` | `DELETE /api/admin-users/{id}` | `[Authorize(Roles = AppRoles.SuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `PermissionsController`

`HRMS.API/Controllers/AdminUsers/PermissionsController.cs` — controller-level: `[Authorize(Roles = AppRoles.SuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| PermissionsController | `GetAll` | `GET /api/permissions` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| PermissionsController | `GetByRole` | `GET /api/permissions/{role}` | `[Authorize(Roles = AppRoles.SuperAdminAndAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| PermissionsController | `Upsert` | `POST /api/permissions` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `RolesController`

`HRMS.API/Controllers/AdminUsers/RolesController.cs` — controller-level: `[Authorize(Roles = AppRoles.SuperAdmin)]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| RolesController | `GetAll` | `GET /api/roles` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RolesController | `Create` | `POST /api/roles` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RolesController | `Update` | `PUT /api/roles/{id:int}` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| RolesController | `Delete` | `DELETE /api/roles/{id:int}` | `[Authorize(Roles = AppRoles.SuperAdmin)]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |

### `AppreciationController`

`HRMS.API/Controllers/Appreciation/AppreciationController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| AppreciationController | `Upload` | `POST /api/appreciation` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AppreciationController | `GetById` | `GET /api/appreciation/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AppreciationController | `GetAll` | `GET /api/appreciation` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AppreciationController | `GetMyAppreciations` | `GET /api/appreciation/my` | `[Authorize(Roles = AppRoles.Employee)]` | Yes — explicit gate, narrower than the controller default. | None. |
| AppreciationController | `Delete` | `DELETE /api/appreciation/{id:int}` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |

### `TimesheetController`

`HRMS.API/Controllers/Timesheet/TimesheetController.cs` — controller-level: `[Authorize(Policy = "RequireMfaCompleted")]`

| Controller | Action | Route | Attribute | Justified? | Action taken |
|---|---|---|---|---|---|
| TimesheetController | `GetMine` | `GET /api/timesheet/my` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TimesheetController | `GetPending` | `GET /api/timesheet/pending` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| TimesheetController | `Create` | `POST /api/timesheet` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TimesheetController | `Update` | `PUT /api/timesheet/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TimesheetController | `Submit` | `POST /api/timesheet/{id:int}/submit` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
| TimesheetController | `Approve` | `POST /api/timesheet/{id:int}/approve` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| TimesheetController | `Reject` | `POST /api/timesheet/{id:int}/reject` | `[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]` | Yes — explicit gate, narrower than the controller default. | None. |
| TimesheetController | `Delete` | `DELETE /api/timesheet/{id:int}` | `[Authorize(Policy = "RequireMfaCompleted")]` *(controller)* | Yes — inherits the controller gate; no broader access than the class default. | None. |
