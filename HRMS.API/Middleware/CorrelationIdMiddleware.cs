using Serilog.Context;

namespace HRMS.API.Middleware;

/// <summary>
/// Generates or propagates an X-Correlation-ID header on every request.
/// The correlation ID is:
///   1. Pushed into Serilog's LogContext so it appears in every log entry.
///   2. Set on HttpContext.Items["CorrelationId"] so controllers and services can read it.
///   3. Echoed back in the response X-Correlation-ID header.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Accept an ID from upstream (load-balancer, API gateway, client) or generate a new one.
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()?.Trim();

        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
            correlationId = Guid.NewGuid().ToString("D");

        // Make it available to downstream middleware and controllers
        context.Items["CorrelationId"] = correlationId;

        // Echo it back in the response BEFORE any writes happen
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Push into Serilog's log context — all log entries within this request
        // will automatically include CorrelationId={correlationId}
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
