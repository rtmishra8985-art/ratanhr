# Troubleshooting Guide
**HRMS v2.1.0** | MySQL 8.4

---

## Startup Issues

### API won't start — "Database migration failed"

**Symptoms**: `docker compose logs api` shows migration error at boot.

**Cause**: `Database__AutoMigrate=true` is set in production (it should be `false`).

**Fix**:
```bash
# 1. Set in .env:
DATABASE__AUTOMIGRATE=false

# 2. Run migration container separately:
docker compose run --rm migrate

# 3. Then start API:
docker compose up -d api
```

### "Jwt:Key is missing or shorter than 32 characters"

**Fix**: Generate a strong key and set it:
```bash
echo "JWT_KEY=$(openssl rand -base64 48)" >> .env
docker compose up -d api
```

### "ENCRYPTION_KEY is not configured"

**Fix**:
```bash
echo "ENCRYPTION_KEY=$(openssl rand -base64 32)" >> .env
docker compose up -d api
```

---

## Database Issues

### Connection refused to MySQL

```bash
# Check mysql is healthy:
docker compose ps mysql
docker compose logs mysql

# Test connection:
docker compose exec mysql mysqladmin ping -u hrms -p"$MYSQL_PASSWORD"
```

### "Table doesn't exist" (table missing)

**Cause**: Migration hasn't run yet.

**Fix**:
```bash
docker compose run --rm migrate
```

### Slow queries

1. Check for missing indexes using MySQL performance schema:
```sql
-- Find tables with high full-scan ratios:
SELECT object_schema, object_name,
       count_read, count_fetch,
       count_full_scan
FROM performance_schema.table_io_waits_summary_by_table
WHERE object_schema = 'hrms_db'
ORDER BY count_full_scan DESC
LIMIT 10;
```

2. Add the `20260719000001_AddPerformanceIndexes` migration if not applied.

3. Check `EXPLAIN ANALYZE` on slow queries via MySQL client.

---

## API Issues

### 500 Internal Server Error

1. Check logs: `docker compose logs -f api`
2. Check correlation ID in response header — search logs by it
3. Check `/health` endpoint for DB/email status

### 401 Unauthorized

- Token has expired (30-minute TTL) — refresh via `/api/v1/auth/refresh`
- Token audience/issuer mismatch — verify `Jwt__Issuer` and `Jwt__Audience` in env
- Clock skew > 0 seconds — ensure server time is synced via NTP

### 429 Too Many Requests

- Rate limit exceeded — check `Retry-After` header
- If legitimate traffic: increase `PermitLimit` in `ServiceExtensions.cs`
- If attack: block the IP at nginx level

### Reports timing out

**Cause**: Large dataset + ClosedXML in-memory processing.

**Fix**: Switch to streaming export endpoints:
- `GET /api/v1/reports/attendance/export-stream` 
- `GET /api/v1/reports/payroll/export-stream`

These use `OpenXmlWriter` and handle 100k+ rows without excessive RAM.

---

## SSL / Nginx Issues

### 502 Bad Gateway

```bash
# Check API is running:
docker compose ps api
# Check API is healthy:
docker compose exec nginx wget -qO- http://api:8080/health
```

### Certificate expired

```bash
# Check cert expiry:
docker compose run --rm certbot certbot certificates

# Force renewal:
docker compose run --rm certbot certbot renew --force-renewal
docker compose exec nginx nginx -s reload
```

### HTTP not redirecting to HTTPS

1. Check nginx.conf has the HTTP → HTTPS redirect block
2. Check `UseForwardedHeaders()` is called before `UseHttpsRedirection()` in `Program.cs`
3. Verify nginx is setting `X-Forwarded-Proto: https`

---

## Redis Issues

### "Redis connection failed"

```bash
# Test Redis connection:
docker compose exec redis redis-cli -a $REDIS_PASSWORD ping
# Expected: PONG

# Check logs:
docker compose logs redis
```

If Redis is down, rate-limiting falls back to in-memory (warning logged). App remains functional.

---

## Diagnostic Commands

```bash
# All service statuses
docker compose ps

# Last 100 lines of all logs
docker compose logs --tail=100

# Real-time logs with timestamps
docker compose logs -f --timestamps api

# Container resource usage
docker stats --no-stream

# API health check
curl -s https://your-domain.com/health | python3 -m json.tool

# Prometheus metrics
curl -s http://localhost:8080/metrics | grep hrms_

# MySQL slow queries (statements exceeding 1s)
docker compose exec mysql mysql -u hrms -p"$MYSQL_PASSWORD" hrms_db -e "
SELECT digest_text AS query,
       count_star AS calls,
       ROUND(avg_timer_wait / 1e9, 2) AS avg_ms
FROM performance_schema.events_statements_summary_by_digest
WHERE schema_name = 'hrms_db'
ORDER BY avg_timer_wait DESC
LIMIT 10;"
```
