# HRMS Project - Running Ports & Superadmin Credentials Guide

## 🌐 Frontend Running Ports

### React SPA Frontend (Vite)
```
Development Frontend Ports:
├── Default Vite Port (Dev Mode):  http://localhost:5173
├── Alternative Dev Port:          http://localhost:5174
├── Production SPA via Nginx:       http://localhost (port 80)
└── Production SPA (HTTPS):         https://localhost (port 443)
```

### CORS Allowed Origins (from `.env`)
```
http://localhost:3000
http://localhost:5173
http://localhost
```

### Port Configuration Source
From `.env` file:
```
ALLOWED_ORIGINS=http://localhost:3000,http://localhost:5173,http://localhost
AllowedHosts=localhost;127.0.0.1;localhost:3000
```

---

## 🔐 Superadmin Credentials

### Default Superadmin Login
```
Email:    superadmin@hrms.com
Status:   MustChangePassword = true (required on first login)
Role:     Super Admin
```

### Password Sources (in order of precedence)

#### 1️⃣ From Environment Variable (`.env`)
```
SUPERADMIN_INITIAL_PASSWORD=Password@123
```
**Status:** ✓ Set in `.env` file

#### 2️⃣ Fallback: Randomly Generated Password
If `SUPERADMIN_INITIAL_PASSWORD` is not set, a secure random password is generated during migration.

**Generation Logic** (from Program.cs - SeedAsync):
```csharp
// If no configured password provided, generate cryptographically secure password
var tempPassword = !string.IsNullOrWhiteSpace(configuredPassword)
    ? configuredPassword
    : GenerateSecurePassword();
```

**Random Password Characteristics:**
- 16 characters long
- At least 1 uppercase letter: A-Z
- At least 1 lowercase letter: a-z
- At least 1 digit: 2-9 (excludes 0,1 for clarity)
- At least 1 special character: @#$!%*?&
- Shuffled randomization (Fisher-Yates)

### Password Policy Requirements
```
MinLength:           12 characters (actual password: 16)
MaxLength:           72 characters
RequireUppercase:    true
RequireLowercase:    true
RequireDigit:        true
RequireSymbol:       true
RejectCommonPasswords: true
DeniedPasswords:     ["ratanhr", "ratan", "hrms"]
```

---

## 🚀 Current Running Services

### Docker Compose Status (from `docker-compose ps`)
```
✓ ratanhr-mysql      MySQL 8.0          Port 3307 → 3306     Up (healthy)
✓ ratanhr-redis      Redis 7-alpine     Port 6379 → 6379     Up (healthy)
✓ ratanhr-mailhog    MailHog Latest     Port 8025 → 8025     Up (unhealthy - normal)

⚠ Note: API and Frontend containers are NOT running in Docker
        Run them locally via:
        - dotnet run (HRMS.API)
        - bun run dev (HRMS.SPA.Source)
```

---

## 📝 Running Migration & Seeding

### Migration Execution (from Program.cs)
```csharp
// Automatic at startup when DATABASE__AUTOMIGRATE=true
if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        if (app.Environment.IsDevelopment())
        {
            db.Database.EnsureCreated();
            Log.Information("Database tables created/verified (Development mode).");
        }
        else
        {
            db.Database.Migrate();
            Log.Information("Database migrated successfully.");
        }
        await SeedAsync(db, builder.Configuration);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Database schema setup failed.");
        if (!app.Environment.IsDevelopment()) throw;
    }
}
```

### Seed Operations (SeedAsync function)

#### 1. Superadmin Account Creation
```csharp
var superadmin = await db.Users.FirstOrDefaultAsync(u => u.Role == AppRoles.SuperAdmin);

if (superadmin == null)
{
    // Fresh install - create superadmin
    var configuredPassword = configuration["SUPERADMIN_INITIAL_PASSWORD"]
        ?? Environment.GetEnvironmentVariable("SUPERADMIN_INITIAL_PASSWORD");
    
    var tempPassword = !string.IsNullOrWhiteSpace(configuredPassword)
        ? configuredPassword
        : GenerateSecurePassword();

    // Validate password meets policy
    PasswordPolicy.EnsureValid(tempPassword, "SUPERADMIN_INITIAL_PASSWORD");

    db.Users.Add(new User {
        Email              = "superadmin@hrms.com",
        PasswordHash       = BcryptPasswordHasher.Hash(tempPassword, configuration),
        Role               = AppRoles.SuperAdmin,
        FullName           = "Super Admin",
        IsActive           = true,
        MustChangePassword = true,  // ← Force password change on first login
        CreatedAt          = DateTime.UtcNow
    });
    
    Log.Warning("Initial superadmin account created with MustChangePassword=true; " +
                "the initial password was not written to logs.");
}
```

