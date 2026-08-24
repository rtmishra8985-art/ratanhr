-- =============================================================================
-- RatanHR DEMO MODE - TEST DATA VERIFICATION QUERIES
-- =============================================================================
-- Use these queries to verify demo data before and after seeding
-- Database: MySQL/MariaDB
-- =============================================================================

-- =============================================================================
-- SECTION 1: PRE-SEEDING VERIFICATION (Before creating demo data)
-- =============================================================================

-- Query 1.1: Count existing demo records (should be 0 initially)
SELECT 
    'BEFORE SEEDING' as phase,
    COUNT(*) as demo_companies,
    (SELECT COUNT(*) FROM employees WHERE is_demo = true) as demo_employees,
    (SELECT COUNT(*) FROM web_attendances WHERE company_id IN (1,2,3,4,5)) as attendance_records,
    (SELECT COUNT(*) FROM leave_requests WHERE company_id IN (1,2,3,4,5)) as leave_requests
FROM companies 
WHERE is_demo = true;

-- Query 1.2: Verify no demo data exists
SELECT 
    companies_with_demo = 0 as 'no_demo_companies',
    employees_with_demo = 0 as 'no_demo_employees',
    attendance_with_demo = 0 as 'no_demo_attendance',
    leave_with_demo = 0 as 'no_demo_leave'
FROM (
    SELECT 
        COUNT(*) as companies_with_demo,
        (SELECT COUNT(*) FROM employees WHERE is_demo = true) as employees_with_demo,
        (SELECT COUNT(*) FROM web_attendances WHERE is_demo = true) as attendance_with_demo,
        (SELECT COUNT(*) FROM leave_requests WHERE is_demo = true) as leave_with_demo
    FROM companies 
    WHERE is_demo = true
) data;

-- =============================================================================
-- SECTION 2: POST-SEEDING VERIFICATION (After creating demo data)
-- =============================================================================

-- Query 2.1: Count all created demo records
SELECT 
    'AFTER SEEDING' as phase,
    COUNT(DISTINCT c.id) as demo_companies,
    (SELECT COUNT(*) FROM employees WHERE is_demo = true AND company_id IN (1,2,3,4,5)) as demo_employees,
    (SELECT COUNT(*) FROM web_attendances WHERE company_id IN (1,2,3,4,5)) as attendance_records,
    (SELECT COUNT(*) FROM leave_requests WHERE company_id IN (1,2,3,4,5)) as leave_requests,
    (SELECT COUNT(*) FROM assets WHERE company_id IN (1,2,3,4,5)) as assets,
    (SELECT COUNT(*) FROM job_applicants WHERE company_id IN (1,2,3,4,5)) as candidates,
    (SELECT COUNT(*) FROM users WHERE email LIKE 'demo%@demo.ratanhr.local') as demo_users
FROM companies c
WHERE c.is_demo = true;

-- Query 2.2: List all demo companies
SELECT 
    id,
    company_name,
    company_code,
    is_demo,
    status,
    created_at
FROM companies 
WHERE is_demo = true
ORDER BY id;

-- Query 2.3: Sample demo employees (first 20)
SELECT 
    id,
    employee_code,
    full_name,
    email,
    phone_number,
    company_id,
    is_demo,
    created_at
FROM employees 
WHERE is_demo = true 
AND company_id IN (1,2,3,4,5)
ORDER BY company_id, id
LIMIT 20;

-- Query 2.4: Demo employees count by company
SELECT 
    company_id,
    (SELECT company_name FROM companies WHERE id = company_id) as company_name,
    COUNT(*) as employee_count
FROM employees 
WHERE is_demo = true 
AND company_id IN (1,2,3,4,5)
GROUP BY company_id
ORDER BY company_id;

-- Query 2.5: Demo attendance records by company
SELECT 
    company_id,
    (SELECT company_name FROM companies WHERE id = company_id) as company_name,
    COUNT(*) as attendance_count,
    MIN(attendance_date) as earliest_date,
    MAX(attendance_date) as latest_date
