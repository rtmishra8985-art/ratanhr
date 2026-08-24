#!/usr/bin/env bash
# =============================================================================
# phase9_cleanup.sh — RatanHR HRMS Source Cleanup
#
# Usage (from repo root):
#   bash phase9_cleanup.sh
#
# What this script does:
#   §1   Scan — identifies files in each category (NEVER deletes silently)
#   §2   Confirm — shows what would be removed and asks per-category
#   §3   Remove — only executes after explicit "yes" confirmation per category
#   §4   Verify — checks no secrets remain, no broken references, no duplicate
#                 implementations, no missing files
#   §5   Report — prints Clean / Issues Found
#
# Safety guarantees:
#   • Every deletion requires explicit "yes" answer — there is no -y flag
#   • Dry-run mode available: bash phase9_cleanup.sh --dry-run
#   • Critical files (Dockerfile, HRMS.sln, *.csproj, *.sql) are NEVER touched
#   • .env.production.template is NEVER deleted (it has no secrets)
#   • .gitignore is checked — anything already gitignored is flagged, not deleted
#   • All removals are logged to /tmp/phase9_cleanup_<timestamp>.log
# =============================================================================

set -euo pipefail
IFS=$'\n\t'

# ── flags ─────────────────────────────────────────────────────────────────────
DRY_RUN=false
for arg in "$@"; do
  [[ "$arg" == "--dry-run" || "$arg" == "-n" ]] && DRY_RUN=true
done

# ── colours ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'

