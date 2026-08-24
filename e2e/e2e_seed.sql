-- =============================================================================
-- e2e/e2e_seed.sql  —  RatanHR HRMS E2E seed data
--
-- Purpose : Insert the two staging companies and six E2E-only test accounts
--           required to run the 625-test Playwright suite.
--
-- Run AFTER migrations have been applied:
--   mysql -h 127.0.0.1 -P 3307 -u root -p"${MYSQL_ROOT_PASSWORD}" hrms < e2e/e2e_seed.sql
--
-- Password hashes generated with:
--   node -e "const b=require('bcryptjs');console.log(b.hashSync('<pass>',12))"
-- Cost factor : 12  (matches Security:BcryptWorkFactor in appsettings.json)
--
-- NOTE: This script is idempotent — it uses INSERT IGNORE so it is safe to
--       re-run without duplicating rows.
--
-- FIX (E2E-SEED-001): Id columns are INT (auto-increment) — UUID strings were
--   previously inserted and failed silently via INSERT IGNORE in strict mode,
--   leaving 0 users in the DB and causing HTTP 401 for all E2E logins.
--   Now using integer IDs in the high range (9001-9006) to avoid PK conflicts
--   with production/seed data.
--
-- FIX (E2E-SEED-002): Companies.Name and Companies.Domain do not exist as
--   columns. The EF Core entity maps to CompanyName; Domain is not a field.
--   Removed Domain and LogoPath; using CompanyName.
--
-- FIX (E2E-SEED-003): Users.EmployeeId does not exist on the User entity.
--   Removed from INSERT column list.
-- =============================================================================

SET @now = UTC_TIMESTAMP(6);

-- ---------------------------------------------------------------------------
-- 1. Staging companies
-- ---------------------------------------------------------------------------
INSERT IGNORE INTO `Companies` (`Id`, `CompanyName`, `IsActive`, `CreatedAt`)
VALUES
  (9001, 'E2E Company A', 1, @now),
  (9002, 'E2E Company B', 1, @now);

-- ---------------------------------------------------------------------------
-- 2. E2E user accounts
--
-- Passwords (staging-only — never use in production):
--   SuperAdmin  → E2E_SuperAdmin_Pass1!
--   Admin A     → E2E_AdminA_Pass1!
--   Employee A  → E2E_EmployeeA_Pass1!
--   Admin B     → E2E_AdminB_Pass1!
--   Employee B  → E2E_EmployeeB_Pass1!
--   Auditor     → E2E_Auditor_Pass1!
--
-- Hashes computed with bcryptjs hashSync(password, 12):
-- ---------------------------------------------------------------------------
INSERT IGNORE INTO `Users`
  (`Id`, `Email`, `PasswordHash`, `Role`, `FullName`,
   `CompanyId`, `IsActive`, `MustChangePassword`, `CreatedAt`)
VALUES
  -- SuperAdmin  — no company
  (
    9001,
    'e2e.superadmin@ratan-staging.local',
    '$2b$12$bg2UAXpFhLC4/K.4JFGhX.yrMDq7QnGuuqTssKEn6dsM44FqEg8Oe',
    'superadmin',
    'E2E SuperAdmin',
    NULL, 1, 0, @now
  ),
  -- Admin A  — Company A
  (
    9002,
    'e2e.adminA@ratan-staging.local',
    '$2b$12$3muaT5MCYNAdOxgxmWLQie5JwQmj9nSFgFAPVUg.AubGBGfOdpJ.6',
    'admin',
    'E2E Admin A',
    9001, 1, 0, @now
  ),
  -- Employee A  — Company A
  (
    9003,
    'e2e.employeeA@ratan-staging.local',
    '$2b$12$0x2.0EUX2Sx44IkDyuMG/OkH8EXspAGbKasToh53B3aZnKag/3YSG',
    'employee',
    'E2E Employee A',
    9001, 1, 0, @now
  ),
  -- Admin B  — Company B
  (
    9004,
    'e2e.adminB@ratan-staging.local',
    '$2b$12$sD8upCdi6Mpl2mf0i.jUWO/49CgW5sMw8rGvGJZGKW/xdHe5qd3G6',
    'admin',
    'E2E Admin B',
    9002, 1, 0, @now
  ),
  -- Employee B  — Company B
  (
    9005,
    'e2e.employeeB@ratan-staging.local',
    '$2b$12$.QUixiQKD0LTVVbPffy9rOe23R3Utcnxg/LnYkP/iRV5fPsDouwqa',
    'employee',
    'E2E Employee B',
    9002, 1, 0, @now
  ),
  -- Auditor  — no company (cross-tenant read access via superadmin role)
  (
    9006,
    'e2e.auditor@ratan-staging.local',
    '$2b$12$5uLiL0DMMMNuFbMldPM7muubrTj7Msv6Rdo8SbsVpJDnk3rkB8hI2',
    'superadmin',
    'E2E Auditor',
    NULL, 1, 0, @now
  );

-- ---------------------------------------------------------------------------
-- 3. Confirmation — must return exactly 6 rows
-- ---------------------------------------------------------------------------
SELECT
  `Email`,
  `Role`,
  `FullName`,
  `CompanyId`,
  `IsActive`
FROM `Users`
WHERE `Email` LIKE 'e2e.%@ratan-staging.local'
ORDER BY `Id`;
