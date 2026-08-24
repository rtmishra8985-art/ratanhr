namespace HRMS.Domain.Entities.Analytics;

/// <summary>
/// Comprehensive API audit log for tracking all API requests and responses
/// </summary>
public class ApiAuditLog
{
    public long Id { get; set; }
    public int? CompanyId { get; set; }
    public string? UserId { get; set; }
    
    /// <summary>API endpoint path (e.g., /api/employees, /api/payslips)</summary>
    public string Endpoint { get; set; } = string.Empty;
    
    /// <summary>HTTP method: GET, POST, PUT, DELETE, PATCH</summary>
    public string Method { get; set; } = string.Empty;
    
    /// <summary>HTTP status code of the response</summary>
    public int? StatusCode { get; set; }
    
    /// <summary>Request body (JSON)</summary>
    public string? RequestBody { get; set; }
    
    /// <summary>Response body (JSON)</summary>
    public string? ResponseBody { get; set; }
    
    /// <summary>Client IP address</summary>
    public string? IpAddress { get; set; }
    
    /// <summary>User agent from request headers</summary>
    public string? UserAgent { get; set; }
    
    /// <summary>Request duration in milliseconds</summary>
    public int? DurationMs { get; set; }
    
    /// <summary>Error message (if request failed)</summary>
    public string? ErrorMessage { get; set; }
    
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
