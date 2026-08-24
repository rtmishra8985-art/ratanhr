# ApplicationDbContext Patch Instructions

Add the following `using` statements at the top of `ApplicationDbContext.cs`:

```csharp
using HRMS.Domain.Entities.Travel;    // TravelApproval, TravelHistory — already has TravelRequest
using HRMS.Domain.Entities.Expense;   // ExpenseItem, ExpenseAttachment, ExpenseApproval, ExpenseHistory
using HRMS.Domain.Entities.Attendance; // AttendanceGps, GeoFence, GeoFenceHistory, AttendanceLocationAudit, AttendanceDevice
```

Add the following `DbSet` properties inside `ApplicationDbContext`, grouped with their related sets:

```csharp
// ── Travel (enhanced) ─────────────────────────────────────────────────────
public DbSet<TravelRequest>   TravelRequests   => Set<TravelRequest>();    // already exists — verify
public DbSet<TravelApproval>  TravelApprovals  => Set<TravelApproval>();   // NEW
public DbSet<TravelHistory>   TravelHistories  => Set<TravelHistory>();    // NEW

// ── Expense (enhanced) ────────────────────────────────────────────────────
public DbSet<ExpenseClaim>      ExpenseClaims      => Set<ExpenseClaim>();     // already exists — verify
public DbSet<ExpenseItem>       ExpenseItems       => Set<ExpenseItem>();      // NEW
public DbSet<ExpenseAttachment> ExpenseAttachments => Set<ExpenseAttachment>(); // NEW
public DbSet<ExpenseApproval>   ExpenseApprovals   => Set<ExpenseApproval>();  // NEW
public DbSet<ExpenseHistory>    ExpenseHistories   => Set<ExpenseHistory>();   // NEW

// ── GPS Attendance ────────────────────────────────────────────────────────
public DbSet<AttendanceGps>          AttendanceGpsLogs      => Set<AttendanceGps>();          // NEW
public DbSet<GeoFence>               GeoFences              => Set<GeoFence>();               // NEW
public DbSet<GeoFenceHistory>        GeoFenceHistories      => Set<GeoFenceHistory>();        // NEW
public DbSet<AttendanceLocationAudit> AttendanceLocationAudits => Set<AttendanceLocationAudit>(); // NEW
public DbSet<AttendanceDevice>       AttendanceDevices      => Set<AttendanceDevice>();       // NEW
```

Add the following EF model configurations inside `OnModelCreating`, after the existing entity configs:

