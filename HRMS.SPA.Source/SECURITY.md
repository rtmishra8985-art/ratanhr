# Security Guide — RatanHR Frontend

## Current State

The frontend stores the JWT access token in **`localStorage`** as a temporary measure.
This is safe enough for internal tools behind a VPN/firewall, but localStorage is
accessible to any JavaScript running on the page, making it theoretically vulnerable
to XSS attacks.

The frontend is fully prepared for migration to **HTTP-only cookies**. Only the
backend needs to change. The frontend switch requires **two line changes**.

---

## Migration to HTTP-only Cookies (Recommended)

### What the ASP.NET Backend must do

**1. On successful login — set an HTTP-only cookie instead of returning the token in the body:**

```csharp
// In your LoginController / AuthController:
Response.Cookies.Append("hrms_token", jwtToken, new CookieOptions
{
    HttpOnly = true,          // JS cannot read this cookie
    Secure   = true,          // HTTPS only
    SameSite = SameSiteMode.Strict,
    Expires  = DateTimeOffset.UtcNow.AddHours(8),
    Path     = "/"
});

// You can still return the token expiry time in the JSON body for the UI
return Ok(new { expiresIn = 28800 });
```

**2. On logout — clear the cookie:**

```csharp
Response.Cookies.Delete("hrms_token");
return Ok();
```

**3. Add CORS — allow credentials from your frontend origin:**

```csharp
// In Program.cs / Startup.cs:
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowHrmsApp", policy =>
        policy.WithOrigins("https://your-hrms-domain.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());   // <-- required for cookie auth
});
```

**4. The JWT validation middleware stays exactly the same.** ASP.NET's JWT bearer
middleware automatically reads from both the `Authorization` header AND cookies when
configured with `TokenValidationParameters`. Add cookie reading:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                // Read from cookie if Authorization header is absent
                if (string.IsNullOrEmpty(ctx.Token))
                    ctx.Token = ctx.Request.Cookies["hrms_token"];
                return Task.CompletedTask;
            }
        };
        // ... rest of your JWT options
    });
```

---

### What the Frontend does once the backend is ready

**Step 1 — switch AuthContext to cookie mode** (`src/contexts/AuthContext.tsx`):

```tsx
// BEFORE (localStorage mode)
const setToken = (token: string | null) => {
  setTokenState(token);
  if (token) tokenStorage.set(token); else tokenStorage.remove();
};

// AFTER (cookie mode — backend sets the cookie, frontend just signals "logged in")
const setToken = (token: string | null) => {
  setTokenState(token ? '__cookie__' : null);
  // tokenStorage not needed; browser handles the cookie automatically
};
```

**Step 2 — tell the API client to always send cookies** (`src/utils/apiConfig.ts`):

```ts
// The customFetch wrapper in @workspace/api-client-react must include:
//   credentials: 'include'
// This makes the browser automatically attach the HTTP-only cookie to every request.
```

That's it. The rest of the codebase — hooks, pages, error handling — changes nothing.

---

## Additional Hardening Already Applied

| Measure | Location | Status |
|---|---|---|
| Content Security Policy | `index.html` meta tag | ✅ Done |
| X-Frame-Options DENY | `index.html` meta tag | ✅ Done |
| X-Content-Type-Options nosniff | `index.html` meta tag | ✅ Done |
| Strict-Origin Referrer-Policy | `index.html` meta tag | ✅ Done |
| Token wrapped in `tokenStorage` utility | `src/utils/tokenStorage.ts` | ✅ Done |
| Auto-logout on 401 API response | `src/components/layout/AuthGuard.tsx` | ✅ Done |
| Demo credentials hidden in production | `src/pages/LoginPage.tsx` | ✅ Done |
| No secrets or tokens in source code | All files | ✅ Done |
| HTTP-only cookie (full fix) | ASP.NET backend | ⏳ Pending backend change |

---

## Recommended Additional Measures (Backend)

- **Rate-limit** the `/api/auth/login` endpoint (e.g., 10 attempts / minute per IP).
- **Rotate refresh tokens** and issue short-lived access tokens (15–30 min).
- **Add HSTS** header: `Strict-Transport-Security: max-age=31536000; includeSubDomains`.
- **Audit log** all authentication events (login, logout, failed attempts).
