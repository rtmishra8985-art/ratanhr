using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HRMS.API.Extensions;

/// <summary>
/// Shared JSON response writer for all health-check endpoints.
/// Extracted from duplicate inline lambdas in Program.cs so /health and /healthz
/// produce identical JSON shapes and any future format changes are made in one place.
/// </summary>
public static class HealthCheckResponseWriter
{
    /// <summary>
    /// Serialises the <see cref="HealthReport"/> as a JSON object with
    /// <c>status</c> (string) and <c>checks</c> (array) fields and writes it
    /// to the HTTP response. Sets Content-Type to <c>application/json</c>.
    /// </summary>
    public static async Task WriteJsonResponse(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        }));
    }
}
