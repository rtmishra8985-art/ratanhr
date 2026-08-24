# CI/CD Guide
**HRMS v2.0.0** | GitHub Actions

---

## Pipeline Overview

The pipeline (`.github/workflows/build.yml`) runs on every push and pull_request to `main` and `develop`.

```
push/PR → build-and-test ─┐
        → code-quality    ┘→ docker-build (push only)
```

### Jobs

| Job | Trigger | Purpose |
|-----|---------|---------|
| `build-and-test` | Always | Restore → Build → Test → Publish → Upload artifact |
| `code-quality` | Always (parallel) | `dotnet format --verify-no-changes` |
| `docker-build` | Push only | Validates Docker image builds successfully |

---

## Build Configuration

```yaml
# Fail on any compiler warning
/p:TreatWarningsAsErrors=true
/warnaserror

# Nullable reference types enforced
/p:Nullable=enable
```

The build **fails** if:
- Any compiler warning is emitted
- Any test fails (`RunConfiguration.FailFast=true`)
- Docker image cannot be built

---

## Test Results

Test results are uploaded as artifacts (`test-results/` directory) and published as a PR check via `dorny/test-reporter`.

View test results:
- In PR → **Checks** tab → **HRMS Test Results**
- In Actions → **build-and-test** → **Artifacts** → `test-results`

---

## Code Coverage

Coverage is collected via Coverlet during `dotnet test`:

```bash
/p:CollectCoverage=true
/p:CoverletOutputFormat=opencover
/p:CoverletOutput=./TestResults/coverage.xml
```

Upload to Codecov (optional — add step to build.yml):
```yaml
- uses: codecov/codecov-action@v4
  with:
    file: ./TestResults/coverage.xml
```

---

## Docker Image Caching

The `docker-build` job uses GitHub Actions cache for layer reuse:

```yaml
cache-from: type=gha
cache-to: type=gha,mode=max
```

This reduces build time by ~60% for unchanged layers.

---

## Secrets Required

Set in **Repository Settings → Secrets and variables → Actions**:

| Secret | Description |
|--------|-------------|
| `DOCKER_USERNAME` | DockerHub / registry username (if pushing images) |
| `DOCKER_PASSWORD` | DockerHub / registry password |

No secrets are required for the basic build + test pipeline.

---

## Adding CD (Continuous Deployment)

Add a `deploy` job after `docker-build`:

```yaml
deploy:
  needs: [ docker-build ]
  if: github.ref == 'refs/heads/main'
  runs-on: ubuntu-latest
  steps:
    - name: Deploy to production
      run: |
        ssh deploy@${{ secrets.PROD_HOST }} '
          cd /opt/hrms
          git pull
          docker compose run --rm migrate
          docker compose up -d --build api
        '
```

---

## Branch Protection Rules

Recommended GitHub branch protection for `main`:

- ✅ Require status checks to pass: `build-and-test`, `code-quality`
- ✅ Require branches to be up to date before merging
- ✅ Require pull request reviews: 1 approver
- ✅ Dismiss stale reviews on new commits
- ✅ Do not allow bypassing the above settings
