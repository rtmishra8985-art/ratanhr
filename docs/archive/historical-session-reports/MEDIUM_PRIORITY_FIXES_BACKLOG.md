# MEDIUM Priority Fixes (18 items) — Sprint Backlog

**Status:** Ready for next sprint  
**Estimated Effort:** 3-4 sprints (18 items @ 2-4 hours each)

---

## 📋 MEDIUM Priority Issues

### MED-1: Department Sorting Using FK Relationship ⭐ START HERE
**Difficulty:** Low | **Time:** 20 min | **Impact:** Query efficiency

**Issue:**
```
Currently: Department sorting uses string column → O(n log n) string comparison
Better:    Sort by DepartmentId FK, then display name in results
```

**Files:**
- `HRMS.Infrastructure/Services/EmployeeService.cs` → `GetAllPagedAsync(sortBy)`
- `HRMS.API/Controllers/Employees/EmployeeController.cs` → add example in docs

**Fix:**
```csharp
// Before
if (sortBy == "Department")
    q = sortDirection == "asc" ? q.OrderBy(e => e.Department) : q.OrderByDescending(e => e.Department);

// After
if (sortBy == "Department" || sortBy == "DepartmentId")
    q = sortDirection == "asc" 
        ? q.OrderBy(e => e.DepartmentId).ThenBy(e => e.Department)
        : q.OrderByDescending(e => e.DepartmentId).ThenByDescending(e => e.Department);
```

**Test:**
```bash
# Sort by department, verify results are correct and database index is used
curl "https://localhost/api/employees?sortBy=DepartmentId&sortDirection=asc"
```

---

### MED-2: Concurrent Edit Detection (Optimistic Locking) ⭐ IMPORTANT
**Difficulty:** High | **Time:** 2-3 hours | **Impact:** Data integrity

**Issue:**
- Two admins edit same employee simultaneously
- Second save overwrites first admin's changes (lost update)
- No warning to second admin

**Fix Approach:**
1. Add `[Timestamp]` byte array column to `Employee` entity
2. EF Core auto-manages this; incremented on each save
3. Include in GET responses; include in PUT requests
4. If timestamp mismatch, return 409 Conflict

**Files:**
- `HRMS.Domain/Entities/Employee/Employee.cs` → add `public byte[] RowVersion { get; set; }`
- `HRMS.Infrastructure/Data/ApplicationDbContext.cs` → configure `IsRowVersion()`
- `HRMS.API/Controllers/Employees/EmployeeController.cs` → update Update() to handle 409
- `HRMS.Application/DTOs/Employee/UpdateEmployeeDto.cs` → add `RowVersion` field

**Code:**
```csharp
// Employee.cs
[Timestamp]
public byte[] RowVersion { get; set; }

// ApplicationDbContext.OnModelCreating
modelBuilder.Entity<Employee>()
    .Property(e => e.RowVersion)
    .IsRowVersion();

// UpdateEmployeeDto
public byte[] RowVersion { get; set; }

// EmployeeController.Update()
try {
    await _service.UpdateAsync(id, dto, userId, companyId);
    return Ok(new { success = true, message = "Employee updated." });
}
catch (DbUpdateConcurrencyException ex) {
    _logger.LogWarning(ex, "Concurrent edit detected for employee {Id}", id);
    return Conflict(new { success = false, message = "Employee was modified by another user. Refresh and retry." });
}
```

**Test:**
```bash
# Simulate concurrent edit
curl -X PUT https://localhost/api/employees/1 \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Alice","rowVersion":"<old-bytes>"}'
# Expected: 409 Conflict if another admin edited first
```

---

### MED-3: Biometric Sync Pruning Job
**Difficulty:** Low | **Time:** 15 min | **Impact:** Database size

**File:** `HRMS.Infrastructure/Jobs/BiometricSyncPruneJob.cs` (ALREADY CREATED ✅)

**Status:** Code exists, needs registration in Program.cs

**Add to Program.cs (Hangfire section):**
```csharp
recurringJobs.AddOrUpdate<HRMS.Infrastructure.Jobs.BiometricSyncPruneJob>(
    "biometric-sync-prune",
    j => j.RunAsync(),
    "0 23 * * *", // Daily at 11 PM UTC
    timeZone: TimeZoneInfo.Utc);
```

---

### MED-4: SalaryStructure Versioning
**Difficulty:** High | **Time:** 3-4 hours | **Impact:** Audit trail

**Issue:**
- When salary structure changes, history is lost
- Cannot calculate retro/backpay based on old structure

**Fix Approach:**
1. Create `SalaryStructureVersion` entity (archive table)
2. On every update, insert current version to archive
3. Query archive for historical rates

