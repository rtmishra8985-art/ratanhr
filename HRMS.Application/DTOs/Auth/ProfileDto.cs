namespace HRMS.Application.DTOs.Auth;

public class UserProfileDto
{
    public int      Id                 { get; set; }
    public string   Email              { get; set; } = string.Empty;
    public string?  FullName           { get; set; }
    public string   Role               { get; set; } = string.Empty;
    public string?  AdminRole          { get; set; }
    public int?     CompanyId          { get; set; }
    public string?  EmployeeId         { get; set; }
    public string?  ProfilePicturePath { get; set; }
    public bool     IsActive           { get; set; }
    public DateTime CreatedAt          { get; set; }
}

public class UpdateProfileDto
{
    public string? FullName { get; set; }
}
