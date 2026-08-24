# Docker Guide
**HRMS v2.1.0** | MySQL 8.4

---

## Image Overview

| Service | Image | Notes |
|---------|-------|-------|
| api | `hrms-api:<release-tag>` (local build) | Multi-stage, non-root, pinned SDK/runtime |
| migrate | `hrms-api-migrate:<release-tag>` (migrate target) | Runs `dotnet ef database migrate`, exits |
| mysql | `mysql:8.4` | Data volume, no exposed host port |
| redis | `redis:7.4-alpine` | Password-protected, no exposed host port |
| nginx | `nginx:1.27.0-alpine` | TLS termination, Certbot challenge |
| certbot | `certbot/certbot:v2.11.0` | Auto-renews Let's Encrypt certs |
| backup | `mysql:8.4` | Daily `mysqldump` to `./backups/` |

---

## Image Digest Pinning

All images should be pinned with SHA256 digests in production:

```bash
# Get digest for an image:
docker pull mysql:8.4
docker inspect --format='{{index .RepoDigests 0}}' mysql:8.4
# Output: mysql@sha256:abc123...

# Then in docker-compose.yml, use the digest-pinned reference:
image: mysql:8.4@sha256:abc123...
```

Repeat this process when updating any image version. The Dockerfile already shows the pattern.

---

## Multi-Stage Build

```dockerfile
# Stage 1: build (SDK image — ~800MB)
FROM mcr.microsoft.com/dotnet/sdk:8.0.16 AS build
RUN dotnet publish ...

# Stage 2: migrate (SDK image — needed for dotnet-ef)
FROM mcr.microsoft.com/dotnet/sdk:8.0.16 AS migrate
RUN dotnet tool install dotnet-ef ...

# Stage 3: runtime (ASP.NET runtime — ~200MB)
FROM mcr.microsoft.com/dotnet/aspnet:8.0.16 AS runtime
COPY --from=build /app/publish .
```

The final runtime image is ~220MB (vs ~800MB SDK image).

---

## Common Commands

```bash
# Build all services
docker compose build

# Start stack (detached)
docker compose up -d

# View logs
docker compose logs -f api
docker compose logs -f migrate

# Run migrations manually
docker compose run --rm migrate

# Open MySQL client
docker compose exec mysql mysql -u hrms -p"$MYSQL_PASSWORD" hrms_db

# Open redis-cli
docker compose exec redis redis-cli -a $REDIS_PASSWORD

# Check resource usage
docker stats

# Stop and remove containers (keep volumes)
docker compose down

# Stop and remove everything (including volumes — DATA LOSS)
docker compose down -v
```

---

## Volume Management

| Volume | Contents | Backup? |
|--------|----------|---------|
| `hrms_mysqldata` | MySQL data | Yes (daily via backup service) |
| `hrms_redis` | Redis AOF log | No (cache only) |
| `hrms_uploads` | Employee document uploads | Yes (include in backup plan) |
| `hrms_logs` | Application log files | Optional |
| `hrms_certbot_conf` | Let's Encrypt certificates | No (re-issued from ACME) |

---

## Security Hardening

- API container runs as non-root user `hrms` (uid 1000)
- MySQL and Redis ports are **not** exposed to the host
- All secrets passed via environment variables (not baked into image)
- Read-only mounts where possible (nginx config, certbot conf)
- Health checks on all services — Docker stops routing traffic to unhealthy containers
