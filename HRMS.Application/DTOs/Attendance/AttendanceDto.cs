namespace HRMS.Application.DTOs.Attendance;

/// <summary>
/// Flat read projection of WebAttendance used by AutoMapper tests and
/// lightweight attendance summary responses.
/// </summary>
public class AttendanceDto
{
    public int      AttendanceId { get; set; }
    public string   EmployeeId   { get; set; } = string.Empty;
    public int?     CompanyId    { get; set; }
    public DateOnly Date         { get; set; }
    public TimeOnly? CheckIn     { get; set; }
    public TimeOnly? CheckOut    { get; set; }
    public string   Status       { get; set; } = string.Empty;
    public DateTime CreatedAt    { get; set; }
}

public class WebCheckInDto
{
    public string EmployeeId { get; set; } = string.Empty;
}

public class WebCheckOutDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public int AttendanceId { get; set; }
}

/// <summary>
/// Body for the legacy admin PATCH endpoint — kept for backward compatibility.
/// For back-dated edits with a mandatory reason, use <see cref="EditAttendanceDto"/>.
/// </summary>
public class UpdateAttendanceStatusDto
{
    public int    AttendanceId { get; set; }
    public string Status       { get; set; } = "Present";
}

/// <summary>
/// Body for the admin attendance-edit endpoint (replaces UpdateAttendanceStatusDto).
/// A <see cref="Reason"/> is mandatory — all admin edits are audited.
/// </summary>
public class EditAttendanceDto
{
    /// <summary>Primary key of the WebAttendance record to update.</summary>
    public int    AttendanceId { get; set; }

    /// <summary>New status: Present | Absent | Half Day | Leave | Holiday | Weekend</summary>
    public string Status       { get; set; } = "Present";

    /// <summary>
    /// Mandatory justification for the edit (minimum 10 characters).
    /// Stored in AuditLog.Details for compliance and back-dated-attendance traceability.
    /// </summary>
    public string Reason       { get; set; } = string.Empty;
}

public class AttendanceFilterDto
{
    public string? EmployeeId { get; set; }
    public string? StartDate  { get; set; }
    public string? EndDate    { get; set; }
    public string? Status     { get; set; }
    /// <summary>When set (admin/superadmin only), filters records to a specific company.</summary>
    public int?    CompanyId  { get; set; }
}

public class WebAttendanceDto
{
    public int      Id           { get; set; }
    public string   EmployeeId   { get; set; } = string.Empty;
    public string   EmployeeName { get; set; } = string.Empty;
    public string   AttDate      { get; set; } = string.Empty;
    public string?  CheckIn      { get; set; }
    public string?  CheckOut     { get; set; }
    public string   Status       { get; set; } = string.Empty;
    public decimal? HoursWorked  { get; set; }
    /// <summary>Populated when the record was edited by an admin and a reason was logged.</summary>
    public string?  AdminEditReason { get; set; }
}

public class ExcelAttendanceDto
{
    public int      Id           { get; set; }
    public string   EmployeeId   { get; set; } = string.Empty;
    public string   EmployeeName { get; set; } = string.Empty;
    public string   AttDate      { get; set; } = string.Empty;
    public string   Status       { get; set; } = string.Empty;
    public decimal? HoursWorked  { get; set; }
}

// ── Status update body (moved from AttendanceController) ──────────────────
public class UpdateStatusBody
{
    public string Status { get; set; } = "Present";
}

/// <summary>
/// Returned by <c>POST /api/attendance/excel/upload</c>.
/// Provides per-row import counts so callers can detect partial failures
/// without re-querying the database.
/// </summary>
public class ExcelUploadResult
{
    /// <summary>Number of rows successfully imported.</summary>
    public int Imported { get; set; }

    /// <summary>
    /// Number of rows skipped because they were missing required fields
    /// (EmployeeId or Date) or contained an unparseable date.
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Human-readable descriptions of any per-row validation errors, capped at 50 entries
    /// to avoid flooding the response for large uploads with systemic format issues.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
