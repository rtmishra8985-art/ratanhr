using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.DTOs.Payroll;

public class BulkPayrollDto
{
    [Required, Range(1, 12)]
    public int Month { get; set; }

    [Required, Range(2000, 2100)]
    public int Year { get; set; }

    /// <summary>Filter by company (null = all companies — superadmin only).</summary>
    public int? CompanyId { get; set; }

    /// <summary>Specific employee IDs. Empty = all active employees in company.</summary>
    public List<string>? EmployeeIds { get; set; }

    /// <summary>If true, regenerate even if a payslip already exists for the month.</summary>
    public bool Overwrite { get; set; } = false;

    /// <summary>
    /// Number of working days in the pay period (1–31).
    /// Required by the validator; defaults to 26 (standard Indian payroll calendar).
    /// </summary>
    [Range(1, 31)]
    public int WorkingDays { get; set; } = 26;
}

public class BulkPayrollResultDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int Generated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}
