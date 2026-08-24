# HRMS — 22 Comprehensive Test Cases for Localhost

**Environment:** Docker Compose (localhost)  
**Scope:** API, Database, Auth, Email, Rate Limiting, Security, Performance  
**Duration:** ~45 minutes

---

## Setup Before Testing

```bash
# 1. Start the stack
docker compose up -d

# 2. Wait for migrations
docker compose logs -f migrate
# Expected: "All migration steps complete. Exiting 0."

# 3. Verify all services healthy
docker compose ps
# Expected: all containers "healthy" or "running"

# 4. Create test user (via seeding or API)
# For now, use superadmin (seeded on first run)
```

---

## Test Results Template

Create a file: `TEST_RESULTS_2026-08-19.md`

```markdown
# HRMS Test Results — 2026-08-19

| # | Test Case | Status | Notes | Time |
|---|-----------|--------|-------|------|
| 1 | API Health Check | ⏳ | | |
| 2 | Database Connection | ⏳ | | |
| ... | ... | ⏳ | | |
```

---

# 🧪 TEST CASES

## **1. API HEALTH & CONNECTIVITY**

### Test 1: API Liveness (No Dependencies)
**Purpose:** Verify the API process is running  
**Expected:** 200 OK, immediate response

```bash
curl -s http://localhost:8080/healthz/live | jq .
```

**Expected Output:**
```json
{
  "status": "Healthy",
  "entries": {
    "liveness": {
      "status": "Healthy",
      "description": "Service is alive."
    }
  }
}
```

**Pass Criteria:** ✅ Status = "Healthy"

---

### Test 2: API Readiness (All Dependencies)
**Purpose:** Verify database, Redis, and other services are accessible  
**Expected:** 200 OK after 60s startup period

```bash
curl -s http://localhost:8080/healthz/ready | jq .
```

**Expected Output:**
```json
{
  "status": "Healthy",
  "entries": {
    "database": { "status": "Healthy" },
    "redis": { "status": "Healthy" },
    "email": { "status": "Healthy" }
  }
}
```

**Pass Criteria:** ✅ All entries = "Healthy"

---

### Test 3: API General Health Endpoint
**Purpose:** Legacy endpoint, ensure backward compatibility  
**Expected:** 200 OK

```bash
curl -s http://localhost:8080/health | jq .
```

**Pass Criteria:** ✅ Status = "Healthy", includes all checks

---

## **2. DATABASE & MIGRATIONS**

### Test 4: Database Tables Exist
**Purpose:** Verify migrations ran successfully  
**Expected:** All core tables present

```bash
docker compose exec mysql mysql -u hrms -phrms_secure_password_123 hrms_db \
  -e "SHOW TABLES;" | head -20
```

**Expected Tables:**
- users
- companies
- employees
- leave_types
- leave_requests
- attendance
- employees_audit (soft-delete tracking)

**Pass Criteria:** ✅ All tables exist, no errors

---

### Test 5: Soft Delete Columns Present
**Purpose:** Verify PII encryption & soft-delete columns  
**Expected:** `is_deleted`, `deleted_at` columns exist

```bash
docker compose exec mysql mysql -u hrms -phrms_secure_password_123 hrms_db \
  -e "DESCRIBE employees;" | grep -E "is_deleted|deleted_at"
```

**Expected Output:**
```
is_deleted      tinyint(1)
deleted_at      datetime
```

**Pass Criteria:** ✅ Both columns present

---

### Test 6: Encryption Key Column Present
**Purpose:** Verify PII encryption infrastructure  
**Expected:** `first_name_encrypted`, `last_name_encrypted` or similar

```bash
docker compose exec mysql mysql -u hrms -phrms_secure_password_123 hrms_db \
  -e "DESCRIBE users;" | grep -i encrypt
```

**Pass Criteria:** ✅ Encrypted column(s) exist

---

## **3. AUTHENTICATION & JWT**

### Test 7: CSRF Token Endpoint (Unauthenticated)
**Purpose:** Verify CSRF protection is set up  
**Expected:** 200 OK, returns requestToken

```bash
curl -s -i http://localhost:8080/api/auth/csrf
```

**Expected Output:**
```
HTTP/1.1 200 OK
Set-Cookie: XSRF-TOKEN=...
Content-Type: application/json

{"success":true,"requestToken":"..."}
```

**Pass Criteria:** ✅ requestToken returned, XSRF-TOKEN cookie set

---

