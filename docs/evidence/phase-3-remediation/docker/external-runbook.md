# External Runbook — MySQL 8.4 / Redis 7.4-alpine / Docker Targets

Run on a laptop, VM, or EC2 instance with Docker Engine + Compose v2 and normal
internet access to Docker Hub (this sandbox has neither — see
`docker-install-attempt.txt`: `docker.io` could not even be installed via apt, and
the sandbox's egress allowlist excludes Docker Hub registry hosts regardless).

```bash
cd RatanHR-merged-release-candidate

docker --version
docker compose version

# docker-compose.e2e.yml is the compose file with exact E2E/prod-like tags
docker compose -f docker-compose.e2e.yml config

# Confirm the rendered config contains exactly:
#   image: mysql:8.4
#   image: redis:7.4-alpine
# (docker-compose.yml and docker-compose.prod.yml additionally pin these by
# sha256 digest — see "Static findings" below)

docker compose -f docker-compose.e2e.yml up -d mysql redis

docker compose -f docker-compose.e2e.yml ps
docker compose -f docker-compose.e2e.yml logs mysql
docker compose -f docker-compose.e2e.yml logs redis
```

Then confirm, recording each result in `docs/phase-3-readiness.md`:

- MySQL image resolved is exactly `mysql:8.4`, Redis exactly `redis:7.4-alpine`
  (not MariaDB, not `redis:8.x`, not the drifted `redis:7-alpine` — see below)
- Both containers reach `healthy` in `docker compose ps`
- API container can connect to both (check API logs / `/health`)
- After running the EF runbook's migrations against this MySQL instance:
  `__EFMigrationsHistory` is populated and `ux_payslips_employee_month_year` exists
- Duplicate `(employee_id, month, year)` payslip inserts are rejected by MySQL

```bash
docker build --target build -t ratanhr-build .
docker build --target migrate -t ratanhr-migrate .
docker build --target runtime -t ratanhr-runtime .

# Confirm the migrate image contains the SQL supplements
docker run --rm ratanhr-migrate ls -la /sql-supplements/
```

Expect to see `db_performance.sql`, `db_indexes_fix.sql`, `db_softdelete_fix.sql`
(the `Dockerfile`'s `migrate` stage copies these explicitly — confirmed by static
inspection, not yet by an actual build).

## Static findings from this sandbox (source inspection only, no docker run)

- `docker-compose.e2e.yml`: `mysql:8.4` / `redis:7.4-alpine` — exact, correct.
- `docker-compose.yml` and `docker-compose.prod.yml`: also `mysql:8.4`, pinned
  additionally by `@sha256:...` digest — exact, correct.
- **`Staging/docker-compose.staging.yml` uses `redis:7-alpine`, not
  `redis:7.4-alpine`.** This is a real drift from the required exact tag, though it
  only affects the staging compose file, not the E2E/prod ones the task targets.
  Flagging as a risk item — recommend pinning it to `redis:7.4-alpine` to match the
  rest of the stack before staging is used for any release-gate decision.
- `Dockerfile` has the three expected stages (`build`, `migrate`, `runtime`) and the
  `migrate` stage does `COPY db_performance.sql db_indexes_fix.sql
  db_softdelete_fix.sql /sql-supplements/`.

None of the above is a substitute for actually starting the containers and
observing them go healthy — mark this item VERIFIED only after the commands above
have actually been run.
