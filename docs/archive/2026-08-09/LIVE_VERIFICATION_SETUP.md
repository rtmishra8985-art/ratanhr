# Live HTTP Verification Setup

**Purpose:** Satisfy auditor brief item 4 — CRUD success/failure pairs per module,
cross-tenant read rejection, and CORS-from-disallowed-origin rejection.  
**Audience:** Whoever runs the next independent verification pass.  
**Prerequisite:** A working MySQL 8.4 instance and the ability to run `dotnet run`
or `docker compose up`.

---

## Step 1 — Start the API

### Option A — Docker Compose (recommended; matches production config)

```bash
cp .env.example .env          # fill in DB creds, JWT_KEY, ENCRYPTION_KEY
docker compose up -d mysql     # start DB first
docker compose run --rm migrate # apply migrations (including RemoveHardcodedSuperadminSeed)
docker compose up -d api
```

The API listens on **http://localhost:5000** by default.  
On first boot, `SeedAsync` prints the generated superadmin password to stdout:

```
dotnet run password for superadmin@hrms.com: <PRINTED HERE>
```

Capture that password before proceeding.

### Option B — dotnet CLI (local dev)

```bash
cd HRMS.API
export DATABASE_URL="Server=localhost;Port=3306;Database=hrms_db;User ID=hrms;Password=yourpw;AllowPublicKeyRetrieval=True;SslMode=Preferred"
export JWT_KEY="$(openssl rand -base64 48)"
export ENCRYPTION_KEY="$(openssl rand -base64 32)"
export ASPNETCORE_ENVIRONMENT=Development   # allows AllowAnyOrigin for CORS tests
dotnet ef database update --project ../HRMS.Infrastructure  # apply all migrations
dotnet run
```

Capture the superadmin password from stdout.

---

## Step 2 — Obtain Auth Tokens

### Superadmin token

```bash
SA_TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@hrms.com","password":"<GENERATED_PASSWORD>","portal":"superadmin"}' \
  | jq -r '.accessToken')
echo "SA_TOKEN=$SA_TOKEN"
```

### Create Tenant A (company + admin user)

```bash
# Create company A
COMPANY_A=$(curl -s -X POST http://localhost:5000/api/companies \
  -H "Authorization: Bearer $SA_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"companyName":"Tenant A","emailAddress":"a@example.com"}' | jq -r '.data.id')

# Create admin user for Tenant A (adjust endpoint to your AdminUser controller)
curl -s -X POST http://localhost:5000/api/admin-users \
  -H "Authorization: Bearer $SA_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin_a@example.com\",\"password\":\"Secure@1234\",\"role\":\"admin\",\"companyId\":$COMPANY_A}"

ADMIN_A_TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin_a@example.com\",\"password\":\"Secure@1234\",\"portal\":\"admin\"}" \
  | jq -r '.accessToken')
echo "ADMIN_A_TOKEN=$ADMIN_A_TOKEN"
```

### Create Tenant B (for cross-tenant test)

```bash
COMPANY_B=$(curl -s -X POST http://localhost:5000/api/companies \
  -H "Authorization: Bearer $SA_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"companyName":"Tenant B","emailAddress":"b@example.com"}' | jq -r '.data.id')

curl -s -X POST http://localhost:5000/api/admin-users \
  -H "Authorization: Bearer $SA_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin_b@example.com\",\"password\":\"Secure@5678\",\"role\":\"admin\",\"companyId\":$COMPANY_B}"

ADMIN_B_TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin_b@example.com\",\"password\":\"Secure@5678\",\"portal\":\"admin\"}" \
  | jq -r '.accessToken')
echo "ADMIN_B_TOKEN=$ADMIN_B_TOKEN"
```

---

## Step 3 — CRUD Verification (per module)

Run the following for each module. Replace `<MODULE>` and body fields as appropriate.
The pattern is identical across all controllers.

### Employee Module

