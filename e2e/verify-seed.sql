-- =============================================================================
-- e2e/verify-seed.sql  —  Confirm all 6 E2E accounts are correctly seeded
--
-- Run manually to inspect the seed before kicking off the test suite:
--   mysql -h 127.0.0.1 -P 3307 -u root -p hrms < e2e/verify-seed.sql
--
-- FIX (VERIFY-SEED-001): Previous version selected Companies.Name and
--   Companies.Domain which do not exist on the EF Core entity. Corrected to
--   Companies.CompanyName (the mapped column). Domain column removed.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 1. Companies — expect 2 rows
-- ---------------------------------------------------------------------------
SELECT
  `Id`,
  `CompanyName`,   -- FIX: was `Name` (column does not exist)
  `IsActive`,
  `CreatedAt`
FROM `Companies`
WHERE `Id` IN (9001, 9002)
ORDER BY `Id`;

-- ---------------------------------------------------------------------------
-- 2. Users — expect exactly 6 rows
-- ---------------------------------------------------------------------------
SELECT
  `Email`,
  `Role`,
  `FullName`,
  `CompanyId`,
  `IsActive`,
  `MustChangePassword`,
  LEFT(`PasswordHash`, 7) AS `HashPrefix`  -- confirms BCrypt format $2b$12$
FROM `Users`
WHERE `Email` LIKE 'e2e.%@ratan-staging.local'
ORDER BY `Id`;

-- ---------------------------------------------------------------------------
-- 3. Count check — must return 6
-- ---------------------------------------------------------------------------
SELECT
  COUNT(*) AS `TotalE2EUsers`,
  SUM(CASE WHEN `Role`      = 'superadmin'                     THEN 1 ELSE 0 END) AS `SuperAdmins`,
  SUM(CASE WHEN `Role`      = 'admin'                          THEN 1 ELSE 0 END) AS `Admins`,
  SUM(CASE WHEN `Role`      = 'employee'                       THEN 1 ELSE 0 END) AS `Employees`,
  SUM(CASE WHEN `CompanyId` = 9001                             THEN 1 ELSE 0 END) AS `CompanyAUsers`,
  SUM(CASE WHEN `CompanyId` = 9002                             THEN 1 ELSE 0 END) AS `CompanyBUsers`,
  SUM(CASE WHEN `CompanyId` IS NULL                            THEN 1 ELSE 0 END) AS `NoCompanyUsers`,
  SUM(CASE WHEN `IsActive`  = 1                                THEN 1 ELSE 0 END) AS `ActiveUsers`,
  SUM(CASE WHEN `MustChangePassword` = 0                       THEN 1 ELSE 0 END) AS `NoForceReset`
FROM `Users`
WHERE `Email` LIKE 'e2e.%@ratan-staging.local';

-- Expected result:
--   TotalE2EUsers = 6
--   SuperAdmins   = 2  (superadmin + auditor — both use 'superadmin' role)
--   Admins        = 2  (adminA + adminB)
--   Employees     = 2  (employeeA + employeeB)
--   CompanyAUsers = 2  (adminA + employeeA)
--   CompanyBUsers = 2  (adminB + employeeB)
--   NoCompanyUsers= 2  (superadmin + auditor)
--   ActiveUsers   = 6
--   NoForceReset  = 6
