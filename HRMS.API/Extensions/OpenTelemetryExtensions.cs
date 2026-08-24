using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HRMS.API.Extensions;

/// <summary>
/// Registers OpenTelemetry tracing and metrics for the HRMS API.
///
/// DECISION (Phase 1, item 5 — "OpenTelemetry decision"):
///   • Metrics  → OpenTelemetry SDK with the **Prometheus pull exporter** as the
///                single source of truth. Prometheus scrapes GET /metrics on the
///                API container (see monitoring/prometheus.yml, job "hrms-api").
///                OTLP *push* of metrics is opt-in only
///                (OpenTelemetry:ExportMetricsViaOtlp=true) so that the default
///                stack has exactly one metrics pipeline and no double counting.
///   • Traces   → **OTLP exporter only**. Jaeger all-in-one (docker-compose service
///                "jaeger") receives OTLP natively on 4317 (gRPC) / 4318 (HTTP),
///                so the deprecated native Jaeger exporter is not used.
///   • Logs     → stay on Serilog; OTel log export is out of scope for Phase 1.
///
/// Everything is opt-out safe: with no endpoints configured the API still records
/// metrics locally and serves /metrics, and simply exports no spans.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Add OpenTelemetry tracing + metrics with the configured exporters.
    /// Call before <c>builder.Build()</c>.
    /// </summary>
    public static IServiceCollection AddHrmsOpenTelemetry(
        this IServiceCollection services,
        IConfiguration config)
    {
        var serviceName    = config["OpenTelemetry:ServiceName"]    ?? "hrms-api";
        var serviceVersion = config["OpenTelemetry:ServiceVersion"] ?? "1.0.0";

        // OtlpEndpoint is the supported key. JaegerEndpoint is kept only as a
        // backwards-compatible alias for existing .env files — it is used ONLY
        // when OtlpEndpoint is blank. Previously both were registered at the
        // same time, which exported every span twice when both were set.
        var otlpEndpoint = FirstNonBlank(
            config["OpenTelemetry:OtlpEndpoint"],
            config["OpenTelemetry:JaegerEndpoint"]);

        var otlpProtocol = ResolveProtocol(config["OpenTelemetry:OtlpProtocol"], otlpEndpoint);

        var exportMetricsViaOtlp =
            bool.TryParse(config["OpenTelemetry:ExportMetricsViaOtlp"], out var m) && m;

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion: serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                ["host.name"]              = Environment.MachineName
            });

        services.AddOpenTelemetry()
            // ── Tracing ───────────────────────────────────────────────────
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(opt =>
                    {
                        opt.RecordException = true;
                        // Health and scrape traffic is high-volume, zero-value noise.
                        opt.Filter = ctx =>
                        {
                            var path = ctx.Request.Path.Value ?? string.Empty;
                            return !path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase)
                                && !path.StartsWith("/health",  StringComparison.OrdinalIgnoreCase);
                        };
                        opt.EnrichWithHttpRequest  = (activity, req)  => activity.SetTag("http.request_id",  req.HttpContext.TraceIdentifier);
                        opt.EnrichWithHttpResponse = (activity, resp) => activity.SetTag("http.response_size", resp.ContentLength);
                    })
                    .AddEntityFrameworkCoreInstrumentation(opt =>
                    {
                        // SetDbStatementForText removed in newer EFCore instrumentation — SQL captured via EnrichWithIDbCommand
                        opt.EnrichWithIDbCommand = (activity, cmd) => activity.SetTag("db.row_count", -1);
                    })
                    .AddRedisInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(opt =>
                    {
                        opt.Endpoint = new Uri(otlpEndpoint!);
                        opt.Protocol = otlpProtocol;
                    });
                }
            })
            // ── Metrics ───────────────────────────────────────────────────
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()   // request duration, error rate
                    .AddHttpClientInstrumentation()   // outbound HTTP latency
                    .AddRuntimeInstrumentation()      // GC, ThreadPool, memory
                    // .AddProcessInstrumentation() intentionally absent — no stable GA package.
                    .AddMeter("HRMS.Payroll")         // custom payroll generation time meter
                    .AddMeter("HRMS.Database")        // custom DB latency meter
                    .AddPrometheusExporter();         // primary pipeline: GET /metrics

                // Opt-in secondary pipeline. Off by default so the Prometheus
                // scrape remains the single source of truth for dashboards/alerts.
                if (exportMetricsViaOtlp && !string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(opt =>
                    {
                        opt.Endpoint = new Uri(otlpEndpoint!);
                        opt.Protocol = otlpProtocol;
                    });
                }
            });

        // NOTE: the previous implementation called
        //   services.Configure<OtlpExporterOptions>(...)
        // which silently rewrote the endpoint/protocol of EVERY OTLP exporter
        // (including the trace one) and never actually added a metrics reader.
        // Exporter options are now set inline per pipeline instead.

        return services;
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>
    /// Resolve the OTLP wire protocol. Explicit config wins; otherwise it is
    /// inferred from the endpoint so that ":4317" does not get sent HTTP/protobuf
    /// (a common misconfiguration that fails silently with dropped spans).
    /// </summary>
    private static OtlpExportProtocol ResolveProtocol(string? configured, string? endpoint)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim().ToLowerInvariant() switch
            {
                "grpc"                          => OtlpExportProtocol.Grpc,
                "http" or "httpprotobuf"
                     or "http/protobuf"         => OtlpExportProtocol.HttpProtobuf,
                _ => throw new InvalidOperationException(
                        $"OpenTelemetry:OtlpProtocol '{configured}' is not valid. Use 'grpc' or 'http/protobuf'.")
            };
        }

        if (!string.IsNullOrWhiteSpace(endpoint) &&
            Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
            uri.Port == 4318)
        {
            return OtlpExportProtocol.HttpProtobuf;
        }

        return OtlpExportProtocol.Grpc;   // 4317 / default
    }
}
