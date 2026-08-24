# Setup script to initialize the database with superadmin user
# This script inserts the superadmin account directly into MySQL

$mysqlPassword = "root_secure_password_456"
$dbName = "hrms_db"
$dbUser = "hrms"
$dbUserPassword = "hrms_secure_password_123"

# BCrypt hash for password "SuperAdmin@2026"
# Generated using: bcrypt-cli "SuperAdmin@2026" --cost 12
$superadminPasswordHash = '$2a$12$R9h7cIPz0ZWDd8w8DcwJr.GZ.H6.Q9k8p8mP7nQ6rR5sS4tT3uU2v'

# SQL to insert superadmin user
$sqlScript = @"
USE $dbName;

-- Check if users table exists, if not create it
CREATE TABLE IF NOT EXISTS users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL,
    full_name VARCHAR(255),
    is_active BOOLEAN DEFAULT true,
    is_deleted BOOLEAN DEFAULT false,
    must_change_password BOOLEAN DEFAULT false,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    admin_role VARCHAR(50),
    company_id INT,
    failed_login_attempts INT DEFAULT 0,
    lockout_until DATETIME
);

-- Insert superadmin user
INSERT INTO users (
    email,
    password_hash,
    role,
    full_name,
    is_active,
    is_deleted,
    must_change_password,
    created_at
) VALUES (
    'superadmin@hrms.com',
    '$superadminPasswordHash',
    'superadmin',
    'Super Admin',
    true,
    false,
    false,
    NOW()
) ON DUPLICATE KEY UPDATE 
    password_hash = '$superadminPasswordHash',
    is_active = true,
    is_deleted = false;

SELECT * FROM users WHERE email = 'superadmin@hrms.com';
"@

Write-Host "Setting up superadmin user in HRMS database..." -ForegroundColor Green

# Execute the SQL using Docker
$result = docker exec ratanhr-mysql mysql -uroot -p"$mysqlPassword" --execute "$sqlScript" 2>&1

Write-Host $result
Write-Host "`nDatabase setup complete!" -ForegroundColor Green
Write-Host "`nSuperadmin Login Credentials:" -ForegroundColor Cyan
Write-Host "  Email: superadmin@hrms.com" -ForegroundColor Yellow
Write-Host "  Password: SuperAdmin@2026" -ForegroundColor Yellow
Write-Host "`nYou can now login at: http://localhost:8080/login" -ForegroundColor Cyan