```bash
# CREATE (success — expect 200/201 with employee object)
EMP_ID=$(curl -s -X POST http://localhost:5000/api/employees \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName":"Alice","lastName":"Smith","employeeId":"EMP001",
    "email":"alice@example.com","mobileNumber":"9999999999",
    "joiningDate":"2024-01-01","departmentId":1,"designationId":1
  }' | tee /tmp/emp_create.json)
echo "CREATE status: $(cat /tmp/emp_create.json | jq -r '.success')"
EMP_ID=$(cat /tmp/emp_create.json | jq -r '.data.id')

# READ (success — expect employee object)
curl -s http://localhost:5000/api/employees/$EMP_ID \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" | jq '{success,id:.data.id,name:.data.fullName}'

# UPDATE (success — expect 200)
curl -s -X PUT http://localhost:5000/api/employees/$EMP_ID \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Alice","lastName":"Jones"}' | jq '{success}'

# DELETE (success — expect 200)
curl -s -X DELETE http://localhost:5000/api/employees/$EMP_ID \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" | jq '{success}'

# FAILURE — unauthenticated (expect 401)
curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/api/employees

# FAILURE — invalid payload (expect 400)
curl -s -X POST http://localhost:5000/api/employees \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"firstName":""}' | jq '{success,message:.message}'
```

### Leave Module

```bash
# CREATE leave request
LEAVE_ID=$(curl -s -X POST http://localhost:5000/api/leaves \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"employeeId\":$EMP_ID,\"leaveTypeId\":1,\"startDate\":\"2025-08-01\",\"endDate\":\"2025-08-02\",\"reason\":\"Personal\"}" \
  | jq -r '.data.id')
echo "LEAVE CREATE id=$LEAVE_ID"

# READ
curl -s "http://localhost:5000/api/leaves?page=1&pageSize=10" \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" | jq '{success,count:.data.totalCount}'

# UPDATE (approve)
curl -s -X PUT http://localhost:5000/api/leaves/$LEAVE_ID/approve \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" | jq '{success}'

# DELETE
curl -s -X DELETE http://localhost:5000/api/leaves/$LEAVE_ID \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" | jq '{success}'
```

### Payroll Module

```bash
# CREATE payroll record
curl -s -X POST http://localhost:5000/api/payroll \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"employeeId\":$EMP_ID,\"month\":7,\"year\":2025,\"basicPay\":30000,\"autoCalculate\":true}" \
  | jq '{success,pf:.data.pfEmployee,esi:.data.esiEmployee,pt:.data.professionalTax}'

# LIST
curl -s "http://localhost:5000/api/payroll?month=7&year=2025" \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" | jq '{success,count:.data.totalCount}'

# FAILURE — wrong month format (expect 400)
curl -s -X POST http://localhost:5000/api/payroll \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"employeeId":1,"month":13,"year":2025,"basicPay":30000}' \
  | jq '{success,message}'
```

---

## Step 4 — Cross-Tenant Read Rejection

This is the critical multi-tenancy check. Tenant B's admin should be **unable** to
read Tenant A's employees, even by guessing the employee ID.

```bash
# Re-create a Tenant A employee if needed
EMP_A_ID=$(curl -s -X POST http://localhost:5000/api/employees \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"firstName":"CrossTenantTarget","lastName":"Test","employeeId":"EMP999",
       "email":"target@a.com","mobileNumber":"8888888888",
       "joiningDate":"2024-01-01","departmentId":1,"designationId":1}' \
  | jq -r '.data.id')
echo "Tenant A employee id: $EMP_A_ID"

# Attempt to read Tenant A's employee using Tenant B's token
# Expected result: 404 (not found — query filter hides the row) or 403 (forbidden)
# NOT acceptable: 200 with Tenant A's employee data
HTTP_CODE=$(curl -s -o /tmp/cross_tenant.json -w "%{http_code}" \
  http://localhost:5000/api/employees/$EMP_A_ID \
  -H "Authorization: Bearer $ADMIN_B_TOKEN")
echo "Cross-tenant read HTTP status: $HTTP_CODE   (expect 404)"
cat /tmp/cross_tenant.json | jq '{success,message}'

# Verify the employee IS accessible to Tenant A (confirms it's a filter, not a delete)
curl -s http://localhost:5000/api/employees/$EMP_A_ID \
  -H "Authorization: Bearer $ADMIN_A_TOKEN" | jq '{success,id:.data.id}'
```

