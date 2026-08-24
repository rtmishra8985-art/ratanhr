# MySQL Workbench Connection Setup Guide for HRMS

## 🔌 MySQL Connection Credentials (Development)

### From `.env` File:
```
Database Host:     localhost
Port:              3307 (Docker host port mapping)
Username:          hrms
Password:          hrms_secure_password_123
Database:          hrms_db
Default Schema:    hrms_db
```

**OR** (Docker internal networking - from within container):
```
Database Host:     mysql
Port:              3306
Username:          hrms
Password:          hrms_secure_password_123
Database:          hrms_db
```

### Root Access (for administration):
```
Username:          root
Password:          root_secure_password_456
Port:              3307 (Docker host)
```

---

## 📋 Complete Connection Details

### Development Environment (.env)
```
MYSQL_DATABASE=hrms_db
MYSQL_USER=hrms
MYSQL_PASSWORD=hrms_secure_password_123
MYSQL_ROOT_PASSWORD=root_secure_password_456
ConnectionStrings__DefaultConnection=Server=localhost;Port=3307;Database=hrms_db;User ID=hrms;Password=hrms_secure_password_123;AllowPublicKeyRetrieval=True;SslMode=none
```

### Connection String Details
```
Server=localhost;
Port=3307;
Database=hrms_db;
User ID=hrms;
Password=hrms_secure_password_123;
AllowPublicKeyRetrieval=True;
SslMode=none
```

---

## 🔧 MySQL Workbench Setup Steps

### 1. **Open MySQL Workbench**
   - Launch MySQL Workbench on your machine
   - Click on **Database** → **Manage Connections** (or use the "+" icon next to "MySQL Connections")

### 2. **Create New Connection**

#### Basic Settings:
- **Connection Name:** `HRMS Local Development`
- **Connection Method:** `Standard (TCP/IP)`
- **Hostname:** `localhost` (or `127.0.0.1`)
- **Port:** `3307`
- **Username:** `hrms`
- **Password:** `hrms_secure_password_123` (check "Store in Vault" for security)
- **Default Schema:** `hrms_db`

#### Advanced Settings (Optional):
- **SSL Mode:** `PREFER` (or `NONE` for local development)
- **SSL CA File:** Leave blank (for local)
- **SSL Cert File:** Leave blank
- **SSL Key File:** Leave blank

### 3. **Test Connection**
- Click **Test Connection**
- If successful, you should see: ✓ **"Connection successful"**
- If failed, verify:
  - Docker containers are running: `docker ps`
  - MySQL container is healthy: `docker logs mysql`
  - Port 3307 is exposed: Check docker-compose.yml

### 4. **Save Connection**
- Click **OK** to save the connection
- The connection appears in MySQL Workbench main screen

### 5. **Connect**
- Double-click the connection to open it
- You should now see all HRMS tables in the **Schemas** panel

---

## 🐳 Docker Port Mapping

### From `.env` - Local Host Access
```
MySQL Service Internal Port:   3306
Exposed to Host:               3307 (mapped)
```

**In docker-compose.yml:**
```yaml
mysql:
  image: mysql:8.4@sha256:...
  ports:
    # Port 3306 intentionally NOT exposed to host — only reachable within Docker network.
    # (This comment means it's NOT exposed in production, but you can access via 3307 locally)
```

**Access from host machine:**
- `localhost:3307` ← Use this in MySQL Workbench

**Access from within Docker network:**
- `mysql:3306` (e.g., from the API container)

---

## 📊 HRMS Database Tables

After connecting, you'll see the following schema structure:

### Main Table Groups:
1. **Authentication** (6 tables)
   - users, roles, permissions, refresh_tokens, password_reset_tokens, audit_logs

2. **Organization** (5 tables)
   - companies, departments, company_branches, company_settings, designations

3. **Employee Management** (13 tables)
   - employees, employee_documents, employee_goals, shifts, assets, etc.

4. **Attendance & Tracking** (8 tables)
   - web_attendances, biometric_logs, geofences, timesheets, etc.

5. **Payroll & Finance** (9+ tables)
   - payslips, salary_structures, bonuses, deductions, expense_claims, etc.

6. **Leave Management** (5 tables)
   - leave_requests, leave_types, leave_balances, holiday_calendars

