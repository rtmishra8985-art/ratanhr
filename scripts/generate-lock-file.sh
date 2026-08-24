#!/usr/bin/env bash
# =============================================================================
# generate-lock-file.sh
# =============================================================================
# Generates (or refreshes) the NuGet lock files for every project in HRMS.sln.
#
# Run this script locally after any PackageReference change, then commit the
# resulting packages.lock.json files so CI and Docker builds use --locked-mode.
#
# Usage:
#   chmod +x scripts/generate-lock-file.sh
#   ./scripts/generate-lock-file.sh
#
# After running, commit the results:
#   git add HRMS.API/packages.lock.json \
#           HRMS.Domain/packages.lock.json \
#           HRMS.Application/packages.lock.json \
#           HRMS.Infrastructure/packages.lock.json \
#           HRMS.Tests/packages.lock.json
#   git commit -m "chore: refresh NuGet packages.lock.json files"
#
# Once lock files are committed you can tighten CI/Docker back to --locked-mode:
#   Dockerfile line:  dotnet restore HRMS.sln --use-lock-file --locked-mode
#   CI yml line:      dotnet restore HRMS.sln --use-lock-file --locked-mode
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

echo "[generate-lock-file] Restoring HRMS.sln with lock-file generation..."
cd "${ROOT_DIR}"

dotnet restore HRMS.sln \
  --use-lock-file \
  --force-evaluate          # re-evaluate even when lock files already exist

echo ""
echo "[generate-lock-file] Done. Lock files generated:"
find . -name "packages.lock.json" -not -path "*/obj/*" | sort

echo ""
echo "[generate-lock-file] Next steps:"
echo "  git add \$(find . -name 'packages.lock.json' -not -path '*/obj/*')"
echo "  git commit -m 'chore: refresh NuGet packages.lock.json files'"
echo ""
echo "  Then enable --locked-mode in Dockerfile and .github/workflows/build.yml."
