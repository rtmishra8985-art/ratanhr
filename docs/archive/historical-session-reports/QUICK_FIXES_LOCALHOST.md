# HRMS — Quick Fixes for Localhost Testing

Apply these fixes in order. Estimated time: **15 minutes**.

---

## Fix 1: Add Email.ToAddress to appsettings.json

**File:** `HRMS.API/appsettings.json`

```json
"Email": {
  "_comment": "SMTP settings for MailKit. When Host is empty, the app logs emails instead of sending. Port 587 + UseSsl=false is the correct STARTTLS configuration (plain TCP upgrades to TLS via EHLO STARTTLS). For implicit TLS use Port=465 and UseSsl=true. Do NOT set UseSsl=true on port 587 — it will handshake-fail.",
  "Host": "",
  "Port": 587,
  "UseSsl": false,
  "Username": "",
  "Password": "",
  "FromAddress": "noreply@hrms.com",
  "FromName": "HRMS System",
  "ToAddress": "test@localhost",
  "AppBaseUrl": "http://localhost:5000"
}
```

**Why:** The email service tries to send to `Email__ToAddress` but it's not defined in the config object.

---

## Fix 2: Update .env for Localhost

**File:** `.env`

Replace these lines:

**OLD:**
```bash
DOMAIN_NAME=hrms.company.com
API_URL=https://hrms.company.com/api
ALLOWED_ORIGINS=https://hrms.company.com
AllowedHosts=hrms.company.com
ALLOWED_HOSTS=hrms.company.com
ConnectionStrings__DefaultConnection=Server=mysql;Port=3306;Database=hrms_db;User ID=hrms;Password=hrms_secure_password_123;AllowPublicKeyRetrieval=True;SslMode=Required
EMAIL_HOST=localhost
EMAIL_PORT=1025
```

**NEW (for localhost testing):**
```bash
DOMAIN_NAME=localhost
API_URL=http://localhost/api
ALLOWED_ORIGINS=http://localhost:3000,http://localhost:5173,http://localhost
AllowedHosts=localhost;127.0.0.1;localhost:3000
ALLOWED_HOSTS=localhost;127.0.0.1;localhost:3000
ConnectionStrings__DefaultConnection=Server=mysql;Port=3306;Database=hrms_db;User ID=hrms;Password=hrms_secure_password_123;AllowPublicKeyRetrieval=True;SslMode=none
EMAIL_HOST=mailhog
EMAIL_PORT=1025
```

**Why:** 
- Localhost CORS: SPA at port 3000 needs explicit origin
- SslMode=none: Docker MySQL doesn't have valid SSL certs
- MailHog: Local SMTP server (no external relay needed)

---

## Fix 3: Add MailHog to docker-compose.yml

**File:** `docker-compose.yml`

Add this service after the `redis:` block (around line 130):

```yaml
  # ── MailHog (Local Email Testing) ────────────────────────────────────────
  mailhog:
    image: mailhog/mailhog:v1.0.1
    networks: [hrms_internal]
    ports:
      - "1025:1025"      # SMTP for api service
      - "8025:8025"      # Web UI for manual testing
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "wget", "-qO-", "http://localhost:1025"]
      interval: 10s
      timeout: 5s
      retries: 3
```

**Also update the `api:` service `depends_on:`** (around line 165):

```yaml
  api:
    ...
    depends_on:
      mailhog:
        condition: service_healthy
      mysql:
        condition: service_healthy
      ...
```

**Why:** The API sends emails via SMTP to MailHog instead of an external relay.

---

## Fix 4: Update appsettings.Development.json (already correct, but verify)

**File:** `HRMS.API/appsettings.Development.json`

Verify this is present:
```json
"Hangfire": {
  "_comment": "Development uses in-process Hangfire storage. Non-development environments use the compatible Redis adapter.",
  "UseInMemory": true
}
```

**Why:** Development mode uses in-memory jobs; no Redis needed for local testing.

---

## Fix 5: Generate JWT Keys (One-time setup)

Run this on your machine (Windows PowerShell or WSL):

```bash
# Generate RSA key pair
openssl genrsa -out /tmp/private_key.pem 2048
openssl rsa -in /tmp/private_key.pem -pubout -out /tmp/public_key.pem

# Format for .env (escape newlines as \n, then surround with quotes)
# On Windows PowerShell:
$priv = (Get-Content /tmp/private_key.pem -Raw) -replace "`n", "\n"
$pub = (Get-Content /tmp/public_key.pem -Raw) -replace "`n", "\n"

