# App.tsx Patch Instructions

In `HRMS.SPA.Source/src/App.tsx`, add the following **lazy imports** alongside the existing ones:

```tsx
// ── Travel (enhanced) ─────────────────────────────────────────────────────
const TravelPage          = lazy(() => import('@/pages/travel/TravelPage'));
const TravelDashboardPage = lazy(() => import('@/pages/travel/TravelDashboardPage'));

// ── Expenses (enhanced) ───────────────────────────────────────────────────
const ExpensesPage         = lazy(() => import('@/pages/expenses/ExpensesPage'));
const ExpenseDashboardPage = lazy(() => import('@/pages/expenses/ExpenseDashboardPage'));

// ── GPS Attendance (new) ──────────────────────────────────────────────────
const GpsAttendancePage       = lazy(() => import('@/pages/gps/GpsAttendancePage'));
const GeoFenceManagementPage  = lazy(() => import('@/pages/gps/GeoFenceManagementPage'));
const GpsReportsPage          = lazy(() => import('@/pages/gps/GpsReportsPage'));
```

> If `TravelPage` and `ExpensesPage` already have lazy imports, **replace** them with the above.
> Do not add duplicates.

Then, inside the `<Router>` / `<Switch>` block, add (or replace) these routes:

```tsx
{/* ── Travel ── */}
<Route path="/travel"           component={TravelPage} />
<Route path="/travel/dashboard" component={TravelDashboardPage} />

{/* ── Expenses ── */}
<Route path="/expenses"           component={ExpensesPage} />
<Route path="/expenses/dashboard" component={ExpenseDashboardPage} />

{/* ── GPS Attendance ── */}
<Route path="/gps/attendance"  component={GpsAttendancePage} />
<Route path="/gps/geofences"   component={GeoFenceManagementPage} />
<Route path="/gps/reports"     component={GpsReportsPage} />
```

All routes go inside the existing authenticated/protected `<PrivateRoute>` wrapper that the
rest of the app uses.
