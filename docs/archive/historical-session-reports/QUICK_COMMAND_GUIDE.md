# QUICK START - RUN THESE 7 COMMANDS

## 📍 WHERE: Open PowerShell on Your Computer

**Windows:** Press `Windows Key + R` → Type `powershell` → Press Enter

---

## 🎯 WHAT TO DO

### Command 1: Navigate to Project
```
cd C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new
```

### Command 2: Delete Bad Migration File
```
rm HRMS.Infrastructure/Migrations/MySql/20260815100000_AddMissingTables.cs
```

### Command 3: Generate New Migration
```
dotnet ef migrations add AddMissingTables --project HRMS.Infrastructure --startup-project HRMS.API
```

### Command 4: Build Project
```
dotnet build
```

### Command 5: Update Database
```
dotnet ef database update --startup-project HRMS.API
```

### Command 6: Run Tests
```
dotnet test --filter "FullStackIntegrationTests" --configuration Release
```

---

## 📋 COPY-PASTE EVERYTHING AT ONCE

Copy all commands below, paste into PowerShell, press Enter:

```
cd C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new
rm HRMS.Infrastructure/Migrations/MySql/20260815100000_AddMissingTables.cs
dotnet ef migrations add AddMissingTables --project HRMS.Infrastructure --startup-project HRMS.API
dotnet build
dotnet ef database update --startup-project HRMS.API
dotnet test --filter "FullStackIntegrationTests" --configuration Release
```

---

## ✅ DONE!

Report back with the results when all commands complete!
