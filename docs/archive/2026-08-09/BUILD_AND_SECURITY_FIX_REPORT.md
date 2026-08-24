> ⚠️ **SUPERSEDED** — This report was generated during an earlier audit/fix pass and no longer reflects the current state of the codebase. The authoritative current-state documents are [`RELEASE_GATE_FINAL.md`](RELEASE_GATE_FINAL.md) and [`VERIFICATION_REPORT_FINAL_v2.md`](VERIFICATION_REPORT_FINAL_v2.md). Do not use this file to assess production readiness.

---

# Build & Security Fix Report

All changes below were made to make the shipped archive actually install, typecheck,
build, and test cleanly. Verified locally with `bun 1.x` / `vite 5+`.

## 1. Install no longer fails (`HRMS.SPA.Source/package.json`)

| Package | Was | Now | Reason |
|---|---|---|---|
| `@replit/vite-plugin-dev-banner` | `^0.0.3` | `^0.1.2` | `0.0.3` was never published (max published: `0.1.2`) |
| `vite-plugin-subresource-integrity` | `^1.0.4` | **removed** | `1.0.4` was never published (max: `0.0.12`), and `0.0.12` crashes on remote `<link>` URLs |

`bun.lock` is committed so `bun audit` / `npm audit` can run against a real lockfile.

## 2. Missing internal package `@workspace/api-client-react`

The SPA imported this package in 20+ files but it was never shipped. It is now
implemented locally:

- `src/api-client/http.ts` — fetch wrapper over the existing `csrfFetch`
  (`credentials: 'include'`, `X-XSRF-TOKEN`, JSON + `FormData` support, typed errors).
- `src/api-client/index.ts` — 35+ React Query hooks typed against `src/types/domain.ts`,
  with a `toPaged()` normalizer for the varying backend paging shapes.

Alias registered in `tsconfig.json`, `vite.config.ts`, `vite.config.local.ts`,
and `vitest.config.ts`.

## 3. TypeScript: 99 → 0 errors

- **React Query v5 API**: `keepPreviousData: true` → `placeholderData: keepPreviousData`
  (`BiometricPage`); untyped `.then((r) => r.data)` responses now typed as `PagedItems`
  (`TimesheetPage`).
- **Lucide icon props**: `icon={<Plane />}` → `icon={Plane}` in 11 places
  (`EmptyState` expects the component, not an element) across Expenses, GPS,
  Onboarding, Sales, Travel.
- **`Pagination`**: `SalesPage` passed `total`; the component requires
  `totalCount` + `totalPages`.
- **`EmployeeDetailPage`**: `joiningDate` → `joinDate` (actual `EmployeeDetail` field).
- **Unsafe casts**: `as Record<string, unknown>` on non-index-signature types
  widened via `as unknown as` (`LoginPage`, `OrgChartPage`, `SalesPage` form defaults).
- **Dead code**: ~35 unused imports/locals removed (`noUnusedLocals` is on).

## 4. Production build now completes

- `public/favicon.svg` was referenced by `index.html` but missing — added.
- SRI: replaced the broken third-party plugin with `build/sri-plugin.ts`, which hashes
  only bundle-relative assets and skips remote URLs (Google Fonts previously crashed
  the server build). Output verified to carry `integrity="sha384-…" crossorigin`.
- Build requires `PORT`, `BASE_PATH` (and `API_BASE_URL`) env vars — by design:
  `PORT=5173 BASE_PATH=/ API_BASE_URL=http://localhost:5000 bun run build`

## 5. Tests: 73 passed / 3 failed → 76 passed / 0 failed

- `SafeAvatar` now renders `?` when **no profile object** is supplied, while
  `getUserInitials` keeps its documented `U` fallback for a profile with no usable
  name — both contracts are now satisfied.
- The image-error test used a raw `dispatchEvent`, whose state update was never
  flushed; switched to `fireEvent.error` (act-wrapped).

## 6. NuGet vulnerabilities pinned

Explicit `PackageReference` pins added so the vulnerable transitives can no longer
be resolved:

| Package | Vulnerable | Pinned | Project |
|---|---|---|---|
| `Newtonsoft.Json` | 11.0.2 (High) | 13.0.3 | API, Infrastructure |
| `System.Data.SqlClient` | 4.4.0 (High + Moderate) | 4.8.6 | API, Infrastructure |
| `SQLitePCLRaw.*` | 2.1.6 (High, test-only) | 2.1.10 | Tests |
| `Microsoft.Extensions.Caching.Memory` | 8.0.0 (High) | already 8.0.1 | API, Infrastructure |

Note: the .NET SDK is not available in this environment, so these pins were not
restored/compiled here — run `dotnet restore HRMS.sln && dotnet list package
--vulnerable --include-transitive` to confirm a clean report.

## Verification commands

```bash
cd HRMS.SPA.Source
bun install
bunx tsc -p tsconfig.json --noEmit          # 0 errors
bunx vitest run                             # 76 passed
PORT=5173 BASE_PATH=/ API_BASE_URL=http://localhost:5000 bun run build
```