```csharp
// ── TravelApproval ─────────────────────────────────────────────────────────
mb.Entity<TravelApproval>(e => {
    e.ToTable("travel_approvals"); e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
    e.Property(x => x.CompanyId).HasColumnName("company_id");
    e.Property(x => x.TravelRequestId).HasColumnName("travel_request_id");
    e.Property(x => x.Step).HasColumnName("step").IsRequired().HasMaxLength(30);
    e.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(30).HasDefaultValue("Pending");
    e.Property(x => x.ApproverId).HasColumnName("approver_id");
    e.Property(x => x.ApproverName).HasColumnName("approver_name").HasMaxLength(255);
    e.Property(x => x.Comments).HasColumnName("comments");
    e.Property(x => x.ActionAt).HasColumnName("action_at");
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.HasOne(x => x.TravelRequest).WithMany(t => t.Approvals)
        .HasForeignKey(x => x.TravelRequestId).OnDelete(DeleteBehavior.Cascade);
    e.HasIndex(x => x.TravelRequestId);
    e.HasIndex(x => x.CompanyId);
});

// ── TravelHistory ──────────────────────────────────────────────────────────
mb.Entity<TravelHistory>(e => {
    e.ToTable("travel_history"); e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
    e.Property(x => x.CompanyId).HasColumnName("company_id");
    e.Property(x => x.TravelRequestId).HasColumnName("travel_request_id");
    e.Property(x => x.Action).HasColumnName("action").IsRequired().HasMaxLength(200);
    e.Property(x => x.PreviousStatus).HasColumnName("previous_status").HasMaxLength(50);
    e.Property(x => x.NewStatus).HasColumnName("new_status").HasMaxLength(50);
    e.Property(x => x.PerformedBy).HasColumnName("performed_by").HasMaxLength(100);
    e.Property(x => x.PerformedByName).HasColumnName("performed_by_name").HasMaxLength(255);
    e.Property(x => x.Remarks).HasColumnName("remarks");
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.HasOne(x => x.TravelRequest).WithMany(t => t.History)
        .HasForeignKey(x => x.TravelRequestId).OnDelete(DeleteBehavior.Cascade);
    e.HasIndex(x => x.TravelRequestId);
});

// ── TravelRequest (enhanced columns) ───────────────────────────────────────
// Extend existing config by appending these mappings to the TravelRequest entity block:
// e.Property(x => x.TravelType).HasColumnName("travel_type").IsRequired().HasMaxLength(30).HasDefaultValue("Domestic");
// e.Property(x => x.FromCity).HasColumnName("from_city").IsRequired().HasMaxLength(200);
// e.Property(x => x.ToCity).HasColumnName("to_city").IsRequired().HasMaxLength(200);
// e.Property(x => x.StartDate).HasColumnName("start_date");
// e.Property(x => x.EndDate).HasColumnName("end_date");
// e.Property(x => x.ModeOfTravel).HasColumnName("mode_of_travel").IsRequired().HasMaxLength(50);
// e.Property(x => x.AdvanceRequired).HasColumnName("advance_required").HasDefaultValue(false);
// e.Property(x => x.AdvanceAmount).HasColumnName("advance_amount").HasColumnType("numeric(18,2)");
// e.Property(x => x.AttachmentPath).HasColumnName("attachment_path");
// e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
// e.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
// e.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
// e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
// e.HasMany(t => t.Approvals).WithOne(a => a.TravelRequest).HasForeignKey(a => a.TravelRequestId);
// e.HasMany(t => t.History).WithOne(h => h.TravelRequest).HasForeignKey(h => h.TravelRequestId);

// ── ExpenseItem ────────────────────────────────────────────────────────────
mb.Entity<ExpenseItem>(e => {
    e.ToTable("expense_items"); e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
    e.Property(x => x.CompanyId).HasColumnName("company_id");
    e.Property(x => x.ExpenseClaimId).HasColumnName("expense_claim_id");
    e.Property(x => x.Category).HasColumnName("category").IsRequired().HasMaxLength(50).HasDefaultValue("Miscellaneous");
    e.Property(x => x.Description).HasColumnName("description").IsRequired().HasMaxLength(500);
    e.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
    e.Property(x => x.GstAmount).HasColumnName("gst_amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
    e.Property(x => x.Currency).HasColumnName("currency").IsRequired().HasMaxLength(10).HasDefaultValue("INR");
    e.Property(x => x.ExpenseDate).HasColumnName("expense_date");
    e.Property(x => x.ReceiptPath).HasColumnName("receipt_path");
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.HasOne(x => x.ExpenseClaim).WithMany(c => c.Items)
        .HasForeignKey(x => x.ExpenseClaimId).OnDelete(DeleteBehavior.Cascade);
    e.HasIndex(x => x.ExpenseClaimId);
});

// ── ExpenseAttachment ──────────────────────────────────────────────────────
mb.Entity<ExpenseAttachment>(e => {
    e.ToTable("expense_attachments"); e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
    e.Property(x => x.CompanyId).HasColumnName("company_id");
    e.Property(x => x.ExpenseClaimId).HasColumnName("expense_claim_id");
    e.Property(x => x.FileName).HasColumnName("file_name").IsRequired().HasMaxLength(500);
    e.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
    e.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100);
    e.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes");
    e.Property(x => x.UploadedBy).HasColumnName("uploaded_by").HasMaxLength(100);
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.HasOne(x => x.ExpenseClaim).WithMany(c => c.Attachments)
        .HasForeignKey(x => x.ExpenseClaimId).OnDelete(DeleteBehavior.Cascade);
    e.HasIndex(x => x.ExpenseClaimId);
});

// ── ExpenseApproval ────────────────────────────────────────────────────────
mb.Entity<ExpenseApproval>(e => {
    e.ToTable("expense_approvals"); e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
    e.Property(x => x.CompanyId).HasColumnName("company_id");
    e.Property(x => x.ExpenseClaimId).HasColumnName("expense_claim_id");
    e.Property(x => x.Step).HasColumnName("step").IsRequired().HasMaxLength(30);
    e.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(30).HasDefaultValue("Pending");
    e.Property(x => x.ApproverId).HasColumnName("approver_id");
    e.Property(x => x.ApproverName).HasColumnName("approver_name").HasMaxLength(255);
    e.Property(x => x.Comments).HasColumnName("comments");
    e.Property(x => x.ActionAt).HasColumnName("action_at");
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.HasOne(x => x.ExpenseClaim).WithMany(c => c.Approvals)
        .HasForeignKey(x => x.ExpenseClaimId).OnDelete(DeleteBehavior.Cascade);
    e.HasIndex(x => x.ExpenseClaimId);
});

// ── ExpenseHistory ─────────────────────────────────────────────────────────
mb.Entity<ExpenseHistory>(e => {
    e.ToTable("expense_history"); e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
    e.Property(x => x.CompanyId).HasColumnName("company_id");
    e.Property(x => x.ExpenseClaimId).HasColumnName("expense_claim_id");
    e.Property(x => x.Action).HasColumnName("action").IsRequired().HasMaxLength(200);
    e.Property(x => x.PreviousStatus).HasColumnName("previous_status").HasMaxLength(50);
    e.Property(x => x.NewStatus).HasColumnName("new_status").HasMaxLength(50);
    e.Property(x => x.PerformedBy).HasColumnName("performed_by").HasMaxLength(100);
    e.Property(x => x.PerformedByName).HasColumnName("performed_by_name").HasMaxLength(255);
    e.Property(x => x.Remarks).HasColumnName("remarks");
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.HasOne(x => x.ExpenseClaim).WithMany(c => c.History)
        .HasForeignKey(x => x.ExpenseClaimId).OnDelete(DeleteBehavior.Cascade);
    e.HasIndex(x => x.ExpenseClaimId);
});

// ── ExpenseClaim (enhanced columns) ─────────────────────────────────────────
// Extend existing config:
// e.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
// e.Property(x => x.TotalGst).HasColumnName("total_gst").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
// e.Property(x => x.TravelRequestId).HasColumnName("travel_request_id");
// e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
// e.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
// e.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
// e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
// e.HasMany(c => c.Items).WithOne(i => i.ExpenseClaim).HasForeignKey(i => i.ExpenseClaimId);
// e.HasMany(c => c.Attachments).WithOne(a => a.ExpenseClaim).HasForeignKey(a => a.ExpenseClaimId);
// e.HasMany(c => c.Approvals).WithOne(a => a.ExpenseClaim).HasForeignKey(a => a.ExpenseClaimId);
// e.HasMany(c => c.History).WithOne(h => h.ExpenseClaim).HasForeignKey(h => h.ExpenseClaimId);

// ── GeoFence ───────────────────────────────────────────────────────────────
mb.Entity<GeoFence>(e => {
    e.ToTable("geofences"); e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
    e.Property(x => x.CompanyId).HasColumnName("company_id");
    e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
    e.Property(x => x.FenceType).HasColumnName("fence_type").IsRequired().HasMaxLength(30).HasDefaultValue("Office");
    e.Property(x => x.Latitude).HasColumnName("latitude");
    e.Property(x => x.Longitude).HasColumnName("longitude");
    e.Property(x => x.RadiusMetres).HasColumnName("radius_metres").HasDefaultValue(200.0);
    e.Property(x => x.BranchId).HasColumnName("branch_id");
    e.Property(x => x.Address).HasColumnName("address");
    e.Property(x => x.AllowOutsideCheckin).HasColumnName("allow_outside_checkin").HasDefaultValue(false);
    e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
    e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
    e.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
    e.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    e.HasMany(f => f.History).WithOne(h => h.GeoFence).HasForeignKey(h => h.GeoFenceId);
    e.HasMany(f => f.GpsLogs).WithOne(g => g.GeoFence).HasForeignKey(g => g.GeoFenceId);
    e.HasIndex(x => x.CompanyId);
    e.HasIndex(x => new { x.CompanyId, x.IsActive });
});

// ── GeoFenceHistory ────────────────────────────────────────────────────────
mb.Entity<GeoFenceHistory>(e => {
    e.ToTable("geofence_history"); e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
    e.Property(x => x.CompanyId).HasColumnName("company_id");
    e.Property(x => x.GeoFenceId).HasColumnName("geofence_id");
    e.Property(x => x.Action).HasColumnName("action").IsRequired().HasMaxLength(50);
    e.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(100);
    e.Property(x => x.ChangeDetails).HasColumnName("change_details");
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.HasOne(x => x.GeoFence).WithMany(f => f.History)
        .HasForeignKey(x => x.GeoFenceId).OnDelete(DeleteBehavior.Cascade);
    e.HasIndex(x => x.GeoFenceId);
});

// ── AttendanceGps ──────────────────────────────────────────────────────────
mb.Entity<AttendanceGps>(e => {
    e.ToTable("attendance_gps"); e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
    e.Property(x => x.CompanyId).HasColumnName("company_id");
    e.Property(x => x.WebAttendanceId).HasColumnName("web_attendance_id");
    e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired().HasMaxLength(100);
    e.Property(x => x.Latitude).HasColumnName("latitude");
    e.Property(x => x.Longitude).HasColumnName("longitude");
    e.Property(x => x.Accuracy).HasColumnName("accuracy");
    e.Property(x => x.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(20).HasDefaultValue("CheckIn");
    e.Property(x => x.Timestamp).HasColumnName("timestamp");
    e.Property(x => x.GeoFenceId).HasColumnName("geofence_id");
    e.Property(x => x.DistanceMetres).HasColumnName("distance_metres");
    e.Property(x => x.IsInsideGeofence).HasColumnName("is_inside_geofence");
    e.Property(x => x.DeviceType).HasColumnName("device_type").HasMaxLength(50);
    e.Property(x => x.Browser).HasColumnName("browser").HasMaxLength(200);
    e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
    e.Property(x => x.Network).HasColumnName("network").HasMaxLength(30);
    e.Property(x => x.BatteryLevel).HasColumnName("battery_level");
    e.Property(x => x.GpsStatus).HasColumnName("gps_status").HasMaxLength(30);
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.HasOne(x => x.GeoFence).WithMany(f => f.GpsLogs)
        .HasForeignKey(x => x.GeoFenceId).OnDelete(DeleteBehavior.SetNull);
    e.HasIndex(x => x.CompanyId);
    e.HasIndex(x => x.EmployeeId);
    e.HasIndex(x => x.Timestamp);
    e.HasIndex(x => x.GeoFenceId);
});

// ── AttendanceLocationAudit ────────────────────────────────────────────────
mb.Entity<AttendanceLocationAudit>(e => {
    e.ToTable("attendance_location_audit"); e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
    e.Property(x => x.CompanyId).HasColumnName("company_id");
    e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired().HasMaxLength(100);
    e.Property(x => x.Latitude).HasColumnName("latitude");
    e.Property(x => x.Longitude).HasColumnName("longitude");
    e.Property(x => x.Accuracy).HasColumnName("accuracy");
    e.Property(x => x.GeoFenceId).HasColumnName("geofence_id");
    e.Property(x => x.DistanceMetres).HasColumnName("distance_metres");
    e.Property(x => x.IsInsideGeofence).HasColumnName("is_inside_geofence");
    e.Property(x => x.WasAllowed).HasColumnName("was_allowed");
    e.Property(x => x.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(20);
    e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
    e.Property(x => x.Browser).HasColumnName("browser").HasMaxLength(200);
    e.Property(x => x.DeviceType).HasColumnName("device_type").HasMaxLength(50);
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.HasIndex(x => new { x.CompanyId, x.EmployeeId });
    e.HasIndex(x => x.CreatedAt);
});

// ── AttendanceDevice ───────────────────────────────────────────────────────
mb.Entity<AttendanceDevice>(e => {
    e.ToTable("attendance_devices"); e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
    e.Property(x => x.CompanyId).HasColumnName("company_id");
    e.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired().HasMaxLength(100);
    e.Property(x => x.DeviceFingerprint).HasColumnName("device_fingerprint").IsRequired().HasMaxLength(512);
    e.Property(x => x.DeviceType).HasColumnName("device_type").HasMaxLength(50);
    e.Property(x => x.Browser).HasColumnName("browser").HasMaxLength(200);
    e.Property(x => x.LastIpAddress).HasColumnName("last_ip_address").HasMaxLength(50);
    e.Property(x => x.IsTrusted).HasColumnName("is_trusted").HasDefaultValue(true);
    e.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at");
    e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
    e.Property(x => x.UseCount).HasColumnName("use_count").HasDefaultValue(1);
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    e.HasIndex(x => new { x.EmployeeId, x.DeviceFingerprint });
});
```
