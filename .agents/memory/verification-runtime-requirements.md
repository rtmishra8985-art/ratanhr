---
name: Verification runtime requirements
description: Environment prerequisites for completing RatanHR backend and release-gate verification.
---

The RatanHR source review can establish static security evidence and frontend
validation, but backend closure requires a .NET 8 SDK, disposable MySQL and Redis,
and an isolated staging environment with protected E2E configuration. Upload-security
failure paths additionally require the approved malware scanner.

**Why:** Treating historical backend test counts or static code inspection as current
runtime proof would incorrectly promote a partially verified release to ready.

**How to apply:** Before the next verification pass, check these prerequisites first;
run backend restore/build/test and migration rehearsal only against disposable data,
then execute two-company authorization, tenant-isolation, and Playwright staging tests.

Compilation and runtime test-host validation must remain separate release gates:
the solution can compile cleanly while integration tests still fail during
dependency-injection startup.

**Why:** A clean compiler result does not prove that the WebApplicationFactory
or shared test fixtures register every runtime dependency used by the API.

**How to apply:** Report build status and test execution counts independently,
and fix test-host registrations before claiming the runtime suite is green.

Docker verification can also be split by layer: container processes may log ready
and be reachable through published host ports while the daemon fails healthcheck
`exec` calls or bridge-network probes with `setns` errors.

**Why:** In restricted containerized workspaces, a Compose `--wait` failure can be
an engine/runtime limitation rather than an application failure, but it still must
remain a blocked Compose gate.

**How to apply:** Preserve the raw daemon healthcheck error, run direct disposable
service/API probes where possible, and report those as separate evidence rather
than promoting them to a green Compose/E2E result.