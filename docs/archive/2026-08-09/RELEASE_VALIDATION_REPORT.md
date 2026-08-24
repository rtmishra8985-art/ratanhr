# RatanHR Release Validation Report

**Scope:** Source archive repair and release validation  
**Environment:** Isolated workspace validation only  
**Production access:** Not used  
**Release decision:** **NO-GO for production until the external image, secret, host, and staging gates are completed**

## Changes made

- Fixed the Docker locked-restore stage by copying every `packages.lock.json`
  before `dotnet restore --locked-mode`.
- Removed the legacy checked-in Kubernetes Secret template from the Kustomize resource list.
- Kept credentials out of the source package and wired production secret
  materialisation through External Secrets Operator.
- Changed the Kubernetes ingress root route to `hrms-api-svc`, which serves the
  built SPA from `wwwroot`; the old `hrms-frontend-svc` was not rendered.
- Replaced mutable API aliases with explicit release-tagged image names. Set
  the actual immutable registry tag or digest before release.
- Corrected `AllowedHosts` to read `APP_HOST` from `hrms-config` instead of
  passing the literal `$(APP_HOST)` string.
- Added pod labels, RuntimeDefault seccomp profiles, and disabled the API
  service-account token mount.
- Pinned MySQL images to the 8.4.6 patch release and retained the existing
  digest-pinned Redis image.

## Passed validation

| Check | Result |
|---|---|
| NuGet locked restore | PASS |
| Production API build | PASS — 0 warnings, 0 errors |
| Production API publish | PASS |
| SPA frozen dependency install | PASS |
| SPA lint | PASS |
| SPA TypeScript compile | PASS |
| SPA production build | PASS |
| Runtime Docker image build | PASS |
| Kubernetes Kustomize render | PASS — 22 resources |
| NuGet vulnerability audit, including transitives | PASS — no vulnerable packages reported |
| OSV source scan | PASS — no issues reported |

The SPA build emits non-fatal existing source-map notices from shared UI
components; they do not prevent TypeScript, lint, or Vite output from
completing.

## Test verification

The full solution test project compiles with zero compiler errors after
reconciling the stale fixtures with the current application contracts. This was
verified from the uploaded source with:

```bash
dotnet restore HRMS.sln --locked-mode
dotnet build HRMS.sln --configuration Release --no-restore
```

The compile verification result was **0 warnings, 0 errors** across all five
projects, including `HRMS.Tests`.

The runtime test command is a separate gate. In the current validation
environment it produced **1,093 passed, 48 failed, and 1 skipped**. The failures
are runtime test-host configuration issues (for example missing test
registrations for `FileStorageService`, `IDbContextFactory<ApplicationDbContext>`
and webhook channel dependencies, plus startup configuration fixtures); they
are not stale source/API compilation mismatches. No test was deleted or
disabled to achieve the clean compilation result.

The E2E SQL fixture contains intentionally non-production BCrypt hashes for
staging-only test accounts. It must only be used with the isolated E2E
environment and never with production data.

## Before applying to production

1. Configure External Secrets Operator and the referenced `ClusterSecretStore`.
2. Replace the example hostname values in `k8s/configmap.yaml` and
   `k8s/ingress.yaml`.
3. Set the immutable API image reference:

   ```bash
   kustomize edit set image \
     hrms-api=registry.example.com/your-org/hrms-api:1.0.0
   ```

4. Run `kustomize build k8s` again and review the rendered output.
5. Complete isolated staging migration, health, authentication, workflow,
   backup, monitoring, and client-UAT gates before production release.