#### 2. Hash Reset for Compromised Passwords
```csharp
const string knownCompromisedHash = "$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";

if (superadmin.PasswordHash == knownCompromisedHash)
{
    // Reset publicly-known hash to new random password
    var tempPassword = GenerateSecurePassword();
    superadmin.PasswordHash       = BcryptPasswordHasher.Hash(tempPassword, configuration);
    superadmin.MustChangePassword = true;
    superadmin.FailedLoginAttempts = 0;
    superadmin.LockoutUntil       = null;
    
    Log.Warning("Committed superadmin password hash detected and reset; " +
                "the replacement password was not written to logs.");
}
```

#### 3. Default Leave Types Seeding
```csharp
if (!await db.LeaveTypes.AnyAsync())
{
    db.LeaveTypes.AddRange(
        new LeaveType { Name = "Casual Leave",    AnnualQuotaDays = 12, IsPaid = true,  IsActive = true },
        new LeaveType { Name = "Sick Leave",      AnnualQuotaDays = 12, IsPaid = true,  IsActive = true },
        new LeaveType { Name = "Earned Leave",    AnnualQuotaDays = 15, IsPaid = true,  IsActive = true },
        new LeaveType { Name = "Unpaid Leave",    AnnualQuotaDays = 30, IsPaid = false, IsActive = true },
        new LeaveType { Name = "Maternity Leave", AnnualQuotaDays = 84, IsPaid = true,  IsActive = true }
    );
    Log.Information("Seeded 5 default leave types.");
}
```

---

## 🔄 Migration Configuration

### Environment Variables (`.env`)
```
# Database Auto-Migration
DATABASE__AUTOMIGRATE=true
SUPERADMIN_INITIAL_PASSWORD=Password@123

# Connection String
ConnectionStrings__DefaultConnection=Server=localhost;Port=3307;Database=hrms_db;User ID=hrms;Password=hrms_secure_password_123;AllowPublicKeyRetrieval=True;SslMode=none
```

### Application Settings (`appsettings.json`)
```json
{
  "Database": {
    "PrimaryConnection": "",
    "ReplicaConnection": "",
    "EnableReadReplica": false,
    "AutoMigrate": false  ← Override to true in development
  },
  "Security": {
    "PasswordPolicy": {
      "MinLength": 12,
      "MaxLength": 72,
      "RequireUppercase": true,
      "RequireLowercase": true,
      "RequireDigit": true,
      "RequireSymbol": true
    }
  }
}
```

---

## 📊 Migration Sequence

```
1. Database Connection
   ↓
2. EnsureCreated() [Dev] or Migrate() [Prod]
   ↓
3. SeedAsync() invoked
   ├─ Check/Create Superadmin Account
   ├─ Validate/Reset Password if Compromised
   ├─ Create Default Leave Types
   └─ SaveChangesAsync()
   ↓
4. Migration Complete ✓
```

---

## 🔒 Security Features During Seeding

✅ **Password Hashing**: BCrypt with configurable work factor (12 iterations)
✅ **Random Password Generation**: Cryptographically secure using RandomNumberGenerator
✅ **Password Policy Enforcement**: Validates length, complexity, denied words
✅ **Force Password Change**: MustChangePassword = true on first login
✅ **Compromised Hash Detection**: Detects and resets publicly-known hashes
✅ **No Credential Logging**: Passwords never written to logs (even in errors)
✅ **PII Masking**: Serilog destructuring redacts sensitive data from logs

---

## 🔐 First Login Workflow

1. **Login Page** (Frontend Port: `http://localhost:5173` or `http://localhost:3000`)
   ```
   Email: superadmin@hrms.com
   Password: Password@123  (from .env SUPERADMIN_INITIAL_PASSWORD)
   ```

