namespace HRMS.Domain.Entities.Training;

public class TrainingEnrollment
{
    public int Id { get; set; }
    public int TrainingProgramId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    /// <summary>Enrolled | Completed | Cancelled</summary>
    public string Status { get; set; } = "Enrolled";
    public DateTime? CompletionDate { get; set; }
    public string? CertificatePath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TrainingProgram? TrainingProgram { get; set; }
}
