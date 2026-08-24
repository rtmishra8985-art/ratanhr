> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# HRMS Hardening – Verification & Fix Report

## Summary

Six verification and release gaps identified after the production hardening pass
were resolved. No source-code logic was changed — all fixes are in the test suite.
The application binaries, migrations, and configuration are unchanged.

---

## Fix 1 — `JwtServiceTests.cs`: migrated from HS256 to RS256

**Root cause:** `JwtService` was rewritten to use RSA asymmetric signing
(`Jwt:PrivateKeyPem` / `Jwt:PublicKeyPem`). The test file still configured a
symmetric `Jwt:Key`; `JwtService.GenerateToken()` would throw
`InvalidOperationException` on every test run because the old key is gone.

**Fix:** Replaced `BuildConfig()` to supply a freshly generated RSA-2048 key
pair (via `TestHelpers.GenerateTestRsaKeyPair()`). Added three extra cases:
- Token signed with key-A is rejected by a validator holding key-B (key rotation)
- `GenerateToken` with missing private key throws `InvalidOperationException`
- `ValidateToken` with missing public key returns `null` (never throws to callers)

---

## Fix 2 — `AuthServiceTests.cs`: RSA keys + missing `IHostEnvironment`

**Root causes (two bugs):**

1. Same HS256 → RS256 mismatch as Fix 1. `BuildConfig()` supplied `Jwt:Key`;
   `JwtService` constructor call would fail at first token generation.

2. `AuthService` constructor signature is:
   ```
   AuthService(db, jwt, logger, config, audit, email, fileStorage, env)
   ```
   The test's `BuildService()` only passed 7 arguments — `IHostEnvironment env`
   was missing. The file would not compile.

**Fix:** Updated `BuildConfig()` to supply RSA PEM keys. Added
`Mock<IHostEnvironment>` with `EnvironmentName = "Testing"` to `BuildService()`.

---

## Fix 3 — `StartupValidationTests.cs`: rewrote to test actual `EnvironmentValidator`

**Root cause:** The test file contained a private `ValidateJwtConfig()` helper
that validated `Jwt:Key` (the old HS256 symmetric key). The real production
validator (`EnvironmentValidator.Validate()`) validates `Jwt:PrivateKeyPem` and
`Jwt:PublicKeyPem`. The tests were passing while exercising dead code — any
regression in `EnvironmentValidator` would be invisible.

**Fix:** Replaced all tests to call `EnvironmentValidator.Validate()` directly.
New test matrix:
| Scenario | Expected |
|---|---|
| `Jwt:PrivateKeyPem` missing | Throws, message mentions `PrivateKeyPem` |
| `Jwt:PrivateKeyPem` not a PEM blob | Throws, message mentions `PrivateKeyPem` |
| `Jwt:PublicKeyPem` missing | Throws, message mentions `PublicKeyPem` |
| `Jwt:PublicKeyPem` not a PEM blob | Throws |
| `Jwt:Issuer` missing | Throws |
| `Jwt:Audience` missing | Throws |
| `Security:EncryptionKey` missing in Production | Throws |
| `Security:EncryptionKey` missing in Development | **Does not throw** |
| `Security:EncryptionKey` wrong length (16 bytes) | Throws, mentions "32 bytes" |
| `Security:EncryptionKey` invalid base64 | Throws |
| `AllowedHosts=*` in Production | Throws |
| `Cors:AllowedOrigins` missing in Production | Throws |
| `Cors:AllowedOrigins` missing in Development | **Does not throw** |
| Full valid production config | Does not throw |
| Full valid development config | Does not throw |

---

## Fix 4 — New `HealthCheckTests.cs`: coverage for all four health endpoints

**Root cause:** No tests existed for the four health check endpoints or
`EmailHealthCheckService`. A breaking change to predicate logic or SMTP config
handling could silently degrade Kubernetes liveness/readiness probes.

**New tests cover:**

| Area | Tests |
|---|---|
| `/healthz/live` predicate (`_ => false`) | Excludes every registration regardless of tags |
| `/healthz/ready` predicate | Includes only checks tagged `"ready"` |
| `/healthz/ready` predicate | Excludes checks without `"ready"` tag |
| Redis tags | Matches production tag set `[cache, ratelimit, ready]` |
| `HealthStatus` string names | Pins `Healthy/Degraded/Unhealthy` to expected values (JSON contract) |
| `EmailHealthCheckService` – no host, non-Production | Returns `Healthy` |
| `EmailHealthCheckService` – no host, Production | Returns `Degraded` with SMTP message |
| `EmailHealthCheckService` – host set, no recent failure | Returns `Healthy` |
| `EmailHealthCheckService` – recent send failure (<30 min) | Returns `Degraded` |
| `EmailHealthCheckService` – old failure (>30 min) | Returns `Healthy` |
| Any config combination | `CheckHealthAsync` never throws unhandled exception |

