using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Application.DTOs.Employee;

public class CreateEmployeeDto
{
    // Personal
    public string FullName { get; set; } = string.Empty;

    /// <summary>Given name — tests and new-style callers use FirstName.</summary>
    public string? FirstName { get; set; }

    /// <summary>Family name — tests and new-style callers use LastName.</summary>
    public string? LastName { get; set; }

    public string? Gender { get; set; }
    public string? Dob { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Nationality { get; set; }
    public string? MaritalStatus { get; set; }
    public string? BloodGroup { get; set; }
    public string? PermanentAddress { get; set; }
    public string? CurrentAddress { get; set; }
    public string? Aadhaar { get; set; }
    public string? Pan { get; set; }
    public string? MedicalConditions { get; set; }
    public string? Hobbies { get; set; }
    public string? Languages { get; set; }

    // Contact
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }

    // Employment
    public string? Doj { get; set; }
    /// <summary>Date of joining — typed alias for Doj.</summary>
    public DateOnly? DateOfJoining { get; set; }
    public string Designation { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    /// <summary>Department FK — new-style tests pass DepartmentId.</summary>
    public int? DepartmentId { get; set; }
    public string? Skills { get; set; }
    public string? Responsibilities { get; set; }
    /// <summary>Employee status: Active | Inactive | Terminated.</summary>
    public string? Status { get; set; }

    // Bank
    public string? BankAccountHolder { get; set; }
    public string? BankName { get; set; }
    public string? BranchName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    public string? Uan { get; set; }

    // Education
    public string? Qualification { get; set; }
    public string? Institution { get; set; }
    public int? YearOfPassing { get; set; }
    public string? Specialization { get; set; }

    // Experience
    public string? PreviousEmployer { get; set; }
    public string? JobTitle { get; set; }
    public string? Duration { get; set; }
    public string? ExpResponsibilities { get; set; }

    // Emergency
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactAddress { get; set; }

    // Generated
    public int? CompanyId { get; set; }
}

public class EmployeeListDto
{
    /// <summary>Int primary-key of the employee row. Tests assert this as int.</summary>
    public int EmployeeId { get; set; }

    /// <summary>Tenant discriminator — needed for IDOR scoping in list endpoints.</summary>
    public int? CompanyId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? Doj { get; set; }
    public bool IsActive { get; set; }
    public string? PassportPhoto { get; set; }
    public string? Gender { get; set; }

    /// <summary>Employee status: Active | Inactive | Terminated.</summary>
    public string Status { get; set; } = string.Empty;
}

public class EmployeeDetailDto : CreateEmployeeDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? PassportPhoto { get; set; }
    public string? IdentityDocs { get; set; }
    public string? EducationalDocs { get; set; }
    public string? ExperienceDocs { get; set; }
    public DateTime CreatedAt { get; set; }

    // FIX MED-9: Shadow PII fields from CreateEmployeeDto so they are
    // always null in the standard detail response. PII is only available
    // via GET /api/employees/{id}/pii (requires PII_VIEWER role).
    // JsonIgnore ensures they are omitted from the serialised JSON entirely.
    [System.Text.Json.Serialization.JsonIgnore]
    public new string? Aadhaar { get; private set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public new string? Pan { get; private set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public new string? AccountNumber { get; private set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public new string? IfscCode { get; private set; }
}
