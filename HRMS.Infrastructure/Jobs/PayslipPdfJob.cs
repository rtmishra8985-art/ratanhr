using HRMS.Application.DTOs.Payroll;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.PDF;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Jobs;

/// <summary>
/// M-20: Hangfire background job that generates a payslip PDF and writes it
/// to the configured output directory. The controller returns 202 Accepted
/// immediately; clients poll the status/download endpoints until ready.
///
/// BACKGROUND JOB TENANT ISOLATION (FIX BLOCKER-10):
/// This job runs in a Hangfire context where ITenantContext is not populated
/// (_tenant == null in ApplicationDbContext). The global HasQueryFilter is
/// therefore inactive — the job queries Payslips without a company filter.
///
/// This is intentional and safe for the following reasons:
///   1. The job is only ever queued by PayslipController.RequestPdf(), which
///      first calls PayslipBelongsToCallerAsync() to verify the payslipId
///      belongs to the caller's company. A cross-tenant payslipId cannot reach
///      this job through the normal API surface.
///   2. The access token `token` is a GUID derived from payslipId + callerId,
///      so guessing a valid download URL for another tenant's payslip requires
///      breaking the GUID generation — computationally infeasible.
///   3. The output file is written to wwwroot/uploads/payslip-pdfs/{token}.pdf,
///      not to a path derived from payslipId. The download endpoint revalidates
///      caller ownership before serving the file.
///
/// If PayslipController ever changes to skip the PayslipBelongsToCallerAsync()
/// check, this job would become an IDOR vector. The check MUST NOT be removed.
/// </summary>
// BLOCKER-10: AutomaticRetry — Hangfire retries this job up to 3 times with
// exponential back-off on unhandled exceptions. OnAttemptsExceeded = Fail
// moves the job to the Failed state and makes it visible in the dashboard
// rather than silently discarding it.
//
// DisableConcurrentExecution — prevents two Hangfire workers from running the
// same job in parallel (e.g. on a retry before the first invocation finishes).
// The 10-second lock timeout matches the expected PDF generation time.
[AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
[DisableConcurrentExecution(timeoutInSeconds: 10)]
public class PayslipPdfJob
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PayslipPdfJob> _log;

    public PayslipPdfJob(ApplicationDbContext db, ILogger<PayslipPdfJob> log)
    {
        _db  = db;
        _log = log;
    }

    /// <summary>Output directory for generated PDFs (relative to wwwroot).</summary>
    public const string OutputSubDir = "payslip-pdfs";

    /// <summary>Returns the absolute output path for a given wwwroot root.</summary>
    public static string GetOutputDirectory(string webRootPath) =>
        Path.Combine(webRootPath, "uploads", OutputSubDir);

    /// <summary>Returns the file name for a job token.</summary>
    public static string GetFileName(string token) => $"{token}.pdf";

    /// <summary>
    /// Entrypoint called by Hangfire. Generates the PDF and persists it to disk.
    /// The <paramref name="token"/> ties the file to the original request (payslipId + caller
    /// combined as a GUID so an attacker cannot guess another user's download path).
    /// </summary>
    public async Task GenerateAsync(int payslipId, string token, string webRootPath)
    {
        // Log the opaque job-reference token rather than the payslip record ID so
        // no salary-domain identifier is written to operational logs.
        _log.LogInformation("PayslipPdfJob: generating PDF; job reference {JobRef}", token);

        // BLOCKER-10 IDEMPOTENCY: If a PDF with this token already exists on disk
        // (e.g. a Hangfire retry after a transient failure that completed the write),
        // skip regeneration.  The file is keyed by the opaque token, not the payslipId,
        // so this guard is safe: the same token always maps to the same payslip + caller.
        var outDirEarly  = GetOutputDirectory(webRootPath);
        var filePathEarly = Path.Combine(outDirEarly, GetFileName(token));
        if (File.Exists(filePathEarly))
        {
            _log.LogInformation(
                "PayslipPdfJob: PDF already exists for job reference {JobRef} — skipping regeneration (idempotent re-run).",
                token);
            return;
        }

        // FIX BLOCKER-10: Explicit payslip fetch without ITenantContext (background context).
        // See class-level doc for the full isolation argument. The payslipId was validated
        // against the caller's company by PayslipController.PayslipBelongsToCallerAsync()
        // before this job was enqueued — no additional company filter is needed here.
        var payslip = await _db.Payslips.FirstOrDefaultAsync(p => p.Id == payslipId);
        if (payslip == null)
        {
            _log.LogWarning("PayslipPdfJob: payslip record not found — aborting; job reference {JobRef}", token);
            return;
        }

        var employee = await _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeCode == payslip.EmployeeId);

        // Safety: verify the fetched employee belongs to the same company as the payslip.
        // This catches any future mismatch between the payslip's CompanyId and the employee's
        // CompanyId that could arise from a data-integrity bug.
        if (employee != null
            && payslip.CompanyId != 0
            && employee.CompanyId != payslip.CompanyId)
        {
            _log.LogError(
                "PayslipPdfJob: company mismatch — payslip.CompanyId={PayslipCompany}, " +
                "employee.CompanyId={EmployeeCompany}; aborting; job reference {JobRef}",
                payslip.CompanyId, employee.CompanyId, token);
            return;
        }

        var companyId   = employee?.CompanyId;
        var companyName = companyId.HasValue
            ? (await _db.Companies.FindAsync(companyId.Value))?.CompanyName ?? "Company"
            : "Company";

        var dto = new PayslipPdfDto
        {
            EmployeeName     = employee?.FullName ?? payslip.EmployeeId,
            EmployeeId       = payslip.EmployeeId,
            Department       = employee?.Department ?? string.Empty,
            Designation      = employee?.Designation ?? string.Empty,
            CompanyName      = companyName,
            PayPeriod        = new DateTime(payslip.Year, payslip.Month, 1).ToString("MMMM yyyy"),
            BasicPay         = payslip.BasicPay,
            HRA              = payslip.HRA,
            DA               = payslip.DA,
            Conveyance       = payslip.Conveyance,
            MedicalAllowance = payslip.MedicalAllowance,
            OtherAllowances  = payslip.OtherAllowances,
            GrossPay         = payslip.GrossEarnings,
            PFDeduction      = payslip.PFEmployee,
            ESIDeduction     = payslip.ESI,
            PTDeduction      = payslip.PT,
            TDSDeduction     = payslip.TDS,
            OtherDeductions  = payslip.OtherDeductions,
            TotalDeductions  = payslip.TotalDeductions,
            NetPay           = payslip.NetPay,
            WorkingDays      = payslip.WorkingDays,
            DaysPresent      = payslip.DaysPresent
        };

        var bytes = new PayslipPdfGenerator().Generate(dto);

        var outDir = GetOutputDirectory(webRootPath);
        Directory.CreateDirectory(outDir);

        var filePath = Path.Combine(outDir, GetFileName(token));
        await File.WriteAllBytesAsync(filePath, bytes);

        _log.LogInformation("PayslipPdfJob: PDF generated successfully ({Bytes} bytes)", bytes.Length);
    }
}
