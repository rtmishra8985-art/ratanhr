using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Payroll;

/// <summary>
/// Phase 1 – B: Represents an administrative lock on a payroll period
/// (company/month/year combination). When <see cref="IsLocked"/> is true,
/// all write operations that affect that period — salary edits, attendance edits,
/// leave approve/cancel, payslip generate/delete — are blocked.
/// One row per company/month/year (unique index); re-locking updates the existing row.
/// </summary>
public class PayrollLock : ICompanyOwned
{
    public int      Id                  { get; set; }
    public int      CompanyId           { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int      Month               { get; set; }  // 1–12
    public int      Year                { get; set; }

    public bool     IsLocked            { get; set; } = true;

    public DateTime LockedAt            { get; set; }
    public int      LockedByUserId      { get; set; }

    /// <summary>Set when the period is unlocked (null while locked).</summary>
    public DateTime? UnlockedAt         { get; set; }
    public int?      UnlockedByUserId   { get; set; }

    /// <summary>Optional admin note explaining why the period was locked or unlocked.</summary>
    public string?  Notes               { get; set; }

    /// <summary>
    /// Optimistic concurrency token backed by MySQL row version (TIMESTAMP(6) auto-updated column).
    /// Prevents concurrent lock/unlock operations from silently overwriting each other.
    /// EF Core raises <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
    /// on concurrent writes. Configured via <c>IsRowVersion()</c> in ApplicationDbContext.
    /// Replaces the PostgreSQL xmin-based uint Version property.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
