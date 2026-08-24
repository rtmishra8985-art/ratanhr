# HRMS Database Encryption & Soft Delete Fixes

## Overview

This document provides step-by-step instructions to deploy two critical database fixes:

1. **PII Encryption** - Encrypt Aadhaar, PAN, Bank details, UAN, IFSC, GST
2. **Soft Deletes** - Add DeletedAt columns to 8 Sales entities + Travel + Expense

---

## Part 1: Setup Encryption

### Step 1.1: Generate Encryption Key

Run the following command to generate a secure 256-bit encryption key:

**macOS/Linux:**
```bash
openssl rand -base64 32
```

**Windows PowerShell:**
```powershell
$key = [Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
Write-Host "Encryption:Key=$key"
```

**Output Example:**
```
Encryption:Key=w3Z5+c8mQ9X/L2pY4vJ6kN1bF7hR8sD3E0tA5uG2wI9=
```

### Step 1.2: Store the Key

**For Local Development:**
```bash
cd HRMS.API
dotnet user-secrets set "Encryption:Key" "w3Z5+c8mQ9X/L2pY4vJ6kN1bF7hR8sD3E0tA5uG2wI9="
```

**For Production (AWS Secrets Manager):**
```bash
aws secretsmanager create-secret \
  --name hrms/encryption-key \
  --secret-string "w3Z5+c8mQ9X/L2pY4vJ6kN1bF7hR8sD3E0tA5uG2wI9="
```

Then update `appsettings.Production.json`:
```json
{
  "Encryption": {
    "Key": "${ENCRYPTION_KEY}"  // Resolved from Secrets Manager
  }
}
```

**For Production (Azure Key Vault):**
```bash
az keyvault secret set \
  --vault-name hrms-vault \
  --name encryption-key \
  --value "w3Z5+c8mQ9X/L2pY4vJ6kN1bF7hR8sD3E0tA5uG2wI9="
```

### Step 1.3: Register Encryption Service

**Update Program.cs:**
```csharp
// Add after builder.Services configuration
builder.Services.AddScoped<IEncryptionService, AesEncryptionService>();

// Verify encryption service loads without error
var encryptionService = app.Services.CreateScope()
    .ServiceProvider.GetRequiredService<IEncryptionService>();
app.Logger.LogInformation("✅ Encryption service initialized");
```

### Step 1.4: Apply Migration

```bash
cd HRMS.Infrastructure

# Verify the migration is recognized
dotnet ef migrations list --project . --startup-project ../HRMS.API

# Create a new migration (if changes to DbContext needed)
dotnet ef migrations add "AddPiiEncryptionColumns" \
  --project . \
  --startup-project ../HRMS.API \
  --output-dir "Migrations/MySql"

# Update the database
dotnet ef database update \
  --project . \
  --startup-project ../HRMS.API \
  --connection "Server=localhost;User Id=root;Password=password;Database=hrms_dev"
```

### Step 1.5: Verify Migration Applied

```sql
-- Check columns exist
DESC employees;
-- Should show: is_aadhaar_encrypted, is_pan_encrypted, is_bank_account_encrypted, etc.

DESC sales_customers;
-- Should show: is_gst_encrypted, is_pan_encrypted
```

---

## Part 2: Create Soft Delete Migration

### Step 2.1: Apply Soft Delete Migration

```bash
cd HRMS.Infrastructure

# Apply the soft delete migration
dotnet ef database update \
  --project . \
  --startup-project ../HRMS.API \
  --connection "Server=localhost;User Id=root;Password=password;Database=hrms_dev"
```

### Step 2.2: Verify Migration Applied

```sql
-- Check new columns exist on all Sales entities
DESC sales_leads;
-- Should show: deleted_at, is_deleted

DESC sales_customers;
-- Should show: deleted_at, is_deleted

DESC sales_follow_ups;
-- Should show: deleted_at

DESC sales_meetings;
-- Should show: deleted_at

DESC sales_tasks;
-- Should show: deleted_at

DESC sales_quotations;
-- Should show: deleted_at

DESC travel_requests;
-- Should show: deleted_at

DESC expense_claims;
-- Should show: deleted_at

-- Verify indexes exist
SHOW INDEX FROM sales_leads WHERE Key_name LIKE 'ix_sales_leads_company_deleted';
SHOW INDEX FROM sales_customers WHERE Key_name LIKE 'ix_sales_customers_company_deleted';
```

---

## Part 3: Update Entity Models

### Step 3.1: Update Employee Entity

**File:** `HRMS.Domain/Entities/Employee.cs`

Add these properties:
```csharp
public class Employee
{
    // ... existing properties ...

    // Encryption flags
    public bool IsAadhaarEncrypted { get; set; }
    public bool IsPanEncrypted { get; set; }
    public bool IsBankAccountEncrypted { get; set; }
    public bool IsUanEncrypted { get; set; }
    public bool IsIfscEncrypted { get; set; }

    // Audit columns
    public DateTime? PiiEncryptedAt { get; set; }
    public string? PiiEncryptionVersion { get; set; }
}
```

### Step 3.2: Update SalesCustomer Entity

