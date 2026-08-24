#!/usr/bin/env bash
# =============================================================================
# scripts/verify-phase3.sh — Phase 3: Runtime & Container Verification
#
# Runs every Phase 3 carry-over and verification item in one shot and captures
# FULL UNABRIDGED output to evidence/phase3/*.txt, then prints
# "PHASE 3 STATUS: PASS | FAIL | BLOCKED" with explicit blockers.
#
# HARD REQUIREMENT: a host with a REAL Docker daemon. gVisor sandboxes are
# rejected up front — no chroot/skopeo simulation fallback (see check 0).
#
# Usage:
#   ./scripts/verify-phase3.sh              # full run
#   ./scripts/verify-phase3.sh --down       # tear down everything
#
# Secrets: a throwaway .env.phase3 is generated on the fly (gitignored) and
# deleted by --down. Nothing is ever committed.
# =============================================================================
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

EV="evidence/phase3"
ENV_FILE=".env.phase3"
E2E_ENV_FILE=".env.e2e"
mkdir -p "$EV"

PASS=0; FAIL=0; BLOCK=0
ok(){   echo "PASS    $1"; PASS=$((PASS+1)); }
bad(){  echo "FAIL    $1"; FAIL=$((FAIL+1)); FAILURES+=("$1"); }
blk(){  echo "BLOCKED $1"; BLOCK=$((BLOCK+1)); BLOCKERS+=("$1"); }
has(){  command -v "$1" >/dev/null 2>&1; }

preflight_docker(){
  echo "== PREFLIGHT: Real Docker daemon =="
  local blocked=0
  local reason=""

  if grep -qi gvisor /proc/version 2>/dev/null || uname -a | grep -qi gvisor; then
    reason="host kernel reports gVisor"
    blocked=1
  elif ! has docker; then
    reason="docker CLI is not installed or not on PATH"
    blocked=1
  elif ! docker info >"$EV/00-docker-info.txt" 2>&1; then
    reason="docker CLI cannot reach a daemon (docker info failed)"
    blocked=1
  else
    docker version >"$EV/00-docker-version.txt" 2>&1
    if grep -qi 'runtime.*runsc\|gvisor' "$EV/00-docker-info.txt"; then
      reason="docker daemon is backed by gVisor (runsc); a real runc/containerd daemon is required"
      blocked=1
    fi
  fi

  if [ "$blocked" -eq 0 ]; then
    ok "real Docker daemon reachable (not gVisor)"
    return 0
  fi

  echo
  echo "PHASE 3 STATUS: BLOCKED"
  echo "  - $reason"
  echo
  cat <<'INSTRUCTIONS'
BLOCKED: Phase 3 requires a REAL Docker daemon.

A real Docker daemon is one that can:
  - build multi-stage images (docker build --target ...)
  - run Linux containers with runc/containerd (not gVisor/runsc)
  - bind ports, create networks, and execute docker compose stacks

You are currently on a sandbox/gVisor host where no such daemon exists.

What to do next:
  1. Move to a Linux host, macOS with Docker Desktop, or Windows with WSL2
     Docker that exposes a real Docker socket.
  2. Ensure the docker CLI can run:
       docker info
       docker run --rm hello-world
  3. Re-run this script from that host:
       ./scripts/verify-phase3.sh
  4. Do NOT attempt to simulate Docker with chroot, skopeo, podman --rootless,
     or any other workaround — Phase 3 must verify actual container behaviour.
INSTRUCTIONS
  exit 2
}


DC="docker compose -f docker-compose.yml -f docker-compose.override.yml --env-file $ENV_FILE"
DCE2E="docker compose -f docker-compose.e2e.yml --env-file $E2E_ENV_FILE"

# ── teardown ────────────────────────────────────────────────────────────────
if [ "${1:-}" = "--down" ]; then
  $DC down -v --remove-orphans
  $DCE2E down -v --remove-orphans
  rm -f "$ENV_FILE" "$E2E_ENV_FILE"
  echo "torn down; throwaway env files removed"
  exit 0
