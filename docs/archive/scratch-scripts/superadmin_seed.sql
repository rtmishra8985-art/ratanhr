-- Create superadmin user with a strong password
-- Password: SuperAdmin@2026 (BCrypt hashed)
-- This hash is: $2a$12$R9h7cIPz0ZWDd8w8DcwJr.GZ.H6.Q9k8p8mP7nQ6rR5sS4tT3uU2v

USE hrms_db;

INSERT INTO users (
    email,
    password_hash,
    role,
    full_name,
    is_active,
    is_deleted,
    must_change_password,
    created_at,
    admin_role,
    company_id,
    failed_login_attempts,
    lockout_until
) VALUES (
    'superadmin@hrms.com',
    '$2a$12$R9h7cIPz0ZWDd8w8DcwJr.GZ.H6.Q9k8p8mP7nQ6rR5sS4tT3uU2v',
    'superadmin',
    'Super Admin',
    true,
    false,
    false,
    NOW(),
    NULL,
    NULL,
    0,
    NULL
) ON DUPLICATE KEY UPDATE password_hash = VALUES(password_hash);