Write-Host "Copy these into .env:`n"
Write-Host "JWT_PRIVATE_KEY_PEM=$priv"
Write-Host "`nJWT_PUBLIC_KEY_PEM=$pub"
```

Paste the output into `.env`:
```bash
JWT_PRIVATE_KEY_PEM=-----BEGIN RSA PRIVATE KEY-----\n...content...\n-----END RSA PRIVATE KEY-----
JWT_PUBLIC_KEY_PEM=-----BEGIN PUBLIC KEY-----\n...content...\n-----END PUBLIC KEY-----
Jwt__PrivateKeyPem=-----BEGIN RSA PRIVATE KEY-----\n...content...\n-----END RSA PRIVATE KEY-----
Jwt__PublicKeyPem=-----BEGIN PUBLIC KEY-----\n...content...\n-----END PUBLIC KEY-----
```

**Why:** The API can't start without RSA keys for JWT signing.

---

## Fix 6: Add Basic .dockerignore Entries

**File:** `.dockerignore`

Append:
```
.git
.gitignore
.github
.vs
.vscode
node_modules
dist/
bin/
obj/
*.md
coverage/
test-results/
```

**Why:** Faster Docker builds (excludes unnecessary files).

---

## Verification Script

Run this before `docker compose up`:

```bash
# Windows PowerShell
$errors = @()

# Check JWT keys
if ([string]::IsNullOrWhiteSpace($env:JWT_PRIVATE_KEY_PEM)) {
    $errors += "❌ JWT_PRIVATE_KEY_PEM not set in .env"
}
if ([string]::IsNullOrWhiteSpace($env:JWT_PUBLIC_KEY_PEM)) {
    $errors += "❌ JWT_PUBLIC_KEY_PEM not set in .env"
}

# Check encryption keys
if ([string]::IsNullOrWhiteSpace($env:ENCRYPTION_KEY)) {
    $errors += "❌ ENCRYPTION_KEY not set in .env"
}

# Check Redis password
if ([string]::IsNullOrWhiteSpace($env:REDIS_PASSWORD)) {
    $errors += "❌ REDIS_PASSWORD not set in .env"
}

# Check DB password
if ([string]::IsNullOrWhiteSpace($env:MYSQL_PASSWORD)) {
    $errors += "❌ MYSQL_PASSWORD not set in .env"
}

# Check domain name
if ([string]::IsNullOrWhiteSpace($env:DOMAIN_NAME)) {
    $errors += "❌ DOMAIN_NAME not set in .env"
}

if ($errors.Count -gt 0) {
    Write-Host "Found $($errors.Count) errors:`n"
    $errors | ForEach-Object { Write-Host $_ }
    exit 1
} else {
    Write-Host "✅ All required environment variables are set."
    exit 0
}
```

Save as `check-env.ps1` and run:
```bash
. ./check-env.ps1
```

---

## Startup Commands

```bash
# Load .env and start the stack
docker compose up -d

# Wait for migrations to complete (watch for "complete" message)
docker compose logs -f migrate

# Tail API logs
docker compose logs -f api

# Check service health
docker compose ps

# Verify database is ready
docker compose exec mysql mysql -u hrms -phrms_secure_password_123 -e "SELECT 1" hrms_db

# Verify Redis is ready
docker compose exec redis redis-cli -a redis_secure_password_789 ping
```

---

## Test API After Startup

```bash
# 1. Health check
curl -s http://localhost:8080/health | jq .

# 2. Database ready check
curl -s http://localhost:8080/healthz/ready | jq .

# 3. Swagger UI
# Open: http://localhost:8080/swagger

# 4. CSRF token endpoint (should work even when not logged in)
curl -s http://localhost:8080/api/auth/csrf | jq .

# 5. Check MailHog web UI
# Open: http://localhost:8025
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `Connection refused: mysql:3306` | Wait 60s for MySQL healthcheck. Check: `docker compose logs mysql` |
| `Redis connection failed` | Check password in .env matches docker-compose.yml. Run: `docker compose logs redis` |
| `CORS error in browser` | Verify `ALLOWED_ORIGINS` includes `http://localhost:3000`. Check: `curl -H "Origin: http://localhost:3000" -v http://localhost:8080/api/auth/csrf` |
| `JWT key validation failed` | Regenerate keys (Fix 5). Check newlines are properly escaped as `\n` in .env |
| `Email not sending` | Verify MailHog is running: `docker compose logs mailhog`. Check: `http://localhost:8025` for messages. |
| `API won't start` | Check logs: `docker compose logs api`. Look for validation errors in startup output. |

---

## Done! 🎉

Your HRMS stack is now ready for localhost testing.

**Next:** Follow the test workflow in CODE_REVIEW_AND_LOCALHOST_SETUP.md