**Schema:**
```sql
CREATE TABLE SalaryStructureVersions (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    SalaryStructureId INT NOT NULL,
    EmployeeId VARCHAR(20) NOT NULL,
    EffectiveDate DATE,
    BasicPay DECIMAL(12,2),
    HRA DECIMAL(12,2),
    DA DECIMAL(12,2),
    -- ... other fields
    VersionNumber INT,
    CreatedAt DATETIME(6),
    FOREIGN KEY (SalaryStructureId) REFERENCES SalaryStructures(Id)
);
```

**Service Update:**
```csharp
public async Task UpdateAsync(int id, UpdateSalaryStructureDto dto)
{
    var ss = await _db.SalaryStructures.FindAsync(id);
    
    // Archive current version
    _db.SalaryStructureVersions.Add(new SalaryStructureVersion {
        SalaryStructureId = ss.Id,
        EmployeeId = ss.EmployeeId,
        BasicPay = ss.BasicPay,
        // ... copy all fields
        VersionNumber = (await _db.SalaryStructureVersions
            .Where(v => v.SalaryStructureId == id)
            .CountAsync()) + 1,
        CreatedAt = DateTime.UtcNow
    });
    
    // Apply new values
    ss.BasicPay = dto.BasicPay;
    // ... update fields
    
    await _db.SaveChangesAsync();
}
```

---

### MED-5: File Access Audit Logging
**Difficulty:** Medium | **Time:** 1-2 hours | **Impact:** Compliance

**Issue:**
- Documents accessed by employees not logged
- PII access not tracked

**Fix:**
1. Intercept GET requests for `/api/employees/{id}/documents`
2. Log to AuditLog: "DOCUMENT_ACCESS", employee ID, user ID, timestamp
3. Add filter or middleware

**Code:**
```csharp
// In EmployeeDocumentController.GetByIdAsync()
await _auditService.LogAsync(
    "DOCUMENT_ACCESS",
    "EmployeeDocument",
    documentId.ToString(),
    userId: currentUserId,
    targetEmployeeId: document.EmployeeId,
    details: $"Downloaded {document.FileName}");
```

---

### MED-6: Leave Approval Transactional
**Difficulty:** Low | **Time:** 20 min | **Impact:** Data consistency

**Issue:**
- When approving leave, multiple updates happen:
  1. Update LeaveRequest.Status
  2. Update LeaveRequest.TotalDays
  3. Send email
  4. Create notification
- If email fails mid-process, request is half-updated

**Fix:**
```csharp
public async Task<(bool ok, string message)> ApproveAsync(int requestId, ...)
{
    using var txn = await _db.Database.BeginTransactionAsync();
    try {
        var req = await _db.LeaveRequests.FindAsync(requestId);
        req.Status = "Approved";
        req.TotalDays = await LeaveDaysAsync(...);
        await _db.SaveChangesAsync();
        
        // Post-save actions (email, notifications) — if they fail, txn is not rolled back
        // because they're outside the try block. This is OK; the approval is committed,
        // and we just retry email delivery later via a background job.
        await SendApprovalEmailAsync(req);
        
        await txn.CommitAsync();
        return (true, "Leave approved.");
    }
    catch (Exception ex) {
        await txn.RollbackAsync();
        _logger.LogError(ex, "Leave approval failed");
        return (false, "Approval failed. Please retry.");
    }
}
```

---

### MED-7: Bulk Employee Update API
**Difficulty:** High | **Time:** 2-3 hours | **Impact:** Operational efficiency

**Endpoint:** `POST /api/employees/bulk-update`

**Payload:**
```json
{
  "updates": [
    { "employeeId": "E001", "department": "Finance", "designation": "Analyst" },
    { "employeeId": "E002", "department": "HR", "designation": "Manager" }
  ]
}
```

**Service Logic:**
```csharp
public async Task<(int updated, List<string> errors)> BulkUpdateAsync(
    List<BulkEmployeeUpdateDto> updates, int companyId, int userId)
{
    var errors = new List<string>();
    int updated = 0;
    
    using var txn = await _db.Database.BeginTransactionAsync();
    try {
        foreach (var upd in updates)
        {
            try {
                var emp = await _db.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeCode == upd.EmployeeId && e.CompanyId == companyId);
                
                if (emp == null) {
                    errors.Add($"Employee {upd.EmployeeId}: not found");
                    continue;
                }
                
                if (!string.IsNullOrEmpty(upd.Department))
                    emp.Department = upd.Department;
                if (!string.IsNullOrEmpty(upd.Designation))
                    emp.Designation = upd.Designation;
                
                updated++;
            }
            catch (Exception ex) {
                errors.Add($"Employee {upd.EmployeeId}: {ex.Message}");
            }
        }
        
        await _db.SaveChangesAsync();
        await txn.CommitAsync();
        
        await _auditService.LogAsync("EMPLOYEE_BULK_UPDATE", "Employee", null, userId,
            details: $"Updated {updated} employees");
        
        return (updated, errors);
    }
    catch (Exception ex) {
        await txn.RollbackAsync();
        throw;
    }
}
```

