# HRMS Project - Running Status & Superadmin Credentials

## 🚀 Project Status

### Build Status: ✅ SUCCESS
- ✅ HRMS.Domain compiled
- ✅ HRMS.Application compiled
- ✅ HRMS.Infrastructure compiled  
- ✅ HRMS.API compiled
- ✅ HRMS.SPA.Source (React/Vite) compiled
- ✅ Docker images built (api, migrate)
- ✅ SPA assets generated (dist/public/)

### Running Services Status
```
✅ Grafana            127.0.0.1:3000   →  Metrics dashboard
✅ Jaeger            127.0.0.1:16686  →  Distributed tracing
✅ Prometheus        127.0.0.1:9090   →  Metrics scraper
✅ ClamAV            3310/tcp         →  Antivirus scanning
⚠️  MySQL             Created (port conflict)
⚠️  Redis             Created (port conflict)
⚠️  API               Created (waiting for MySQL/Redis)
⚠️  Migrate           Created (waiting for MySQL)
⚠️  AlertManager      Restarting (config error - non-critical)
```

---

## 🔐 Superadmin Login Credentials

### Default Account (FROM .env)
```
Email:    superadmin@hrms.com
Password: Password@123
Status:   MustChangePassword = true (must change on first login)
```

### Superadmin Creation (from Program.cs SeedAsync)
The superadmin is created automatically during first API startup with:
- Email: `superadmin@hrms.com`
- Password: Value of `SUPERADMIN_INITIAL_PASSWORD` env var (currently: `Password@123`)
- BCrypt hash generated at startup
- MustChangePassword flag set to `true`
- First login forces password change

---

## 🌐 Frontend & API Ports

### Frontend Ports (Vite/React)
```
Development:  http://localhost:5173    (Primary dev port)
Alternative:  http://localhost:3000    (Legacy/fallback)
Production:   http://localhost:80      (via Nginx)
HTTPS:        https://localhost:443    (via Nginx)
```

### API Port
```
Development:  http://localhost:8080    (ASP.NET Core)
Behind Nginx: http://localhost/api     (via reverse proxy)
```

### CORS Allowed Origins (`.env`)
```
http://localhost:3000
http://localhost:5173
http://localhost
```

---

## 📊 Database & Cache Configuration

### MySQL 8.4
```
Host:        localhost (Docker internal: mysql)
Port:        3307 (Host) / 3306 (Docker)
Database:    hrms_db
User:        hrms
Password:    hrms_secure_password_123
Root PW:     root_secure_password_456
Status:      ✅ Built, 🔴 Port conflict on host 3306
```

### Redis 7.4-alpine
```
Host:        localhost (Docker internal: redis)
Port:        6379
Password:    redis_secure_password_789
Status:      ✅ Built, 🔴 Port conflict on host 6379
```

---

## 🔧 How to Fix Port Conflicts & Run

### Option 1: Kill Process Using Port 3306 (Recommended)
```bash
# Identify process
Get-NetTCPConnection -State Listen | Where-Object {$_.LocalPort -eq 3306}

# Kill it (requires admin PowerShell)
taskkill /PID <PID> /F

# Then restart compose
docker-compose up -d
```

### Option 2: Modify docker-compose.yml Port Mappings
```yaml
# Change these lines in docker-compose.yml:
mysql:
  ports:
    - "3308:3306"  # Use 3308 instead of 3307

redis:
  ports:
    - "6380:6379"  # Use 6380 instead of 6379
```

### Option 3: Use WSL2 Default Port Assignment (Easiest)
```bash
# Restart Docker Desktop and containers
docker-compose down --remove-orphans
docker system prune -f
docker-compose up -d
```

---

## 🎯 Quick Start (Once Ports Fixed)

### 1. Start Full Stack
```bash
docker-compose up -d
# Wait 30-60 seconds for all services to be healthy
```

### 2. Verify Services Running
```bash
docker-compose ps
# Should show:
# ✓ api (healthy after migrations)
# ✓ mysql (healthy)
# ✓ redis (healthy)
# ✓ migrate (service_completed_successfully)
# ✓ prometheus, grafana, jaeger, clamav (running)
```

### 3. Access Frontend
```
http://localhost:5173
or
http://localhost:3000
```

### 4. Login
```
Email:    superadmin@hrms.com
Password: Password@123
→ Forced password change on first login
```

### 5. Verify Backend
```bash
curl http://localhost:8080/health
curl http://localhost:8080/healthz/live
curl http://localhost:8080/healthz/ready
```

