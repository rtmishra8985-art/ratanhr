# Integration Guide — Biometric Module Patch
**Date:** 2026-07-22  
**Version:** v1.0 — applies to ratanhr_fixed_v4

This guide explains how to integrate the biometric module changeset into the existing production HRMS.

---

## Overview of Files in this Package

```
HRMS.Domain/
  Enums/
    BiometricProviderType.cs          ← NEW (copy to project)
    BiometricStatus.cs                ← NEW
  Entities/Attendance/
    BiometricDevice.cs                ← NEW
    BiometricLog.cs                   ← NEW
    BiometricSyncHistory.cs           ← NEW
    BiometricSettings.cs              ← NEW

HRMS.Application/
  DTOs/Attendance/
    BiometricDtos.cs                  ← NEW
  Interfaces/Biometric/
    IBiometricDeviceRepository.cs     ← NEW
    IBiometricLogRepository.cs        ← NEW
    IBiometricSyncHistoryRepository.cs ← NEW
    IBiometricDeviceService.cs        ← NEW
    (Existing IBiometricProvider.cs, IBiometricProviderFactory.cs,
     IBiometricSyncService.cs — NOT CHANGED)

HRMS.Infrastructure/
  Biometric/
    BiometricDeviceRepository.cs      ← NEW
    BiometricLogRepository.cs         ← NEW
    BiometricSyncHistoryRepository.cs ← NEW
    BiometricSettingsRepository.cs    ← NEW
    BiometricDeviceService.cs         ← NEW
    ApplicationDbContext_BiometricAdditions.cs ← PATCH GUIDE (see step 2)
    (Existing providers and BiometricSyncService — NOT CHANGED)
  BackgroundServices/
    BiometricHostedService.cs         ← NEW
  Migrations/
    20260722000001_AddBiometricTables.cs ← NEW

HRMS.API/
  Controllers/Attendance/
    BiometricController_Extended.cs   ← REPLACES BiometricController.cs (see step 3)
  Extensions/
    ServiceExtensions_BiometricPatch.cs ← PATCH GUIDE (see step 4)
  appsettings_BiometricSection.json   ← MERGE GUIDE (see step 5)
  wwwroot/
    biometric-dashboard.html          ← NEW
    biometric-devices.html            ← NEW
    biometric-logs.html               ← NEW
    biometric-realtime.html           ← NEW
    biometric-sync-history.html       ← NEW
    biometric-settings.html           ← NEW
```

---

## Step-by-Step Integration

### Step 1 — Copy New Files

Copy all NEW files (listed above) directly into the matching paths in the project.  
Do **not** overwrite any existing files unless explicitly stated.

### Step 2 — Patch ApplicationDbContext.cs

Open `HRMS.Infrastructure/Data/ApplicationDbContext.cs` and:

**a) Add these DbSet properties** (after the existing Attendance block):
```csharp
// ── Biometric ─────────────────────────────────────────────────────────
public DbSet<BiometricDevice>      BiometricDevices       => Set<BiometricDevice>();
public DbSet<BiometricLog>         BiometricLogs          => Set<BiometricLog>();
public DbSet<BiometricSyncHistory> BiometricSyncHistories => Set<BiometricSyncHistory>();
public DbSet<BiometricSettings>    BiometricSettings      => Set<BiometricSettings>();
```

**b) Add EF model configuration** (inside `OnModelCreating` or create the override if absent).  
The full configuration block is in `ApplicationDbContext_BiometricAdditions.cs`.

### Step 3 — Replace BiometricController.cs

Replace `HRMS.API/Controllers/Attendance/BiometricController.cs` with  
`BiometricController_Extended.cs` (rename it to `BiometricController.cs`).

All existing endpoints (`/vendors`, `/status/{vendor}`, `/sync`) are preserved verbatim.  
New endpoints are additive.

### Step 4 — Add DI Registrations to ServiceExtensions.cs

Open `HRMS.API/Extensions/ServiceExtensions.cs`, find the `// ── Attendance ─` block  
(or the existing biometric provider registrations), and add **after** them:

```csharp
// ── Biometric — device management, repos, background service ──────────
services.AddScoped<IBiometricDeviceRepository, BiometricDeviceRepository>();
services.AddScoped<IBiometricLogRepository, BiometricLogRepository>();
services.AddScoped<IBiometricSyncHistoryRepository, BiometricSyncHistoryRepository>();
services.AddScoped<BiometricSettingsRepository>();
services.AddScoped<IBiometricDeviceService, BiometricDeviceService>();
services.AddHostedService<BiometricHostedService>();
```

**Required additional usings** at the top of `ServiceExtensions.cs`:
```csharp
using HRMS.Application.Interfaces.Biometric;
using HRMS.Infrastructure.Biometric;
using HRMS.Infrastructure.BackgroundServices;
```

### Step 5 — Merge appsettings.json

Open `HRMS.API/appsettings.json` and add the `"Biometric"` section from  
`appsettings_BiometricSection.json` as a top-level key. Do NOT replace any existing keys.

### Step 6 — Run the Migration

```bash
# From the solution root:
dotnet ef migrations add AddBiometricTables \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API \
  --context ApplicationDbContext

# Then apply to the database:
dotnet ef database update \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API
```

> **Note:** The migration file `20260722000001_AddBiometricTables.cs` is provided.  
> You can use it directly instead of running `dotnet ef migrations add`.  
> Ensure the snapshot is also updated after applying.

### Step 7 — Add Sidebar Links (Optional)

To include biometric navigation in existing admin pages, add this block to each page's sidebar:

```html
<div class="nav-label">Biometric</div>
<a class="nav-link" href="biometric-dashboard.html"><i class="bi bi-fingerprint"></i> Biometric Dashboard</a>
<a class="nav-link" href="biometric-devices.html"><i class="bi bi-hdd-network"></i> Devices</a>
<a class="nav-link" href="biometric-logs.html"><i class="bi bi-list-ul"></i> Punch Logs</a>
<a class="nav-link" href="biometric-realtime.html"><i class="bi bi-broadcast"></i> Realtime Monitor</a>
<a class="nav-link" href="biometric-sync-history.html"><i class="bi bi-arrow-repeat"></i> Sync History</a>
<a class="nav-link" href="biometric-settings.html"><i class="bi bi-gear"></i> Settings</a>
```

---

## Activating Vendor Providers

Each vendor provider stub (`ZKTecoProvider.cs`, `EsslProvider.cs`, etc.) throws  
`NotImplementedException`. To activate a provider:

1. Install the vendor's SDK NuGet package.
2. Replace the stub body in the corresponding provider class.
3. The factory and sync service automatically pick up the working provider — no other changes needed.

**ZKTeco:** Install `ZKTeco.ZKLib` and implement `FetchLogsAsync` using the ZKLib TCP connection.

---

## What Was NOT Changed

- `IBiometricProvider.cs` — unchanged
- `IBiometricProviderFactory.cs` — unchanged  
- `IBiometricSyncService.cs` — unchanged
- `BiometricProviderFactory.cs` — unchanged
- `BiometricSyncService.cs` — unchanged
- All 7 provider stubs — unchanged (activation is a separate step per vendor)
- All existing attendance, payroll, leave, employee, dashboard logic — unchanged
- Auth/JWT/RBAC — unchanged
- Serilog config — unchanged
- All existing migrations — unchanged

---

## Production Readiness Checklist

- [ ] Migration applied to staging database and verified
- [ ] At least one device registered via `POST /api/biometric/devices`
- [ ] `POST /api/biometric/devices/{id}/test` returns expected response (stub = offline, SDK = online)
- [ ] `GET /api/biometric/settings` returns defaults
- [ ] `biometric-dashboard.html` loads in browser without errors
- [ ] `BiometricHostedService` starts in application logs (check `[BiometricHostedService] Started`)
- [ ] `POST /api/biometric/sync` returns `501` for stub providers (expected behaviour)
- [ ] Vendor SDK installed and provider activated when hardware is available