7. **Recruitment** (4 tables)
   - candidates, job_requisitions, interviews, offer_letters

8. **And 80+ more tables** for Performance, Travel, Sales, Training, Helpdesk, etc.

**Total: 95 tables**

---

## 🔐 Security Notes

### Development (Your Current Setup)
- ✓ SslMode=none (acceptable for local development)
- ✓ AllowPublicKeyRetrieval=True (allows password-based auth)
- ⚠️ Passwords stored in `.env` (not committed to git)
- ✓ MySQL root user password set

### Production (When Deploying)
- ✗ Never expose MySQL port 3306 to internet
- ✓ Use SslMode=Required
- ✓ Use strong, randomly-generated passwords
- ✓ Store secrets in Docker Secrets or env vars (not .env)
- ✓ Network: MySQL only accessible within Docker internal network
- ✓ Encrypted backups: AES-256-CBC + PBKDF2

---

## 📝 Sample Queries (from MySQL Workbench)

After connecting, you can run queries:

### 1. List All Companies (Tenants)
```sql
SELECT * FROM companies;
```

### 2. Count Employees per Company
```sql
SELECT company_id, COUNT(*) as employee_count
FROM employees
WHERE is_active = 1
GROUP BY company_id;
```

### 3. Recent Audit Logs
```sql
SELECT * FROM audit_logs
ORDER BY occurred_at DESC
LIMIT 20;
```

### 4. Employee Attendance Summary
```sql
SELECT 
    e.first_name, 
    e.last_name, 
    COUNT(wa.id) as attendance_count,
    MAX(wa.att_date) as last_attendance
FROM employees e
LEFT JOIN web_attendances wa ON e.id = wa.employee_id
GROUP BY e.id
ORDER BY last_attendance DESC;
```

### 5. Monthly Payroll Data
```sql
SELECT 
    e.first_name,
    e.last_name,
    p.month,
    p.year,
    p.gross_pay,
    p.net_pay
FROM payslips p
JOIN employees e ON p.employee_id = e.id
ORDER BY p.year DESC, p.month DESC
LIMIT 50;
```

---

## 🚀 Starting the HRMS Stack

Before connecting in MySQL Workbench, ensure containers are running:

```bash
# Start all services (including MySQL)
docker-compose up -d

# Check if MySQL is healthy
docker-compose ps
# You should see: mysql ... healthy

# Check MySQL logs
docker-compose logs mysql

# Access MySQL from CLI (alternative to Workbench)
docker exec -it mysql mysql -u hrms -p hrms_db
# Password: hrms_secure_password_123
```

---

## ⚠️ Troubleshooting Connection Issues

### Issue: "Connection refused" or "Can't connect to MySQL server"

**Solution 1: Verify Docker is running**
```bash
docker ps
```
If no containers shown, start Docker Desktop or service.

**Solution 2: Check if MySQL container is running**
```bash
docker-compose ps
```
Should show `mysql ... healthy`

**Solution 3: Restart MySQL container**
```bash
docker-compose restart mysql
docker-compose logs mysql
```

**Solution 4: Verify port mapping**
```bash
# Should show 0.0.0.0:3307->3306/tcp
docker-compose ps mysql

# Or check ports
netstat -tlnp | grep 3307  # Linux/macOS
netstat -ano | findstr :3307  # Windows
```

**Solution 5: Check MySQL logs**
```bash
docker-compose logs mysql

# Look for errors like:
# ERROR 2002 (HY000): Can't connect to local MySQL server
# ERROR 1045 (28000): Access denied for user 'hrms'
```

**Solution 6: Test connection from command line**
```bash
# From host machine
mysql -h 127.0.0.1 -P 3307 -u hrms -p hrms_db
# When prompted for password, enter: hrms_secure_password_123

# If successful, you'll see: mysql>
# Type: SHOW TABLES;
# Then: EXIT;
```

