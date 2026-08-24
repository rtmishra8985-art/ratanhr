# Login Error Troubleshooting Guide

## Problem
You're getting "An unexpected error occurred" when trying to login with superadmin credentials.

## Root Cause
The database connection string is not properly configured for your local Windows environment.

## Solution

### Option 1: Use Docker Compose (Recommended)
Run the entire stack with Docker, which includes MySQL, Redis, and MailHog:

```bash
docker-compose -f docker-compose-dev.yml up -d
```

This will start:
- **MySQL** on `localhost:3306` (credentials in `.env`)
- **Redis** on `localhost:6379`
- **MailHog** on `localhost:8025` (email web UI)

Then run the .NET app and it will auto-migrate the database.

### Option 2: Connect to Local MySQL
If you have MySQL running locally on Windows:

1. **Verify MySQL is running:**
   ```bash
   mysql -h localhost -u root -p
   ```

2. **Create the database and user:**
   ```sql
   CREATE DATABASE hrms_db;
   CREATE USER 'hrms'@'localhost' IDENTIFIED BY 'hrms_secure_password_123';
   GRANT ALL PRIVILEGES ON hrms_db.* TO 'hrms'@'localhost';
   FLUSH PRIVILEGES;
   ```

3. **Set the connection string in `.env.local`:**
   ```
   ConnectionStrings__DefaultConnection=Server=localhost;Port=3306;Database=hrms_db;User ID=hrms;Password=hrms_secure_password_123;AllowPublicKeyRetrieval=True;SslMode=none
   ```

4. **Restart the API** — it will auto-migrate on startup.

## Login Credentials

**Email:** `superadmin@hrms.com` (NOT `superadmin@test.local`)  
**Password:** Set via `SUPERADMIN_INITIAL_PASSWORD` env var or generated randomly on first startup

If the database is seeded for the first time, the superadmin password is logged at startup. Check the application logs.

## Verify Setup

After the app starts:

1. **Check database connectivity:**
   - If you see "Entity Framework Core" errors, the database isn't reachable.
   
2. **Check JWT keys:**
   - If you see "Jwt:PrivateKeyPem is not configured", set:
     ```
     Jwt__PrivateKeyPem=<your-rsa-private-key>
     Jwt__PublicKeyPem=<your-rsa-public-key>
     ```
   - Generate with: `scripts/generate-rsa-keys.sh`

3. **Check encryption key:**
   - If you see "ENCRYPTION_KEY must decode to exactly 32 bytes", set:
     ```
     ENCRYPTION_KEY=<base64-encoded-32-byte-key>
     ```
   - Generate with: `openssl rand -base64 32`

## Error Messages in Development

The API now returns detailed error messages in Development mode. In Production, errors are generic for security. Check the returned JSON response for `details` field with the full stack trace.

## Still Having Issues?

1. **Restart the API** (rebuild may be required after config changes)
2. **Check the logs** — detailed error messages are logged with the trace ID
3. **Verify all required environment variables** are set correctly
