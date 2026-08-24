# Phase 3 — How to actually run the verification

## The blocker (and what it is not)

`scripts/verify-phase3.sh` refuses to run unless it finds a **real Docker daemon
(runc/containerd)**. That preflight is intentional (script lines 35–60). It cannot
be satisfied inside a gVisor sandbox, which has:

- kernel `4.19.0-gvisor`
- no `docker` CLI, no `/var/run/docker.sock`
- no privileges to start `dockerd`

This is an **environment** blocker, not a repository defect. A static audit of the
build inputs found no missing files:

| Dockerfile input | Status |
| --- | --- |
| `HRMS.SPA.Source/package.json`, `bun.lock` | present |
| `HRMS.API/…/packages.lock.json` (all 5 projects) | present |
| `docker/migrate-entrypoint.sh` | present |
| `.config/dotnet-tools.json` | present |
| SPA `build:ci` -> `dist/public` (matches `COPY --from=spa-builder /spa/dist/public`) | consistent |
| `global.json` SDK `8.0.416` == Dockerfile `sdk:8.0.416-alpine3.21` | consistent |

## Option A — GitHub Actions (no local setup)

`.github/workflows/phase3-verify.yml` runs the whole Phase 3 suite on
`ubuntu-latest`, which is a real runc/containerd Docker host with .NET 8.0.416
pinned from `global.json`.

1. Push this repo to GitHub.
2. **Actions → "Phase 3 Verification (real Docker daemon)" → Run workflow.**

The job:
- asserts the runner is not gVisor and the SDK matches `global.json`
- runs `docker build --target spa-builder .` and `docker build .` as separate,
  fully logged steps
- runs `./scripts/verify-phase3.sh`
- prints every `evidence/phase3/*.txt` file unabridged into the job log
- uploads them as the **`phase3-evidence`** artifact
- fails the job unless the script prints `PHASE 3 STATUS: PASS`
- tears down compose stacks and deletes `.env.phase3` / `.env.e2e` afterwards

## Option B — any Linux VM / laptop with Docker

Requirements: Docker Engine (runc), Docker Compose v2, .NET SDK 8.0.416, `openssl`, `curl`.

```bash
unzip hrms-phase3-*.zip -d hrms && cd hrms
chmod +x scripts/*.sh docker/*.sh
docker info | grep -i 'Server Version\|Runtime'   # must NOT say runsc/gvisor
dotnet --version                                  # must print 8.0.416

docker build --target spa-builder --progress=plain -t hrms:spa-builder . 2>&1 | tee build-spa-builder.txt
docker build --progress=plain -t hrms:phase3 .    2>&1 | tee build-full.txt
./scripts/verify-phase3.sh                        2>&1 | tee phase3-run.txt

# teardown
./scripts/verify-phase3.sh --down
```

Evidence lands in `evidence/phase3/*.txt`. Share those files (or the CI artifact)
to get the run analyzed.

## Status

`PHASE 3 STATUS: BLOCKED` remains correct **until one of the two options above is
executed**. Nothing in this repo can change that from inside a sandbox — the
verification is by definition a runtime/container test.
