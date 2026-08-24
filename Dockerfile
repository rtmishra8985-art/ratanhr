# ============================================================
# RatanHR HRMS – Multi-Stage Dockerfile
# Stage names (authoritative):
#   spa-builder   – Bun/Vite SPA build
#   build         – .NET publish
#   migrate       – EF Core + supplementary SQL runner
#   runtime       – final ASP.NET runtime image
# ============================================================

# ── SPA builder ─────────────────────────────────────────────
FROM oven/bun:1.2.0-alpine AS spa-builder
# NOTE: stage is named "spa-builder", not "spa-build".
# Every reference in runbooks and scripts must use --target spa-builder.
# Uses bun because the project provides bun.lock (no package-lock.json).

WORKDIR /spa
COPY HRMS.SPA.Source/package.json HRMS.SPA.Source/bun.lock ./
RUN bun install --frozen-lockfile
COPY HRMS.SPA.Source/ .
# build:ci sets PORT=3000 BASE_PATH=/ NODE_ENV=production (required by vite.config.ts)
RUN bun run build:ci
# Produces /spa/dist/public/

# ── .NET build ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0.416-alpine3.21 AS build
ARG BUILD_TIMESTAMP="unknown"
ARG GIT_SHA="unknown"
# Item 7: keep in lockstep with CHANGELOG.md and every csproj/package.json.
ARG APP_VERSION="1.0.4"

WORKDIR /src
# Copy global.json FIRST so the SDK pin is enforced at the top of this stage.
# With global.json in the working directory, `dotnet --version` fails fast when
# the base image SDK does not satisfy the pin. Historic evidence
# (evidence/docker-build-build.txt) shows this class of drift surfacing late, at
# `dotnet publish`, only after a full restore had already run.
COPY global.json ./
RUN dotnet --version
COPY *.sln ./
COPY HRMS.API/HRMS.API.csproj                       HRMS.API/
COPY HRMS.Infrastructure/HRMS.Infrastructure.csproj HRMS.Infrastructure/
COPY HRMS.Application/HRMS.Application.csproj       HRMS.Application/
COPY HRMS.Domain/HRMS.Domain.csproj                 HRMS.Domain/
COPY HRMS.Tests/HRMS.Tests.csproj                   HRMS.Tests/
# Locked restore requires every project's lock file to be present before
# restore. Keep these copies next to the project files so Docker builds are
# reproducible and fail fast when a lock file is stale or missing.
COPY HRMS.API/packages.lock.json            HRMS.API/
COPY HRMS.Infrastructure/packages.lock.json HRMS.Infrastructure/
COPY HRMS.Application/packages.lock.json    HRMS.Application/
COPY HRMS.Domain/packages.lock.json         HRMS.Domain/
COPY HRMS.Tests/packages.lock.json          HRMS.Tests/

RUN dotnet restore --locked-mode

COPY . .
RUN dotnet publish HRMS.API/HRMS.API.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publish \
      -p:Version="${APP_VERSION}" \
      -p:AssemblyVersion="${APP_VERSION}.0" \
      -p:FileVersion="${APP_VERSION}.0" \
      -p:InformationalVersion="${APP_VERSION}+${GIT_SHA}+${BUILD_TIMESTAMP}"

# ── EF Core migrate + supplementary SQL runner ───────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0.416-alpine3.21 AS migrate

WORKDIR /src
COPY --from=build /src .

RUN dotnet tool restore && \
    apk add --no-cache mysql-client

# No supplementary SQL is copied any more: db_performance.sql,
# db_indexes_fix.sql and db_softdelete_fix.sql were folded into the EF Core
# migration chain (20260811080000_FoldDbScriptIndexes) on 2026-08-11.

# Entry-point script: runs the EF Core migration chain (single source of truth)
COPY docker/migrate-entrypoint.sh /migrate-entrypoint.sh
RUN chmod +x /migrate-entrypoint.sh

ENTRYPOINT ["/migrate-entrypoint.sh"]

# ── Runtime ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0.20-alpine3.21 AS runtime

# Non-root user
RUN addgroup -S hrms && adduser -S hrms -G hrms

WORKDIR /app
COPY --from=build /app/publish .
COPY --from=spa-builder /spa/dist/public ./wwwroot

RUN chown -R hrms:hrms /app
USER hrms

ARG BUILD_TIMESTAMP="unknown"
ARG GIT_SHA="unknown"
# Item 7: keep in lockstep with CHANGELOG.md and every csproj/package.json.
ARG APP_VERSION="1.0.4"
LABEL org.opencontainers.image.version="${APP_VERSION}" \
      org.opencontainers.image.revision="${GIT_SHA}" \
      org.opencontainers.image.created="${BUILD_TIMESTAMP}"

ENV ASPNETCORE_URLS="http://+:8080" \
    ASPNETCORE_ENVIRONMENT="Production"

EXPOSE 8080
ENTRYPOINT ["dotnet", "HRMS.API.dll"]
