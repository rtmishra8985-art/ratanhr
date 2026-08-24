# Rate Limiting Guide
**HRMS v2.0.0**

---

## Policies

| Policy | Limit | Window | Burst | Applies To |
|--------|-------|--------|-------|------------|
| `login` | 10 req | 1 min | 0 | `POST /auth/login`, `POST /auth/forgot-password` |
| `sensitive` | 5 req | 1 min | 0 | `POST /auth/refresh`, `POST /auth/change-password`, `POST /auth/reset-password` |
| `api` | 120 req | 1 min | 20 | All other endpoints |

All limits are **per IP address** using a sliding window algorithm.

---

## Backend

**With Redis** (`Redis:ConnectionString` set — recommended for production):
- Counters stored in Redis, shared across all API instances
- Safe for horizontal scaling and load-balanced deployments
- Key format: `ratelimit:{policy}:{ip}`

**Without Redis** (single-instance fallback):
- In-memory sliding window (per-process)
- Not safe for multiple API instances — each instance has independent counters
- Warning logged at startup

---

## 429 Response

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 42
Content-Type: application/json

{"success":false,"message":"Too many requests. Please try again later."}
```

The `Retry-After` header tells clients how many seconds to wait before retrying.

---

## Nginx Backup Rate Limiting

`nginx.conf` adds a secondary rate limit layer before requests reach the API:

```nginx
limit_req_zone $binary_remote_addr zone=api:10m  rate=30r/m;
limit_req_zone $binary_remote_addr zone=auth:10m rate=5r/m;

location ~ ^/api/.*/auth/login {
    limit_req zone=auth burst=3 nodelay;
    ...
}
location / {
    limit_req zone=api burst=20 nodelay;
    ...
}
```

This provides a first line of defence even if the API rate limiter is bypassed or restarting.

---

## Tuning

To adjust limits, modify `ServiceExtensions.cs`:

```csharp
opt.AddSlidingWindowLimiter("api", o => {
    o.PermitLimit = 200;  // Increase for high-traffic endpoints
    o.Window      = TimeSpan.FromMinutes(1);
    o.QueueLimit  = 0;
});
```

Or set via environment variable override in a custom `IOptions<>` configuration.
