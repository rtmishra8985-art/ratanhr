#!/usr/bin/env bash
# =============================================================================
# scripts/pre-deploy-check.sh — RatanHR pre-deployment gate
#
# FIX SEC-08: Verifies that all deployment-time substitutions have been made
# before the nginx config / Docker stack is started. Run this script as the
# first step in any CI/CD deploy pipeline.
#
# Usage:
#   chmod +x scripts/pre-deploy-check.sh
#   ./scripts/pre-deploy-check.sh
#
# Exits 1 with a human-readable error if any check fails.
# =============================================================================

set -euo pipefail

ERRORS=0

echo "── RatanHR Pre-Deploy Check ────────────────────────────────────────────"

# ── 1. nginx: YOUR_DOMAIN_NAME placeholder must be replaced ─────────────────
if grep -q 'YOUR_DOMAIN_NAME' nginx/nginx.conf 2>/dev/null; then
    echo "❌ FAIL nginx/nginx.conf still contains YOUR_DOMAIN_NAME."
    echo "   Run: sed -i 's/YOUR_DOMAIN_NAME/app.yourdomain.com/g' nginx/nginx.conf"
    ERRORS=$((ERRORS + 1))
else
    echo "✅ PASS nginx/nginx.conf: domain placeholder replaced."
fi

# ── 2. Dockerfile SDK stage must be digest-pinned ───────────────────────────
if grep -qE 'dotnet/sdk:[^@]+@sha256:' Dockerfile 2>/dev/null; then
    echo "✅ PASS Dockerfile: SDK stage is digest-pinned."
else
    echo "❌ FAIL Dockerfile SDK stage is not digest-pinned."
    echo "   Run: chmod +x scripts/pin-docker-digests.sh && ./scripts/pin-docker-digests.sh"
    ERRORS=$((ERRORS + 1))
fi

# ── 3. RSA key pair env vars must be set (not placeholder values) ────────────
if [ -z "${JWT_PRIVATE_KEY_PEM:-}" ]; then
    echo "❌ FAIL JWT_PRIVATE_KEY_PEM is not set."
    echo "   Run: chmod +x scripts/generate-rsa-keys.sh && ./scripts/generate-rsa-keys.sh"
    ERRORS=$((ERRORS + 1))
elif echo "${JWT_PRIVATE_KEY_PEM}" | grep -qi 'YOUR_\|PLACEHOLDER\|CHANGEME'; then
    echo "❌ FAIL JWT_PRIVATE_KEY_PEM looks like a placeholder value."
    ERRORS=$((ERRORS + 1))
else
    echo "✅ PASS JWT_PRIVATE_KEY_PEM is set."
fi

if [ -z "${JWT_PUBLIC_KEY_PEM:-}" ]; then
    echo "❌ FAIL JWT_PUBLIC_KEY_PEM is not set."
    ERRORS=$((ERRORS + 1))
elif echo "${JWT_PUBLIC_KEY_PEM}" | grep -qi 'YOUR_\|PLACEHOLDER\|CHANGEME'; then
    echo "❌ FAIL JWT_PUBLIC_KEY_PEM looks like a placeholder value."
    ERRORS=$((ERRORS + 1))
else
    echo "✅ PASS JWT_PUBLIC_KEY_PEM is set."
fi

# ── 4. Encryption key must be set ───────────────────────────────────────────
if [ -z "${ENCRYPTION_KEY:-}" ]; then
    echo "❌ FAIL ENCRYPTION_KEY is not set."
    ERRORS=$((ERRORS + 1))
else
    echo "✅ PASS ENCRYPTION_KEY is set."
fi

# ── 5. Database password must not be empty ──────────────────────────────────
if [ -z "${MYSQL_PASSWORD:-}" ]; then
    echo "❌ FAIL MYSQL_PASSWORD is not set."
    ERRORS=$((ERRORS + 1))
else
    echo "✅ PASS MYSQL_PASSWORD is set."
fi

# ── 6. packages.lock.json files must be present for all projects ─────────────
LOCK_FILES_OK=true
for proj in HRMS.API HRMS.Tests HRMS.Application HRMS.Infrastructure HRMS.Domain; do
    if [ ! -f "${proj}/packages.lock.json" ]; then
        echo "❌ FAIL ${proj}/packages.lock.json is missing."
        echo "   Run: dotnet restore --use-lock-file in that project."
        LOCK_FILES_OK=false
        ERRORS=$((ERRORS + 1))
    fi
done
$LOCK_FILES_OK && echo "✅ PASS All NuGet packages.lock.json files present."

# ── 7. .env.e2e template must not contain un-filled placeholders ─────────────
#  (only checked when the file exists — staging/local runs)
if [ -f "HRMS.SPA.Source/.env.e2e" ]; then
    if grep -q 'YOUR_\|CHANGEME\|PLACEHOLDER' "HRMS.SPA.Source/.env.e2e" 2>/dev/null; then
        echo "❌ FAIL HRMS.SPA.Source/.env.e2e still contains placeholder values."
        ERRORS=$((ERRORS + 1))
    else
        echo "✅ PASS HRMS.SPA.Source/.env.e2e: no placeholder values."
    fi
fi

# ── Summary ──────────────────────────────────────────────────────────────────
echo "────────────────────────────────────────────────────────────────────────"
if [ "${ERRORS}" -eq 0 ]; then
    echo "✅ All pre-deploy checks passed. Safe to deploy."
    exit 0
else
    echo "❌ ${ERRORS} pre-deploy check(s) FAILED. Fix the issues above before deploying."
    exit 1
fi