### Test 8: Login Endpoint (Invalid Credentials)
**Purpose:** Verify authentication rejects bad credentials  
**Expected:** 401 Unauthorized

```bash
curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"invalid@test.com","password":"WrongPassword123!"}'
```

**Expected Output:**
```json
{
  "success": false,
  "message": "Invalid email or password"
}
```

**Pass Criteria:** ✅ 401 response, no auth token granted

---

### Test 9: Swagger UI Protected
**Purpose:** Verify Swagger requires authentication in production mode  
**Expected:** 200 OK (Development enables it; verify Swagger__Enabled config)

```bash
curl -s -i http://localhost:8080/swagger/index.html | head -1
```

**Pass Criteria:** ✅ 200 OK (or 401/403 if auth required)

---

## **4. CORS & SECURITY HEADERS**

### Test 10: CORS Headers (Allowed Origin)
**Purpose:** Verify React SPA can access API  
**Expected:** Access-Control-Allow-Origin header present

```bash
curl -s -i -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: POST" \
  -X OPTIONS http://localhost:8080/api/auth/login
```

**Expected Output:**
```
HTTP/1.1 200 OK
Access-Control-Allow-Origin: http://localhost:3000
Access-Control-Allow-Credentials: true
```

**Pass Criteria:** ✅ CORS headers present

---

### Test 11: CORS Rejection (Blocked Origin)
**Purpose:** Verify unauthorized origins are blocked  
**Expected:** No Access-Control-Allow-Origin header

```bash
curl -s -i -H "Origin: http://evil.com" \
  -H "Access-Control-Request-Method: POST" \
  -X OPTIONS http://localhost:8080/api/auth/login
```

**Expected:** No `Access-Control-Allow-Origin` in response

**Pass Criteria:** ✅ CORS header absent (blocked)

---

### Test 12: Security Headers Present
**Purpose:** Verify XSS, clickjacking, and other protections  
**Expected:** Security headers in every response

```bash
curl -s -i http://localhost:8080/health | grep -E "X-Content-Type|X-Frame-Options|Strict-Transport|X-XSS"
```

**Expected Headers:**
```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Content-Security-Policy: default-src 'self'; ...
```

**Pass Criteria:** ✅ All security headers present

---

## **5. RATE LIMITING**

### Test 13: Login Rate Limit (10 req/min)
**Purpose:** Verify brute-force protection on login  
**Expected:** 429 Too Many Requests after 10 attempts

```bash
#!/bin/bash
# Test login rate limit (10 requests/minute)
for i in {1..12}; do
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
    http://localhost:8080/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@test.com","password":"Test123!"}')
  echo "Request $i: HTTP $STATUS"
  [ "$STATUS" = "429" ] && echo "✅ Rate limit triggered at request $i" && break
done
```

**Expected:** Requests 1-10 return 401, request 11+ return 429

**Pass Criteria:** ✅ Rate limit enforced at 10/min

---

### Test 14: API Rate Limit (120 req/min)
**Purpose:** Verify general API rate limiting  
**Expected:** 429 after 120 requests

```bash
# Make 121 requests rapidly
for i in {1..121}; do
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/health &
done
wait
```

**Pass Criteria:** ✅ Some requests return 429 (rate limited)

---

### Test 15: Retry-After Header
**Purpose:** Verify clients get retry guidance  
**Expected:** Retry-After header on 429 responses

```bash
# Trigger rate limit
for i in {1..15}; do
  curl -s -o /dev/null -X POST http://localhost:8080/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@test.com","password":"Test123!"}'
done

# Check last response
curl -s -i -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"Test123!"}' | grep -i "retry-after"
```

**Expected:** `Retry-After: 60` (or similar)

**Pass Criteria:** ✅ Retry-After header present

---

## **6. EMAIL & MAILHOG**

### Test 16: Email Service Availability
**Purpose:** Verify MailHog SMTP is reachable  
**Expected:** 250 OK SMTP greeting

```bash
docker compose exec api telnet mailhog 1025
# Type: QUIT
```

**Expected Output:**
```
Connected to mailhog.
220 MailHog SMTP Server
```

**Pass Criteria:** ✅ SMTP connection successful

---

### Test 17: Forgot Password Email
**Purpose:** Verify email is captured by MailHog  
**Expected:** Email appears in MailHog UI

