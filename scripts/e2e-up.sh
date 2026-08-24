#!/usr/bin/env bash
# =============================================================================
# scripts/e2e-up.sh — resilient bring-up for the E2E stack
#
# Strategy
#   1. Try the normal path:  docker compose ... up -d --wait
#   2. If that fails because the host cannot exec into containers
#      ("error executing setns process"), retry with
#      docker-compose.e2e.nohealthcheck.yml and assert readiness from the
#      HOST over TCP/HTTP instead of via in-container healthchecks.
#   3. Print per-service status and exit non-zero if anything is not ready.
#
# Usage
#   cp .env.e2e.template .env.e2e   # fill in the values
#   ./scripts/e2e-up.sh
#   ./scripts/e2e-up.sh --down      # tear everything down
# =============================================================================
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

COMPOSE_FILE="docker-compose.e2e.yml"
FALLBACK_FILE="docker-compose.e2e.nohealthcheck.yml"
ENV_FILE="${ENV_FILE:-.env.e2e}"
LOG_DIR="${LOG_DIR:-evidence/e2e-compose}"

MYSQL_PORT="${MYSQL_HOST_PORT:-3307}"
REDIS_PORT="${REDIS_HOST_PORT:-6380}"
API_PORT="${API_HOST_PORT:-8082}"
SPA_PORT="${SPA_HOST_PORT:-3000}"

mkdir -p "$LOG_DIR"

log() { printf '\033[1;34m[e2e-up]\033[0m %s\n' "$*"; }
err() { printf '\033[1;31m[e2e-up]\033[0m %s\n' "$*" >&2; }

compose() { docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" "$@"; }
compose_fallback() {
  docker compose -f "$COMPOSE_FILE" -f "$FALLBACK_FILE" --env-file "$ENV_FILE" "$@"
}

if [[ "${1:-}" == "--down" ]]; then
  log "Tearing down the E2E stack"
  compose_fallback down -v --remove-orphans
  exit $?
fi

# ── Preflight ───────────────────────────────────────────────────────────────
command -v docker >/dev/null 2>&1 || { err "docker is not installed"; exit 127; }
docker compose version >/dev/null 2>&1 || { err "docker compose v2 plugin is required"; exit 127; }
[[ -f "$ENV_FILE" ]] || { err "$ENV_FILE not found — copy HRMS.SPA.Source/.env.e2e.example and fill it in"; exit 2; }

log "Validating compose configuration"
if ! compose config >"$LOG_DIR/compose-config.yml" 2>"$LOG_DIR/compose-config.err"; then
  err "docker compose config failed:"; cat "$LOG_DIR/compose-config.err" >&2; exit 2
fi

# ── Host-side readiness probes (no docker exec involved) ────────────────────
wait_tcp() { # host port name timeout
  local port="$1" name="$2" timeout="${3:-180}" i=0
  while (( i < timeout )); do
    if (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; then
      exec 3>&- 2>/dev/null || true
      log "$name is accepting connections on 127.0.0.1:$port"
      return 0
    fi
    sleep 1; ((i++))
  done
  err "$name did not open 127.0.0.1:$port within ${timeout}s"
  return 1
}

wait_http() { # port path name timeout [expected-substring]
  local port="$1" path="$2" name="$3" timeout="${4:-240}" expect="${5:-}" i=0 body
  while (( i < timeout )); do
    body="$(curl -fsS --max-time 5 "http://127.0.0.1:${port}${path}" 2>/dev/null)" && {
      if [[ -z "$expect" || "$body" == *"$expect"* ]]; then
        log "$name responded on http://127.0.0.1:${port}${path}"
        return 0
      fi
    }
    sleep 2; ((i+=2))
  done
  err "$name was not ready on http://127.0.0.1:${port}${path} within ${timeout}s"
  return 1
}

check_all_ready() {
  local rc=0
  wait_tcp  "$MYSQL_PORT" "MySQL" 240      || rc=1
  wait_tcp  "$REDIS_PORT" "Redis" 60       || rc=1
  wait_http "$API_PORT" "/health" "API" 300 "Healthy" || rc=1
  wait_http "$SPA_PORT" "/" "SPA" 300      || rc=1
  return $rc
}

# ── Attempt 1: standard `up --wait` ─────────────────────────────────────────
log "Attempt 1/2 — docker compose up -d --wait"
if compose up -d --wait --wait-timeout 420 2>&1 | tee "$LOG_DIR/up-wait.log"; then
  log "Compose reported all services healthy"
  compose ps | tee "$LOG_DIR/ps-healthy.log"
  check_all_ready && { log "E2E stack is UP (healthchecks path)"; exit 0; }
  err "Compose said healthy but host probes failed — see $LOG_DIR"
  exit 1
fi

# ── Diagnose ────────────────────────────────────────────────────────────────
compose ps -a > "$LOG_DIR/ps-after-failure.log" 2>&1 || true
compose logs --no-color --tail 200 > "$LOG_DIR/logs-after-failure.log" 2>&1 || true

if grep -qiE 'setns process|OCI runtime exec failed|unable to start container process' \
     "$LOG_DIR/up-wait.log" "$LOG_DIR/logs-after-failure.log" 2>/dev/null; then
  err "Host cannot exec into containers (runc/setns). Docker healthchecks are"
  err "unusable on this host. Upgrade runc >= 1.1.12 / Docker >= 25 for a"
  err "permanent fix. Falling back to host-side readiness probes."
else
  err "up --wait failed for a reason other than setns — retrying without"
  err "in-container healthchecks so the real error surfaces in the logs."
fi

# ── Attempt 2: fallback overlay + host-side probes ──────────────────────────
log "Attempt 2/2 — compose up -d with healthchecks disabled"
compose_fallback up -d 2>&1 | tee "$LOG_DIR/up-fallback.log" || {
  err "Fallback bring-up failed"; compose_fallback logs --no-color --tail 200 > "$LOG_DIR/logs-fallback.log" 2>&1 || true
  exit 1
}

if check_all_ready; then
  compose_fallback ps | tee "$LOG_DIR/ps-fallback.log"
  log "E2E stack is UP (fallback path — readiness verified from the host)"
  exit 0
fi

err "E2E stack did not become ready. Diagnostics written to $LOG_DIR"
compose_fallback ps -a > "$LOG_DIR/ps-fallback-failed.log" 2>&1 || true
compose_fallback logs --no-color --tail 400 > "$LOG_DIR/logs-fallback-failed.log" 2>&1 || true
exit 1
