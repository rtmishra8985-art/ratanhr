namespace HRMS.Application.DTOs.Employee;

/// <summary>
/// DTO for the employee self-service profile-update endpoint (PUT /api/my/profile).
/// Contains only the fields an employee is permitted to edit on their own record.
/// Fields that are admin-controlled (CompanyId, Designation, Department, EmployeeId,
/// DateOfJoining, etc.) are intentionally absent to prevent privilege escalation.
/// </summary>
public class UpdateSelfProfileDto
{
    // Personal information
    public string? Gender { get; set; }
    public string? Dob { get; set; }
    public string? Nationality { get; set; }
    public string? MaritalStatus { get; set; }
    public string? BloodGroup { get; set; }
    public string? PermanentAddress { get; set; }
    public string? CurrentAddress { get; set; }

    // Contact
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactAddress { get; set; }

    // Bank details (employee may update their own bank info)
    public string? BankAccountHolder { get; set; }
    public string? BankName { get; set; }
    public string? BranchName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    public string? Uan { get; set; }

    // Education (self-reported)
    public string? Qualification { get; set; }
    public string? Institution { get; set; }
    public int? YearOfPassing { get; set; }
    public string? Specialization { get; set; }

    // Experience (self-reported)
    public string? PreviousEmployer { get; set; }
    public string? JobTitle { get; set; }
    public string? Duration { get; set; }
    public string? ExpResponsibilities { get; set; }

    // Miscellaneous personal
    public string? Hobbies { get; set; }
    public string? Languages { get; set; }
    public string? Skills { get; set; }
    public string? MedicalConditions { get; set; }
}
