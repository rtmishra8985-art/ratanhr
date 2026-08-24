namespace HRMS.Application.DTOs.Employee;

/// <summary>
/// Fields the caller may update on an employee record via the int-PK–based
/// UpdateEmployeeAsync path.  All properties are nullable so callers can
/// supply a partial update (only the fields they want to change).
/// </summary>
public class UpdateEmployeeDto
{
    public string? FirstName    { get; set; }
    public string? LastName     { get; set; }
    public int?    DepartmentId { get; set; }
}