2. **Forced Password Change Middleware**
   ```csharp
   // MustChangePasswordMiddleware intercepts all requests
   if (user.MustChangePassword)
       → Redirect to /change-password
   ```

3. **Change Password Form**
   - Current Password: `Password@123`
   - New Password: (new value meeting policy requirements)
   - Confirm Password: (re-enter new password)

4. **Password Updated**
   - MustChangePassword = false
   - PasswordHash = BCrypt(newPassword)
   - Redirect to Dashboard

5. **Dashboard Access**
   - Full system access as Super Admin

---

## 📋 Docker Ports Reference

| Service | Internal Port | Host Port | Protocol | Status |
|---------|---------------|-----------|----------|--------|
| MySQL | 3306 | 3307 | TCP | ✓ Running |
| Redis | 6379 | 6379 | TCP | ✓ Running |
| MailHog SMTP | 1025 | 1025 | TCP | ✓ Running |
| MailHog Web | 8025 | 8025 | TCP | ✓ Running |
| API (local) | 8080 | - | HTTP | ⏸ Stopped |
| Frontend (Vite) | 5173 | 5173 | HTTP | ⏸ Stopped |
| Nginx (Docker) | 80/443 | 80/443 | HTTP/HTTPS | ⏸ Stopped |

---

## 🚀 Starting the Full Stack Locally

### Step 1: Start Docker Services
```bash
docker-compose up -d
# Starts: MySQL, Redis, MailHog
```

### Step 2: Verify Database Connection
```bash
mysql -h localhost -P 3307 -u hrms -p hrms_db
# Password: hrms_secure_password_123
```

### Step 3: Start Backend API (Terminal 1)
```bash
cd HRMS.API
dotnet run
# Runs on http://localhost:8080
# Triggers migration & seeding
# Superadmin account created with Password@123
```

### Step 4: Start Frontend (Terminal 2)
```bash
cd HRMS.SPA.Source
bun install
bun run dev
# Runs on http://localhost:5173
```

### Step 5: Access Application
```
Frontend:  http://localhost:5173
API:       http://localhost:8080
MailHog:   http://localhost:8025
```

### Step 6: Login
```
Email:     superadmin@hrms.com
Password:  Password@123
```

### Step 7: Change Password
First login will redirect to force password change.

---

## 📧 Email Testing (MailHog)

### MailHog Web UI
```
http://localhost:8025
```

### Configuration
```
Email__Host=mailhog        (in .env)
Email__Port=1025
Email__UseSsl=false
Email__Username= (empty)
Email__Password= (empty)
```

### Viewing Emails
1. Open http://localhost:8025
2. All emails sent during development are captured
3. Click email to view content, headers, etc.

---

## 📈 Health Check Endpoints

```
Liveness:    http://localhost:8080/healthz/live
Readiness:   http://localhost:8080/healthz/ready
General:     http://localhost:8080/health
Metrics:     http://localhost:8080/metrics (Prometheus)
CSRF Token:  GET http://localhost:8080/api/auth/csrf
```

---

## 🔍 Debug Logging

### Application Logs Location
```
Logs/hrms-{date}.log  (Rolling daily)
```

### Log Levels (Development)
```
Information: Default level
Warning:     Startup diagnostics, configuration issues
Error:       Exceptions, database errors
Debug:       (enabled in development)
```

### Important Log Messages
```
✓ "HRMS API v1.0.0 starting."
✓ "Initial superadmin account created with MustChangePassword=true"
✓ "Database tables created/verified (Development mode)"
✓ "Seeded 5 default leave types"
✓ "Rate limiter: Redis-backed distributed counters"
```

---

## 🎯 Quick Command Reference

```bash
# Docker
docker-compose ps                          # Check services
docker-compose logs mysql                  # MySQL logs
docker-compose logs redis                  # Redis logs
docker-compose restart mysql               # Restart MySQL

# Backend
cd HRMS.API && dotnet run                 # Start API
dotnet user-secrets init                   # Enable secrets
dotnet user-secrets set "key" "value"     # Store secret

# Frontend
cd HRMS.SPA.Source && bun install         # Install deps
bun run dev                                 # Start dev server
bun run build:ci                           # Production build

# Database
mysql -h localhost -P 3307 -u hrms -p     # MySQL CLI

# Verification
curl http://localhost:8080/health          # API health
curl http://localhost:8080/healthz/live    # Liveness probe
curl http://localhost:8080/api/auth/csrf   # CSRF token
```

