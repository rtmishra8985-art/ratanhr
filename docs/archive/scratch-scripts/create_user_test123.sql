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
    '$2a$12$ixCVxT3mBo7F.HBgKQZgXOqM/VQy4p/6uVDNCJdwFHZu1yT.xvBYC',
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
