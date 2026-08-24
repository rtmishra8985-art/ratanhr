INSERT INTO hrms_db.users (
    email, 
    password_hash, 
    role, 
    full_name, 
    is_active, 
    is_deleted, 
    must_change_password,
    admin_role,
    company_id,
    failed_login_attempts,
    lockout_until
) VALUES (
    'superadmin@hrms.com',
    '$2a$12$R9h7cIPz0ZWDd8w8DcwJr.GZ.H6.Q9k8p8mP7nQ6rR5sS4tT3uU2v',
    'superadmin',
    'Super Admin',
    1,
    0,
    0,
    NULL,
    NULL,
    0,
    NULL
);
