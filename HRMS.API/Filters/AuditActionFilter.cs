using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace HRMS.API.Filters;

/// <summary>
/// Automatically writes an audit log entry for every mutating HTTP request
/// (POST, PUT, PATCH, DELETE) that succeeds (2xx response).
/// Read-only endpoints (GET, HEAD) are intentionally excluded.
/// Services that already call _audit.LogAsync() internally (Auth, Attendance,
/// Payroll, Leave) will produce a second entry — this is acceptable duplication;
/// do NOT remove the service-level calls.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuditActionFilter : ActionFilterAttribute
{
    private static readonly HashSet<string> MutatingMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        // Only log mutations that succeeded (2xx)
        if (!MutatingMethods.Contains(context.HttpContext.Request.Method))
            return;

        if (executed.Result is ObjectResult { StatusCode: >= 300 } ||
            executed.Result is ObjectResult { StatusCode: < 200 })
            return;

        if (executed.Exception != null)
            return;

        try
        {
            var audit = context.HttpContext.RequestServices
                .GetRequiredService<IAuditService>();
            var user  = context.HttpContext.User;

            var actorId    = user.FindFirstValue(ClaimTypes.NameIdentifier);

            var method     = context.HttpContext.Request.Method;
            var path       = context.HttpContext.Request.Path.Value ?? "";
            context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controllerVal);
            var controller = controllerVal ?? "Unknown";
            var entityId   = context.RouteData.Values["id"]?.ToString();

            var eventType = method.ToUpperInvariant() switch
            {
                "POST"   => $"{controller.ToUpper()}_CREATE",
                "PUT"    => $"{controller.ToUpper()}_UPDATE",
                "PATCH"  => $"{controller.ToUpper()}_PATCH",
                "DELETE" => $"{controller.ToUpper()}_DELETE",
                _        => $"{controller.ToUpper()}_{method.ToUpper()}"
            };

            int? actorIdInt = int.TryParse(actorId, out var parsed) ? parsed : null;

            await audit.LogAsync(
                eventType,
                controller,
                entityId,
                actorIdInt,
                actorId,
                null,
                $"{method} {path}",
                true);
        }
        catch
        {
            // Audit logging must never crash the request pipeline
        }
    }
}
