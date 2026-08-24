# Adding MailHog to docker-compose.yml

This file shows exactly where and how to add the MailHog service.

---

## Location

Add this service **after the Redis service** (around line 130, before the API service).

---

## Service Definition (Copy/Paste)

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

---

## Update API Service Dependencies

Find the `api:` service (around line 165) and update its `depends_on:` section:

### BEFORE:
```yaml
  api:
    ...
    depends_on:
      mysql:
        condition: service_healthy
      redis:
        condition: service_healthy
      migrate:
        condition: service_completed_successfully
      clamav:
        condition: service_healthy
```

### AFTER:
```yaml
  api:
    ...
    depends_on:
      mailhog:
        condition: service_healthy
      mysql:
        condition: service_healthy
      redis:
        condition: service_healthy
      migrate:
        condition: service_completed_successfully
      clamav:
        condition: service_healthy
```

---

## Why This Matters

1. **API can't start** if it tries to send emails to a missing MailHog host
2. **depends_on health condition** ensures MailHog SMTP is ready before API boots
3. **Local testing** — emails captured at http://localhost:8025 instead of failing silently

---

## Verification

After adding MailHog and starting the stack:

```bash
# Check MailHog is running
docker compose ps | grep mailhog

# Test SMTP connection
docker compose exec api telnet mailhog 1025

# View web UI
# Open: http://localhost:8025

# Send test email
curl -X POST http://localhost:8080/api/auth/forgot-password \
  -H "Content-Type: application/json" \
  -d '{"email":"test@localhost"}'

# Email should appear at http://localhost:8025
```

---

## Alternative: Use an Actual SMTP Provider

If you prefer Brevo or Gmail SMTP for testing:

**In .env:**
```bash
EMAIL_HOST=smtp.gmail.com
EMAIL_PORT=587
EMAIL_USE_SSL=false
EMAIL_USERNAME=your-email@gmail.com
EMAIL_PASSWORD=your-app-password
EMAIL_FROM_ADDRESS=your-email@gmail.com
EMAIL_TO_ADDRESS=test@gmail.com

# Remove or skip MailHog service in docker-compose.yml
# and remove it from api depends_on
```

**Note:** Requires Gmail App Password setup (2FA enabled, generated app-specific password).

---

**Done!** MailHog is now part of your local stack.
