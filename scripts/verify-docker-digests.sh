#!/usr/bin/env bash
# ============================================================
# verify-docker-digests.sh
#
# CI gate: fails if any FROM line in the Dockerfile is NOT
# digest-pinned (i.e. missing @sha256:...).
#
# Run in CI before docker build:
#   chmod +x scripts/verify-docker-digests.sh
#   ./scripts/verify-docker-digests.sh
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKERFILE="$SCRIPT_DIR/../Dockerfile"

UNPINNED=0
while IFS= read -r line; do
    # Skip comment lines
    [[ "$line" =~ ^[[:space:]]*# ]] && continue

    if [[ "$line" =~ ^FROM[[:space:]] ]]; then
        if [[ "$line" =~ @sha256:[a-f0-9]{64} ]]; then
            echo "✓  Pinned: $line"
        else
            echo "✗  UNPINNED: $line"
            UNPINNED=$((UNPINNED + 1))
        fi
    fi
done < "$DOCKERFILE"

if [ "$UNPINNED" -gt 0 ]; then
    echo ""
    echo "ERROR: $UNPINNED FROM line(s) are not digest-pinned."
    echo "Run scripts/pin-docker-digests.sh and commit the result."
    exit 1
fi

echo ""
echo "All FROM lines are digest-pinned. Safe to build."