fi

preflight_docker

# ── throwaway env (never committed) ─────────────────────────────────────────
echo "== 0b. Throwaway .env generation =="
gen(){ openssl rand -base64 "${1:-32}" | tr -d '\n=+/' | cut -c1-"${2:-32}"; }
if [ ! -f "$ENV_FILE" ]; then
  MYSQL_PASSWORD="$(gen 32 28)"; MYSQL_ROOT_PASSWORD="$(gen 32 28)"; REDIS_PASSWORD="$(gen 32 28)"
  ENCRYPTION_KEY="$(openssl rand -base64 32)"
  openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out /tmp/phase3_jwt.pem 2>/dev/null
  openssl rsa -in /tmp/phase3_jwt.pem -pubout -out /tmp/phase3_jwt.pub.pem 2>/dev/null
  JWT_PRIVATE_KEY_PEM="$(awk '{printf "%s\\n", $0}' /tmp/phase3_jwt.pem)"
  JWT_PUBLIC_KEY_PEM="$(awk '{printf "%s\\n", $0}' /tmp/phase3_jwt.pub.pem)"
  rm -f /tmp/phase3_jwt.pem /tmp/phase3_jwt.pub.pem
  REDIS_CONN="redis:6379,password=${REDIS_PASSWORD},ssl=False,abortConnect=False"
  cat > "$ENV_FILE" <<ENVEOF
# THROWAWAY Phase 3 verification values — generated $(date -u +%FT%TZ). DO NOT COMMIT.
MYSQL_DATABASE=hrms_db
MYSQL_USER=hrms
MYSQL_PASSWORD=${MYSQL_PASSWORD}
MYSQL_ROOT_PASSWORD=${MYSQL_ROOT_PASSWORD}
ConnectionStrings__DefaultConnection=Server=mysql;Port=3306;Database=hrms_db;User ID=hrms;Password=${MYSQL_PASSWORD};AllowPublicKeyRetrieval=True;SslMode=Preferred
JWT_PRIVATE_KEY_PEM=${JWT_PRIVATE_KEY_PEM}
JWT_PUBLIC_KEY_PEM=${JWT_PUBLIC_KEY_PEM}
Jwt__PrivateKeyPem=${JWT_PRIVATE_KEY_PEM}
Jwt__PublicKeyPem=${JWT_PUBLIC_KEY_PEM}
ENCRYPTION_KEY=${ENCRYPTION_KEY}
Security__EncryptionKey=${ENCRYPTION_KEY}
REDIS_PASSWORD=${REDIS_PASSWORD}
REDIS_CONNECTION_STRING=${REDIS_CONN}
Redis__ConnectionString=${REDIS_CONN}
Hangfire__UseRedis=true
Hangfire__UseInMemory=false
Hangfire__RedisConnectionString=${REDIS_CONN}
# Phase 3 explicitly verifies Redis OUTSIDE Development.
ASPNETCORE_ENVIRONMENT=Staging
APP_ENV=staging
DOMAIN_NAME=localhost
API_URL=http://localhost:8080/api
APP_BASE_URL=http://localhost:8080
APP_COMPANY_NAME=RatanHR
APP_SUPPORT_EMAIL=support@example.invalid
APP_SUPPORT_NAME=RatanHR Support
AllowedHosts=localhost
ALLOWED_HOSTS=localhost
ALLOWED_ORIGINS=http://localhost:8080
CORS_ALLOWED_ORIGINS=http://localhost:8080
SWAGGER_USERNAME=phase3
SWAGGER_PASSWORD=$(gen 24 20)
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=$(gen 24 20)
BACKUP_ENCRYPTION_KEY=$(openssl rand -base64 32)
OTEL_OTLP_ENDPOINT=
OTEL_OTLP_PROTOCOL=grpc
OTEL_EXPORT_METRICS_VIA_OTLP=false
ENVEOF
  chmod 600 "$ENV_FILE"
