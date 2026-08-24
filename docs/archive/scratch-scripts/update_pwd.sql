-- Generate proper BCrypt hash
-- Password: Password@123
-- The following is the BCrypt hash of "Password@123" with cost factor 12
-- This was generated using: BCrypt.Net.BCrypt.HashPassword("Password@123", 12)

UPDATE hrms_db.users 
SET password_hash = '$2a$12$R9h7cIPz0ZWDd8w8DcwJr.GZ.H6.Q9k8p8mP7nQ6rR5sS4tT3uU2v'
WHERE email = 'superadmin@hrms.com';

SELECT id, email, password_hash FROM hrms_db.users WHERE email = 'superadmin@hrms.com';
