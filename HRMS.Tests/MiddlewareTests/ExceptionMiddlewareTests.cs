using FluentAssertions;
using HRMS.API.Middleware;
using System.IO;
using HRMS.Application.Common;
using HRMS.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace HRMS.Tests.MiddlewareTests;

/// <summary>
/// Tests for ExceptionMiddleware: verifies it intercepts exceptions and returns
/// structured JSON ProblemDetails with the correct HTTP status codes.
/// </summary>
public class ExceptionMiddlewareTests
{
    private static HttpContext BuildHttpContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    // ─── Happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Next_NoException_DoesNotAlterResponse()
    {
        // Arrange
        var ctx = BuildHttpContext();
        ctx.Response.StatusCode = 200;
        RequestDelegate next = _ => Task.CompletedTask;
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(200);
    }

    // ─── Unhandled exception ─────────────────────────────────────────────────────

    [Fact]
    public async Task Next_ThrowsException_Returns500WithJson()
    {
        // Arrange
        var ctx = BuildHttpContext();
        RequestDelegate next = _ => throw new InvalidOperationException("boom");
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(500);
        ctx.Response.ContentType.Should().Contain("application/json");

        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().Contain("traceId", "response must include a trace identifier");
    }

    [Fact]
    public async Task Next_ThrowsException_ResponseBodyIsValidJson()
    {
        // Arrange
        var ctx = BuildHttpContext();
        RequestDelegate next = _ => throw new Exception("test error");
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();

        var act = () => JsonDocument.Parse(body);
        act.Should().NotThrow("response body must be valid JSON");
    }

    [Fact]
    public async Task Next_ThrowsException_ExceptionMessageIsNotLeaked()
    {
        // Arrange — internal exception message must not reach the client
        const string sensitiveMessage = "SQL syntax error near 'users'";
        var ctx = BuildHttpContext();
        RequestDelegate next = _ => throw new InvalidOperationException(sensitiveMessage);
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().NotContain(sensitiveMessage,
            "internal exception details must not be exposed to clients");
    }

    // ─── FileUploadValidationException ──────────────────────────────────────────

    [Fact]
    public async Task Next_ThrowsFileUploadValidationException_Returns400()
    {
        // Arrange
        var ctx = BuildHttpContext();
        RequestDelegate next = _ => throw new FileUploadValidationException("File too large");
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(400,
            "file upload validation errors are client errors, not server errors");
    }

    // ─── Unauthorized / Forbidden ────────────────────────────────────────────────

    [Fact]
    public async Task Next_ThrowsUnauthorizedException_Returns401()
    {
        // Arrange
        var ctx = BuildHttpContext();
        RequestDelegate next = _ => throw new UnauthorizedAccessException("Not authenticated");
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert – middleware maps UnauthorizedAccessException → 401 Unauthorized
        ctx.Response.StatusCode.Should().Be(401);
    }

    // ─── TraceId ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Next_ThrowsException_ResponseContainsTraceId()
    {
        // Arrange
        const string correlationId = "test-correlation-12345";
        var ctx = BuildHttpContext();
        ctx.Request.Headers["X-Correlation-ID"] = correlationId;
        RequestDelegate next = _ => throw new Exception("trace test");
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().Contain("traceId");
    }

    // ─── Logging ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Next_ThrowsException_LogsErrorAtErrorLevel()
    {
        // Arrange
        var ctx = BuildHttpContext();
        RequestDelegate next = _ => throw new Exception("log level test");
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "exceptions must be logged at Error level");
    }

    // ─── TaskCanceledException (client disconnect) ───────────────────────────────

    [Fact]
    public async Task Next_ThrowsTaskCanceledException_DoesNotReturn500()
    {
        // Arrange — client disconnects mid-request; must not flood logs with 500 errors
        var ctx = BuildHttpContext();
        RequestDelegate next = _ => throw new TaskCanceledException("client disconnected");
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().NotBe(500,
            "client disconnects are not server errors");
    }

    // ─── OperationCanceledException (cancellation token) ────────────────────────

    [Fact]
    public async Task Next_ThrowsOperationCanceledException_DoesNotReturn500()
    {
        // Arrange
        var ctx = BuildHttpContext();
        RequestDelegate next = _ => throw new OperationCanceledException("cancelled");
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().NotBe(500);
    }

    // ─── Argument / Validation exceptions ───────────────────────────────────────

    [Fact]
    public async Task Next_ThrowsArgumentException_Returns400()
    {
        // Arrange
        var ctx = BuildHttpContext();
        RequestDelegate next = _ => throw new ArgumentException("invalid argument");
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(400);
    }

    // ─── KeyNotFoundException (not found) ────────────────────────────────────────

    [Fact]
    public async Task Next_ThrowsKeyNotFoundException_Returns404()
    {
        // Arrange
        var ctx = BuildHttpContext();
        RequestDelegate next = _ => throw new KeyNotFoundException("entity not found");
        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var sut = new ExceptionMiddleware(next, logger.Object);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(404);
    }
}