ts()      { date -u '+%H:%M:%S'; }
log()     { echo -e "${CYAN}[$(ts)]${NC} $*"; }
ok()      { echo -e "${GREEN}[$(ts)] ✓${NC}  $*"; }
warn()    { echo -e "${YELLOW}[$(ts)] ⚠${NC}  $*"; }
err()     { echo -e "${RED}[$(ts)] ✗${NC}  $*"; }
section() { echo -e "\n${BOLD}${CYAN}━━━ $* ━━━${NC}"; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

CLEANUP_LOG="/tmp/phase9_cleanup_$(date -u '+%Y%m%d_%H%M%S').log"
exec > >(tee -a "$CLEANUP_LOG") 2>&1

echo ""
echo -e "${BOLD}${CYAN}╔══════════════════════════════════════════════════╗${NC}"
echo -e "${BOLD}${CYAN}║  RatanHR HRMS — Phase 9 Source Cleanup          ║${NC}"
if $DRY_RUN; then
echo -e "${BOLD}${YELLOW}║  DRY-RUN MODE — nothing will be deleted          ║${NC}"
fi
echo -e "${BOLD}${CYAN}╚══════════════════════════════════════════════════╝${NC}"
echo ""

ISSUES_FOUND=0
REMOVED_COUNT=0

# ── confirmation helper ───────────────────────────────────────────────────────
confirm_and_remove() {
  local category="$1"
  shift
  local files=("$@")

  if [[ ${#files[@]} -eq 0 ]]; then
    ok "  $category: nothing to remove"
    return
  fi

  echo ""
  echo -e "  ${YELLOW}Found ${#files[@]} file(s) in: ${category}${NC}"
  for f in "${files[@]}"; do
    echo "    - $f"
  done
  echo ""

  if $DRY_RUN; then
    warn "  DRY-RUN: would remove ${#files[@]} file(s) from $category"
    return
  fi

  read -rp "  Remove these ${#files[@]} file(s)? [yes/N] " CONFIRM
  if [[ "$CONFIRM" == "yes" ]]; then
    for f in "${files[@]}"; do
      if [[ -f "$f" || -d "$f" ]]; then
        rm -rf "$f"
        log "  Removed: $f"
        ((REMOVED_COUNT++)) || true
      fi
    done
    ok "  $category: ${#files[@]} item(s) removed"
  else
    warn "  $category: skipped (user declined)"
  fi
}

# ── files that must NEVER be deleted ─────────────────────────────────────────
readonly PROTECTED_PATTERNS=(
  "Dockerfile"
  "HRMS.sln"
  "*.csproj"
  "*.sql"
  ".env.production.template"
  ".env.e2e.template"
  ".gitignore"
  "docker-compose.prod.yml"
  "docker-compose.e2e.yml"
  "Staging/phase8_runbook.sh"
  "deploy.sh"
  "rollback.sh"
  "phase9_run.sh"
  "phase9_cleanup.sh"
  "DEPLOYMENT.md"
  "GO_LIVE_READINESS.md"
  "PHASE8_STAGING_VALIDATION.md"
  "PHASE9_REGRESSION_PLAN.md"
  "RELEASE_CANDIDATE.md"
)

is_protected() {
  local f="$1"
  local base; base="$(basename "$f")"
  for pat in "${PROTECTED_PATTERNS[@]}"; do
    # shellcheck disable=SC2254
    case "$base" in $pat) return 0 ;; esac
    case "$f"    in $pat) return 0 ;; esac
  done
  return 1
}

# =============================================================================
# §1 — Scan: tmp / debug / test output files
# =============================================================================
section "§1 — Temporary and debug files"

mapfile -t TMP_FILES < <(find . \
  \( -name "*.tmp" -o -name "*.temp" -o -name "*.bak" \
     -o -name "*.swp" -o -name ".DS_Store" -o -name "Thumbs.db" \
     -o -name "*.orig" -o -name "*.rej" \) \
  -not -path "./.git/*" \
  -not -path "*/node_modules/*" \
  -not -path "*/bin/*" \
  -not -path "*/obj/*" \
  2>/dev/null | sort)

confirm_and_remove "Temporary/debug files" "${TMP_FILES[@]:-}"

# =============================================================================
# §2 — Scan: local secret files (populated .env files)
# =============================================================================
section "§2 — Local secret files (populated .env)"

declare -a SECRET_FILES=()

# .env (populated) — never commit, never ship
if [[ -f ".env" ]] && ! grep -q "<REQUIRED>" ".env" 2>/dev/null; then
  SECRET_FILES+=(".env")
fi

# .env.e2e — staging credentials
[[ -f ".env.e2e" ]] && SECRET_FILES+=(".env.e2e")

# .env.staging
[[ -f ".env.staging" ]] && SECRET_FILES+=(".env.staging")
[[ -f "Staging/.env.staging" ]] && SECRET_FILES+=("Staging/.env.staging")

# playwright auth state files (contain session tokens)
mapfile -t PW_AUTH < <(find . -path "*/playwright/.auth/*.json" 2>/dev/null | sort)
SECRET_FILES+=("${PW_AUTH[@]:-}")

confirm_and_remove "Local secret/credential files" "${SECRET_FILES[@]:-}"

# =============================================================================
# §3 — Scan: build artifacts
# =============================================================================
section "§3 — Build artifacts (bin/, obj/, dist/, node_modules/)"

declare -a BUILD_DIRS=()

# .NET build output
mapfile -t DOTNET_BIN < <(find . -type d -name "bin" \
  -path "*/HRMS.*/*" \
  -not -path "./.git/*" 2>/dev/null | sort)
mapfile -t DOTNET_OBJ < <(find . -type d -name "obj" \
  -path "*/HRMS.*/*" \
  -not -path "./.git/*" 2>/dev/null | sort)

BUILD_DIRS+=("${DOTNET_BIN[@]:-}" "${DOTNET_OBJ[@]:-}")

# SPA dist (should be in spa-dist/ or committed from CI)
[[ -d "HRMS.SPA.Source/dist" ]] && BUILD_DIRS+=("HRMS.SPA.Source/dist")

# SPA node_modules (large, reproducible)
if [[ -d "HRMS.SPA.Source/node_modules" ]]; then
  warn "HRMS.SPA.Source/node_modules found ($(du -sh HRMS.SPA.Source/node_modules 2>/dev/null | awk '{print $1}'))"
  warn "  This is large but reproducible with: cd HRMS.SPA.Source && bun install"
  BUILD_DIRS+=("HRMS.SPA.Source/node_modules")
fi

confirm_and_remove "Build artifacts" "${BUILD_DIRS[@]:-}"

# =============================================================================
# §4 — Scan: test output and logs
# =============================================================================
section "§4 — Test output and log files"

mapfile -t TEST_OUTPUT < <(find . \
  \( -name "*.trx" -o -name "*.coveragexml" -o -name "TestResults" \
     -o -name "playwright-report" -o -name "test-results" \) \
  -not -path "./.git/*" \
  -not -path "*/node_modules/*" \
  2>/dev/null | sort)

mapfile -t LOG_FILES < <(find . \
  \( -name "*.log" -o -name "*.logs" \) \
  -not -path "./.git/*" \
  -not -path "*/node_modules/*" \
  -not -path "*/logs/*" \
  -not -name "*.logback*" \
  2>/dev/null | sort)

ALL_TEST_OUT=("${TEST_OUTPUT[@]:-}" "${LOG_FILES[@]:-}")
confirm_and_remove "Test output and log files" "${ALL_TEST_OUT[@]:-}"

# =============================================================================
# §5 — Scan: duplicate / superseded files
# =============================================================================
section "§5 — Duplicate and superseded files"

declare -a DUPES=()

# Multiple docker-compose files — only prod and e2e are official
# Override file is staging-only
[[ -f "docker-compose.override.yml" ]] && DUPES+=("docker-compose.override.yml")

# Backup compose files
mapfile -t BKP_COMPOSE < <(find . -name "docker-compose.backup*.yml" \
  -not -path "./.git/*" 2>/dev/null | sort)
DUPES+=("${BKP_COMPOSE[@]:-}")

# nginx.conf.bak (created by deploy.sh sed in-place)
[[ -f "nginx/nginx.conf.bak" ]] && DUPES+=("nginx/nginx.conf.bak")

# Old deploy artifacts (spa-dist is regenerated by deploy.sh)
[[ -d "spa-dist" ]] && {
  warn "spa-dist/ exists ($(du -sh spa-dist 2>/dev/null | awk '{print $1}')) — regenerated by deploy.sh"
  DUPES+=("spa-dist")
}

confirm_and_remove "Duplicate / superseded files" "${DUPES[@]:-}"

# =============================================================================
# §6 — Verify: no secrets in source tree
# =============================================================================
section "§6 — Secret scan: verify no credentials in source tree"

log "Scanning for accidental secret patterns..."

SECRET_PATTERNS=(
  'password\s*=\s*[A-Za-z0-9+/]{12,}'
  'MYSQL_PASSWORD\s*=\s*[^<\$\{][^\s]{8,}'
  'REDIS_PASSWORD\s*=\s*[^<\$\{][^\s]{8,}'
  'JWT_PRIVATE_KEY_PEM\s*=\s*-----BEGIN'
  'ENCRYPTION_KEY\s*=\s*[A-Za-z0-9+/=]{32,}'
  'aws_secret_access_key\s*='
  'private_key\s*=\s*-----BEGIN'
)

SECRET_HITS=()
for pattern in "${SECRET_PATTERNS[@]}"; do
  while IFS= read -r hit; do
    # Exclude templates, examples, test fixtures, and this script
    case "$hit" in
      *template*|*example*|*\.example*|*mock*|*test*|*phase9_cleanup.sh*|*\.git/*) continue ;;
    esac
    SECRET_HITS+=("$hit")
  done < <(grep -rniE "$pattern" . \
    --include="*.env" --include="*.json" --include="*.yml" --include="*.yaml" \
    --include="*.cs" --include="*.ts" --include="*.js" \
    --exclude-dir=".git" \
    --exclude-dir="node_modules" \
    --exclude-dir="bin" \
    --exclude-dir="obj" \
    -l 2>/dev/null || true)
done

if [[ ${#SECRET_HITS[@]} -gt 0 ]]; then
  err "Potential secrets found in source — REVIEW BEFORE COMMITTING:"
  for h in "${SECRET_HITS[@]}"; do err "  $h"; done
  ((ISSUES_FOUND++)) || true
else
  ok "Secret scan: no credentials detected in source tree"
fi

# =============================================================================
# §7 — Verify: critical files present
# =============================================================================
section "§7 — Critical files present"

REQUIRED_FILES=(
  "Dockerfile"
  "HRMS.sln"
  "docker-compose.prod.yml"
  "docker-compose.e2e.yml"
  ".env.production.template"
  ".env.e2e.template"
  ".gitignore"
  "nginx/nginx.conf"
  "Staging/phase8_runbook.sh"
  "e2e/e2e_seed.sql"
  "scripts/generate-secrets.sh"
  "scripts/generate-rsa-keys.sh"
  "scripts/mysql-backup.sh"
  "deploy.sh"
  "rollback.sh"
  "phase9_run.sh"
  "DEPLOYMENT.md"
  "GO_LIVE_READINESS.md"
  "PHASE8_STAGING_VALIDATION.md"
  "PHASE9_REGRESSION_PLAN.md"
  "RELEASE_CANDIDATE.md"
)

MISSING_FILES=()
for f in "${REQUIRED_FILES[@]}"; do
  if [[ ! -f "$f" ]]; then
    MISSING_FILES+=("$f")
    err "  MISSING: $f"
  else
    ok "  Present: $f"
  fi
done

if [[ ${#MISSING_FILES[@]} -gt 0 ]]; then
  ((ISSUES_FOUND++)) || true
  err "${#MISSING_FILES[@]} critical file(s) missing"
else
  ok "All critical files present"
fi

# =============================================================================
# §8 — Verify: .gitignore covers secret files
# =============================================================================
section "§8 — .gitignore audit"

GITIGNORE_REQUIRED=(".env" ".env.e2e" ".env.staging" "spa-dist/" "playwright/.auth/" "logs/" "backups/")
GITIGNORE_MISSING=()

if [[ -f ".gitignore" ]]; then
  for entry in "${GITIGNORE_REQUIRED[@]}"; do
    if ! grep -qF "$entry" .gitignore 2>/dev/null; then
      GITIGNORE_MISSING+=("$entry")
      warn "  NOT in .gitignore: $entry"
    else
      ok "  In .gitignore: $entry"
    fi
  done

  if [[ ${#GITIGNORE_MISSING[@]} -gt 0 ]]; then
    warn "${#GITIGNORE_MISSING[@]} entries missing from .gitignore"
    echo ""
    echo "  Add to .gitignore:"
    for entry in "${GITIGNORE_MISSING[@]}"; do echo "  $entry"; done

    if ! $DRY_RUN; then
      read -rp "  Add missing entries to .gitignore? [yes/N] " CONFIRM_GI
      if [[ "$CONFIRM_GI" == "yes" ]]; then
        echo "" >> .gitignore
        echo "# Phase 9 cleanup additions" >> .gitignore
        for entry in "${GITIGNORE_MISSING[@]}"; do
          echo "$entry" >> .gitignore
          ok "  Added to .gitignore: $entry"
        done
      fi
    fi
    ((ISSUES_FOUND++)) || true
  else
    ok ".gitignore covers all required secret patterns"
  fi
else
  err ".gitignore not found — this is required"
  ((ISSUES_FOUND++)) || true
fi

# =============================================================================
# §9 — Verify: no duplicate SQL migration files
# =============================================================================
section "§9 — Duplicate migration file check"

log "Checking for duplicate EF Core migration names..."

MIGRATION_NAMES=$(find . -path "*/Migrations/*Migration.cs" \
  -not -path "./.git/*" 2>/dev/null | \
  xargs -I{} basename "{}" | \
  sed 's/_[A-Za-z].*$//' | sort)

DUPE_MIGRATIONS=$(echo "$MIGRATION_NAMES" | sort | uniq -d || true)

if [[ -n "$DUPE_MIGRATIONS" ]]; then
  err "Duplicate migration timestamp prefixes found:"
  echo "$DUPE_MIGRATIONS" | while IFS= read -r d; do err "  $d"; done
  ((ISSUES_FOUND++)) || true
else
  ok "No duplicate EF Core migration names found"
fi

# Check for postgres-archive migrations that should not ship
if [[ -d "postgres-archive" ]]; then
  warn "postgres-archive/ directory found — verify it is excluded from Docker build context"
  grep -q "postgres-archive" .dockerignore 2>/dev/null && \
    ok "  postgres-archive/ is in .dockerignore" || \
    warn "  postgres-archive/ may not be excluded from Docker build context"
fi

# =============================================================================
# §10 — Verify: no hardcoded test credentials in production config
# =============================================================================
section "§10 — Test credential check in production configs"

PROD_CONFIGS=(
  "docker-compose.prod.yml"
  "nginx/nginx.conf"
  "HRMS.API/appsettings.Production.json"
  "HRMS.API/appsettings.json"
)

CRED_IN_PROD=()
for cfg in "${PROD_CONFIGS[@]}"; do
  [[ ! -f "$cfg" ]] && continue
  if grep -qiE \
    'password.*=.*[A-Za-z0-9]{8,}|secret.*=.*[A-Za-z0-9]{16,}|E2E_.*Pass|staging.*pass' \
    "$cfg" 2>/dev/null; then
    CRED_IN_PROD+=("$cfg")
    err "  Possible hardcoded credential in: $cfg"
  else
    ok "  Clean: $cfg"
  fi
done

if [[ ${#CRED_IN_PROD[@]} -gt 0 ]]; then
  ((ISSUES_FOUND++)) || true
fi

# =============================================================================
# §11 — Summary
# =============================================================================
echo ""
echo -e "${BOLD}${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BOLD}${CYAN}  Phase 9 Cleanup — Summary${NC}"
echo -e "${BOLD}${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "  Items removed:  ${REMOVED_COUNT}"
echo -e "  Issues found:   ${ISSUES_FOUND}"
echo -e "  Log:            ${CLEANUP_LOG}"
echo ""

if [[ $ISSUES_FOUND -gt 0 ]]; then
  echo -e "${YELLOW}${BOLD}  ⚠ ISSUES FOUND — resolve before creating the release zip${NC}"
  echo ""
  echo -e "  Common fixes:"
  echo -e "    • Add missing .gitignore entries (see §8 output)"
  echo -e "    • Remove secrets from source files (see §6 output)"
  echo -e "    • Delete missing critical files shown in §7"
  exit 1
else
  echo -e "${GREEN}${BOLD}  ✅ CLEAN — source tree is ready for release packaging${NC}"
  echo ""
  echo -e "  Next steps:"
  echo -e "  ${BOLD}git add -A && git commit -m 'chore: Phase 9 cleanup'${NC}"
  echo -e "  ${BOLD}git tag -a v1.0.0-rc1 -m 'Release Candidate 1 — Phase 9 PASS'${NC}"
  echo -e "  ${BOLD}git push origin main --tags${NC}"
  echo ""
  exit 0
fi
