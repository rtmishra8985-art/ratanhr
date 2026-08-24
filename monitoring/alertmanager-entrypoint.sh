#!/bin/sh
# =============================================================
# monitoring/alertmanager-entrypoint.sh
#
# RHR-003 FIX: alertmanager.yml previously embedded shell-style
# ${VAR:-default} placeholders directly in the file mounted into the
# container. Alertmanager's own YAML parser has no variable-substitution
# support, so it read those placeholders as literal strings (e.g. the
# smtp_smarthost literally became the string
# "${ALERTMANAGER_SMTP_SMARTHOST:-smtp.example.com:587}"), which fails
# YAML/address parsing and crash-loops the container indefinitely
# (visible via `docker compose ps` / `docker logs alertmanager`).
#
# NOTE: unlike nginx's alpine-based image, prom/alertmanager's minimal
# base image does not ship `envsubst` (gettext). This entrypoint uses
# plain `sed` (always present) to substitute six known placeholders
# instead of installing a package at container start.
# =============================================================
set -eu

ALERTMANAGER_SMTP_FROM="${ALERTMANAGER_SMTP_FROM:-alerts@example.com}"
ALERTMANAGER_SMTP_SMARTHOST="${ALERTMANAGER_SMTP_SMARTHOST:-smtp.example.com:587}"
ALERTMANAGER_SMTP_USERNAME="${ALERTMANAGER_SMTP_USERNAME:-}"
ALERTMANAGER_SMTP_PASSWORD="${ALERTMANAGER_SMTP_PASSWORD:-}"
ALERTMANAGER_EMAIL_TO="${ALERTMANAGER_EMAIL_TO:-ops@example.com}"
ALERTMANAGER_ONCALL_EMAIL="${ALERTMANAGER_ONCALL_EMAIL:-oncall@example.com}"

echo "[alertmanager-entrypoint] Expanding alertmanager.yml.template..."

sed \
    -e "s|\${ALERTMANAGER_SMTP_FROM}|${ALERTMANAGER_SMTP_FROM}|g" \
    -e "s|\${ALERTMANAGER_SMTP_SMARTHOST}|${ALERTMANAGER_SMTP_SMARTHOST}|g" \
    -e "s|\${ALERTMANAGER_SMTP_USERNAME}|${ALERTMANAGER_SMTP_USERNAME}|g" \
    -e "s|\${ALERTMANAGER_SMTP_PASSWORD}|${ALERTMANAGER_SMTP_PASSWORD}|g" \
    -e "s|\${ALERTMANAGER_EMAIL_TO}|${ALERTMANAGER_EMAIL_TO}|g" \
    -e "s|\${ALERTMANAGER_ONCALL_EMAIL}|${ALERTMANAGER_ONCALL_EMAIL}|g" \
    /etc/alertmanager/alertmanager.yml.template \
    > /etc/alertmanager/alertmanager.yml

# Guard: fail fast rather than start with literal ${...} placeholders in any
# ACTIVE (non-comment) line. The template intentionally leaves optional Slack/
# PagerDuty blocks commented out with their own ${ALERTMANAGER_SLACK_WEBHOOK_URL}
# / ${ALERTMANAGER_PAGERDUTY_ROUTING_KEY} placeholders for operators to fill in
# manually if they uncomment those receivers -- those are expected to remain
# unless the operator opts in, so only check uncommented lines.
if grep -v '^\s*#' /etc/alertmanager/alertmanager.yml | grep -q '\${ALERTMANAGER_'; then
    echo "[alertmanager-entrypoint] ERROR: unsubstituted variables remain in an active config line:" >&2
    grep -v '^\s*#' /etc/alertmanager/alertmanager.yml | grep '\${ALERTMANAGER_' >&2
    exit 1
fi

echo "[alertmanager-entrypoint] Config generated. Starting Alertmanager..."
exec /bin/alertmanager \
    --config.file=/etc/alertmanager/alertmanager.yml \
    --storage.path=/alertmanager \
    --web.listen-address=:9093
