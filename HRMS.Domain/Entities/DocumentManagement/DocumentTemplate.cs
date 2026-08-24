namespace HRMS.Domain.Entities.DocumentManagement;

/// <summary>
/// Stores document templates for generating HR documents (offers, contracts, policies, etc.)
/// </summary>
public class DocumentTemplate
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    /// <summary>Document category: Offer, Contract, Policy, Agreement, Letter, etc.</summary>
    public string? Category { get; set; }
    
    /// <summary>Template content in HTML, JSON, or plain text format</summary>
    public string TemplateContent { get; set; } = string.Empty;
    
    /// <summary>File extension: .docx, .pdf, .html, .txt</summary>
    public string? FileExtension { get; set; }
    
    /// <summary>Template variables as JSON: ["{{employee_name}}", "{{company_name}}", ...]</summary>
    public string? TemplateVariables { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
