namespace HRMS.Domain.Entities.Configuration;

/// <summary>
/// Stores global system configuration and company-level settings
/// </summary>
public class SystemSetting
{
    public int Id { get; set; }
    
    /// <summary>NULL for global settings, otherwise company-specific</summary>
    public int? CompanyId { get; set; }
    
    /// <summary>Setting key (e.g., "default_currency", "timezone", "workweek_days")</summary>
    public string SettingKey { get; set; } = string.Empty;
    
    /// <summary>Setting value (string representation, parsed by SettingType)</summary>
    public string? SettingValue { get; set; }
    
    /// <summary>Type: String, Int, Boolean, Json, Decimal, Date</summary>
    public string SettingType { get; set; } = "String";
    
    public string? Description { get; set; }
    
    /// <summary>Whether this setting value should be encrypted</summary>
    public bool IsEncrypted { get; set; } = false;
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
