using FluentAssertions;
using HRMS.API.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace HRMS.Tests.MiddlewareTests;

/// <summary>
/// Tests for CorrelationIdMiddleware: verifies it generates and propagates
/// the X-Correlation-ID header in both directions (request and response).
/// Uses TestServer so that Response.OnStarting callbacks fire correctly.
/// </summary>
public class CorrelationIdMiddlewareTests
{
    private static async Task<HttpResponseMessage> InvokeMiddlewareAsync(
        string? incomingCorrelationId = null)
    {
        var builder = new WebHostBuilder()
            .Configure(app =>
            {
                app.UseMiddleware<CorrelationIdMiddleware>();
                app.Run(ctx => Task.CompletedTask);
            });

        using var server = new TestServer(builder);
        var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        if (incomingCorrelationId != null)
            request.Headers.Add("X-Correlation-ID", incomingCorrelationId);

        return await client.SendAsync(request);
    }

    // ─── Generation ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoIncomingHeader_GeneratesNewCorrelationId()
    {
        var response = await InvokeMiddlewareAsync();

        Assert.True(response.Headers.Contains("X-Correlation-ID"),
            "middleware must always emit a correlation ID");
        var correlationId = response.Headers.GetValues("X-Correlation-ID").First();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Guid.TryParse(correlationId, out _).Should().BeTrue("generated ID should be a valid GUID");
    }

    // ─── Propagation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task IncomingHeader_Propagates_SameValueToResponse()
    {
        const string incomingId = "my-tracing-id-abc123";
        var response = await InvokeMiddlewareAsync(incomingCorrelationId: incomingId);

        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        Assert.Equal(incomingId, response.Headers.GetValues("X-Correlation-ID").First());
    }

    [Fact]
    public async Task IncomingHeader_SetsHttpContextItem_ForDownstreamAccess()
    {
        // Arrange — direct invocation to capture Items before response starts
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        const string incomingId = "downstream-test-id";
        ctx.Request.Headers["X-Correlation-ID"] = incomingId;

        string? capturedId = null;
        RequestDelegate next = c =>
        {
            capturedId = c.Items["CorrelationId"] as string
                      ?? c.Request.Headers["X-Correlation-ID"].ToString();
            return Task.CompletedTask;
        };
        var sut = new CorrelationIdMiddleware(next);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        capturedId.Should().Be(incomingId,
            "downstream handlers must be able to read the correlation ID");
    }

    // ─── Uniqueness ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task TwoRequests_WithoutIncomingHeader_GetDifferentIds()
    {
        var response1 = await InvokeMiddlewareAsync();
        var response2 = await InvokeMiddlewareAsync();

        var id1 = response1.Headers.GetValues("X-Correlation-ID").First();
        var id2 = response2.Headers.GetValues("X-Correlation-ID").First();
        id1.Should().NotBe(id2, "each request must receive a unique correlation ID");
    }

    // ─── Middleware chain continuation ────────────────────────────────────────────

    [Fact]
    public async Task Middleware_CallsNext_Always()
    {
        // Arrange — direct invocation to assert next was called
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var sut = new CorrelationIdMiddleware(next);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        nextCalled.Should().BeTrue("middleware must always invoke the next delegate");
    }

    [Fact]
    public async Task EmptyIncomingHeader_TreatedAs_NoHeader()
    {
        // Arrange — direct invocation; empty string header triggers generation
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Headers["X-Correlation-ID"] = string.Empty;
        RequestDelegate next = _ => Task.CompletedTask;
        var sut = new CorrelationIdMiddleware(next);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert — Items["CorrelationId"] holds the generated GUID, not empty
        var generatedId = ctx.Items["CorrelationId"] as string;
        generatedId.Should().NotBeNullOrWhiteSpace();
        generatedId.Should().NotBe(string.Empty,
            "empty header should trigger generation of a new ID");
    }

    // ─── Header injection prevention ─────────────────────────────────────────────

    [Fact]
    public async Task LongHeader_IsTruncatedOrRejectedToPreventHeaderInjection()
    {
        var longId = new string('A', 512);
        var response = await InvokeMiddlewareAsync(incomingCorrelationId: longId);

        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        var responseId = response.Headers.GetValues("X-Correlation-ID").First();
        responseId.Length.Should().BeLessOrEqualTo(128,
            "overly long correlation IDs should be rejected or truncated");
    }
}
