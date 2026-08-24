#!/usr/bin/env bash
# ============================================================
# pin-docker-digests.sh
#
# Fetches the current SHA256 digests for the Docker base images
# used by HRMS and rewrites the FROM lines in Dockerfile so all
# stages are digest-pinned.
#
# FIX: SDK_TAG corrected from "8.0.16" to "8.0.416" to match the
# Dockerfile FROM line (mcr.microsoft.com/dotnet/sdk:8.0.416).
#
# Works on FROM lines whether or not they already have a digest
# (handles both fresh lines and lines being refreshed).
#
# Run this after any image version bump and commit the result.
#   chmod +x scripts/pin-docker-digests.sh
#   ./scripts/pin-docker-digests.sh
#   git add Dockerfile && git commit -m 'chore: refresh Docker base image digests'
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKERFILE="$SCRIPT_DIR/../Dockerfile"

SDK_TAG="mcr.microsoft.com/dotnet/sdk:8.0.416-alpine3.21"
ASPNET_TAG="mcr.microsoft.com/dotnet/aspnet:8.0.20-alpine3.21"

echo "→ Pulling images to get current digests..."
docker pull "$SDK_TAG"
docker pull "$ASPNET_TAG"

SDK_DIGEST=$(docker inspect --format='{{index .RepoDigests 0}}' "$SDK_TAG" | sed 's/.*@//')
ASPNET_DIGEST=$(docker inspect --format='{{index .RepoDigests 0}}' "$ASPNET_TAG" | sed 's/.*@//')

echo "   SDK     digest: $SDK_DIGEST"
echo "   ASP.NET digest: $ASPNET_DIGEST"

# Rewrite FROM lines — handles both pinned (with @sha256:...) and unpinned lines.
# Two SDK-stage FROM lines exist (build + migrate), both must be updated.
sed -i -E \
  "s|FROM mcr.microsoft.com/dotnet/sdk:8.0.416-alpine3.21(@sha256:[a-f0-9]+)? AS build|FROM mcr.microsoft.com/dotnet/sdk:8.0.416-alpine3.21@${SDK_DIGEST} AS build|" \
  "$DOCKERFILE"

sed -i -E \
  "s|FROM mcr.microsoft.com/dotnet/sdk:8.0.416-alpine3.21(@sha256:[a-f0-9]+)? AS migrate|FROM mcr.microsoft.com/dotnet/sdk:8.0.416-alpine3.21@${SDK_DIGEST} AS migrate|" \
  "$DOCKERFILE"

sed -i -E \
  "s|FROM mcr.microsoft.com/dotnet/aspnet:8.0.20-alpine3.21(@sha256:[a-f0-9]+)? AS runtime|FROM mcr.microsoft.com/dotnet/aspnet:8.0.20-alpine3.21@${ASPNET_DIGEST} AS runtime|" \
  "$DOCKERFILE"

echo "→ Dockerfile updated with pinned digests."
echo ""
echo "Verify with:  scripts/verify-docker-digests.sh"
echo "Commit with:  git add Dockerfile && git commit -m 'chore: refresh Docker base image digests'"