**Solution 7: Check credentials**
- Username: `hrms` (not `root`)
- Password: `hrms_secure_password_123`
- Host: `localhost` or `127.0.0.1`
- Port: `3307` (NOT 3306 - that's internal Docker port)

---

## 🔗 Alternative Connection Methods

### 1. MySQL Command Line (CLI)
```bash
mysql -h localhost -P 3307 -u hrms -p hrms_db
# Password: hrms_secure_password_123
```

### 2. DBeaver (Alternative to Workbench)
- **Driver:** MySQL 8.0
- **Server Host:** localhost
- **Port:** 3307
- **Database:** hrms_db
- **Username:** hrms
- **Password:** hrms_secure_password_123

### 3. DataGrip (JetBrains)
- **Host:** localhost
- **Port:** 3307
- **User:** hrms
- **Password:** hrms_secure_password_123
- **Database:** hrms_db

### 4. From .NET Application (Connection String)
```
Server=localhost;Port=3307;Database=hrms_db;User ID=hrms;Password=hrms_secure_password_123;AllowPublicKeyRetrieval=True;SslMode=none
```

### 5. Docker Network (from another Docker container)
```
Server=mysql;Port=3306;Database=hrms_db;User ID=hrms;Password=hrms_secure_password_123;AllowPublicKeyRetrieval=True;SslMode=Required
```

---

## 📊 Database Information

### MySQL Version
- **Version:** 8.4
- **Image:** `mysql:8.4@sha256:1d6b6a8fcee8ff758ff151d017f5203cd06792a0e698f0a593c9dfcb14609cf0`
- **Character Set:** utf8mb4
- **Collation:** utf8mb4_unicode_ci

### Database Statistics
- **Database Name:** hrms_db
- **Total Tables:** 95
- **Total Columns:** ~2,500+
- **Indexes:** 300+
- **Foreign Keys:** 150+

### Tablespace Info
```sql
SELECT table_schema, 
       ROUND(SUM(data_length + index_length) / 1024 / 1024, 2) as size_mb
FROM information_schema.tables
WHERE table_schema = 'hrms_db'
GROUP BY table_schema;
```

---

## 🔄 Data Flow

```
MySQL Workbench
       ↓ (TCP/IP)
localhost:3307
       ↓ (port mapping)
Docker Bridge Network
       ↓
mysql:3306 (container internal)
       ↓
/var/lib/mysql (volume mount)
       ↓
hrms_mysqldata (Docker volume)
```

---

## 📋 Quick Reference Card

```
╔══════════════════════════════════════════════════════╗
║        HRMS MySQL Connection Quick Reference        ║
╠══════════════════════════════════════════════════════╣
║ Connection Name:    HRMS Local Development          ║
║ Connection Method:  Standard (TCP/IP)               ║
║ Hostname:          localhost (or 127.0.0.1)         ║
║ Port:              3307                             ║
║ Username:          hrms                             ║
║ Password:          hrms_secure_password_123         ║
║ Database:          hrms_db                          ║
║ Default Schema:    hrms_db                          ║
║ SSL Mode:          PREFER (or NONE for local)       ║
╠══════════════════════════════════════════════════════╣
║ Status Commands:                                     ║
║ • docker-compose ps                                 ║
║ • docker-compose logs mysql                         ║
║ • mysql -h localhost -P 3307 -u hrms -p             ║
║ • curl http://localhost:8080/health                 ║
╠══════════════════════════════════════════════════════╣
║ Database Version:  MySQL 8.4                        ║
║ Total Tables:      95                               ║
║ Volume Mount:      hrms_mysqldata                   ║
╚══════════════════════════════════════════════════════╝
```

---

## 🎯 Next Steps

1. **Configure MySQL Workbench** using the credentials above
2. **Test the connection** by clicking "Test Connection"
3. **Explore the 95 tables** in the Schemas panel
4. **Run sample queries** to understand the data structure
5. **Review the schema** for your development/testing needs
6. **Backup workflows** - automated encrypted backups run daily at 02:00 UTC

---

## 📞 Support

If you encounter issues:
1. Check `docker-compose logs mysql` for MySQL errors
2. Verify port 3307 is open: `netstat -tlnp | grep 3307`
3. Ensure credentials match `.env` file exactly
4. Restart containers: `docker-compose restart mysql`
5. Check network: `docker network ls` and `docker network inspect hrms_internal`

---

**Document Generated:** 2026-08-19  
**HRMS Version:** 1.0.4  
**MySQL Version:** 8.4  
**Status:** Ready for Local Development