fi
[ -f "$E2E_ENV_FILE" ] || { cp .env.e2e.template "$E2E_ENV_FILE" 2>/dev/null && chmod 600 "$E2E_ENV_FILE"; }
ok "throwaway $ENV_FILE generated (gitignored, removed by --down)"

# ── 1. spa-builder stage ────────────────────────────────────────────────────
echo "== 1. docker build --target spa-builder =="
if docker build --target spa-builder --progress=plain -t hrms:spa-builder . >"$EV/01-build-spa-builder.txt" 2>&1; then
  ok "docker build --target spa-builder"
else
  bad "docker build --target spa-builder (see $EV/01-build-spa-builder.txt)"
fi

# ── 2. bun run build:ci inside oven/bun:1.2.0-alpine ────────────────────────
echo "== 2. bun run build:ci inside oven/bun:1.2.0-alpine =="
# Carry-over 3: assert the SPA build really succeeds in the pinned Bun image.
# Bun only — never npm install.
if grep -aq "bun run build:ci" "$EV/01-build-spa-builder.txt" && \
   docker run --rm -v "$ROOT/HRMS.SPA.Source":/spa:ro -w /spa oven/bun:1.2.0-alpine \
     sh -c 'cp -r /spa /tmp/spa && cd /tmp/spa && bun install --frozen-lockfile && bun run build:ci && ls -la dist/public' \
     >"$EV/02-bun-build-ci.txt" 2>&1; then
  ok "bun run build:ci inside oven/bun:1.2.0-alpine"
else
  bad "bun run build:ci inside oven/bun:1.2.0-alpine (see $EV/02-bun-build-ci.txt)"
fi

# ── 3. full multi-stage build ───────────────────────────────────────────────
echo "== 3. docker build (spa-builder -> build -> migrate -> runtime) =="
for stage in build migrate runtime; do
  if docker build --target "$stage" --progress=plain -t "hrms:$stage" . >"$EV/03-build-$stage.txt" 2>&1; then
    ok "docker build --target $stage"
  else
    bad "docker build --target $stage (see $EV/03-build-$stage.txt)"
  fi
done
if docker build --progress=plain -t hrms:phase3 . >"$EV/03-build-full.txt" 2>&1; then
  ok "docker build . (all stages)"
else
  bad "docker build . (see $EV/03-build-full.txt)"
fi

# ── 4. compose up: MySQL + Redis + API ──────────────────────────────────────
echo "== 4. docker compose up -d (mysql + redis + api) =="
$DC config >"$EV/04-compose-config.txt" 2>&1
if $DC up -d mysql redis migrate api >"$EV/04-compose-up.txt" 2>&1; then
  ok "docker compose up -d"
else
  bad "docker compose up -d (see $EV/04-compose-up.txt)"
fi
$DC ps >"$EV/04-compose-ps.txt" 2>&1

# ── 5. EF Core migrations via the migrate stage (no new migrations) ─────────
echo "== 5. EF Core migrations via migrate stage =="
# NOTE: we only APPLY existing migrations. Never `migrations add`; never touch
# HRMS.Infrastructure/Migrations.
$DC logs migrate >"$EV/05-migrate-logs.txt" 2>&1
MIG_RC="$($DC ps -a --format '{{.Service}} {{.ExitCode}}' 2>/dev/null | awk '$1=="migrate"{print $2}')"
if [ "${MIG_RC:-1}" = "0" ]; then
  ok "migrate stage applied EF Core migrations cleanly (exit 0)"
else
  bad "migrate stage exited ${MIG_RC:-unknown} (see $EV/05-migrate-logs.txt)"
fi

