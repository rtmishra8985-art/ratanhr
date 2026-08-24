using System.Text;
using System.Text.RegularExpressions;

namespace HRMS.API.Middleware;

/// <summary>
/// Response-body transformation middleware that injects the per-request CSP nonce
/// (placed in HttpContext.Items["CspNonce"] by <see cref="CspNonceMiddleware"/>) into
/// every &lt;script&gt; and &lt;style&gt; opening tag in HTML responses.
///
/// This lets static HTML pages (served from wwwroot) honour the nonce-based CSP without
/// requiring a build step to pre-compute sha256 hashes for each inline block.
///
/// Only buffers text/html responses; all other content types (JSON, images, etc.) are
/// streamed directly to the original body stream with zero overhead.
/// </summary>
public class HtmlNonceInjectionMiddleware
{
    // Matches <script...> and <style...> opening tags that do not already carry a nonce attribute.
    // The negative look-ahead (?![^>]*\bnonce\b) prevents double-injection on re-processed responses.
    private static readonly Regex TagRe = new(
        @"<(script|style)(?![^>]*\bnonce\b)([^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly RequestDelegate _next;

    public HtmlNonceInjectionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var originalBody = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;

        try
        {
            await _next(ctx);
        }
        finally
        {
            // Always restore the original stream even if an exception occurred
            ctx.Response.Body = originalBody;
        }

        buffer.Seek(0, SeekOrigin.Begin);

        var contentType = ctx.Response.ContentType ?? string.Empty;
        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
            ctx.Items.TryGetValue("CspNonce", out var nonceObj) &&
            nonceObj is string nonce)
        {
            // Read, transform, and re-write the body
            using var reader = new StreamReader(buffer, Encoding.UTF8, leaveOpen: true);
            var html = await reader.ReadToEndAsync();

            var transformed = TagRe.Replace(html, $@"<$1 nonce=""{nonce}""$2>");

            var bytes = Encoding.UTF8.GetBytes(transformed);
            // Update Content-Length so the client doesn't truncate or over-read
            ctx.Response.ContentLength = bytes.Length;
            await originalBody.WriteAsync(bytes);
        }
        else
        {
            // Not HTML — stream the buffer through unchanged
            buffer.Seek(0, SeekOrigin.Begin);
            await buffer.CopyToAsync(originalBody);
        }
    }
}
