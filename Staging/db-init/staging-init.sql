-- Staging-only initialization.
-- The official MySQL image creates the application database and user from
-- MYSQL_DATABASE, MYSQL_USER, and MYSQL_PASSWORD. EF Core migrations own the
-- application schema; this file only ensures the expected database encoding.
CREATE DATABASE IF NOT EXISTS hrms_staging
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;