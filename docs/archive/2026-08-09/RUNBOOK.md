# HRMS Operations Runbook
**Version:** 2.0 | **For:** System Administrator / Client IT Team | **Database:** MySQL 8.4

---

## 1. First-Time Setup (Do This Once Before Go-Live)

### Step 1 — Generate secrets
```bash
chmod +x scripts/generate-secrets.sh
./scripts/generate-secrets.sh
```
This creates `.env` with strong random values for JWT_KEY, ENCRYPTION_KEY, MYSQL_PASSWORD, MYSQL_ROOT_PASSWORD, and REDIS_PASSWORD.

### Step 2 — Edit `.env` and fill in your details
Open `.env` and update:
```
ALLOWED_ORIGINS=https://yourdomain.com
APP_BASE_URL=https://yourdomain.com
EMAIL_HOST=smtp.gmail.com        # or your SMTP provider
EMAIL_PORT=587
EMAIL_USERNAME=your@email.com
EMAIL_PASSWORD=your-app-password
EMAIL_FROM_ADDRESS=noreply@yourdomain.com
PAYROLL_DEFAULT_STATE=Maharashtra   # change to your state
```

### Step 3 — Install TLS certificate
Place your SSL certificate files at:
```
./nginx/ssl/cert.pem
./nginx/ssl/key.pem
```
**Free option (Let's Encrypt):**
```bash
sudo apt install certbot
sudo certbot certonly --standalone -d yourdomain.com
sudo cp /etc/letsencrypt/live/yourdomain.com/fullchain.pem ./nginx/ssl/cert.pem
sudo cp /etc/letsencrypt/live/yourdomain.com/privkey.pem   ./nginx/ssl/key.pem
```
Set up auto-renewal: `sudo certbot renew --dry-run`

### Step 4 — Start the system
```bash
docker compose up -d
```
Wait ~30 seconds, then check:
```bash
docker compose ps          # all services should show "healthy"
curl https://yourdomain.com/health
```
Expected response: `{"status":"Healthy"}`

### Step 5 — Change the default password (CRITICAL)
1. After the first `dotnet run` / container start, find the line in **stdout** that reads:
   ```
   [SeedAsync] SuperAdmin created. One-time password: <generated-value>
   ```
   Copy that password immediately — it is never stored or logged in plaintext again.

   > **Tip:** If you prefer a deterministic password for automated deployments, set the
   > `SUPERADMIN_INITIAL_PASSWORD` environment variable before first boot. The seeder will
   > use that value instead of generating a random one. Change it immediately after first login.

2. Open `https://yourdomain.com` in a browser and log in with `superadmin@hrms.com` and the
   password printed in step 1. There is no hardcoded default — do **not** attempt `Admin@123`.
3. Go to Profile → Change Password immediately.

### Step 6 — Disable auto-migrations
After the first successful start, open `.env` and set:
```
AUTO_MIGRATE=false
```
Then: `docker compose up -d api` (restarts only the API)

From now on, migrations are applied manually via `./scripts/migrate.sh`.

---

## 2. Daily Operations

### Start all services
```bash
docker compose up -d
```

### Stop all services (data is preserved)
```bash
docker compose down
```

### Restart a single service
```bash
docker compose restart api     # restart only the API
docker compose restart nginx   # restart only the web server
```

### Check service health
```bash
docker compose ps
curl https://yourdomain.com/health
```

---

## 3. Where Logs Live

| Source | Location |
|---|---|
| API application logs | `HRMS.API/Logs/hrms-YYYY-MM-DD.log` (inside container) |
| API live logs | `docker compose logs -f api` |
| Nginx access logs | `docker compose logs -f nginx` |
| Database logs | `docker compose logs mysql` |
| All services | `docker compose logs -f` |

**View last 100 lines of API log:**
```bash
docker compose logs --tail=100 api
```

**Search for errors:**
```bash
docker compose logs api 2>&1 | grep -i "error\|exception\|fail"
```

---

## 4. Database Backup & Restore

### Manual backup (creates a compressed .sql.gz file)
```bash
chmod +x scripts/mysql-backup.sh
./scripts/mysql-backup.sh
```
Backups are saved to `./backups/hrms_YYYYMMDD_HHMMSS.sql.gz`

### Automated daily backup (via cron)
Run as root or the Docker user:
```bash
crontab -e
```
Add this line (runs backup at 2:00 AM daily):
```
0 2 * * * /path/to/hrms/scripts/mysql-backup.sh >> /var/log/hrms-backup.log 2>&1
```
Backups older than 14 days are auto-deleted. Change `RETAIN_DAYS` in the script to adjust.

### Restore from backup
```bash
# Stop the API so no writes happen during restore
docker compose stop api

# Restore
gunzip < backups/hrms_20260716_020000.sql.gz | \
  docker compose exec -T mysql \
  mysql -u hrms -p"$MYSQL_PASSWORD" hrms_db

# Restart
docker compose start api
```

---

## 5. Database Migrations (After First Deploy)

When a new version of HRMS is deployed that includes schema changes:

```bash
chmod +x scripts/migrate.sh
./scripts/migrate.sh
```
This script will:
1. Ask for confirmation
2. Create an automatic backup first
3. Apply pending EF Core migrations
4. Report success or failure

**Never** run migrations directly in production without a fresh backup.

---

## 6. Rotating Secrets

### Rotate JWT_KEY (forces all users to log in again)
```bash
NEW_KEY=$(openssl rand -base64 48)
# Edit .env — replace JWT_KEY=... with the new value
# Then restart the API:
docker compose restart api
```

### Rotate database password
```bash
# 1. Generate new password
NEW_PASS=$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 28)

# 2. Change it in MySQL
docker exec -it $(docker compose ps -q mysql) \
  mysql -u root -p"$MYSQL_ROOT_PASSWORD" -e \
  "ALTER USER 'hrms'@'%' IDENTIFIED BY '$NEW_PASS';"

# 3. Update .env — replace MYSQL_PASSWORD=...
# 4. Update the connection string in .env — replace the password part
# 5. Restart
docker compose restart api mysql
```

### Renew TLS certificate (Let's Encrypt)
```bash
sudo certbot renew
sudo cp /etc/letsencrypt/live/yourdomain.com/fullchain.pem ./nginx/ssl/cert.pem
sudo cp /etc/letsencrypt/live/yourdomain.com/privkey.pem   ./nginx/ssl/key.pem
docker compose restart nginx
```

---

## 7. Adding Users

### Add a new company admin
1. Log in as superadmin
2. Go to **Admin Users → Add User**
3. Set role to `admin`
4. The user receives a temporary password by email and must change it on first login

### Add a regular employee
1. Log in as admin
2. Go to **Employees → Add Employee**
3. Fill all required fields and submit
4. The employee receives login credentials by email

---

## 8. Common Troubleshooting

### Problem: App shows blank page or can't connect
```bash
docker compose ps           # check all containers are "Up"
docker compose logs nginx   # check for SSL cert errors
docker compose logs api     # check for startup errors
curl http://localhost:8080/health   # test API directly (bypasses nginx)
```

### Problem: "Invalid JWT" or users getting logged out unexpectedly
- JWT_KEY was likely rotated. All active sessions are invalidated when JWT_KEY changes.
- Ensure `.env` has a stable JWT_KEY that doesn't change between restarts.

### Problem: Emails not being sent
```bash
docker compose logs api 2>&1 | grep -i "email\|smtp\|mail"
```
- Check `EMAIL_HOST`, `EMAIL_USERNAME`, `EMAIL_PASSWORD` in `.env`
- For Gmail: use an App Password (not your account password)
- Test SMTP: `telnet smtp.gmail.com 587`

### Problem: Database connection error on startup
```bash
docker compose logs mysql    # check MySQL started cleanly
docker compose logs api      # look for "connection refused" or auth errors
```
- Ensure `MYSQL_PASSWORD` in `.env` matches what MySQL was initialized with
- If MySQL was initialized with a different password, you must reset it or use a fresh volume

### Problem: File uploads failing
- Check `HRMS.API/wwwroot/uploads/` exists inside the container
- The Docker volume `hrms_uploads` must be mounted (check `docker compose.yml`)
- Check disk space: `df -h`

### Problem: Rate limit errors (429 Too Many Requests)
- This is expected behaviour for more than 10 login attempts per minute per IP
- If legitimate users are blocked, check Redis is running: `docker compose ps redis`
- Temporary fix: `docker compose restart redis` (clears all rate limit counters)

---

## 9. Disk Space Management

```bash
# Check disk usage
df -h

# Check Docker volume sizes
docker system df

# Clean up stopped containers, unused images (safe to run)
docker system prune -f

# Check backup folder size
du -sh backups/
```

---

## 10. Emergency Contacts & Escalation

| Issue | Who to Contact |
|---|---|
| Application bugs / feature requests | Development team |
| Server / infrastructure issues | Your hosting provider |
| SSL certificate problems | Certbot docs or your CA |
| Data recovery | Use backup restore procedure (Section 4) |

---

*Last updated: July 2026 — MySQL 8.4 migration*
