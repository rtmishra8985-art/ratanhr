# STEP 0 EVIDENCE — RatanHR HRMS Audit Remediation (run 5)
Date: 2026-08-12 (UTC), sandbox fresh. Docker unavailable — not used.

## 1. nix profile install nixpkgs#dotnet-sdk_8 nixpkgs#mysql80 nixpkgs#redis
Verbatim tail of /tmp/step0/nix-install.log:
```
warning: 'install' is a deprecated alias for 'add'
warning: 'dotnet-sdk_8' is already added
warning: 'mysql80' is already added
warning: 'redis' is already added
```
Toolchain was already present in the profile this run; the add completed with no rebuild.

## 2. Versions
```
=== dotnet --version ===
8.0.418
=== mysqld --version ===
/nix/store/rbnzh90njjad80ch31xrgqd8zkqnv5ly-mysql-8.0.45/bin/mysqld  Ver 8.0.45 for Linux on x86_64 (Source distribution)
=== redis-server --version ===
Redis server v=8.2.3 sha=00000000:0 malloc=jemalloc-5.3.0 bits=64 build=24a0ed788753e020
```

## 3. MySQL initialize + start
`mysqld --initialize-insecure --datadir=/tmp/mysql` (exit 0):
```
2026-08-12T08:19:11.486898Z 0 [System] [MY-013169] [Server] .../bin/mysqld (mysqld 8.0.45) initializing of server in progress as process 3927
2026-08-12T08:19:11.501022Z 1 [System] [MY-013576] [InnoDB] InnoDB initialization has started.
2026-08-12T08:19:11.678716Z 1 [System] [MY-013577] [InnoDB] InnoDB initialization has ended.
2026-08-12T08:19:12.507789Z 6 [Warning] [MY-010453] [Server] root@localhost is created with an empty password !
```
First start attempt failed (running as uid 0 without --user):
```
[ERROR] [MY-010123] [Server] Fatal error: Please read "Security" section of the manual to find out how to run mysqld as root!
```
Retried with `--user=root`; server up on port 3306:
```
2026-08-12T08:20:43.059561Z 0 [System] [MY-010931] [Server] .../bin/mysqld: ready for connections. Version: '8.0.45'  socket: '/tmp/mysqlrun/mysql.sock'  port: 3306  Source distribution.
```
`mysqladmin ping`:
```
mysqld is alive
```

## 4. Database + app user
```
Database
hrms
information_schema
mysql
performance_schema
sys
user	host	plugin
hrms_app	%	caching_sha2_password
hrms_app	localhost	caching_sha2_password
```
db `hrms` = utf8mb4 / utf8mb4_unicode_ci. User `hrms_app` granted ALL on `hrms.*`.

## 5. Redis
`redis-server --daemonize yes` then `redis-cli PING`:
```
PONG
```

## 6. dotnet tool restore
```
Tool 'dotnet-ef' (version '8.0.8') was restored. Available commands: dotnet-ef
Restore was successful.
```
`dotnet tool run dotnet-ef --version`:
```
Entity Framework Core .NET Command-line Tools
8.0.8
```

## 7. RESIDUAL RISK — MySQL version delta
Runtime MySQL here is **8.0.45**, NOT the 8.4 production target. All DB-dependent
verification in later runs is therefore performed against 8.0.x semantics.
Flagged deltas that remain UNVERIFIED against 8.4:
- **Auth plugins**: `mysql_native_password` is removed in 8.4 (plugin not built in);
  8.0.45 still ships it. Any connection string, seeded user, or ops script relying on
  `mysql_native_password` will fail on 8.4. Users created here default to
  `caching_sha2_password` (confirmed above), which is 8.4-safe, but the app's own
  provisioning scripts must be re-checked on 8.4.
- **utf8mb3 deprecation**: 8.4 escalates utf8mb3 deprecation warnings; with
  TreatWarningsAsErrors semantics in CI and any DDL/collation still on utf8mb3
  (or bare `utf8` aliasing), migrations may warn/behave differently. Schema created
  here is utf8mb4 explicitly, but existing migration files must be audited for
  `utf8`/`utf8mb3` literals before an 8.4 cutover.
- Default `--mysqlx` and various sysvar defaults differ; X-Protocol was disabled here.

## STEP 0 STATUS: GREEN
Every sub-step above produced pasted output. No code edits were made in this run.