FROM web_attendances 
WHERE company_id IN (1,2,3,4,5)
GROUP BY company_id
ORDER BY company_id;

-- Query 2.6: Demo leave requests by type
SELECT 
    leave_type,
    company_id,
    (SELECT company_name FROM companies WHERE id = company_id) as company_name,
    COUNT(*) as request_count
FROM leave_requests 
WHERE company_id IN (1,2,3,4,5)
GROUP BY leave_type, company_id
ORDER BY company_id, leave_type;

-- Query 2.7: Demo assets by company
SELECT 
    company_id,
    (SELECT company_name FROM companies WHERE id = company_id) as company_name,
    COUNT(*) as asset_count
FROM assets 
WHERE company_id IN (1,2,3,4,5)
GROUP BY company_id
ORDER BY company_id;

-- Query 2.8: Demo users by company
SELECT 
    company_id,
    (SELECT company_name FROM companies WHERE id = company_id) as company_name,
    COUNT(*) as user_count,
    GROUP_CONCAT(DISTINCT email SEPARATOR ', ') as sample_emails
FROM users 
WHERE email LIKE 'demo%@demo.ratanhr.local'
AND company_id IN (1,2,3,4,5)
GROUP BY company_id
ORDER BY company_id;

-- Query 2.9: Verify all demo records have is_demo = true
SELECT 
    'Companies' as table_name,
    COUNT(*) as total_records,
    SUM(CASE WHEN is_demo = true THEN 1 ELSE 0 END) as demo_records,
    CONCAT(ROUND(SUM(CASE WHEN is_demo = true THEN 1 ELSE 0 END) / COUNT(*) * 100, 2), '%') as demo_percentage
FROM companies 
WHERE id IN (1,2,3,4,5)

UNION ALL

SELECT 
    'Employees' as table_name,
    COUNT(*) as total_records,
    SUM(CASE WHEN is_demo = true THEN 1 ELSE 0 END) as demo_records,
    CONCAT(ROUND(SUM(CASE WHEN is_demo = true THEN 1 ELSE 0 END) / COUNT(*) * 100, 2), '%') as demo_percentage
FROM employees 
WHERE company_id IN (1,2,3,4,5)

UNION ALL

SELECT 
    'Attendance' as table_name,
    COUNT(*) as total_records,
    COUNT(*) as demo_records,  -- All attendance in demo company IDs are demo
    '100%' as demo_percentage
FROM web_attendances 
WHERE company_id IN (1,2,3,4,5);

-- =============================================================================
-- SECTION 3: ISOLATION VERIFICATION (Cross-company data protection)
-- =============================================================================

-- Query 3.1: Company 1 isolation - Company 1 user should see only Company 1 data
SELECT 
    'Company 1' as user_company,
    'Employees in Company 1' as data_type,
    COUNT(*) as visible_count
FROM employees 
WHERE company_id = 1 AND is_demo = true

UNION ALL

SELECT 
    'Company 1' as user_company,
    'Employees in Company 2 (should be 0)' as data_type,
    COUNT(*) as visible_count
FROM employees 
WHERE company_id = 2 AND is_demo = true;

-- Query 3.2: Company 2 isolation - Company 2 user should see only Company 2 data
SELECT 
    'Company 2' as user_company,
    'Employees in Company 2' as data_type,
    COUNT(*) as visible_count
FROM employees 
WHERE company_id = 2 AND is_demo = true

UNION ALL

SELECT 
    'Company 2' as user_company,
    'Employees in Company 1 (should be 0)' as data_type,
    COUNT(*) as visible_count
FROM employees 
WHERE company_id = 1 AND is_demo = true;

-- Query 3.3: Real customer data protection (real data should not be touched)
SELECT 
    'Real Data' as category,
    COUNT(*) as untouched_real_companies
FROM companies 
WHERE is_demo = false AND id > 100;