# ── 6. health + swagger status codes ────────────────────────────────────────
echo "== 6. /health, /health/ready, /swagger =="
BASE="${API_BASE:-http://localhost:8080}"
for i in $(seq 1 60); do curl -sf -o /dev/null "$BASE/health" && break; sleep 2; done
: >"$EV/06-endpoints.txt"
for path in /health /health/ready /swagger; do
  code="$(curl -s -o "$EV/06-body${path//\//-}.txt" -w '%{http_code}' -u "phase3:$(grep '^SWAGGER_PASSWORD=' "$ENV_FILE" | cut -d= -f2-)" "$BASE$path")"
  echo "$path -> $code" | tee -a "$EV/06-endpoints.txt"
  case "$path:$code" in
    /health:200|/health/ready:200|/swagger:200|/swagger:301|/swagger:302) ok "GET $path -> $code" ;;
    *) bad "GET $path -> $code (expected 200)" ;;
  esac
done

# ── 7. Redis connectivity outside Development ───────────────────────────────
echo "== 7. Redis connectivity (ASPNETCORE_ENVIRONMENT=Staging) =="
$DC exec -T api printenv ASPNETCORE_ENVIRONMENT >"$EV/07-api-env.txt" 2>&1
$DC exec -T redis redis-cli -a "$(grep '^REDIS_PASSWORD=' "$ENV_FILE" | cut -d= -f2-)" ping >"$EV/07-redis-ping.txt" 2>&1
# The API health report names the redis check; a Degraded/Unhealthy redis entry fails here.
curl -s "$BASE/health/ready" >"$EV/07-health-ready.json" 2>&1
if grep -q '^Staging$' "$EV/07-api-env.txt" && grep -qi 'PONG' "$EV/07-redis-ping.txt" \
   && ! grep -qi '"redis"[^}]*"Unhealthy"' "$EV/07-health-ready.json"; then
  ok "Redis reachable from API outside Development"
else
  bad "Redis connectivity outside Development (see $EV/07-*.txt)"
fi

# ── 8. e2e suite ────────────────────────────────────────────────────────────
echo "== 8. e2e suite (docker-compose.e2e.yml) =="
if ./scripts/e2e-up.sh >"$EV/08-e2e-up.txt" 2>&1; then
  ok "e2e stack up"
  if $DCE2E run --rm spa bun run e2e >"$EV/08-e2e-run.txt" 2>&1; then
    ok "e2e suite"
  else
    bad "e2e suite (see $EV/08-e2e-run.txt)"
  fi
else
  bad "e2e stack bring-up (see $EV/08-e2e-up.txt)"
fi

# ── 9. container logs for any non-zero exit ─────────────────────────────────
echo "== 9. non-zero container exits =="
: >"$EV/09-nonzero-exits.txt"
NONZERO=0
for svc in $($DC ps -a --services 2>/dev/null) ; do
  code="$($DC ps -a --format '{{.Service}} {{.ExitCode}}' | awk -v s="$svc" '$1==s{print $2}')"
  state="$($DC ps -a --format '{{.Service}} {{.State}}'    | awk -v s="$svc" '$1==s{print $2}')"
  if [ "$state" = "exited" ] && [ "${code:-0}" != "0" ]; then
    NONZERO=1
    { echo "=== $svc exited $code ==="; $DC logs --no-color "$svc"; echo; } >>"$EV/09-nonzero-exits.txt" 2>&1
  fi
done
[ "$NONZERO" = "0" ] && ok "no service exited non-zero" || bad "non-zero container exits (full logs in $EV/09-nonzero-exits.txt)"

# ── summary ─────────────────────────────────────────────────────────────────
echo
echo "checks: PASS=$PASS FAIL=$FAIL BLOCKED=$BLOCK   evidence: $EV/"
if   [ "$BLOCK" -gt 0 ]; then echo "PHASE 3 STATUS: BLOCKED"; printf '  - %s\n' "${BLOCKERS[@]}"; exit 2
elif [ "$FAIL"  -gt 0 ]; then echo "PHASE 3 STATUS: FAIL";    printf '  - %s\n' "${FAILURES[@]}"; exit 1
else                          echo "PHASE 3 STATUS: PASS";    exit 0
fi