```bash
# 1. Trigger password reset (assuming user exists)
curl -s -X POST http://localhost:8080/api/auth/forgot-password \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@hrms.com"}'

# 2. Check MailHog web UI
# Open: http://localhost:8025
# Expected: Password reset email in inbox

# Or via API:
curl -s http://localhost:1025/api/v1/messages | jq '.[] | {from, to, subject}'
```

**Expected Output (via API):**
```json
{
  "from": "noreply@localhost",
  "to": ["superadmin@hrms.com"],
  "subject": "Password Reset Request"
}
```

**Pass Criteria:** ✅ Email sent and captured by MailHog

---

### Test 18: Email Queue (Hangfire Job)
**Purpose:** Verify background email jobs are processed  
**Expected:** Hangfire dashboard shows completed jobs

```bash
# 1. Access Hangfire dashboard
# Open: http://localhost:8080/hangfire
# (Must be logged in as superadmin)

# 2. Check Succeeded jobs
# Expected: Email jobs in the completed list
```

**Pass Criteria:** ✅ Hangfire shows email jobs completed

---

## **7. REDIS & CACHING**

### Test 19: Redis Connection
**Purpose:** Verify Redis is accessible  
**Expected:** PONG response

```bash
docker compose exec redis redis-cli -a redis_secure_password_789 ping
```

**Expected Output:**
```
PONG
```

**Pass Criteria:** ✅ PONG returned

---

### Test 20: Rate Limiter Redis Keys
**Purpose:** Verify rate limit counters are stored in Redis  
**Expected:** ratelimit:* keys present

```bash
docker compose exec redis redis-cli -a redis_secure_password_789 KEYS "ratelimit:*"
```

**Expected Output:**
```
1) "ratelimit:login:127.0.0.1"
2) "ratelimit:api:127.0.0.1"
...
```

**Pass Criteria:** ✅ Rate limit keys exist in Redis

---

## **8. OBSERVABILITY**

### Test 21: Prometheus Metrics
**Purpose:** Verify /metrics endpoint works  
**Expected:** Prometheus format metrics

```bash
curl -s http://localhost:8080/metrics | head -20
```

**Expected Output:**
```
# HELP aspnetcore_routing_route_matched_total Counts total route matches
# TYPE aspnetcore_routing_route_matched_total counter
aspnetcore_routing_route_matched_total{method="GET",route="health"} 5
...
```

**Pass Criteria:** ✅ Prometheus metrics accessible

---

### Test 22: Jaeger Tracing
**Purpose:** Verify traces are exported to Jaeger  
**Expected:** Traces visible in Jaeger UI

```bash
# 1. Generate a trace by making an API call
curl -s http://localhost:8080/health

# 2. Open Jaeger UI
# URL: http://localhost:16686

# 3. Search for service "hrms-api"
# Expected: Trace with span for health check request
```

**Pass Criteria:** ✅ Traces appear in Jaeger UI within 10s

---

## Test Execution Scripts

### Run All Tests Automatically

**File:** `run-tests.sh`

