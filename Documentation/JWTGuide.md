# JWT Authentication Guide
**HRMS v2.0.0**

---

## Token Structure

```
Header.Payload.Signature
```

### Claims

| Claim | Key | Example |
|-------|-----|---------|
| User ID | `sub` | `"user-123"` |
| Email | `email` | `"admin@company.com"` |
| Role | `role` | `"admin"` |
| Company ID | `CompanyId` | `"5"` |
| Full Name | `FullName` | `"John Smith"` |
| Expiry | `exp` | Unix timestamp |

---

## Token Lifecycle

```
1. POST /api/v1/auth/login
   → { accessToken, refreshToken, expiresIn }

2. Use accessToken in Authorization header:
   Authorization: Bearer <accessToken>

3. When accessToken expires (12 hours):
   POST /api/v1/auth/refresh  { refreshToken }
   → { accessToken, refreshToken (rotated) }

4. Logout:
   POST /api/v1/auth/logout  { refreshToken }
   → Refresh token invalidated in DB
```

---

## Configuration

```json
{
  "Jwt": {
    "Key": "<64-char base64 secret>",
    "Issuer": "HRMS.API",
    "Audience": "HRMS.Client",
    "ExpiresInHours": 12
  }
}
```

Generate a secure key: `openssl rand -base64 48`

---

## Calling the API

### Login

```bash
# Replace <one-time-password> with the value printed to stdout by SeedAsync on first run.
# There is no hardcoded default password — do not attempt "Admin@123".
curl -X POST https://api.yourcompany.com/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@hrms.com","password":"<one-time-password>"}'
```

Response:
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJ...",
    "refreshToken": "abc123...",
    "expiresIn": 43200,
    "role": "admin"
  }
}
```

### Authenticated Request

```bash
TOKEN="eyJ..."
curl https://api.yourcompany.com/api/v1/employees \
  -H "Authorization: Bearer $TOKEN"
```

### Refresh Token

```bash
curl -X POST https://api.yourcompany.com/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"abc123..."}'
```

---

## Security Notes

- JWT signing secret must be ≥ 32 characters (validated at startup)
- `ClockSkew = TimeSpan.Zero` — no tolerance for clock drift
- `RequireHttpsMetadata = true` in production
- Refresh tokens are single-use (rotation on every refresh)
- Refresh tokens are stored hashed in PostgreSQL, not in plaintext
