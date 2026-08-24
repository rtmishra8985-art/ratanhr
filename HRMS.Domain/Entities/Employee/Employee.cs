using HRMS.Domain.Entities;
using HRMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Domain.Entities.Employee;

public class Employee : ICompanyOwned
{
    public int Id { get; set; }
    /// <summary>Domain-prefixed int PK alias — maps to Id. Tests use EmployeeId as int.</summary>
    [NotMapped] public int EmployeeId { get => Id; set => Id = value; }

    // ── String business key (maps to employee_id column) ──────────────────
    /// <summary>
    /// Auto-generated employee code, e.g. EMP1234. Formerly EmployeeId — renamed to
    /// EmployeeCode to free the name for the int PK alias above.
    /// </summary>
    public string EmployeeCode { get; set; } = string.Empty;

    public int? UserId { get; set; }
    // FIX CRIT-1: CompanyId is NOT NULL at the DB level (migration 20260726000001_MySqlInitialSchema).
    // Changed from int? to int to keep the C# model consistent with the DB constraint.
    // EF Core will no longer generate nullable column mappings for this property.
    public int CompanyId { get; set; }
    // Explicit interface implementation: ICompanyOwned requires int? but the column is NOT NULL.
    // Widening conversion preserves the int value while satisfying the interface contract.
    int? ICompanyOwned.CompanyId => CompanyId;

    // Personal Info
    /// <summary>Given name. Tests use FirstName.</summary>
    public string? FirstName { get; set; }
    /// <summary>Family name. Tests use LastName.</summary>
    public string? LastName { get; set; }
    /// <summary>Full name stored for backward compatibility. Derived callers may also use FirstName+LastName.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Presentation-safe name. Rows created before FullName was populated (or created
    /// through paths that only set FirstName/LastName) would otherwise render blank in
    /// lists, reports and payslips. Falls back to "FirstName LastName".
    /// Not persisted — FullName remains the stored column.
    /// </summary>
    [NotMapped]
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(FullName)
            ? FullName
            : $"{FirstName} {LastName}".Trim();

    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Nationality { get; set; }
    public string? MaritalStatus { get; set; }
    public string? BloodGroup { get; set; }
    public string? PermanentAddress { get; set; }
    public string? CurrentAddress { get; set; }
    public string? Aadhaar { get; set; }
    public string? PAN { get; set; }
    public string? IdentityDocs { get; set; }
    public string? MedicalConditions { get; set; }
    public string? Hobbies { get; set; }
    public string? Languages { get; set; }

    // Contact
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }

    // Employment Info
    public DateOnly? DateOfJoining { get; set; }
    public string? Designation { get; set; }
    /// <summary>Free-text department name — kept for backward compatibility.</summary>
    public string? Department { get; set; }
    public string? Skills { get; set; }
    public string? Responsibilities { get; set; }
    /// <summary>Employee status: Active | Inactive | Terminated.</summary>
    public string Status { get; set; } = "Active";

    // FIX 6: Department FK — additive only.
    public int? DepartmentId { get; set; }
    public Department? DepartmentEntity { get; set; }

    // Bank Details
    public string? BankAccountHolder { get; set; }
    public string? BankName { get; set; }
    public string? BranchName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IFSCCode { get; set; }
    public string? UAN { get; set; }

    // Education
    public string? Qualification { get; set; }
    public string? Institution { get; set; }
    public int? YearOfPassing { get; set; }
    public string? Specialization { get; set; }
    public string? EducationalDocs { get; set; }
    public string? PassportPhoto { get; set; }

    // Experience
    public string? PreviousEmployer { get; set; }
    public string? JobTitle { get; set; }
    public string? Duration { get; set; }
    public string? ExpResponsibilities { get; set; }
    public string? ExperienceDocs { get; set; }

    // Emergency Contact
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactAddress { get; set; }

    public int? ShiftId { get; set; }

    public bool IsActive { get; set; } = true;
    /// <summary>Demo employee marker — true indicates this is a test/demo employee created for seed operations.</summary>
    public bool IsDemo { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
