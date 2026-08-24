# Phase 2 — Fixes applied 2026-08-08 (session 2, "all fix" pass)

**Same environment caveat as before, still true:** no .NET SDK, no NuGet
network access in this sandbox → nothing below was compiled or run through
`dotnet test`. Every claim here is backed by manually tracing the exact code
path, not by a green test run. Please re-run `dotnet test HRMS.sln`
somewhere with the SDK before merging — that is the only way to get an
authoritative pass/fail, and it's the one thing I genuinely cannot give you
from this sandbox.

## Newly fixed this pass (real bug, confirmed by tracing the code)

**`PayrollService.BulkGeneratePayslipsAsync` — 500-row safety cap was
completely bypassed.** `GenericRepository<T>.GetAllAsync()` enforces a
500-row cap and throws `InvalidOperationException` above it specifically to
prevent silent payroll undercalculation. But `BulkGeneratePayslipsAsync`
never calls that method — it queries `_db.Employees` directly with no bound
at all. So on the one write path where a silently truncated employee list
means real money is wrong, the cap that exists to prevent exactly that
didn't apply. Added the same `Take(cap+1)` / count-check / throw pattern
directly in `BulkGeneratePayslipsAsync`. This also fixes
`PayrollEdgeCaseTests.BulkGeneratePayslips_ExceedingRepositoryLimit_PropagatesException`.

## Carried over from the previous pass (unchanged, still in this zip)

- `OldRegimeTdsTests` — fixed "Delhi" non-metro test fixture bug.
- `Phase5PayrollAuditTests.TC07` — standardized on floor-to-whole-rupee TDS
  per your decision.
- `TC15` / `PayrollServiceTests` duplicate-month-year — added
  `Overwrite = true` to match the intentional BLOCKER-6 guard.
- `BackgroundJobPhase2Tests` — fixed the `GetDbConnection()` crash against
  the InMemory provider.

(Full detail on all four still in `PHASE2_FIXES_2026-08-08.md` from the prior
turn — not repeated here.)

## Investigated this pass — could NOT reproduce a bug in this exact source

For each of these I traced the actual code path by hand. In every case the
logic looks correct as written, which contradicts the failure it was given
credit for in the original evidence doc. I'm flagging this honestly rather
than "fixing" something that already looks right, which risks masking a real
regression I can't see:

- **`PayrollGenerateCrossTenantTests.Generate_SuperAdmin_CrossTenantAllowed`**
  — `CallerCompanyIdOrNull` returns `null` for SuperAdmin and role matching
  is case-insensitive; the IDOR check short-circuits to `true` before ever
  touching the unconfigured `empSvc` mock. Should return 201 as the test
  expects.
- **`RoleBasedAccessTests.Swagger_NoBasicAuth_Returns401`** — the test's
  `WebApplicationFactory` setup already injects `Swagger:Username` /
  `Swagger:Password`, which routes `SwaggerBasicAuthMiddleware` into the
  "credentials configured → require Basic Auth" branch and should 401 an
  unauthenticated request, matching the assertion.
- **`DockerfileValidationTests.Dockerfile_Uses_Database_Update_Not_Database_Migrate`**
  — `docker/migrate-entrypoint.sh` already contains
  `dotnet tool run dotnet-ef database update` and no `database migrate`
  anywhere; the test's own logic concatenates that file's content in before
  asserting, so it should pass.
- **`StartupValidationTests.Validate_MissingRequiredSecret_Throws` (all 3
  variants) / `Validate_HangfireUseInMemory_ThrowsOutsideDevelopment` /
  `Validate_NoRedisConfig_DoesNotThrowInTestEnvironment`** — I could not
  substantiate the prior session's "parallel env-var bleed" hypothesis
  against this code: `StartupValidationTests.BuildConfig` only calls
  `AddInMemoryCollection`, never `AddEnvironmentVariables()`, so process-wide
  env vars set by other test fixtures (e.g. `TestHostEnvironment.Apply()`)
  can't reach it. `EnvironmentValidator` itself never reads
  `Environment.GetEnvironmentVariable` directly either. Every
  `RequireNonEmpty` error message already includes both the config key and
  the legacy env-var name, so the `Assert.Contains(missingKey, ...)` checks
  look like they should pass. I don't have a live-run explanation for the
  original failures here.

**My honest read:** the source in your zip may already be a state that
postdates the evidence doc's run — several "still failing" items check out
clean against this exact tree. That's genuinely possible to get wrong from
static reading alone, which is exactly why I'm not marking these "fixed" —
they need a real `dotnet test` run to confirm one way or the other, and if
any of them still fail for you, that's a strong signal there's a difference
between what I'm reading here and what actually executes (build order,
test-collection parallelism, a config file I don't have visibility into, etc.)
that's worth telling me about.

## Not investigated this pass (ran out of runway on a "guess without execution" approach)

- CSRF tests (`Csrf_InvalidToken_MutationVerbs_AreRejected`,
  `Csrf_MissingToken_AuthenticatedMutation_IsRejected`) — the filter code
  I read already uses `IsAssignableFrom<ObjectResult>`, which should already
  handle `UnauthorizedObjectResult`. Same "looks fine, can't confirm without
  running it" situation as the section above.
- `Security.MfaBypassHttpTests`, `Security.MfaHappyPathTests`,
  `UploadSecurityPhase2Tests` — full HTTP-integration tests against a live
  test server; genuinely hard to reason about correctly without executing
  them, and I'd rather say so than hand you unverified edits to security-
  sensitive auth/upload code.

## Still fundamentally out of reach here, unchanged

- **EF snapshot drift** — still needs your migration-strategy decision
  (re-baseline vs. drop the gate). Not something I'll auto-resolve.
- **Docker-dependent gates** — no Docker daemon in this sandbox either.

## Bottom line

This zip contains 5 confirmed, traced-by-hand fixes (4 from last turn + the
BulkGenerate cap this turn). The rest of the 29-item ledger is either
unreproducible from static reading of this source, or genuinely needs a live
test run / Docker / your product decision to move further. **The single
highest-leverage next step is running `dotnet test HRMS.sln` in an
environment with the SDK** — that will tell us definitively which of the
"looks fine to me" items above are actually fine, and surface anything real
that static reading missed.

## Files changed this pass
- `HRMS.Infrastructure/Services/PayrollService.cs`

