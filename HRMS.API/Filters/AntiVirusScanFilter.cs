// FIX MED-03: Global action filter — scans every IFormFile argument before
// the controller action executes.  Applied globally via
// MvcOptions.Filters.Add<AntiVirusScanFilter>() in Program.cs so no
// controller needs to be changed individually.
using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HRMS.API.Filters;

/// <summary>
/// Inspects all <see cref="IFormFile"/> and <see cref="IFormFileCollection"/>
/// action arguments and returns 422 Unprocessable Entity when a virus is detected.
/// </summary>
public sealed class AntiVirusScanFilter : IAsyncActionFilter
{
    private readonly IVirusScanService _scanner;
    private readonly ILogger<AntiVirusScanFilter> _log;

    public AntiVirusScanFilter(IVirusScanService scanner,
        ILogger<AntiVirusScanFilter> log)
    {
        _scanner = scanner;
        _log     = log;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // Only process multipart requests — skip JSON / query-only requests.
        if (!context.HttpContext.Request.HasFormContentType)
        {
            await next();
            return;
        }

        var form = context.HttpContext.Request.Form;
        if (form.Files.Count == 0)
        {
            await next();
            return;
        }

        foreach (var file in form.Files)
        {
            if (file.Length == 0) continue;

            await using var stream = file.OpenReadStream();
            VirusScanResult result;
            try
            {
                result = await _scanner.ScanAsync(stream, file.FileName,
                    context.HttpContext.RequestAborted);
            }
            catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Fail closed: an unavailable scanner must never allow an
                // unscanned upload through the request pipeline.
                _log.LogError(ex,
                    "[AntiVirus] Scanner unavailable while scanning {FileName}; rejecting upload (fail-closed).",
                    file.FileName);

                context.Result = new UnprocessableEntityObjectResult(
                    ApiResponse.Fail(
                        $"File '{file.FileName}' could not be scanned for malware because the " +
                        "antivirus scanner is unavailable. Try again later."));
                return;
            }

            if (!result.IsClean)
            {
                _log.LogWarning(
                    "[AntiVirus] Infected file rejected: {FileName} | Threat: {Threat} | User: {User}",
                    file.FileName,
                    result.ThreatName,
                    context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous");

                context.Result = new UnprocessableEntityObjectResult(
                    ApiResponse.Fail(
                        $"File '{file.FileName}' was rejected by the antivirus scanner" +
                        (result.ThreatName is not null ? $" ({result.ThreatName})" : "") +
                        ". Upload a clean file."));
                return;
            }
        }

        await next();
    }
}
