# Pagination Guide
**HRMS v2.0.0**

---

## Standard Query Parameters

All list endpoints accept:

| Parameter | Default | Max | Description |
|-----------|---------|-----|-------------|
| `page` | 1 | — | Page number (1-based) |
| `pageSize` | 20 | 200 | Results per page |
| `search` | — | — | Full-text search filter |
| `sortBy` | entity-specific | — | Sort field |
| `sortDir` | `asc` | — | `asc` or `desc` |

---

## Paginated Response Shape

```json
{
  "success": true,
  "data": {
    "items": [...],
    "totalCount": 1250,
    "page": 2,
    "pageSize": 20,
    "totalPages": 63,
    "hasPreviousPage": true,
    "hasNextPage": true
  }
}
```

---

## Implementation

Pagination is implemented in `QueryableExtensions.ToPagedResultAsync<T>()`:

```csharp
// Usage in a service:
var result = await _db.Employees
    .AsNoTracking()
    .Where(e => e.IsActive && e.CompanyId == companyId)
    .OrderBy(e => e.FullName)
    .ToPagedResultAsync(page, pageSize, ct);
```

This issues exactly **two SQL queries**:
1. `SELECT COUNT(*)` — total count
2. `SELECT ... OFFSET x FETCH NEXT y` — the page

---

## Example Requests

```bash
# Page 1, default page size
GET /api/v1/employees?page=1&pageSize=20

# Search + sort
GET /api/v1/employees?search=Smith&sortBy=department&sortDir=asc

# Large page size (report use case)
GET /api/v1/employees?page=1&pageSize=100
```

---

## Performance Notes

- Always use `AsNoTracking()` on paginated read queries
- Add ORDER BY before OFFSET — without ordering, pages may return duplicate rows
- Use composite indexes on columns that appear in both WHERE and ORDER BY
- For report exports (all rows), use the streaming export endpoints instead of pagination