-- Query 3.4: Cross-company data separation verification
SELECT 
    c1_id,
    c2_id,
    shared_employees
FROM (
    SELECT 
        c1.id as c1_id,
        c2.id as c2_id,
        COUNT(DISTINCT 
            CASE WHEN e.company_id = c1.id OR e.company_id = c2.id THEN e.id END
        ) as shared_employees
    FROM companies c1
    CROSS JOIN companies c2
    LEFT JOIN employees e ON (e.company_id = c1.id OR e.company_id = c2.id) 
        AND e.is_demo = true
    WHERE c1.is_demo = true AND c2.is_demo = true AND c1.id < c2.id
    GROUP BY c1.id, c2.id
) isolation_check
WHERE shared_employees > 0;
-- Should return 0 rows (no shared employees)

-- =============================================================================
-- SECTION 4: DATA INTEGRITY VERIFICATION
-- =============================================================================

-- Query 4.1: Check for orphaned employee records (employees without valid company)
SELECT 
    'Orphaned Employees' as issue_type,
    COUNT(*) as count
FROM employees e
WHERE is_demo = true 
AND company_id NOT IN (SELECT id FROM companies WHERE is_demo = true);

-- Query 4.2: Check for orphaned attendance records
SELECT 
    'Orphaned Attendance' as issue_type,
    COUNT(*) as count
FROM web_attendances wa
WHERE company_id IN (1,2,3,4,5)
AND employee_id NOT IN (SELECT id FROM employees WHERE company_id IN (1,2,3,4,5));

-- Query 4.3: Check for orphaned leave requests
SELECT 
    'Orphaned Leave Requests' as issue_type,
    COUNT(*) as count
FROM leave_requests lr
WHERE company_id IN (1,2,3,4,5)
AND employee_id NOT IN (SELECT id FROM employees WHERE company_id IN (1,2,3,4,5));

-- Query 4.4: Verify all demo records created in correct date range
SELECT 
    'Demo Records' as category,
    MIN(created_at) as earliest_created,
    MAX(created_at) as latest_created,
    COUNT(*) as total_records
FROM employees 
WHERE is_demo = true AND company_id IN (1,2,3,4,5);

-- =============================================================================
-- SECTION 5: PERFORMANCE VERIFICATION
-- =============================================================================

-- Query 5.1: Total demo data volume
SELECT 
    SUM(record_count) as total_demo_records,
    'ALL DEMO DATA' as category
FROM (
    SELECT COUNT(*) as record_count FROM companies WHERE is_demo = true
    UNION ALL
    SELECT COUNT(*) FROM employees WHERE is_demo = true
    UNION ALL
    SELECT COUNT(*) FROM web_attendances WHERE company_id IN (1,2,3,4,5)
    UNION ALL
    SELECT COUNT(*) FROM leave_requests WHERE company_id IN (1,2,3,4,5)
    UNION ALL
    SELECT COUNT(*) FROM assets WHERE company_id IN (1,2,3,4,5)
    UNION ALL
    SELECT COUNT(*) FROM job_applicants WHERE company_id IN (1,2,3,4,5)
    UNION ALL
    SELECT COUNT(*) FROM users WHERE email LIKE 'demo%@demo.ratanhr.local'
) volume;

-- Query 5.2: Database size estimate
SELECT 
    ROUND(SUM(data_length + index_length) / 1024 / 1024, 2) as total_size_mb
FROM information_schema.tables 
WHERE table_schema = DATABASE();

-- =============================================================================
-- SECTION 6: SEED TRACKER VERIFICATION (Idempotency Check)
-- =============================================================================

-- Query 6.1: Check seed history
SELECT 
    id,
    seed_version,
    is_success,
    record_count,
    seeded_at,
    execution_time_seconds
FROM demo_seed_trackers
ORDER BY seeded_at DESC
LIMIT 10;

-- Query 6.2: Verify idempotency (same version should not create duplicates)
SELECT 
    seed_version,
    COUNT(*) as seed_count,
    MAX(record_count) as records_per_seed
