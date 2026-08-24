# Migration: Add Asset Management & Helpdesk Modules

## Overview
This migration adds two new modules to the HRMS system:
- **Asset Management** — tracks company-owned physical and digital assets
- **Helpdesk** — manages employee support tickets

## New Tables

### Asset Management
| Table | Description |
|-------|-------------|
| `Assets` | Core asset records (code, name, status, assignment) |
| `AssetCategories` | Category groupings (Laptops, Mobile Phones, Furniture, etc.) |
| `AssetHistories` | Immutable audit log for every asset state change |

### Helpdesk
| Table | Description |
|-------|-------------|
| `HelpdeskTickets` | Support tickets raised by employees |
| `HelpdeskCategories` | Category groupings (IT Support, HR Queries, Facilities, etc.) |
| `HelpdeskComments` | Public replies and internal notes on tickets |
| `HelpdeskHistories` | Immutable audit log for every ticket state change |

## Generating the EF Core Migration

```bash
# From the solution root:
cd HRMS.Infrastructure

dotnet ef migrations add AddAssetAndHelpdeskModules \
  --context ApplicationDbContext \
  --startup-project ../HRMS.API \
  --output-dir Migrations

dotnet ef database update \
  --context ApplicationDbContext \
  --startup-project ../HRMS.API
```

## Registering Services in Program.cs / DI

```csharp
// In HRMS.API/Program.cs — add alongside existing service registrations:
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IHelpdeskService, HelpdeskService>();
```

## DbContext Changes

The following DbSets have been added to `ApplicationDbContext`:

```csharp
// Asset Management
public DbSet<Asset>          Assets          => Set<Asset>();
public DbSet<AssetCategory>  AssetCategories => Set<AssetCategory>();
public DbSet<AssetHistory>   AssetHistories  => Set<AssetHistory>();

// Helpdesk
public DbSet<HelpdeskTicket>   HelpdeskTickets   => Set<HelpdeskTicket>();
public DbSet<HelpdeskCategory> HelpdeskCategories => Set<HelpdeskCategory>();
public DbSet<HelpdeskComment>  HelpdeskComments   => Set<HelpdeskComment>();
public DbSet<HelpdeskHistory>  HelpdeskHistories  => Set<HelpdeskHistory>();
```

EF Core fluent configurations are in:
- `HRMS.Infrastructure/Persistence/Configurations/AssetConfiguration.cs`
- `HRMS.Infrastructure/Persistence/Configurations/HelpdeskConfiguration.cs`
