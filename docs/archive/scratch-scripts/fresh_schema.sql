-- Recreate full schema (minimal for login)
USE hrms_db;

CREATE TABLE users (
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
  is_mfa_enabled BOOLEAN DEFAULT false,
  isMfaEnabled BOOLEAN DEFAULT false,
  created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
  updated_at DATETIME(6),
  INDEX idx_email (email),
  INDEX idx_is_deleted (is_deleted)
);

CREATE TABLE email_queue (
  id INT AUTO_INCREMENT PRIMARY KEY,
  to_address VARCHAR(255),
  subject VARCHAR(500),
  html_body LONGTEXT,
  status VARCHAR(50) DEFAULT 'Pending',
  sent_at DATETIME,
  retry_count INT DEFAULT 0,
  next_retry_at DATETIME,
  last_error LONGTEXT,
  created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6)
);

CREATE TABLE refresh_tokens (
  id INT AUTO_INCREMENT PRIMARY KEY,
  user_id INT NOT NULL,
  token_hash VARCHAR(255) UNIQUE,
  expires_at DATETIME NOT NULL,
  revoked_at DATETIME,
  mfa_verified BOOLEAN DEFAULT false,
  created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6)
);

-- Insert test superadmin with password "123456"
-- BCrypt hash: $2b$12$6qwFN9f6OnKmFTZXQ8hJ8eYzCuXu/xhEoNNbFk5DEW/T9zBr9z7ca
INSERT INTO users (email, password_hash, role, full_name, is_active, is_deleted) 
VALUES ('superadmin@hrms.com', '$2b$12$6qwFN9f6OnKmFTZXQ8hJ8eYzCuXu/xhEoNNbFk5DEW/T9zBr9z7ca', 'superadmin', 'Super Admin', 1, 0);

SELECT id, email, role FROM users;