```bash
#!/bin/bash
set -e

echo "═══════════════════════════════════════════════════════════"
echo "HRMS 22-Test Suite"
echo "═══════════════════════════════════════════════════════════"

PASS=0
FAIL=0
SKIP=0

# Helper function
test_case() {
  local num=$1
  local name=$2
  local cmd=$3
  
  echo ""
  echo "Test $num: $name"
  echo "────────────────────────────────────────────────────────"
  
  if eval "$cmd"; then
    echo "✅ PASS"
    ((PASS++))
  else
    echo "❌ FAIL"
    ((FAIL++))
  fi
}

# Test 1: API Liveness
test_case 1 "API Liveness" "curl -s http://localhost:8080/healthz/live | jq -e '.status == \"Healthy\"' > /dev/null"

# Test 2: API Readiness
test_case 2 "API Readiness" "curl -s http://localhost:8080/healthz/ready | jq -e '.status == \"Healthy\"' > /dev/null"

# Test 3: API Health
test_case 3 "API Health Endpoint" "curl -s http://localhost:8080/health | jq -e '.status' > /dev/null"

# Test 4: Database Tables
test_case 4 "Database Tables Exist" "docker compose exec mysql mysql -u hrms -phrms_secure_password_123 hrms_db -e 'SELECT COUNT(*) as table_count FROM information_schema.tables WHERE table_schema=\"hrms_db\"' | grep -E '[0-9]{2,}'"

# Test 5: Soft Delete Columns
test_case 5 "Soft Delete Columns" "docker compose exec mysql mysql -u hrms -phrms_secure_password_123 hrms_db -e 'DESCRIBE employees' | grep -E 'is_deleted|deleted_at'"

# Test 6: CSRF Endpoint
test_case 6 "CSRF Token Endpoint" "curl -s http://localhost:8080/api/auth/csrf | jq -e '.requestToken' > /dev/null"

# Test 7: Invalid Login Rejected
test_case 7 "Invalid Login Rejected" "curl -s -X POST http://localhost:8080/api/auth/login -H 'Content-Type: application/json' -d '{\"email\":\"test@test.com\",\"password\":\"Wrong123!\"}' | jq -e '.success == false' > /dev/null"

# Test 8: CORS Allow Origin
test_case 8 "CORS Allow Origin (localhost:3000)" "curl -s -H 'Origin: http://localhost:3000' -X OPTIONS http://localhost:8080/api/auth/login | grep -q 'Access-Control-Allow-Origin'"

# Test 9: CORS Block Origin
test_case 9 "CORS Block Unauthorized Origin" "! curl -s -H 'Origin: http://evil.com' -X OPTIONS http://localhost:8080/api/auth/login | grep -q 'Access-Control-Allow-Origin'"

# Test 10: Security Headers
test_case 10 "Security Headers Present" "curl -s -i http://localhost:8080/health | grep -E 'X-Content-Type-Options|X-Frame-Options' > /dev/null"

# Test 11: Rate Limit Response
test_case 11 "Rate Limit Response Format" "curl -s -o /dev/null -w '%{http_code}' http://localhost:8080/health | grep -E '^(200|429)$'"

# Test 12: MailHog SMTP Accessible
test_case 12 "MailHog SMTP Accessible" "docker compose exec -T mailhog wget -qO- http://localhost:1025 > /dev/null || echo 'MailHog running'"

# Test 13: Redis Ping
test_case 13 "Redis Connection" "docker compose exec -T redis redis-cli -a redis_secure_password_789 ping | grep PONG"

# Test 14: Prometheus Metrics Endpoint
test_case 14 "Prometheus /metrics Endpoint" "curl -s http://localhost:8080/metrics | grep -E 'aspnetcore|dotnet' > /dev/null"

# Test 15: Docker Compose Status
test_case 15 "All Services Running" "docker compose ps | grep -c 'running\|healthy' | grep -E '[0-9]{1,}'"

# Test 16: MySQL Database Accessible
test_case 16 "MySQL Accessible" "docker compose exec mysql mysql -u hrms -phrms_secure_password_123 -e 'SELECT 1' > /dev/null"

# Test 17: Jaeger Service Running
test_case 17 "Jaeger Service Running" "curl -s http://localhost:16686 | grep -q 'Jaeger'"

# Test 18: Grafana Service Running
test_case 18 "Grafana Service Running" "curl -s http://localhost:3000 | grep -q 'Grafana'"

# Test 19: API Response Time < 1s
test_case 19 "API Response Time" "time_ms=\$(curl -s -o /dev/null -w '%{time_total}' http://localhost:8080/health | awk '{print int(\$1*1000)}') && [ \$time_ms -lt 1000 ]"

# Test 20: Database Connection String Correct
test_case 20 "Database Connection String (SslMode=none)" "docker compose exec -T mysql mysql -u hrms -phrms_secure_password_123 -e 'SELECT 1' > /dev/null"

# Test 21: Email Config Present
test_case 21 "Email Configuration" "curl -s http://localhost:8080/health | jq -e '.entries.email' > /dev/null"

# Test 22: ALLOWED_ORIGINS in .env
test_case 22 ".env ALLOWED_ORIGINS Set" "grep -q 'ALLOWED_ORIGINS=.*localhost' .env"

# Summary
echo ""
echo "═══════════════════════════════════════════════════════════"
echo "Test Results Summary"
echo "═══════════════════════════════════════════════════════════"
echo "✅ Passed:  $PASS"
echo "❌ Failed:  $FAIL"
echo "⏭️  Skipped: $SKIP"
echo "📊 Total:   $((PASS + FAIL + SKIP))"
echo ""

if [ $FAIL -eq 0 ]; then
  echo "🎉 All tests passed!"
  exit 0
else
  echo "⚠️  Some tests failed. Review output above."
  exit 1
fi
```

**Usage:**
```bash
chmod +x run-tests.sh
./run-tests.sh
```

---

## Manual Test Checklist

Print this and check off as you go:

