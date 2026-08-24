using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Training;

public class TrainingProgram : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Trainer { get; set; }
    public int MaxSeats { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
