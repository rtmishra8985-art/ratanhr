using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Attendance;

public class ExcelAttendance : ICompanyOwned
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public DateOnly AttDate { get; set; }
    public string Status { get; set; } = "Present";
    public decimal? HoursWorked { get; set; }
    public int? CompanyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
