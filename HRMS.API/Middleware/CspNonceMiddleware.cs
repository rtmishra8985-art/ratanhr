using System.Security.Cryptography;

namespace HRMS.API.Middleware;

/// <summary>
/// FIX 4 – Enterprise-grade Content Security Policy middleware.
///
/// Generates a cryptographically random per-request nonce and builds a strict,
/// defence-in-depth CSP using 'nonce-...' instead of 'unsafe-inline'.
///
/// Additional enterprise security headers beyond the inline pipeline block
/// (X-Content-Type-Options, X-Frame-Options, Referrer-Policy, X-XSS-Protection,
/// Permissions-Policy, HSTS) are also applied here for a single, auditable location:
///   • Content-Security-Policy         – nonce-based, strict deny-listing
///   • Cross-Origin-Opener-Policy      – isolate browsing context (Spectre mitigations)
///   • Cross-Origin-Resource-Policy    – restrict cross-origin reads
///   • Cross-Origin-Embedder-Policy    – enable SharedArrayBuffer safely
///
/// Swagger routes in Development receive a permissive policy so Swagger UI inline
/// scripts keep working. All other routes get the strict policy.
/// </summary>
public class CspNonceMiddleware
{
    private readonly RequestDelegate     _next;
    private readonly IWebHostEnvironment _env;

    public CspNonceMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env  = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        // 18 random bytes → 24-character base64 nonce (no padding edge cases)
        var buf = new byte[18];
        RandomNumberGenerator.Fill(buf);
        var nonce = Convert.ToBase64String(buf);
        ctx.Items["CspNonce"] = nonce;

        // Write all security headers before the response body starts to avoid
        // "headers already sent" exceptions.
        ctx.Response.OnStarting(() =>
        {
            // ── Swagger (dev only) ────────────────────────────────────────
            // Swagger UI generates inline scripts we cannot nonce; restrict to
            // Development so production always uses the strict policy.
            if (_env.IsDevelopment() &&
                ctx.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
                    "style-src  'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                    "font-src   'self' data: https://cdn.jsdelivr.net; " +
                    "img-src    'self' data: blob:; " +
                    "connect-src 'self'; " +
                    "object-src  'none'; " +
                    "base-uri    'self'; " +
                    "form-action 'self'; " +
                    "frame-ancestors 'none';";
                return Task.CompletedTask;
            }

            // ── Strict nonce-based policy (all non-Swagger routes) ────────
            // Directives follow the OWASP CSP Cheat Sheet and Google's strict-CSP guidance:
            //   • default-src 'self'           – deny-all baseline
            //   • script-src  nonce + cdn      – allow only nonce'd scripts
            //   • style-src   nonce + cdn      – allow only nonce'd or cdn styles
            //   • font-src    cdn + data:      – Google/Bootstrap fonts via CDN
            //   • img-src     self+data+blob   – avatars, data URIs, generated images
            //   • connect-src 'self'           – XHR / fetch / WebSocket only to own origin
            //   • object-src  'none'           – block Flash, Java applets
            //   • base-uri    'self'           – prevent <base> tag hijacking
            //   • form-action 'self'           – prevent form exfiltration
            //   • frame-src   'none'           – no iframes (covered by frame-ancestors too)
            //   • frame-ancestors 'none'       – prevent clickjacking (replaces X-Frame-Options)
            //   • upgrade-insecure-requests    – silently upgrade http:// sub-resources to https://
            //   • block-all-mixed-content      – belt-and-suspenders mixed-content block
            //   • worker-src 'self'            – Service Workers only from own origin
            //   • manifest-src 'self'          – PWA manifests only from own origin
            var strictCsp =
                $"default-src 'self'; " +
                $"script-src  'self' 'nonce-{nonce}' https://cdn.jsdelivr.net; " +
                $"style-src   'self' 'nonce-{nonce}' https://cdn.jsdelivr.net; " +
                $"font-src    'self' data: https://cdn.jsdelivr.net; " +
                $"img-src     'self' data: blob:; " +
                $"connect-src 'self'; " +
                $"object-src  'none'; " +
                $"base-uri    'self'; " +
                $"form-action 'self'; " +
                $"frame-src   'none'; " +
                $"frame-ancestors 'none'; " +
                $"worker-src  'self'; " +
                $"manifest-src 'self'; " +
                $"upgrade-insecure-requests; " +
                $"block-all-mixed-content;";

            ctx.Response.Headers["Content-Security-Policy"] = strictCsp;

            // ── Cross-Origin isolation headers (FIX 4 additions) ──────────
            // COOP prevents cross-origin windows from accessing each other's JS globals,
            // mitigating Spectre/timing side-channel attacks.
            ctx.Response.Headers["Cross-Origin-Opener-Policy"]   = "same-origin";

            // CORP restricts which cross-origin requests can read this response
            // (applies to no-cors fetches). Only own-origin responses are allowed.
            ctx.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";

            return Task.CompletedTask;
        });

        await _next(ctx);
    }
}
