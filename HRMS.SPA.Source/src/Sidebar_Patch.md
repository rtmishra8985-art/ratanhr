# Sidebar.tsx Patch Instructions

In `HRMS.SPA.Source/src/components/layout/Sidebar.tsx`, add the following items to the
navigation configuration array (the `navItems` / `menuItems` object or wherever the sidebar
links are defined).

Add them **after the existing Attendance group** and **before the Reports / Settings group**:

```tsx
// ── GPS Attendance ─────────────────────────────────────────────────────────
{
  group: 'GPS Attendance',
  icon: MapPin,          // import { MapPin } from 'lucide-react'
  items: [
    { label: 'GPS Check-In/Out', path: '/gps/attendance' },
    { label: 'Geofence Management', path: '/gps/geofences', adminOnly: true },
    { label: 'GPS Reports', path: '/gps/reports', adminOnly: true },
  ],
},

// ── Travel & Expense ────────────────────────────────────────────────────────
// (update the existing Travel and Expense groups if present, or add new ones)
{
  group: 'Travel',
  icon: Plane,           // import { Plane } from 'lucide-react'
  items: [
    { label: 'My Requests', path: '/travel' },
    { label: 'Travel Dashboard', path: '/travel/dashboard', adminOnly: true },
  ],
},
{
  group: 'Expenses',
  icon: Receipt,         // import { Receipt } from 'lucide-react'
  items: [
    { label: 'My Claims', path: '/expenses' },
    { label: 'Expense Dashboard', path: '/expenses/dashboard', adminOnly: true },
  ],
},
```

Add these imports at the top of `Sidebar.tsx` if not already present:

```tsx
import { MapPin, Plane, Receipt } from 'lucide-react';
```

> Note: The exact shape of a nav item (e.g., whether it uses `adminOnly`, `roles`,
> `requiresPermission`, etc.) should match the pattern already used in the sidebar.
> Adjust field names to match the existing pattern — do **not** change the router paths.