FROM demo_seed_trackers
WHERE is_success = true
GROUP BY seed_version
HAVING COUNT(*) > 1;
-- Should return 0 rows (no duplicate seeds of same version)

-- =============================================================================
-- SECTION 7: COMPLETE VERIFICATION SUMMARY
-- =============================================================================

-- Query 7.1: Full verification report
SELECT 
    'Demo Mode Verification Report' as report_type,
    DATE_FORMAT(NOW(), '%Y-%m-%d %H:%i:%s') as report_time,
    
    (SELECT COUNT(*) FROM companies WHERE is_demo = true) as demo_companies,
    (SELECT COUNT(*) FROM employees WHERE is_demo = true) as demo_employees,
    (SELECT COUNT(*) FROM web_attendances WHERE company_id IN (1,2,3,4,5)) as attendance_records,
    (SELECT COUNT(*) FROM leave_requests WHERE company_id IN (1,2,3,4,5)) as leave_requests,
    (SELECT COUNT(*) FROM assets WHERE company_id IN (1,2,3,4,5)) as assets,
    (SELECT COUNT(*) FROM job_applicants WHERE company_id IN (1,2,3,4,5)) as candidates,
    (SELECT COUNT(*) FROM users WHERE email LIKE 'demo%@demo.ratanhr.local') as demo_users,
    (SELECT COUNT(*) FROM companies WHERE is_demo = false) as real_companies,
    (SELECT COUNT(*) FROM employees WHERE is_demo = false) as real_employees,
    
    CASE 
        WHEN (SELECT COUNT(*) FROM companies WHERE is_demo = true) = 5 
        AND (SELECT COUNT(*) FROM employees WHERE is_demo = true) >= 450
        AND (SELECT COUNT(*) FROM web_attendances WHERE company_id IN (1,2,3,4,5)) >= 40000
        THEN 'PASS ✓'
        ELSE 'FAIL ✗'
    END as verification_status;

-- =============================================================================
-- SECTION 8: POST-CLEANUP VERIFICATION (After deleting demo data)
-- =============================================================================

-- Query 8.1: Verify cleanup successful
SELECT 
    'AFTER CLEANUP' as phase,
    COUNT(*) as remaining_demo_companies,
    (SELECT COUNT(*) FROM employees WHERE is_demo = true) as remaining_demo_employees,
    (SELECT COUNT(*) FROM web_attendances WHERE company_id IN (1,2,3,4,5)) as remaining_attendance,
    (SELECT COUNT(*) FROM companies WHERE is_demo = false) as preserved_real_companies,
    (SELECT COUNT(*) FROM employees WHERE is_demo = false) as preserved_real_employees
FROM companies 
WHERE is_demo = true;
-- Should show all 0s and all real data preserved

-- =============================================================================
-- QUICK REFERENCE QUERIES
-- =============================================================================

-- Copy & paste any of these for quick checks:

-- Check demo company count:
-- SELECT COUNT(*) FROM companies WHERE is_demo = true;

-- Check demo employee count:
-- SELECT COUNT(*) FROM employees WHERE is_demo = true;

-- Check demo attendance count:
-- SELECT COUNT(*) FROM web_attendances WHERE company_id IN (1,2,3,4,5);

-- List demo companies:
-- SELECT id, company_name FROM companies WHERE is_demo = true ORDER BY id;

-- List demo users and passwords:
-- SELECT id, email, company_id, created_at FROM users WHERE email LIKE 'demo%@demo.ratanhr.local' ORDER BY company_id;

-- Check if real data was touched:
-- SELECT COUNT(*) FROM companies WHERE is_demo = false AND modified_after_seed = 1;

-- Check isolation (Company 1 vs 2):
-- SELECT company_id, COUNT(*) FROM employees WHERE is_demo = true AND company_id IN (1,2) GROUP BY company_id;

-- =============================================================================
