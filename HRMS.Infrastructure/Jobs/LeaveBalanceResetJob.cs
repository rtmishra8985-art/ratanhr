using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job that resets employee leave balances annually.
/// 
/// FIX HIGH-3: Automates the leave balance reset process that was previously
/// triggered manually via the API. This job runs on April 1st at 00:00 UTC and:
/// 
/// 1. Identifies all active employees across all companies
/// 2. For each leave type, creates a LeaveBalanceAdjustment with the annual quota
/// 3. Logs the operation in the audit trail
/// 
/// This ensures no manual intervention is required; employees start the fiscal
/// year with their full quota automatically.
/// 
/// Recurring schedule: 0 0 1 4 * (April 1st, 00:00 UTC)
/// </summary>
public class LeaveBalanceResetJob
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<LeaveBalanceResetJob> _logger;

    public LeaveBalanceResetJob(ApplicationDbContext db, ILogger<LeaveBalanceResetJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        try
        {
            _logger.LogInformation("LeaveBalanceResetJob started.");
            var currentYear = DateTime.UtcNow.Year;
            var targetYear = currentYear; // Reset for current fiscal year

            // Get all active employees across all companies
            var employees = await _db.Employees
                .Where(e => e.IsActive)
                .AsNoTracking()
                .Select(e => new { e.EmployeeCode, e.CompanyId })
                .ToListAsync();

            // Get all active leave types
            var leaveTypes = await _db.LeaveTypes
                .Where(t => t.IsActive)
                .AsNoTracking()
                .ToListAsync();

            int adjustmentsCreated = 0;

            // For each employee-leave-type combination, check if a reset
            // adjustment already exists for this year (idempotency).
            // If not, create one to set the balance to the annual quota.
            foreach (var emp in employees)
            {
                foreach (var lt in leaveTypes)
                {
                    // Check if reset already applied this year
                    var existingReset = await _db.LeaveBalanceAdjustments
                        .Where(a => a.EmployeeId == emp.EmployeeCode
                                 && a.LeaveTypeId == lt.Id
                                 && a.Year == targetYear
                                 && a.Reason.Contains("Annual reset"))
                        .FirstOrDefaultAsync();

                    if (existingReset != null)
                        continue; // Already reset for this year

                    // Check current year's used balance
                    var usedDays = await _db.LeaveRequests
                        .Where(r => r.EmployeeId == emp.EmployeeCode
                                 && r.LeaveTypeId == lt.Id
                                 && r.Status == "Approved"
                                 && r.StartDate.Year == targetYear)
                        .SumAsync(r => (int?)r.TotalDays) ?? 0;

                    // Get previous adjustments (carry-forward, etc.)
                    var existingAdjustments = await _db.LeaveBalanceAdjustments
                        .Where(a => a.EmployeeId == emp.EmployeeCode
                                 && a.LeaveTypeId == lt.Id
                                 && a.Year == targetYear)
                        .SumAsync(a => (int?)a.Days) ?? 0;

                    // Current balance: quota + adjustments - used
                    var currentBalance = lt.AnnualQuotaDays + existingAdjustments - usedDays;

                    // Only create adjustment if balance needs to be topped up to quota
                    if (currentBalance < lt.AnnualQuotaDays)
                    {
                        var topUp = lt.AnnualQuotaDays - currentBalance;
                        _db.LeaveBalanceAdjustments.Add(new Domain.Entities.Leave.LeaveBalanceAdjustment
                        {
                            EmployeeId = emp.EmployeeCode,
                            CompanyId = emp.CompanyId,
                            LeaveTypeId = lt.Id,
                            Year = targetYear,
                            Days = topUp,
                            Reason = $"Annual reset for {targetYear} ({lt.Name})",
                            AdjustedByUserId = 0, // System job, no user
                            CreatedAt = DateTime.UtcNow
                        });
                        adjustmentsCreated++;
                    }
                }
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation(
                "LeaveBalanceResetJob completed. Created {Count} adjustments for year {Year}.",
                adjustmentsCreated, targetYear);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LeaveBalanceResetJob failed.");
            throw;
        }
    }
}
