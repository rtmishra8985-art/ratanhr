# E2E Compose runbook

## TL;DR

```bash
cp HRMS.SPA.Source/.env.e2e.example .env.e2e     # fill in every value
./scripts/e2e-up.sh                              # brings the stack up, verifies readiness
cd HRMS.SPA.Source && npx playwright test        # full staging suite (631 tests)
./scripts/e2e-up.sh --down                       # tear down
```

## What changed (2026-08-09)

The previous `docker compose -f docker-compose.e2e.yml up -d --wait` run failed
with:

```
OCI runtime exec failed: unable to start container process:
error executing setns process: exit status 1: unknown
```

MySQL and Redis were healthy in reality — both logged "ready for connections" —
but Docker could not `exec` into the containers to run the healthcheck, so
Compose marked them `unhealthy` and never started `api` or `spa`.

Fixes applied:

| ID | File | Fix |
|----|------|-----|
| E2E-COMPOSE-002 | `docker-compose.e2e.yml` | `ConnectionStrings__DefaultConnection` was a folded (`>-`) YAML scalar, so the emitted connection string contained embedded spaces. Now a single-line quoted string. |
| E2E-COMPOSE-003 | `docker-compose.e2e.yml` | The `spa` service bind-mounted the source `:ro`, so `bun install` / `vite build` failed with `EROFS`. Mount is now writable with `node_modules` and `dist` isolated in anonymous volumes, so the host checkout stays clean. |
| E2E-COMPOSE-004 | `docker-compose.e2e.yml` | MySQL and Redis healthchecks converted from bare `CMD` argv execs to `CMD-SHELL`, with the MySQL password read from the container environment instead of argv (no credential leak via `docker inspect`), payload assertions (`PONG`), and longer `start_period`/`retries` so a cold initdb is not failed prematurely. |
| E2E-COMPOSE-005 | `docker-compose.e2e.yml` | The API healthcheck used `curl`, which the `aspnet` runtime image does not ship — it exited 127 forever. Now tries `curl`, then `wget`. |
| E2E-COMPOSE-006 | `docker-compose.e2e.nohealthcheck.yml` (new) | Overlay that disables in-container healthchecks and relaxes `depends_on` to `service_started`, for hosts where `docker exec` is broken. |
| E2E-COMPOSE-007 | `scripts/e2e-up.sh` (new) | Resilient bring-up: tries `up -d --wait`; on a `setns`/exec failure automatically retries with the overlay and verifies readiness **from the host** over TCP (MySQL 3307, Redis 6380) and HTTP (`/health` on 8082, `/` on 3000). Writes all diagnostics to `evidence/e2e-compose/`. |

## Permanent host fix

`setns` exec failures are a host-level runc/kernel mismatch, not a compose bug.
On the staging server:

```bash
docker version                       # check Docker >= 25
runc --version                       # check runc >= 1.1.12
sudo apt-get update && sudo apt-get install --only-upgrade docker-ce docker-ce-cli containerd.io
sudo systemctl restart docker
```

After upgrading, `./scripts/e2e-up.sh` will succeed on attempt 1 and the
fallback overlay is never used.

## Ports

| Service | Host port | Container port |
|---------|-----------|----------------|
| MySQL   | 3307      | 3306 |
| Redis   | 6380      | 6379 |
| API     | 8082      | 8080 |
| SPA     | 3000      | 3000 |
