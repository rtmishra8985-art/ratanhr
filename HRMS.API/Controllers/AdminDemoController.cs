using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Services.Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace HRMS.API.Controllers;

/// <summary>
/// Admin-only endpoints for demo mode operations (seed, cleanup, validation).
/// Requires SuperAdmin role authorization.
/// All operations require explicit confirmation and support dry-run mode.
/// </summary>
[Authorize(Roles = AppRoles.SuperAdmin)]
[ApiController]
[Route("api/admin/demo")]
[Tags("Admin - Demo Mode")]
public class AdminDemoController : ControllerBase
{
    private readonly IDemoSeedService _demoService;
    private readonly ILogger<AdminDemoController> _logger;

    public AdminDemoController(
        IDemoSeedService demoService,
        ILogger<AdminDemoController> logger)
    {
        _demoService = demoService;
        _logger = logger;
    }

    /// <summary>
    /// Preview demo seed operation without modifying database.
    /// Shows what would be created: companies, employees, attendance, payroll, etc.
    /// Safe to call - no database modifications.
    /// </summary>
    /// <remarks>
    /// Returns estimated record counts for:
    /// - 5 demo companies
    /// - ~500 employees
    /// - ~90,000 attendance records
    /// - ~6,000 payslips
    /// - And other demo data
    /// </remarks>
    /// <response code="200">Preview successful, no modifications made</response>
    /// <response code="400">Validation failed - see error message for reason</response>
    /// <response code="403">Forbidden - requires SuperAdmin role</response>
    [HttpGet("seed/dry-run")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(typeof(DemoSeedResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DemoSeedResult>> DryRunSeed()
    {
        _logger.LogInformation("[AdminDemo] DryRun seed requested by {User}", User.Identity?.Name);

        var result = await _demoService.SeedAsync(dryRun: true, verbose: true);
        
        if (!result.IsSuccess)
            return BadRequest(new ErrorResponse { Success = false, Message = result.ErrorMessage ?? result.Message });

        return Ok(result);
    }

    /// <summary>
    /// Execute demo seed operation - creates demo data in database.
    /// IMPORTANT: Requires explicit confirmation.
    /// Creates 5 demo companies with ~500 employees and 100K+ supporting records.
    /// Idempotent: same seed version never creates duplicates.
    /// </summary>
    /// <param name="confirm">Must be true to execute actual seeding (safety requirement)</param>
    /// <remarks>
    /// Before calling:
    /// 1. Call GET /api/admin/demo/seed/dry-run to preview
    /// 2. Verify DemoMode:Enabled = true in configuration
    /// 3. Verify DemoMode:SeedEnabled = true in configuration
    /// 4. Call with confirm=true to proceed
    /// 
    /// After calling:
    /// - All demo records are marked with IsDemo = true
    /// - Demo companies have IDs 1-5
    /// - Can be cleaned up later with DELETE /api/admin/demo/cleanup
    /// - Seed is idempotent: same SeedVersion never runs twice
    /// </remarks>
    /// <response code="200">Seed successful or already seeded</response>
    /// <response code="400">Validation failed or confirm not set</response>
    /// <response code="403">Forbidden - requires SuperAdmin role or seed disabled</response>
    [HttpPost("seed")]
    [EnableRateLimiting("sensitive")]
    [ProducesResponseType(typeof(DemoSeedResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DemoSeedResult>> Seed([FromQuery] bool confirm = false)
    {
        _logger.LogWarning("[AdminDemo] Seed requested by {User} (confirm={Confirm})", User.Identity?.Name, confirm);

        if (!confirm)
        {
            _logger.LogWarning("[AdminDemo] Seed blocked: confirm not set");
            return BadRequest(new ErrorResponse
            {
                Success = false,
                Message = "Seed requires confirm=true query parameter to proceed"
            });
        }

        var result = await _demoService.SeedAsync(dryRun: false, verbose: true);

        if (!result.IsSuccess)
        {
            _logger.LogError("[AdminDemo] Seed failed: {Error}", result.ErrorMessage);
            return BadRequest(new ErrorResponse { Success = false, Message = result.ErrorMessage ?? result.Message });
        }

        _logger.LogInformation("[AdminDemo] Seed completed successfully: {Total} records created", result.TotalRecordsCreated);
        return Ok(result);
    }

    /// <summary>
    /// Preview demo cleanup operation without modifying database.
    /// Shows what would be deleted: all records where IsDemo = true.
    /// Safe to call - no database modifications.
    /// </summary>
    /// <remarks>
    /// Returns record counts that would be deleted:
    /// - Demo companies
    /// - Demo employees and related records
    /// - Demo attendance, payroll, assets, etc.
    /// 
    /// All deletion is scoped to IsDemo = true flag.
    /// </remarks>
    /// <response code="200">Preview successful, no modifications made</response>
    /// <response code="400">Validation failed</response>
    /// <response code="403">Forbidden - requires SuperAdmin role</response>
    [HttpGet("cleanup/dry-run")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(typeof(DemoCleanupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DemoCleanupResult>> DryRunCleanup()
    {
        _logger.LogInformation("[AdminDemo] DryRun cleanup requested by {User}", User.Identity?.Name);

        var result = await _demoService.CleanupAsync(dryRun: true, confirmCleanup: false, verbose: true);

        if (!result.IsSuccess)
            return BadRequest(new ErrorResponse { Success = false, Message = result.ErrorMessage ?? result.Message });

        return Ok(result);
    }

    /// <summary>
    /// Execute demo cleanup operation - deletes all demo records.
    /// IMPORTANT: Requires explicit confirmation via query parameter.
    /// Deletes only records where IsDemo = true.
    /// Safe: foreign key aware deletion order (children first).
    /// </summary>
    /// <param name="confirm">Must be true to execute actual cleanup (safety requirement)</param>
    /// <remarks>
    /// Before calling:
    /// 1. Call GET /api/admin/demo/cleanup/dry-run to see what would be deleted
    /// 2. Verify you want to delete all demo data
    /// 3. Call with confirm=true to proceed
    /// 
    /// After calling:
    /// - All demo records (IsDemo = true) are permanently deleted
    /// - Demo companies and employees removed
    /// - Related payroll, attendance, assets removed
    /// - Real customer data is never touched (protected by IsDemo flag)
    /// 
    /// Safety:
    /// - Deletion respects foreign key constraints
    /// - Uses transactions for atomicity
    /// - Only IsDemo = true records deleted
    /// - Cannot delete real customer data
    /// </remarks>
    /// <response code="200">Cleanup successful or no demo data found</response>
    /// <response code="400">Validation failed or confirm not set</response>
    /// <response code="403">Forbidden - requires SuperAdmin role or seed disabled</response>
    [HttpDelete("cleanup")]
    [EnableRateLimiting("sensitive")]
    [ProducesResponseType(typeof(DemoCleanupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DemoCleanupResult>> Cleanup([FromQuery] bool confirm = false)
    {
        _logger.LogWarning("[AdminDemo] Cleanup requested by {User} (confirm={Confirm})", User.Identity?.Name, confirm);

        if (!confirm)
        {
            _logger.LogWarning("[AdminDemo] Cleanup blocked: confirm not set");
            return BadRequest(new ErrorResponse
            {
                Success = false,
                Message = "Cleanup requires confirm=true query parameter to proceed"
            });
        }

        var result = await _demoService.CleanupAsync(dryRun: false, confirmCleanup: true, verbose: true);

        if (!result.IsSuccess)
        {
            _logger.LogError("[AdminDemo] Cleanup failed: {Error}", result.ErrorMessage);
            return BadRequest(new ErrorResponse { Success = false, Message = result.ErrorMessage ?? result.Message });
        }

        _logger.LogInformation("[AdminDemo] Cleanup completed successfully: {Total} records deleted", result.TotalRecordsDeleted);
        return Ok(result);
    }

    /// <summary>
    /// Validate demo mode preconditions.
    /// Checks database connectivity, configuration, schema, etc.
    /// Use this to troubleshoot issues before seeding.
    /// </summary>
    /// <remarks>
    /// Validates:
    /// 1. DemoMode:Enabled configuration
    /// 2. Production environment safeguards
    /// 3. Database connectivity
    /// 4. Reserved company IDs isolation
    /// 5. Required tables and columns exist
    /// 
    /// Returns detailed validation check results.
    /// </remarks>
    /// <response code="200">Validation results (may include failures)</response>
    /// <response code="403">Forbidden - requires SuperAdmin role</response>
    [HttpGet("validate")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(typeof(DemoValidationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<DemoValidationResult>> Validate()
    {
        _logger.LogInformation("[AdminDemo] Validation requested by {User}", User.Identity?.Name);

        var result = await _demoService.ValidateAsync();
        return Ok(result);
    }

    /// <summary>
    /// Get current demo mode status and configuration.
    /// Useful for monitoring and verification.
    /// </summary>
    [HttpGet("status")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(typeof(DemoStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DemoStatusResponse>> GetStatus()
    {
        var validation = await _demoService.ValidateAsync();
        
        return Ok(new DemoStatusResponse
        {
            IsValid = validation.IsValid,
            ValidationChecks = validation.Checks,
            Timestamp = DateTime.UtcNow
        });
    }
}

/// <summary>Standard error response format.</summary>
public class ErrorResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>Demo status response.</summary>
public class DemoStatusResponse
{
    public bool IsValid { get; set; }
    public List<ValidationCheck> ValidationChecks { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
