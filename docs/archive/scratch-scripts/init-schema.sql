-- Basic schema for HRMS - Email Queue and Users tables
USE hrms_db;

CREATE TABLE IF NOT EXISTS users (
  id INT AUTO_INCREMENT PRIMARY KEY,
  email VARCHAR(255) UNIQUE NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  role VARCHAR(50) NOT NULL DEFAULT 'employee',
  full_name VARCHAR(255),
  is_active BOOLEAN DEFAULT true,
  is_deleted BOOLEAN DEFAULT false,
  must_change_password BOOLEAN DEFAULT false,
  admin_role VARCHAR(100),
  company_id INT,
  failed_login_attempts INT DEFAULT 0,
  lockout_until DATETIME,
  created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
  updated_at DATETIME(6),
  INDEX idx_email (email),
  INDEX idx_is_deleted (is_deleted)
);

CREATE TABLE IF NOT EXISTS email_queue (
  id INT AUTO_INCREMENT PRIMARY KEY,
  to_address VARCHAR(255) NOT NULL,
  subject VARCHAR(500),
  html_body LONGTEXT,
  status VARCHAR(50) DEFAULT 'Pending',
  sent_at DATETIME,
  retry_count INT DEFAULT 0,
  next_retry_at DATETIME,
  last_error LONGTEXT,
  created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6)
);

CREATE TABLE IF NOT EXISTS leave_types (
  id INT AUTO_INCREMENT PRIMARY KEY,
  company_id INT,
  name VARCHAR(100),
  annual_quota_days INT,
  is_paid BOOLEAN DEFAULT true,
  is_active BOOLEAN DEFAULT true,
  created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
  updated_at DATETIME(6),
  INDEX idx_company_id (company_id)
);

CREATE TABLE IF NOT EXISTS companies (
  id INT AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  code VARCHAR(50),
  is_active BOOLEAN DEFAULT true,
  created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
  updated_at DATETIME(6),
  INDEX idx_code (code)
);

CREATE TABLE IF NOT EXISTS employees (
  id INT AUTO_INCREMENT PRIMARY KEY,
  company_id INT,
  email VARCHAR(255),
  full_name VARCHAR(255),
  is_active BOOLEAN DEFAULT true,
  created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
  updated_at DATETIME(6),
  INDEX idx_company_id (company_id),
  INDEX idx_email (email)
);

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
) ON DUPLICATE KEY UPDATE email=email;
