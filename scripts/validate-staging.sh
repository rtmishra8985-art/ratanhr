#!/usr/bin/env bash
# RatanHR HRMS — safe isolated staging validation runner
#
# Default mode validates the staging configuration only.
# --start additionally starts the isolated staging stack, checks API,
# frontend, and MailHog reachability, then removes the staging containers,
# network, and volumes on exit.
#
# Usage:
#   bash scripts/validate-staging.sh
#   bash scripts/validate-staging.sh --env-file Staging/.env.staging --start
#
# This script never reads, prints, or sends secrets. It refuses placeholder
# values and refuses to use a production compose file.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/Staging/docker-compose.staging.yml"
ENV_FILE="$ROOT_DIR/Staging/.env.staging"
START_STACK=0
KEEP_STACK="${KEEP_STAGING:-0}"

usage() {
  sed -n '1,24p' "$0"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --start)
      START_STACK=1
      shift
      ;;
    --env-file)
      [[ $# -ge 2 ]] || { echo "ERROR: --env-file requires a path." >&2; exit 2; }
      ENV_FILE="$2"
      shift 2
      ;;
    --keep)
      KEEP_STACK=1
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "ERROR: unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

[[ -x "$(command -v docker)" ]] || { echo "ERROR: docker is required." >&2; exit 1; }
[[ -x "$(command -v curl)" ]] || { echo "ERROR: curl is required." >&2; exit 1; }
[[ -f "$COMPOSE_FILE" ]] || { echo "ERROR: staging compose file is missing." >&2; exit 1; }
[[ -f "$ENV_FILE" ]] || {
  echo "ERROR: $ENV_FILE is missing. Copy Staging/staging.env.template and fill staging-only values." >&2
  exit 1
}

case "$COMPOSE_FILE" in
  *docker-compose.staging.yml) ;;
  *) echo "ERROR: refusing a non-staging compose file." >&2; exit 1 ;;
esac

required_keys=(
  STAGING_DB_PASSWORD
  STAGING_REDIS_PASSWORD
  JWT_PRIVATE_KEY_PEM
  JWT_PUBLIC_KEY_PEM
  ENCRYPTION_KEY_STAGING
  SUPERADMIN_INITIAL_PASSWORD
)

for key in "${required_keys[@]}"; do
  value="$(grep -E "^${key}=" "$ENV_FILE" | head -n 1 | cut -d= -f2- || true)"
  if [[ -z "$value" ]]; then
    echo "ERROR: required staging value is missing: $key" >&2
    exit 1
  fi
  if [[ "$value" == *"<REPLACE_"* || "$value" == *"changeme"* || "$value" == *"CHANGE_ME"* ]]; then
    echo "ERROR: placeholder value remains for $key." >&2
    exit 1
  fi
done

echo "Checking staging configuration..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" config --quiet

grep -q '127.0.0.1:3307:3306' "$COMPOSE_FILE"
grep -q '127.0.0.1:6380:6379' "$COMPOSE_FILE"
grep -q '127.0.0.1:8081:8081' "$COMPOSE_FILE"
grep -q '127.0.0.1:3001:80' "$COMPOSE_FILE"
grep -q 'Database__AutoMigrate: "false"' "$COMPOSE_FILE"
grep -q 'Biometric__EnableLiveSync: "false"' "$COMPOSE_FILE"
grep -q 'Hangfire__UseInMemory: "false"' "$COMPOSE_FILE"
grep -q 'name: hrms_staging_net' "$COMPOSE_FILE"
grep -q 'dotnet tool run dotnet-ef' "$ROOT_DIR/Dockerfile"
grep -q 'MYSQL_ROOT_PASSWORD' "$COMPOSE_FILE"

echo "PASS: staging Compose interpolation and isolation settings."

if [[ "$START_STACK" -eq 0 ]]; then
  echo "Runtime checks not requested. Use --start only with approved staging access."
  echo "Authenticated roles, tenant isolation, email triggers, and Hangfire jobs still require approved staging evidence."
  exit 0
fi

compose() {
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" "$@"
}

cleanup() {
  if [[ "$KEEP_STACK" -eq 0 ]]; then
    echo "Cleaning up isolated staging resources..."
    compose down -v --remove-orphans >/dev/null 2>&1 || true
    echo "PASS: staging cleanup completed."
  else
    echo "KEEP_STAGING=1/--keep set; isolated staging stack was left running."
  fi
}
trap cleanup EXIT

echo "Starting isolated staging stack..."
compose up -d

wait_for_url() {
  local label="$1"
  local url="$2"
  local attempts=0
  while (( attempts < 40 )); do
    if curl --silent --show-error --fail --max-time 5 "$url" >/dev/null 2>&1; then
      echo "PASS: $label"
      return 0
    fi
    attempts=$((attempts + 1))
    sleep 3
  done
  echo "ERROR: $label did not become ready: $url" >&2
  compose ps
  compose logs --no-color --tail=80 hrms_staging_api hrms_staging_frontend hrms_staging_mailhog >&2 || true
  exit 1
}

wait_for_url "API /health" "http://127.0.0.1:8081/health"
wait_for_url "API /healthz" "http://127.0.0.1:8081/healthz"
wait_for_url "API /healthz/live" "http://127.0.0.1:8081/healthz/live"
wait_for_url "API /healthz/ready" "http://127.0.0.1:8081/healthz/ready"
wait_for_url "MailHog inbox API" "http://127.0.0.1:8025/api/v1/messages"
wait_for_url "staging frontend" "http://127.0.0.1:3001/"

echo "PASS: isolated staging runtime endpoints are reachable."
echo "BLOCKED: authenticated role, tenant-isolation, workflow, email-trigger, and Hangfire job evidence still require approved staging execution."