**File:** `HRMS.Domain/Entities/SalesCustomer.cs`

Add these properties:
```csharp
public class SalesCustomer
{
    // ... existing properties ...

    // Encryption flags
    public bool IsGstEncrypted { get; set; }
    public bool IsPanEncrypted { get; set; }

    // Audit columns
    public DateTime? PiiEncryptedAt { get; set; }
    public string? PiiEncryptionVersion { get; set; }
}
```

### Step 3.3: Update Sales Entity Models (Add DeletedAt)

**For SalesLead, SalesFollowUp, SalesMeeting, SalesVisit, SalesTask, SalesQuotation, SalesLeadAssignment:**

```csharp
public class SalesLead  // Apply to all 8 entities
{
    // ... existing properties ...
    
    public DateTime? DeletedAt { get; set; }
}
```

### Step 3.4: Update DbContext (ApplicationDbContext.OnModelCreating)

**File:** `HRMS.Infrastructure/Data/ApplicationDbContext.cs`

Add/update configurations:
```csharp
// Configure PII encryption flags for Employee
mb.Entity<Employee>()
    .Property(x => x.IsAadhaarEncrypted).HasColumnName("is_aadhaar_encrypted").HasDefaultValue(false);
mb.Entity<Employee>()
    .Property(x => x.IsPanEncrypted).HasColumnName("is_pan_encrypted").HasDefaultValue(false);
mb.Entity<Employee>()
    .Property(x => x.IsBankAccountEncrypted).HasColumnName("is_bank_account_encrypted").HasDefaultValue(false);
mb.Entity<Employee>()
    .Property(x => x.IsUanEncrypted).HasColumnName("is_uan_encrypted").HasDefaultValue(false);
mb.Entity<Employee>()
    .Property(x => x.IsIfscEncrypted).HasColumnName("is_ifsc_encrypted").HasDefaultValue(false);

mb.Entity<Employee>()
    .Property(x => x.PiiEncryptedAt).HasColumnName("pii_encrypted_at");
mb.Entity<Employee>()
    .Property(x => x.PiiEncryptionVersion).HasColumnName("pii_encryption_version").HasMaxLength(10);

// Add soft-delete filters for all Sales entities
mb.Entity<SalesLead>().HasQueryFilter(l =>
    !l.IsDeleted && l.DeletedAt == null &&
    (!_filterByTenant || l.CompanyId == _tenantCompanyId));

mb.Entity<SalesCustomer>().HasQueryFilter(c =>
    !c.IsDeleted && c.DeletedAt == null &&
    (!_filterByTenant || c.CompanyId == _tenantCompanyId));

mb.Entity<SalesFollowUp>().HasQueryFilter(f =>
    !f.IsDeleted && f.DeletedAt == null &&
    (!_filterByTenant || f.CompanyId == _tenantCompanyId));

// Repeat for SalesMeeting, SalesVisit, SalesTask, SalesQuotation, SalesLeadAssignment
```

---

## Part 4: Update Services (Encryption Integration)

### Step 4.1: Create EncryptionInterceptor

**File:** `HRMS.Infrastructure/Interceptors/EncryptionInterceptor.cs`

```csharp
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using HRMS.Infrastructure.Services;

namespace HRMS.Infrastructure.Interceptors;

public class EncryptionInterceptor : SaveChangesInterceptor
{
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<EncryptionInterceptor> _logger;

    public EncryptionInterceptor(IEncryptionService encryptionService, ILogger<EncryptionInterceptor> logger)
    {
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        EncryptPiiFields(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EncryptPiiFields(DbContextEventData eventData)
    {
        var context = eventData.Context;
        if (context == null) return;

        foreach (var entry in context.ChangeTracker.Entries().Where(x => x.State == EntityState.Added || x.State == EntityState.Modified))
        {
            EncryptEmployeePii(entry);
            EncryptSalesCustomerPii(entry);
        }
    }

    private void EncryptEmployeePii(EntityEntry entry)
    {
        if (entry.Entity is not Employee employee) return;

        // Encrypt Aadhaar if not already encrypted
        if (!employee.IsAadhaarEncrypted && !string.IsNullOrEmpty(employee.Aadhaar))
        {
            employee.Aadhaar = _encryptionService.Encrypt(employee.Aadhaar);
            employee.IsAadhaarEncrypted = true;
            employee.PiiEncryptedAt ??= DateTime.UtcNow;
            employee.PiiEncryptionVersion ??= "AES-256-v1";
        }

        // Repeat for PAN, BankAccountNumber, UAN, IFSC
    }

    private void EncryptSalesCustomerPii(EntityEntry entry)
    {
        if (entry.Entity is not SalesCustomer customer) return;

        // Encrypt GST if not already encrypted
        if (!customer.IsGstEncrypted && !string.IsNullOrEmpty(customer.Gst))
        {
            customer.Gst = _encryptionService.Encrypt(customer.Gst);
            customer.IsGstEncrypted = true;
            customer.PiiEncryptedAt ??= DateTime.UtcNow;
        }

        // Encrypt PAN if not already encrypted
        if (!customer.IsPanEncrypted && !string.IsNullOrEmpty(customer.Pan))
        {
            customer.Pan = _encryptionService.Encrypt(customer.Pan);
            customer.IsPanEncrypted = true;
        }
    }
}
```