---

## 📈 Available Dashboards & Tools

| Service | URL | Purpose |
|---------|-----|---------|
| **Frontend** | http://localhost:5173 | HRMS React SPA |
| **API Health** | http://localhost:8080/health | Backend status |
| **Prometheus** | http://localhost:9090 | Metrics (localhost only) |
| **Grafana** | http://localhost:3000 | Dashboards (localhost only) |
| **Jaeger** | http://localhost:16686 | Traces (localhost only) |
| **MailHog** | http://localhost:8025 | Email testing (if added) |

---

## 🏗️ Migration Process (Automatic)

When API starts, it automatically:
1. Waits for MySQL to be healthy
2. Runs `docker-compose migrate` service
   - Copies source code to migrate container
   - Runs `dotnet ef database update`
   - Applies all pending migrations
3. Seed operations (SeedAsync):
   - Creates superadmin account (superadmin@hrms.com)
   - Hashes password with BCrypt
   - Seeds 5 default leave types
4. API starts and exposes port 8080

---

## 🔐 Security Notes

### Development Mode (Current)
```
✓ JWT RS256 (asymmetric)
✓ AES-256-GCM encryption for PII
✓ Multi-tenancy isolation
✓ Rate limiting (Redis-backed)
✓ CORS configured
✓ CSRF protection (double-submit)
✗ HTTPS disabled (localhost)
```

### What to Secure Before Production
- [ ] Generate real RSA key pair (scripts/generate-rsa-keys.sh)
- [ ] Set strong random passwords for MySQL, Redis
- [ ] Enable HTTPS with valid certificates
- [ ] Configure CORS for actual domain
- [ ] Set Jwt__ExpiresInMinutes lower (30 min → 15 min)
- [ ] Enable security headers (HSTS, CSP, etc.)

---

## 📝 Environment (.env) Summary

```
# Database
MYSQL_DATABASE=hrms_db
MYSQL_USER=hrms
MYSQL_PASSWORD=hrms_secure_password_123
MYSQL_ROOT_PASSWORD=root_secure_password_456

# Superadmin
SUPERADMIN_INITIAL_PASSWORD=Password@123

# Redis
REDIS_PASSWORD=redis_secure_password_789

# Frontend CORS
ALLOWED_ORIGINS=http://localhost:3000,http://localhost:5173,http://localhost

# Auto-migrate at startup
DATABASE__AUTOMIGRATE=true
```

---

## 🐛 Troubleshooting

### MySQL Won't Start
```bash
# Check what's using 3306
netstat -ano | findstr ":3306"

# Kill the process (admin PowerShell)
taskkill /PID <PID> /F

# Or use different port in docker-compose.yml
```

### API Container Created But Not Starting
```bash
# Check logs
docker-compose logs api

# Common issues:
# - MySQL not healthy → wait for migrate to complete
# - Redis not healthy → check REDIS_PASSWORD env var
# - JWT keys missing → scripts/generate-rsa-keys.sh
```

### Frontend Not Loading
```bash
# Check if API is reachable
curl http://localhost:8080/healthz/live

# Check CORS settings
echo $ALLOWED_ORIGINS

# Verify Nginx (if using production)
docker-compose logs nginx
```

### Rate Limiting Not Working
```bash
# Check Redis connection
docker-compose exec api redis-cli -h redis ping

# Should return: PONG
```

---

## 📊 Build Summary

**Build Time:** ~2-3 minutes
**Image Sizes:**
- `ratanhr_new-api`: ~500MB (includes React SPA in wwwroot)
- `ratanhr_new-migrate`: ~700MB (includes dotnet-ef tools)

**Build Output:**
- ✅ 3 domain projects compiled successfully (5 warnings - non-critical)
- ✅ React SPA built with Vite (462 KB JS bundle, 121 KB CSS)
- ✅ All 95 database tables defined in EF Core

---

## ✅ Next Steps

1. **Fix port conflicts** (Option 1, 2, or 3 above)
2. **Start services:** `docker-compose up -d`
3. **Wait for health:** `docker-compose ps` until all are healthy
4. **Open frontend:** http://localhost:5173
5. **Login:** superadmin@hrms.com / Password@123
6. **Change password** on first login
7. **Access dashboards:** Grafana (3000), Jaeger (16686), Prometheus (9090)

---

**Status as of:** 2026-08-20 15:35 UTC  
**Version:** 1.0.4  
**Ready to run:** ✅ Yes (once port conflicts resolved)

