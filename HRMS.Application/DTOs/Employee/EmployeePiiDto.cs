using System.Text.Json.Serialization;

namespace HRMS.Application.DTOs.Employee;

/// <summary>
/// FIX MED-9: PII-gated DTO for sensitive employee fields.
/// Only returned by the dedicated <c>GET /api/employees/{id}/pii</c> endpoint
/// which requires the <c>PII_VIEWER</c> role.
/// This DTO is intentionally separate from <see cref="EmployeeDetailDto"/> so that
/// the standard employee detail endpoint never exposes PII regardless of caller role.
/// </summary>
public class EmployeePiiDto
{
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>Aadhaar number — AES-256 encrypted at rest; returned masked (last 4 digits only).</summary>
    public string? AadhaarMasked { get; set; }

    /// <summary>PAN number — AES-256 encrypted at rest; returned masked (first 5 chars replaced).</summary>
    public string? PanMasked { get; set; }

    /// <summary>Bank account number — AES-256 encrypted at rest; returned masked (last 4 digits only).</summary>
    public string? AccountNumberMasked { get; set; }

    /// <summary>IFSC code — not PII but co-located with bank details.</summary>
    public string? IFSCCode { get; set; }

    /// <summary>UAN (Universal Account Number for PF) — returned in full; required for payroll.</summary>
    public string? UAN { get; set; }

    /// <summary>
    /// Raw (unmasked) values — only populated when the caller holds the
    /// <c>PII_VIEWER</c> role AND explicitly requests full data via <c>?unmask=true</c>.
    /// Default: null (masked values used).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PiiRawValues? Raw { get; set; }
}

/// <summary>Unmasked PII — only returned when explicitly requested by a PII_VIEWER.</summary>
public class PiiRawValues
{
    public string? Aadhaar { get; set; }
    public string? Pan { get; set; }
    public string? AccountNumber { get; set; }
}
