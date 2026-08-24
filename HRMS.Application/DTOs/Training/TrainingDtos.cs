namespace HRMS.Application.DTOs.Training;

public class CreateTrainingDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Trainer { get; set; }
    public int MaxSeats { get; set; }
}

public class TrainingDto
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Trainer { get; set; }
    public int MaxSeats { get; set; }
    public bool IsActive { get; set; }
    public int EnrolledCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EnrollmentDto
{
    public int Id { get; set; }
    public int TrainingProgramId { get; set; }
    public string TrainingTitle { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletionDate { get; set; }
    public string? CertificatePath { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EnrollDto
{
    public string EmployeeId { get; set; } = string.Empty;
}

public class MarkCompleteDto
{
    public DateTime? CompletionDate { get; set; }
    public string? CertificatePath { get; set; }
}
