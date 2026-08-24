#!/bin/bash
# Docker startup verification test
# This script builds the production Docker image and verifies it starts correctly

set -euo pipefail

CONTAINER_NAME="ratanhr-test-${RANDOM}"
IMAGE_NAME="ratanhr-api:1.0.4"
TIMEOUT=60

echo "[$(date -Iseconds)] Starting container startup test..."

# Build image if not exists
if ! docker image inspect "$IMAGE_NAME" &>/dev/null; then
  echo "[$(date -Iseconds)] Building image $IMAGE_NAME..."
  docker build --target runtime -t "$IMAGE_NAME" .
fi

echo "[$(date -Iseconds)] Starting container: $CONTAINER_NAME"

# FIX: EnvironmentValidator.Validate() requires these to be set in Production
# (JWT RS256 key pair, DB connection string, ALLOWED_HOSTS, Redis-backed Hangfire).
# Without them the container throws InvalidOperationException at startup and exits
# immediately, so the readiness-wait loop below always hit the timeout.
# These are throwaway values for a local startup smoke test only — never real secrets.
TMP_JWT_DIR=$(mktemp -d)
openssl genrsa -out "$TMP_JWT_DIR/private.pem" 2048 2>/dev/null
openssl rsa -in "$TMP_JWT_DIR/private.pem" -pubout -out "$TMP_JWT_DIR/public.pem" 2>/dev/null
JWT_PRIVATE_KEY_PEM=$(awk '{printf "%s\\n", $0}' "$TMP_JWT_DIR/private.pem")
JWT_PUBLIC_KEY_PEM=$(awk '{printf "%s\\n", $0}' "$TMP_JWT_DIR/public.pem")
rm -rf "$TMP_JWT_DIR"

docker run -d \
  --name "$CONTAINER_NAME" \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Testing \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e Jwt__PrivateKeyPem="$JWT_PRIVATE_KEY_PEM" \
  -e Jwt__PublicKeyPem="$JWT_PUBLIC_KEY_PEM" \
  -e Security__EncryptionKey="$(openssl rand -base64 32)" \
  -e ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;Database=hrms_startup_test;User ID=root;Password=test;AllowPublicKeyRetrieval=True;SslMode=None" \
  -e ALLOWED_HOSTS="localhost" \
  -e Hangfire__UseRedis=false \
  -e Hangfire__UseInMemory=true \
  "$IMAGE_NAME"

# Wait for startup
echo "[$(date -Iseconds)] Waiting for container to be ready (max ${TIMEOUT}s)..."
start_time=$(date +%s)
while true; do
  current_time=$(date +%s)
  elapsed=$((current_time - start_time))
  
  if [ $elapsed -gt $TIMEOUT ]; then
    echo "[$(date -Iseconds)] ✗ FAILED: Container startup timeout"
    docker logs "$CONTAINER_NAME"
    docker rm -f "$CONTAINER_NAME"
    exit 1
  fi
  
  if docker exec "$CONTAINER_NAME" wget -q -O- http://localhost:8080/health &>/dev/null; then
    echo "[$(date -Iseconds)] ✓ SUCCESS: Container is healthy"
    break
  fi
  
  sleep 2
done

# Test health endpoint
echo "[$(date -Iseconds)] Testing health endpoint..."
HEALTH_RESPONSE=$(docker exec "$CONTAINER_NAME" wget -q -O- http://localhost:8080/health)
# FIX: HealthCheckResponseWriter serialises HealthStatus.Healthy.ToString() as "Healthy"
# (capital H). grep -q "healthy" (lowercase) never matched, so this check always failed
# even when the container was genuinely healthy. Match case-insensitively instead.
if echo "$HEALTH_RESPONSE" | grep -qi "healthy"; then
  echo "[$(date -Iseconds)] ✓ Health check: PASSED"
else
  echo "[$(date -Iseconds)] ✗ Health check: FAILED"
  echo "Response: $HEALTH_RESPONSE"
  docker rm -f "$CONTAINER_NAME"
  exit 1
fi

# Cleanup
docker rm -f "$CONTAINER_NAME"
echo "[$(date -Iseconds)] ✓ Docker startup test: PASSED"
