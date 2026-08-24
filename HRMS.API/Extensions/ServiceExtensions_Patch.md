# ServiceExtensions Patch Instructions

In `HRMS.API/Extensions/ServiceExtensions.cs`, locate the section with the comment
`// ── Company ────────────────────────────────────────────────────────────────`
and add these service registrations AFTER the last `AddScoped` line in that section:

```csharp
// ── Travel & Expense (enhanced) ────────────────────────────────────────────
services.AddScoped<ITravelService,          TravelService>();
services.AddScoped<IExpenseService,         ExpenseService>();

// ── GPS Attendance ─────────────────────────────────────────────────────────
services.AddScoped<IGpsAttendanceService,   GpsAttendanceService>();
```

Also add these `using` statements at the top of `ServiceExtensions.cs` if not already present:

```csharp
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Services;
```

> Note: If `ITravelService` and `IExpenseService` were already registered (stubs), replace
> the existing registrations. Do NOT duplicate them.
