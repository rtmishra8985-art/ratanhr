using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Performance;

public class ContinuousFeedback : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public string FromEmployeeId { get; set; } = string.Empty;
    public string ToEmployeeId { get; set; } = string.Empty;
    public string FeedbackText { get; set; } = string.Empty;
    public string FeedbackType { get; set; } = "Praise"; // Praise, Suggestion, Concern
    public bool IsAnonymous { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