### Step 4.2: Register Interceptor in Program.cs

```csharp
builder.Services.AddDbContext<ApplicationDbContext>((provider, options) =>
{
    options.UseMySql(...)
        .AddInterceptors(provider.GetRequiredService<EncryptionInterceptor>());
});

builder.Services.AddScoped<EncryptionInterceptor>();
```

---

## Part 5: Testing

### Step 5.1: Unit Tests

**File:** `HRMS.Tests/Infrastructure/EncryptionServiceTests.cs`

```csharp
[TestClass]
public class EncryptionServiceTests
{
    private IEncryptionService _encryptionService;
    private IConfiguration _config;

    [TestInitialize]
    public void Setup()
    {
        var inMemorySettings = new Dictionary<string, string>
        {
            { "Encryption:Key", Convert.ToBase64String(new byte[32]) }
        };
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        _encryptionService = new AesEncryptionService(_config);
    }

    [TestMethod]
    public void Encrypt_EmptyString_ReturnsEmpty()
    {
        var result = _encryptionService.Encrypt("");
        Assert.AreEqual("", result);
    }

    [TestMethod]
    public void Encrypt_ValidPlaintext_ReturnsEncrypted()
    {
        var plaintext = "4532-1234-5678-9012";  // Example IFSC
        var encrypted = _encryptionService.Encrypt(plaintext);
        Assert.AreNotEqual(plaintext, encrypted);
        Assert.IsTrue(_encryptionService.IsEncrypted(encrypted));
    }

    [TestMethod]
    public void Decrypt_EncryptedText_ReturnsOriginal()
    {
        var plaintext = "1234567890123456";  // Example Aadhaar
        var encrypted = _encryptionService.Encrypt(plaintext);
        var decrypted = _encryptionService.Decrypt(encrypted);
        Assert.AreEqual(plaintext, decrypted);
    }
}
```

### Step 5.2: Integration Tests

```bash
# Run all tests
cd HRMS.Tests
dotnet test

# Run encryption-specific tests
dotnet test --filter "EncryptionService"
```

### Step 5.3: Manual Testing

```bash
# Start the application
cd HRMS.API
dotnet run

# Create a test employee with PII
POST /api/employees
{
  "name": "John Doe",
  "aadhaar": "1234-5678-9012-3456",
  "pan": "ABCDE1234F",
  "bankAccountNumber": "1234567890123456"
}

# Verify encryption in database
SELECT id, name, aadhaar, is_aadhaar_encrypted FROM employees WHERE name = 'John Doe';
-- Should show aadhaar column as encrypted (hex + base64), is_aadhaar_encrypted = true
```

---

## Part 6: Deployment Checklist

- [ ] Encryption key generated and stored in Secrets Manager
- [ ] Migrations created and tested in DEV environment
- [ ] EncryptionService registered in DI container
- [ ] Entity models updated with new properties
- [ ] DbContext configuration updated with query filters
- [ ] Unit tests passing
- [ ] Integration tests passing
- [ ] Manual testing completed
- [ ] Staging deployment successful
- [ ] Performance testing (encryption overhead acceptable)
- [ ] Production deployment completed
- [ ] Verify query filters active (no soft-deleted records visible)
- [ ] Monitor application logs for encryption warnings

---

## Part 7: Rollback Plan

If issues occur in production:

### Rollback Steps:

```bash
# Revert migrations
cd HRMS.Infrastructure
dotnet ef database update <PreviousMigration> \
  --project . \
  --startup-project ../HRMS.API

# Disable encryption in Program.cs (comment out interceptor registration)
# Restart application
```

### Partial Rollback (Keep columns, disable encryption):

```csharp
// In Program.cs, comment out:
// builder.Services.AddScoped<EncryptionInterceptor>();
// .AddInterceptors(provider.GetRequiredService<EncryptionInterceptor>());

// Application will still read encrypted data but won't encrypt new entries
// (data remains protected, but queries may fail if decryption not attempted)
```

---

## Timeline

| Phase | Duration | Checklist |
|-------|----------|-----------|
| **Setup** | 30 min | Generate key, store in Secrets Manager |
| **Development** | 2 hours | Apply migrations, update entities, register services |
| **Testing** | 4 hours | Unit tests, integration tests, manual testing |
| **Staging** | 2 hours | Deploy, verify query filters, performance test |
| **Production** | 1 hour | Deploy during low-traffic window, monitor logs |
| **Verification** | 1 hour | Confirm encryption active, soft deletes working |

**Total:** ~10 hours (1.5-2 days)

---

## Support

For issues or questions:
1. Check application logs: `$HOME/.docker/desktop/log/HRMS.API.log`
2. Verify encryption key is correctly set: `dotnet user-secrets list`
3. Run tests: `dotnet test --filter "Encryption"`
4. Contact: devops@ratanhr.com

---

**Status:** ✅ READY FOR DEPLOYMENT

**Requires:** .NET 8.0+, MySQL 8.0+, 256-bit encryption key
