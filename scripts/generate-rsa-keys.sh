#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# generate-rsa-keys.sh
# Generates a 2048-bit RSA key pair for JWT RS256 signing.
# Writes Jwt__PrivateKeyPem and Jwt__PublicKeyPem to your .env file.
#
# FIX H-01: HRMS now uses RS256 (asymmetric) instead of HS256 (symmetric).
#   - Private key: signs access tokens (stays on the API server only)
#   - Public  key: verifies tokens (can be shared with downstream services)
#
# Usage:
#   chmod +x scripts/generate-rsa-keys.sh
#   ./scripts/generate-rsa-keys.sh
#
# Prerequisites: openssl (pre-installed on Linux/macOS; on Windows use Git Bash or WSL)
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/../.env"
TMP_PRIV=$(mktemp)
TMP_PUB=$(mktemp)

cleanup() { rm -f "$TMP_PRIV" "$TMP_PUB"; }
trap cleanup EXIT

echo "🔑 Generating RSA-2048 key pair for JWT RS256 signing..."

# Generate private key
openssl genrsa -out "$TMP_PRIV" 2048 2>/dev/null

# Derive public key
openssl rsa -in "$TMP_PRIV" -pubout -out "$TMP_PUB" 2>/dev/null

# Convert PEM to single-line (newlines → \n) for .env compatibility
PRIVATE_PEM=$(awk 'NF {printf "%s\\n", $0} !NF {printf "\\n"}' "$TMP_PRIV")
PUBLIC_PEM=$(awk  'NF {printf "%s\\n", $0} !NF {printf "\\n"}' "$TMP_PUB")

# ── Write to .env ──────────────────────────────────────────────────────────
if [[ ! -f "$ENV_FILE" ]]; then
  echo "⚠️  .env not found. Run scripts/generate-secrets.sh first, then re-run this script."
  exit 1
fi

# Remove any previous key variable names before appending the new pair. This
# supports both the legacy aliases and the .NET double-underscore names.
sed -i.bak \
  '/^JWT_PRIVATE_KEY_PEM=/d;/^JWT_PUBLIC_KEY_PEM=/d' \
  "$ENV_FILE" && rm -f "${ENV_FILE}.bak"
sed -i.bak \
  '/^Jwt__PrivateKeyPem=/d;/^Jwt__PublicKeyPem=/d' \
  "$ENV_FILE" && rm -f "${ENV_FILE}.bak"

# Append fresh keys. The values intentionally use literal \n sequences so
# Docker Compose/.env parsers pass one valid PEM value to .NET.
cat >> "$ENV_FILE" << EOF

# ── JWT RSA Key Pair (generated $(date -u '+%Y-%m-%d %H:%M UTC')) ─────────────────
# FIX H-01: RS256 asymmetric signing. NEVER commit these values.
Jwt__PrivateKeyPem=${PRIVATE_PEM}
Jwt__PublicKeyPem=${PUBLIC_PEM}
EOF

chmod 600 "$ENV_FILE"

echo ""
echo "✅ RSA-2048 key pair written to .env"
echo ""
echo "📋 These map to docker-compose environment variables:"
echo "     Jwt__PrivateKeyPem=<generated PEM value>"
echo "     Jwt__PublicKeyPem=<generated PEM value>"
echo ""
echo "⚠️  SECURITY REMINDERS:"
echo "   • Jwt__PrivateKeyPem must NEVER leave the API server."
echo "   • Jwt__PublicKeyPem is safe to share with services that verify tokens."
echo "   • .env is excluded from git via .gitignore — verify this before committing."
echo "   • Rotate these keys at least annually, or immediately after any suspected breach."
echo ""
echo "🔄 After rotating keys: all existing access tokens are instantly invalidated."
echo "   Users will need to log in again. Plan accordingly for production rotations."
echo ""