**Pass criteria:** Tenant B gets `404` (or `403`). Tenant A still gets `200`.  
**Fail criteria:** Tenant B gets `200` with Tenant A's employee data — tenant isolation is broken.

---

## Step 5 — CORS Rejection from Disallowed Origin

Run this test against a **Production-mode** instance with `Cors__AllowedOrigins`
set to a specific origin (e.g. `https://app.example.com`).

```bash
# In a separate terminal, restart the API in Production mode with a specific origin:
export ASPNETCORE_ENVIRONMENT=Production
export Cors__AllowedOrigins="https://app.example.com"
# ... other required env vars ...
dotnet run

# Test 1 — allowed origin (expect CORS headers present)
curl -s -I -X OPTIONS http://localhost:5000/api/employees \
  -H "Origin: https://app.example.com" \
  -H "Access-Control-Request-Method: GET" \
  | grep -i "access-control-allow-origin"
# Expected output: access-control-allow-origin: https://app.example.com

# Test 2 — disallowed origin (expect NO CORS headers)
curl -s -I -X OPTIONS http://localhost:5000/api/employees \
  -H "Origin: https://evil.attacker.com" \
  -H "Access-Control-Request-Method: GET" \
  | grep -i "access-control-allow-origin"
# Expected output: (empty — no header returned)

# Test 3 — no Cors__AllowedOrigins set in Production (expect startup failure)
unset Cors__AllowedOrigins
dotnet run 2>&1 | grep "Cors:AllowedOrigins"
# Expected output: line containing "Cors:AllowedOrigins (Cors__AllowedOrigins) is missing in production"
# (application should refuse to start)
```

---

## Step 6 — /health Endpoint

```bash
# Expect HTTP 200, JSON body with status="Healthy"
curl -s http://localhost:5000/health | jq .

# With Redis down (stop redis container, then):
docker compose stop redis
curl -s http://localhost:5000/health | jq .
# Expect status="Degraded" or "Unhealthy" — NOT "Healthy"
docker compose start redis
```

---

## Pass / Fail Criteria Summary

| Check | Pass | Fail |
|-------|------|------|
| Employee CRUD | All 4 ops return success=true | Any returns unexpected error |
| Unauthenticated request | HTTP 401 | HTTP 200 |
| Invalid payload | HTTP 400 with validation message | HTTP 500 or silent success |
| Cross-tenant read | HTTP 404/403 for foreign employee | HTTP 200 with foreign data |
| CORS allowed origin | `access-control-allow-origin` header present | No header |
| CORS disallowed origin | No `access-control-allow-origin` header | Header present |
| No AllowedOrigins in Production | App refuses to start | App starts |
| /health with Redis up | `status: Healthy` | Any other status |
| /health with Redis down | `status: Degraded` or `Unhealthy` | `status: Healthy` |

---

## Environment Not Available?

If no local .NET runtime or Docker is available, the minimum requirement is:

1. **Docker** (any recent version) — `docker compose up` handles everything else.
2. A copy of `.env` with real values for `DATABASE_URL`, `JWT_KEY` (≥ 32 chars),
   `ENCRYPTION_KEY` (base64 of 32 bytes), and `SMTP` settings (or `Email__Host`
   left blank to skip email delivery — reset tokens will appear in Dev logs only).

The `docker-compose.yml` in this repo already wires up MySQL 8.4, Redis, and the
API container. No separate .NET SDK installation is required for Docker-based verification.