---

### MED-8: Query Parameter Validation
**Difficulty:** Low | **Time:** 30 min | **Impact:** Data validation

**Issue:**
- Endpoints accept page=0 (invalid), pageSize=10000 (too large)
- No validation at controller level

**Fix:**
```csharp
// In GetAllPagedAsync(int page, int pageSize, ...)
[Range(1, int.MaxValue, ErrorMessage = "Page must be >= 1")]
public int Page { get; set; }

[Range(1, 200, ErrorMessage = "PageSize must be between 1 and 200")]
public int PageSize { get; set; }

// Or in controller:
if (page < 1) page = 1;
if (pageSize < 1) pageSize = 10;
if (pageSize > 200) pageSize = 200;
```

---

### MED-9: Version Endpoint
**Difficulty:** Low | **Time:** 15 min | **Impact:** Operational

**Endpoint:** `GET /api/version`

**Response:**
```json
{
  "version": "1.0.5",
  "buildDate": "2026-08-19T10:30:00Z",
  "environment": "production",
  "apiVersion": "v1",
  "databaseVersion": "8.4",
  "dotnetVersion": "8.0.0"
}
```

**Implementation:**
```csharp
[HttpGet("version")]
[AllowAnonymous]
public IActionResult GetVersion()
{
    return Ok(new {
        version = "1.0.5",
        buildDate = new DateTime(2026, 8, 19, 10, 30, 0),
        environment = _env.EnvironmentName,
        apiVersion = "v1"
    });
}
```

---

### MED-10: Offline Mode with Service Worker
**Difficulty:** Very High | **Time:** 8-12 hours | **Impact:** UX

**Approach:**
1. Create service worker (`src/service-worker.ts`)
2. Cache GET requests
3. Queue POST/PUT/DELETE offline
4. Sync when back online

**Scaffolding:**
```typescript
// src/service-worker.ts
const CACHE_NAME = "hrms-v1";

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE_NAME));
});

self.addEventListener("fetch", (event) => {
  if (event.request.method === "GET") {
    // Cache GET requests
    event.respondWith(
      caches.match(event.request).then((response) => {
        if (response) return response;
        return fetch(event.request).then((r) => {
          caches.open(CACHE_NAME).then(() => cache.put(event.request, r.clone()));
          return r;
        });
      })
    );
  } else {
    // Queue mutations for later sync
    event.respondWith(queueOfflineRequest(event.request));
  }
});
```

---

## Remaining MEDIUM Issues (9 More)

| ID | Title | Difficulty | Time | Impact |
|--|--|--|--|--|
| MED-11 | Add [Required] attributes to DTOs | Low | 30m | Type safety |
| MED-12 | Implement graceful degradation for external APIs | Medium | 1.5h | Resilience |
| MED-13 | Add batch delete endpoint | Medium | 1h | UX |
| MED-14 | Implement audit log export (CSV/Excel) | Medium | 1.5h | Compliance |
| MED-15 | Add department hierarchy (parent/child) | High | 3h | Structure |
| MED-16 | Implement employee transfer approval workflow | High | 3h | Process |
| MED-17 | Add performance appraisal forms versioning | High | 3h | Audit |
| MED-18 | Implement notification preferences | Medium | 2h | UX |
| MED-19 | Add bulk export APIs (employee roster, payroll) | Medium | 1.5h | Operations |

---

## 🎯 Recommended Sprint Order

### Sprint 1 (Week 2)
1. MED-1: Department sorting FK (20m)
2. MED-3: Biometric pruning registration (15m)
3. MED-8: Query validation (30m)
4. MED-9: Version endpoint (15m)
5. MED-5: File access audit (1-2h)

**Subtotal:** 4.5 hours

### Sprint 2 (Week 3)
1. MED-6: Leave approval transactional (20m)
2. MED-7: Bulk employee update API (2-3h)
3. MED-4: SalaryStructure versioning (3-4h)

**Subtotal:** 6-7 hours

### Sprint 3 (Week 4)
1. MED-2: Concurrent edit detection (2-3h)
2. MED-10: Offline mode with SW (8-12h)

**Subtotal:** 10-15 hours (split across 2 weeks if needed)

---

## 📊 Effort Estimation

```
Total MEDIUM items:     18
Low difficulty:         5 items × 0.5h = 2.5h
Medium difficulty:     10 items × 2h   = 20h
High difficulty:        3 items × 3h   = 9h

Total estimated:        31.5 hours (~1 week, 1 person)
Or:                     2 weeks (split with other work)
```

---

## ✅ Acceptance Criteria

Each MEDIUM fix must:
- [ ] Have code and tests merged to main
- [ ] Be documented with example cURL commands
- [ ] Pass code review
- [ ] Include audit/logging where applicable
- [ ] Not introduce regressions in critical flows