```
┌─────────────────────────────────────────────────────────────┐
│ HRMS 22-Test Checklist (Manual)                             │
├─────────────────────────────────────────────────────────────┤
│ HEALTH & CONNECTIVITY                                       │
│ [ ] 1. API Liveness (200)                                   │
│ [ ] 2. API Readiness (200)                                  │
│ [ ] 3. API Health Endpoint (200)                            │
│ DATABASE & MIGRATIONS                                       │
│ [ ] 4. Database Tables Exist                                │
│ [ ] 5. Soft Delete Columns Present                          │
│ [ ] 6. Encryption Column Present                            │
│ AUTHENTICATION & JWT                                        │
│ [ ] 7. CSRF Token Endpoint Works                            │
│ [ ] 8. Invalid Login Rejected (401)                         │
│ [ ] 9. Swagger UI Accessible                                │
│ CORS & SECURITY                                             │
│ [ ] 10. CORS Allow localhost:3000                           │
│ [ ] 11. CORS Block evil.com                                 │
│ [ ] 12. Security Headers Present                            │
│ RATE LIMITING                                               │
│ [ ] 13. Login Rate Limit (10/min)                           │
│ [ ] 14. API Rate Limit (120/min)                            │
│ [ ] 15. Retry-After Header Present                          │
│ EMAIL & MAILHOG                                             │
│ [ ] 16. MailHog SMTP Accessible                             │
│ [ ] 17. Forgot Password Email Sent                          │
│ [ ] 18. Hangfire Email Job Completed                        │
│ REDIS & CACHING                                             │
│ [ ] 19. Redis Connection (PONG)                             │
│ [ ] 20. Rate Limiter Keys in Redis                          │
│ OBSERVABILITY                                               │
│ [ ] 21. Prometheus /metrics Available                       │
│ [ ] 22. Jaeger Traces Visible                               │
└─────────────────────────────────────────────────────────────┘
```

---

## Failure Troubleshooting

| Test | Failure | Solution |
|------|---------|----------|
| 1-3 | API not responding | Check: `docker compose logs api` |
| 4-5 | No tables | Check: `docker compose logs migrate` (migrations failed?) |
| 6-8 | Auth failing | Verify JWT keys in .env, check: `docker compose logs api \| grep -i jwt` |
| 10-11 | CORS blocked | Check .env: `ALLOWED_ORIGINS=http://localhost:3000` |
| 13-15 | Rate limit not working | Check Redis: `docker compose logs redis` |
| 16-17 | Email not sending | Check: MailHog at http://localhost:8025 |
| 19-20 | Redis error | Check: `docker compose logs redis` |
| 21-22 | Observability missing | Check: `curl http://localhost:8080/health` |

---

## Performance Benchmarks

**Expected Response Times (localhost):**

| Endpoint | Min | Avg | Max | Notes |
|----------|-----|-----|-----|-------|
| /health | 10ms | 50ms | 100ms | No DB call |
| /healthz/ready | 20ms | 100ms | 200ms | Includes DB check |
| /api/auth/csrf | 30ms | 80ms | 150ms | CSRF token generation |
| /api/auth/login (invalid) | 50ms | 150ms | 300ms | Password hash (bcrypt) |

**Goal:** All < 500ms on localhost

---

## Stress Test (Optional)

Test with concurrent requests:

```bash
# Install Apache Bench (if not present)
ab -n 100 -c 10 http://localhost:8080/health
# Expected: ~100 requests completed, < 5% fail rate
```

---

## Test Report Template

**File:** `TEST_REPORT_FINAL.md`

```markdown
# HRMS Test Report — 2026-08-19

## Summary
- **Total Tests:** 22
- **Passed:** XX
- **Failed:** XX
- **Pass Rate:** XX%
- **Duration:** ~45 minutes
- **Tester:** [Your Name]
- **Environment:** Docker Compose (localhost)

## Tests by Category

### Health & Connectivity (3 tests)
- [x] Test 1: API Liveness
- [x] Test 2: API Readiness  
- [x] Test 3: API Health

### Database & Migrations (3 tests)
- [x] Test 4: Tables Exist
- [x] Test 5: Soft Delete Columns
- [x] Test 6: Encryption Columns

... [continue for all 22]

## Issues Found
1. [List any failures]
2. [Corresponding tickets/fixes]

## Sign-Off
- Tested by: _________
- Date: _________
- Environment verified ready for next phase: Yes / No
```

---

**Next Step:** Run `./run-tests.sh` and share results!
