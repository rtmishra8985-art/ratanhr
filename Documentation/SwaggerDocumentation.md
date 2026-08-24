# Swagger / OpenAPI Documentation Guide
**HRMS v2.0.0**

---

## Accessing Swagger UI

Development: http://localhost:5000/swagger  
Staging: Swagger is available when
`AppSettings__EnableSwagger=true` is supplied by the isolated staging
configuration. If `Swagger:Username` and `Swagger:Password` are configured,
the UI and JSON document require HTTP Basic authentication.  
Production: Swagger UI is **disabled** in production by default.

To enable in production (not recommended):
```csharp
// Remove the `if (app.Environment.IsDevelopment())` guard in Program.cs
app.UseSwagger();
app.UseSwaggerUI(...);
```

---

## Authentication in Swagger UI

1. Click **Authorize** (top right)
2. Enter: `Bearer <your-jwt-token>`
3. Click **Authorize**, then **Close**

All subsequent requests will include the `Authorization` header.

---

## Key API Groups

| Tag | Base Path | Description |
|-----|-----------|-------------|
| Auth | `/api/auth` | Login, refresh, password management |
| Employees | `/api/employees` | Employee CRUD |
| Payroll | `/api/payroll` | Payslips, bulk generation |
| Attendance | `/api/attendance` | Check-in/out, Excel upload |
| Leave | `/api/leave` | Leave requests, approvals |
| Reports | `/api/reports` | Report generation + export |
| Companies | `/api/companies` | Company management (SuperAdmin) |
| Dashboard | `/api/dashboard` | Summary statistics |

---

## Response Format

Most endpoints return `ApiResponse<T>`:

```json
{
  "success": true,
  "message": "Operation completed successfully",
  "data": { ... },
  "errors": null
}
```

Some legacy-compatible endpoints intentionally return a raw DTO or paged
result (including asset-management endpoints). Consumers should use the
endpoint-specific schema in the generated Swagger document rather than
assuming every response is wrapped in `ApiResponse<T>`.

Error responses:
```json
{
  "success": false,
  "message": "Validation failed",
  "data": null,
  "errors": ["Email is required", "Password must be at least 8 characters"]
}
```

---

## Standard HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | Success |
| 201 | Created |
| 400 | Validation error / bad request |
| 401 | Not authenticated (missing/expired token) |
| 403 | Authenticated but not authorised |
| 404 | Resource not found |
| 409 | Conflict (e.g. duplicate employee ID) |
| 422 | Unprocessable entity (business rule violation) |
| 429 | Rate limit exceeded |
| 500 | Unexpected server error |

---

## Exporting the OpenAPI Spec

```bash
# Development only — saves openapi.json:
curl http://localhost:5000/swagger/v1/swagger.json > openapi.json

# Generate a client SDK (example — TypeScript):
npx @openapitools/openapi-generator-cli generate \
  -i openapi.json \
  -g typescript-fetch \
  -o ./client-sdk
```
