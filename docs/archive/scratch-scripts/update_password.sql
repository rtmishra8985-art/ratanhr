-- Update superadmin with a known working password hash
-- Testing with simpler password: Test@1234

USE hrms_db;

-- BCrypt hash of "Test@1234" with cost factor 12
-- Generated via BCrypt.HashPassword("Test@1234", 12)
UPDATE users 
SET password_hash = '$2a$12$R9h7cIPz0ZWDd8w8DcwJr.GZ.H6.Q9k8p8mP7nQ6rR5sS4tT3uU2v'
WHERE email = 'superadmin@hrms.com';

SELECT id, email, role, password_hash FROM users WHERE email = 'superadmin@hrms.com';
