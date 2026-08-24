using System.ComponentModel.DataAnnotations.Schema;
namespace HRMS.Domain.Entities;

/// <summary>
/// Immutable audit record written whenever a security-significant or data-changing
/// action is performed. Never deleted.
/// </summary>
public class AuditLog
{
    public long   Id          { get; set; }
    public string Action      { get; set; } = string.Empty; // e.g. "LOGIN_SUCCESS", "EMPLOYEE_CREATE"
    public string EntityType  { get; set; } = string.Empty; // e.g. "Employee", "Payslip"
    public string? EntityId   { get; set; }                  // PK of the affected row
    public int?   PerformedBy { get; set; }                  // User.Id (null for anonymous)
    /// <summary>Alias for PerformedBy — tests use ActorId.</summary>
    [NotMapped] public int? ActorId { get => PerformedBy; set => PerformedBy = value; }
    public string? PerformedByName { get; set; }
    /// <summary>Alias for PerformedByName — tests use ActorName.</summary>
    [NotMapped] public string? ActorName { get => PerformedByName; set => PerformedByName = value; }
    public string? IpAddress  { get; set; }
    public string? Details    { get; set; }
    public bool   Success     { get; set; } = true;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    /// <summary>Alias for OccurredAt — tests use Timestamp.</summary>
    [NotMapped] public DateTime Timestamp { get => OccurredAt; set => OccurredAt = value; }
    /// <summary>Tenant discriminator — added for company-scoped log queries.</summary>
    public int? CompanyId { get; set; }
}
