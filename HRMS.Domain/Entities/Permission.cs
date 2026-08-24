namespace HRMS.Domain.Entities;

public class Permission
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    // Fine-grained employee CRUD
    public bool AddEmployee { get; set; } = true;
    public bool EditEmployee { get; set; } = true;
    public bool ViewEmployee { get; set; } = true;
    public bool DeleteEmployee { get; set; } = true;
    // Attendance
    public bool AttendanceUpload { get; set; } = true;
    // Legacy / coarser-grained aliases kept for backward compat
    public bool EmployeeRegistration { get => AddEmployee; set => AddEmployee = value; }
    public bool ViewAllEmployees { get => ViewEmployee; set => ViewEmployee = value; }
    public bool CompanyDetails { get; set; } = true;
    public bool WebAttendanceView { get; set; } = true;
    public bool ExcelAttendanceUpload { get => AttendanceUpload; set => AttendanceUpload = value; }
    public bool ExcelAttendanceView { get; set; } = true;
    public bool PayrollView { get; set; } = true;
    public bool PayrollGenerate { get; set; } = true;
    public bool ReportsAttendance { get; set; } = true;
    public bool ReportsEmployee { get; set; } = true;
    public bool Appreciation { get; set; } = true;
    public bool LogoUpload { get; set; } = true;
    public bool ManageAdminUsers { get; set; } = true;
    public bool LeaveManagement { get; set; } = true;

    // ── Mini CRM (Sales) – module-level permissions ────────────────────────
    public bool SalesView   { get; set; } = false;
    public bool SalesCreate { get; set; } = false;
    public bool SalesEdit   { get; set; } = false;
    public bool SalesDelete { get; set; } = false;

    // ── Lead permissions ───────────────────────────────────────────────────
    public bool LeadView   { get; set; } = false;
    public bool LeadCreate { get; set; } = false;
    public bool LeadEdit   { get; set; } = false;
    public bool LeadDelete { get; set; } = false;

    /// <summary>Can assign a lead to a sales executive (first assignment).</summary>
    public bool LeadAssign { get; set; } = false;

    /// <summary>Can reassign a lead that is already owned by another executive.</summary>
    public bool LeadReassign { get; set; } = false;

    /// <summary>Can view leads assigned to themselves.</summary>
    public bool LeadViewAssigned { get; set; } = false;

    /// <summary>Can view all leads in the company regardless of assignment.</summary>
    public bool LeadViewAll { get; set; } = false;

    // ── Customer permissions ───────────────────────────────────────────────
    public bool CustomerView   { get; set; } = false;
    public bool CustomerCreate { get; set; } = false;
    public bool CustomerEdit   { get; set; } = false;
    public bool CustomerDelete { get; set; } = false;

    // ── Meeting permissions ────────────────────────────────────────────────
    public bool MeetingView   { get; set; } = false;
    public bool MeetingCreate { get; set; } = false;
    public bool MeetingEdit   { get; set; } = false;
    public bool MeetingDelete { get; set; } = false;

    // ── Visit permissions ──────────────────────────────────────────────────
    public bool VisitView   { get; set; } = false;
    public bool VisitCreate { get; set; } = false;
    public bool VisitEdit   { get; set; } = false;
    public bool VisitDelete { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
