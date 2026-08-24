# WHERE TO RUN THE COMMANDS - COMPLETE GUIDE

## 📍 LOCATION: Your Computer / Development Machine

### On Windows:
- **PowerShell** (Recommended) - Press Windows Key + R → Type `powershell` → Press Enter
- **Command Prompt** - Press Windows Key + R → Type `cmd` → Press Enter  
- **Visual Studio Terminal** - Open VS → View → Terminal

### On Mac:
- **Terminal** - Command + Space → Type Terminal → Press Enter

### On Linux:
- **Terminal** - Press Ctrl + Alt + T

---

## 📁 YOUR PROJECT LOCATION

```
C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new
```

---

## 🚀 STEP-BY-STEP EXECUTION

### **STEP 1: Open Terminal/PowerShell**
Windows: Press `Windows Key + R` → Type `powershell` → Press Enter

### **STEP 2: Navigate to Your Project Folder**
Copy and paste this command:
```bash
cd C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new
```
Then press **Enter**

Your terminal should now show:
```
PS C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new>
```

### **STEP 3: Delete the Bad Migration File**
Copy and paste this command:
```bash
rm HRMS.Infrastructure/Migrations/MySql/20260815100000_AddMissingTables.cs
```
Then press **Enter**

Expected: No output (file deleted successfully)

### **STEP 4: Regenerate the Migration**
Copy and paste this command:
```bash
dotnet ef migrations add AddMissingTables --project HRMS.Infrastructure --startup-project HRMS.API
```
Then press **Enter**

Expected output:
```
Added migration 'AddMissingTables' to project 'HRMS.Infrastructure'.
```

### **STEP 5: Build the Project**
Copy and paste this command:
```bash
dotnet build
```
Then press **Enter**

Expected output:
```
Build succeeded.
0 Failed.
```

### **STEP 6: Apply Database Migration**
Copy and paste this command:
```bash
dotnet ef database update --startup-project HRMS.API
```
Then press **Enter**

Expected output:
```
Applying migration '20260815_AddMissingTables'.
Done.
```

### **STEP 7: Run Tests**
Copy and paste this command:
```bash
dotnet test --filter "FullStackIntegrationTests" --configuration Release
```
Then press **Enter**

Expected output:
```
Test Run Successful.
Total tests: 27
Passed: 27
Failed: 0
```

---

## 📋 ALL COMMANDS - QUICK REFERENCE

If your terminal supports multi-line copy-paste, copy all these commands:

```bash
cd C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR_new
rm HRMS.Infrastructure/Migrations/MySql/20260815100000_AddMissingTables.cs
dotnet ef migrations add AddMissingTables --project HRMS.Infrastructure --startup-project HRMS.API
dotnet build
dotnet ef database update --startup-project HRMS.API
dotnet test --filter "FullStackIntegrationTests" --configuration Release
```

Paste all into terminal at once and press Enter.

---

## ⚠️ IMPORTANT NOTES

✅ **Copy-paste commands exactly as shown**
✅ **Press Enter after each command**
✅ **Wait for each to complete before running next**
✅ **Don't close terminal between commands**
✅ **If error occurs, report it exactly as shown**

---

## 🆘 TROUBLESHOOTING

### Error: "dotnet: command not found"
- **Cause:** .NET SDK not installed
- **Fix:** Download from https://dotnet.microsoft.com/download

### Error: "rm: command not found" (Windows)
- **Cause:** Using Command Prompt instead of PowerShell
- **Fix:** Use `del` instead:
  ```bash
  del "HRMS.Infrastructure/Migrations/MySql/20260815100000_AddMissingTables.cs"
  ```

### Error: "Permission denied"
- **Cause:** Terminal doesn't have admin rights
- **Fix:** Right-click PowerShell → Run as Administrator

---

## ✅ READY?

**Open PowerShell and navigate to your project folder, then run the commands!**

When you finish all 7 steps, report back with the test results.