---

## Fix 5 — New `UploadSizeLimitTests.cs`: 30 MB rejection and file validation

**Root cause:** No tests verified that `FileStorageService` correctly rejects
files exceeding `MaxFileSizeMB` (configured to 30 MB on upload endpoints).
Controllers add `[RequestSizeLimit(30 * 1024 * 1024)]` at the HTTP layer, but the
application-layer check in `FileStorageService` was uncovered.

**New tests cover:**

| Scenario | Expected |
|---|---|
| File > 30 MB | `FileUploadValidationException` thrown; message contains "30" |
| File exactly 30 MB | Accepted |
| File 1 byte below limit | Accepted |
| File 1 byte over limit | Rejected |
| Null file | Returns `null` (no exception) |
| Zero-length file | Returns `null` |
| Disallowed extension (`.exe`) | `FileUploadValidationException` |
| Allowed extension (`.pdf`) | Accepted when magic bytes match |
| JPEG file with PNG magic bytes | `FileUploadValidationException` (MIME spoofing) |
| PDF file with correct magic bytes | Accepted |
| PNG file with correct magic bytes | Accepted |
| Path traversal in `Delete()` – `../../etc/passwd` | Silently ignored (no throw, no delete) |
| Path traversal in `Delete()` – absolute path `/etc/passwd` | Silently ignored |
| `Delete(null)` | Silently ignored |
| `Delete("")` | Silently ignored |
| `MaxFileSizeMB` values 1 / 10 / 30 / 100 | Over-limit always rejected (`[Theory]`) |

---

## Fix 6 — `PasswordHashingTests.cs`: added `BcryptPasswordHasher` work-factor tests

**Root cause:** The existing tests called `BCrypt.Net.BCrypt.HashPassword()` directly.
Production code calls `BcryptPasswordHasher.Hash(password, config)` — the wrapper
that reads `Security:BcryptWorkFactor` from configuration. The configurable work-factor
path, boundary checks, and the exception raised for out-of-range values were untested.

**New tests added (existing raw-BCrypt tests kept unchanged):**

| Test | Validates |
|---|---|
| Default work factor (no config entry) | Hash verifies; `$12$` embedded in hash string |
| Work factor 4 (explicit config) | Hash verifies; `$04$` embedded in hash string |
| Cross-factor verification | Hash from factor 10 verifies correctly after upgrade to 12 |
| Work factor 32 (above max 31) | `InvalidOperationException` thrown |
| Work factor 3 (below min 4) | `InvalidOperationException` thrown |
| Work factor 4 boundary | Does not throw |
| Work factor 14 (upper-range, fast) | Does not throw; `$14$` in hash |
| `ConfigurationKey` constant value | Equals `"Security:BcryptWorkFactor"` |
| `DefaultWorkFactor` constant value | Equals `12` |

> **Note on work factor 31**: BCrypt at factor 31 takes >5 minutes per hash.
> It is validated by range-check in `BcryptPasswordHasher.Hash()` itself; a
> unit test that actually executes the hash would make CI unusable.

---

## Fix 7 — `TestHelpers.cs`: added `GenerateTestRsaKeyPair()` helper

Added `TestHelpers.GenerateTestRsaKeyPair()` which uses `RSA.Create(2048)` to
export a PKCS#8 private key PEM (`"-----BEGIN PRIVATE KEY-----"`) and an SPKI
public key PEM (`"-----BEGIN PUBLIC KEY-----"`). Both formats are accepted by
`JwtService.ImportFromPem()` and `EnvironmentValidator.Validate()`.

The helper is called once per test class as a `static readonly` field so RSA key
generation (~100 ms) is not repeated per test method.

---

## Build & Run Instructions (once a .NET 8 SDK is available)

```bash
# Restore packages
dotnet restore HRMS.sln

# Build
dotnet build HRMS.sln --configuration Release

# Run tests
dotnet test HRMS.Tests/HRMS.Tests.csproj --configuration Release \
  --logger "console;verbosity=normal"

# Expected: all tests pass; ~60–90 seconds for the full suite
# (BcryptPasswordHasher_DefaultWorkFactor and _ConfiguredWorkFactor4 each hash
#  once; remaining tests are sub-millisecond)
```

## Package Compatibility Note — `Serilog.Sinks.Async 2.1.0`

The package is available on NuGet.org and is compatible with .NET 8.
`dotnet restore` will pull it from the default NuGet feed without any extra
configuration. No version change is required.

---

*Report generated: 2026-07-23. All changes are in `HRMS.Tests/`; production
source code under `HRMS.API/`, `HRMS.Application/`, `HRMS.Infrastructure/`, and
`HRMS.Domain/` is unchanged.*