---

## 📝 Superadmin Flow Summary

```
┌─────────────────────────────────────────────────┐
│   Application Startup (dotnet run)              │
├─────────────────────────────────────────────────┤
│   1. Read .env environment variables            │
│   2. DATABASE__AUTOMIGRATE=true                 │
│   3. EnsureCreated() / Migrate()                │
│   4. SeedAsync() invoked                        │
│      ├─ Query: User where Role==SuperAdmin     │
│      ├─ NOT FOUND → Create new                 │
│      ├─ Read: SUPERADMIN_INITIAL_PASSWORD      │
│      ├─ Found: Password@123                     │
│      ├─ Hash: BCrypt(Password@123)              │
│      ├─ Set: MustChangePassword=true           │
│      ├─ Insert User record                      │
│      └─ Log: "Initial superadmin account..."   │
│   5. Seed: Default Leave Types                  │
│   6. SaveChangesAsync()                         │
└─────────────────────────────────────────────────┘
                    ↓
          ✓ Migration Complete
                    ↓
┌─────────────────────────────────────────────────┐
│   User Opens Frontend (localhost:5173)          │
│   Clicks "Login"                                │
├─────────────────────────────────────────────────┤
│   Enter Credentials:                            │
│   Email:    superadmin@hrms.com                │
│   Password: Password@123                        │
│   Submit                                        │
└─────────────────────────────────────────────────┘
                    ↓
                POST /api/auth/login
                    ↓
        ┌──────────────────────────┐
        │ AuthService.AuthAsync()  │
        ├──────────────────────────┤
        │ 1. Find user by email    │
        │ 2. Verify password       │
        │ 3. Check MustChange      │
        │ 4. Generate JWT          │
        │ 5. Return 401 + redirect │
        └──────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│   MustChangePasswordMiddleware                  │
│   Intercepts request                            │
│   Detects: MustChangePassword=true              │
│   Redirects: → /change-password                 │
├─────────────────────────────────────────────────┤
│   Form appears:                                 │
│   - Current Password: ***                       │
│   - New Password: [input]                       │
│   - Confirm Password: [input]                   │
│   Submit                                        │
└─────────────────────────────────────────────────┘
                    ↓
            POST /api/auth/change-password
                    ↓
        ┌──────────────────────────┐
        │ AuthService.ChangePass() │
        ├──────────────────────────┤
        │ 1. Verify current pwd    │
        │ 2. Validate new pwd      │
        │ 3. Hash new password     │
        │ 4. Update: MustChange=F  │
        │ 5. Save to DB            │
        │ 6. Return success        │
        └──────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│   ✓ Password Changed Successfully               │
│   Redirect → Dashboard                          │
│   MustChangePassword = false                    │
│   Full system access as Super Admin             │
└─────────────────────────────────────────────────┘
```

---

## ✅ Summary

### Frontend Login Access
```
Ports:     http://localhost:3000 (legacy)
          http://localhost:5173 (Vite dev)
          http://localhost (production via Nginx)
```

### Superadmin Default Credentials
```
Email:     superadmin@hrms.com
Initial PW: Password@123 (from .env SUPERADMIN_INITIAL_PASSWORD)
Status:     MustChangePassword = true (change on first login)
Role:       Super Admin
```

### Migration Execution
```
Trigger:   Automatic at app startup (DATABASE__AUTOMIGRATE=true)
Process:   EnsureCreated() → SeedAsync()
Actions:   1) Create superadmin user
           2) Reset compromised hashes
           3) Seed leave types
Result:    ✓ Complete with superadmin ready to login
```

### Services Status
```
✓ MySQL 8.0 on port 3307
✓ Redis 7 on port 6379
✓ MailHog on port 8025
⏸ API (start with: dotnet run)
⏸ Frontend (start with: bun run dev)
```

---

**Ready to go! 🚀**

1. Start backend: `dotnet run` in HRMS.API
2. Start frontend: `bun run dev` in HRMS.SPA.Source
3. Open: http://localhost:5173
4. Login: superadmin@hrms.com / Password@123
5. Change password on first login

