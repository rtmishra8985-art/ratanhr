using System.Net;
using System.Text.Json;
using HRMS.Application.Common;
using HRMS.Infrastructure.FileStorage;

namespace HRMS.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (HRMS.Infrastructure.Security.UploadValidationException ex)
        {
            // Audit item 9 — the shared UploadValidator throws this when a file fails
            // any of the five gates. Surface the validator's message verbatim: it is
            // written to be caller-safe (no paths, no server internals).
            _logger.LogWarning("Upload rejected [{TraceId}]: {Message}", context.TraceIdentifier, ex.Message);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            var uploadResponse = ApiResponse.Fail(ex.Message);
            await context.Response.WriteAsync(JsonSerializer.Serialize(uploadResponse,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        catch (FileUploadValidationException ex)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            var response = ApiResponse.Fail(ex.Message);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or request was cancelled — not a server error.
            // Log at debug level only; do not return a 500 to avoid noise.
            _logger.LogDebug("Request was cancelled (client disconnect or timeout).");
            // Do not write a response body — the client is gone.
            // Leave StatusCode as-is (default 200) so monitors are not alerted.
        }
        catch (UnauthorizedAccessException ex)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogWarning(ex, "Unauthorized access [{TraceId}]", traceId);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            var response = new { success = false, message = "Unauthorized.", traceId };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        catch (KeyNotFoundException ex)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogWarning(ex, "Resource not found [{TraceId}]", traceId);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            var response = new { success = false, message = "The requested resource was not found.", traceId };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        catch (ArgumentException ex)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogWarning(ex, "Bad request [{TraceId}]", traceId);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            var response = new { success = false, message = "Invalid request parameters.", traceId };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception {TraceId}: {Message}\n{StackTrace}", traceId, ex.Message, ex.StackTrace);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.Headers["X-Correlation-Id"] = traceId;
            
            var isDevelopment = context.RequestServices?.GetService(typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment))
                is Microsoft.AspNetCore.Hosting.IWebHostEnvironment env && env.IsDevelopment();
            
            // ALWAYS show detailed error in Development for debugging
            var message = isDevelopment ? $"{ex.GetType().Name}: {ex.Message}" : "An unexpected error occurred. Please try again.";
            var details = isDevelopment ? $"{ex.StackTrace}\n\nInner Exception: {ex.InnerException?.Message}" : null;
            
            var response = new { success = false, message, traceId, details };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
