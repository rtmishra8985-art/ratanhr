-- =============================================================
-- scripts/db-init.sql — MySQL 8.4 initialisation
-- Mounted as /docker-entrypoint-initdb.d/00-init.sql
-- Runs once when the MySQL data directory is first created.
-- Idempotent — safe to run multiple times.
--
-- Phase 5: Replaced PostgreSQL WAL replication init script entirely.
-- Previous content created a PostgreSQL replication_user, replication slot,
-- and WAL configuration — none of which apply to MySQL.
-- =============================================================
CREATE DATABASE IF NOT EXISTS hrms_db
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

-- Do not create users here. The official MySQL image creates MYSQL_USER with
-- MYSQL_PASSWORD before running this directory, and a hard-coded fallback
-- password here would either be insecure or conflict with custom credentials.
-- EF Core migrations own the application schema.
