# API Versioning Strategy
**HRMS v2.0.0**

---

## Current Strategy: URL Path Versioning

All endpoints are prefixed with `/api/v{n}/`:

```
GET  /api/v1/employees
POST /api/v1/auth/login
GET  /api/v1/payroll/payslips
```

**Why URL path versioning**: 
- Explicit and visible in browser address bar, logs, and API gateways
- Easy to route with nginx/load balancers
- Unambiguous for caching (unlike header versioning)

---

## Versioning Rules

### When to increment the version

| Change Type | Version Impact |
|-------------|---------------|
| Add new optional field to response | **None** (non-breaking) |
| Add new optional query parameter | **None** (non-breaking) |
| Add new endpoint | **None** (non-breaking) |
| Remove or rename a field | **New major version** (breaking) |
| Change field type | **New major version** (breaking) |
| Change authentication scheme | **New major version** (breaking) |
| Remove an endpoint | **New major version** (breaking) |

### Backward Compatibility Policy

- A version is supported for **12 months** after its successor is released
- Deprecated endpoints return `Deprecation` and `Sunset` response headers
- Breaking changes are announced in `CHANGELOG.md` with migration path

---

## Adding v2 Endpoints

When v2 is required for a module:

```csharp
[ApiController]
[Route("api/v2/[controller]")]
public class EmployeesV2Controller : ControllerBase
{
    // v2 implementation
}
```

v1 endpoints remain unchanged and continue to function.

---

## Swagger Versioning

Each version gets its own Swagger document:

```csharp
services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "HRMS API", Version = "v1" });
    c.SwaggerDoc("v2", new OpenApiInfo { Title = "HRMS API", Version = "v2" });
});

app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "HRMS API v1");
    c.SwaggerEndpoint("/swagger/v2/swagger.json", "HRMS API v2");
});
```

---

## Client Compatibility

- Frontend applications should pin to a specific version (`/api/v1/`)
- Mobile apps that cannot be force-updated should be given extended v1 support
- Integration partners receive 6-month advance notice of version deprecation
