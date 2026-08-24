# CRITICAL IMMEDIATE ACTION ITEMS - RatanHR Demo Mode

**BEFORE NEXT BUILD/TEST:**

## ⚠️ MUST DO (Will Fail Without These)

### 1. Add DbSet to ApplicationDbContext.cs
**File:** `HRMS.Infrastructure/Data/ApplicationDbContext.cs`  
**Location:** Around line 50-100 with other DbSet declarations  
**Action:** Add this single line:

```csharp
public DbSet<HRMS.Domain.Entities.Demo.DemoSeedTracker> DemoSeedTrackers { get; set; } = null!;
```

**Exact place:** After `public DbSet<User> Users` declaration

---

### 2. Register Services in ServiceExtensions.cs
**File:** `HRMS.API/Extensions/ServiceExtensions.cs`  
**Method:** `AddInfrastructure()` method  
**Location:** Around line 100-150, after other service registrations  
**Action:** Add these 2 lines:

```csharp
services.AddScoped<IDemoSeedService, DemoSeedService>();
services.Configure<DemoModeOptions>(configuration.GetSection(DemoModeOptions.SectionName));
```

**Add AFTER:**
```csharp
services.AddScoped<IWebhookService, WebhookService>();
```

---

### 3. Verify appsettings.json
**File:** `HRMS.API/appsettings.json`  
**Check:** Must contain DemoMode section

```json
"DemoMode": {
  "_comment": "Production-safe demo mode configuration. Demo Mode disabled by default.",
  "Enabled": false,
  "SeedEnabled": false,
  "AllowProduction": false,
  "SeedVersion": "1.0.0",
  "DryRunByDefault": true
}
```

**Status:** ✅ Already updated in this session

---

## 🔍 VERIFICATION STEPS

### Step 1: Check Compilation
```bash
cd C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new
dotnet build --configuration Release
```

**Expected:** 0 errors, 0 warnings (or acceptable warnings)  
**If fails:** Check the 3 items above

---

### Step 2: Check File Exists
```bash
# Verify all new files created
ls -la HRMS.Infrastructure/Services/Demo/
# Should show:
#   DemoSeedService.cs
#   IDemoSeedService.cs

ls -la HRMS.Infrastructure/Migrations/MySql/ | grep 20260819
# Should show:
#   20260819000001_AddIsDemoColumn.cs
#   20260819000001_AddIsDemoColumn.Designer.cs

ls -la HRMS.Domain/Entities/Demo/
# Should show:
#   DemoSeedTracker.cs
```

---

### Step 3: Test DI Resolution (Optional, for confidence)
```csharp
// Add this to a test or Program.cs temporarily to verify DI works
using (var scope = app.Services.CreateScope())
{
    var demoService = scope.ServiceProvider.GetRequiredService<IDemoSeedService>();
    var result = await demoService.ValidateAsync();
    Console.WriteLine($"Demo validation: {result.IsValid}");
}
```

---

## 🎯 NEXT SESSION STARTING POINT

After completing the 3 action items above:

1. Create `HRMS.API/Controllers/AdminDemoController.cs` (150 lines)
2. Create test files (200+ lines)
3. Run `dotnet test` (verify all tests pass)
4. Run `dotnet ef database update` (apply migration)
5. Test dry-run: `curl http://localhost:5000/api/admin/demo/seed/dry-run`

---

## ✅ VERIFICATION AFTER ACTION ITEMS

To verify everything is correct, run:

```bash
dotnet build --configuration Release 2>&1 | grep -i "error"
# Should have NO matches

dotnet build --configuration Release 2>&1 | tail -5
# Should show: Build succeeded. X warning(s)
```

---

## 📌 DO NOT FORGET

❌ **DO NOT** modify existing authentication/authorization (already works)  
❌ **DO NOT** change existing tenant filters (already optimized)  
❌ **DO NOT** create separate database (use existing)  
❌ **DO NOT** hardcode secrets or credentials  

✅ **DO** use IsDemo flag for isolation  
✅ **DO** respect CompanyId scoping (1-5 for demo)  
✅ **DO** make demo features opt-in (disabled by default)  
✅ **DO** log all seed/cleanup operations  

---

**Total Effort for Action Items: ~15 minutes**

**After these 3 items → Ready for API controller + tests**
