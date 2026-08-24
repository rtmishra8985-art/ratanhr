# Phase 3 — v4 changelog

## Context

No Phase 3 run logs were attached with the v3 upload. `evidence/phase3/` in the
uploaded archive contains only its placeholder `README.md`, so the requested
per-check PASS/FAIL summary could not be produced from real evidence.

## Files touched

| File | Change | Justification |
| --- | --- | --- |
| `Dockerfile` (build stage, after `WORKDIR /src`) | Added `COPY global.json ./` + `RUN dotnet --version` | Fail fast on SDK-pin drift |
| `PHASE3_CHANGELOG_v4.md` | New | This changelog |

No other file was modified, added, or removed.

## Why this one change

`evidence/docker-build-build.txt` (a historic run, from an older Dockerfile
revision) failed at `dotnet publish` with exit code 145:

```
Requested SDK version: 8.0.416
global.json file: /src/global.json
Installed SDKs:
  8.0.303 [/usr/share/dotnet/sdk]
```

The base image tag had drifted from `global.json`. The current Dockerfile already
pins `mcr.microsoft.com/dotnet/sdk:8.0.416-alpine3.21`, so the specific defect is
fixed. The *structural* weakness remained: `global.json` was only copied at
`COPY . .`, i.e. after `dotnet restore --locked-mode`, so any future drift would
again waste a full restore before failing with a confusing error at publish.

Copying `global.json` first and running `dotnet --version` makes the SDK resolver
assert the pin as the second instruction in the stage. `.dockerignore` does not
exclude `global.json`, so the copy resolves. The added layer is cached and adds no
meaningful build time.

## Not changed

No speculative fixes were applied to the restore/migrate/runtime stages, Hangfire
Redis configuration, health checks, or the non-root `hrms` user. Those checks have
produced no failing evidence — changing them without a failing log would be
guesswork.
