using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HRMS.Infrastructure.Telemetry;

/// <summary>
/// Centralised OpenTelemetry instrumentation for HRMS business metrics.
/// Services inject this singleton to record payroll generation time,
/// DB query latency, Redis latency, and error counters.
/// </summary>
public sealed class HrmsMetrics : IDisposable
{
    // Activity source for distributed tracing spans
    public static readonly ActivitySource ActivitySource = new("HRMS.Infrastructure");

    private readonly Meter _payrollMeter;
    private readonly Meter _dbMeter;

    // ── Payroll ──────────────────────────────────────────────────────────────
    private readonly Histogram<double> _payrollGenerationMs;
    private readonly Counter<long>     _payrollGenerationCount;
    private readonly Counter<long>     _payrollErrorCount;

    // ── Database ─────────────────────────────────────────────────────────────
    private readonly Histogram<double> _dbQueryMs;

    // ── Redis ─────────────────────────────────────────────────────────────────
    private readonly Histogram<double> _redisLatencyMs;

    // ── Reports ──────────────────────────────────────────────────────────────
    private readonly Histogram<long>   _reportRowCount;
    private readonly Histogram<double> _reportGenerationMs;

    public HrmsMetrics()
    {
        _payrollMeter = new Meter("HRMS.Payroll", "2.0.0");
        _dbMeter      = new Meter("HRMS.Database", "2.0.0");

        _payrollGenerationMs = _payrollMeter.CreateHistogram<double>(
            "hrms.payroll.generation_duration_ms",
            "ms",
            "Time to generate payroll for all employees in a period");

        _payrollGenerationCount = _payrollMeter.CreateCounter<long>(
            "hrms.payroll.generation_count",
            "runs",
            "Number of payroll generation runs");

        _payrollErrorCount = _payrollMeter.CreateCounter<long>(
            "hrms.payroll.error_count",
            "errors",
            "Number of payroll generation errors");

        _dbQueryMs = _dbMeter.CreateHistogram<double>(
            "hrms.db.query_duration_ms",
            "ms",
            "Database query execution time");

        _redisLatencyMs = _dbMeter.CreateHistogram<double>(
            "hrms.redis.operation_duration_ms",
            "ms",
            "Redis operation execution time");

        _reportRowCount = _payrollMeter.CreateHistogram<long>(
            "hrms.report.row_count",
            "rows",
            "Number of rows in a generated report");

        _reportGenerationMs = _payrollMeter.CreateHistogram<double>(
            "hrms.report.generation_duration_ms",
            "ms",
            "Report generation time");
    }

    // ── Public recording methods ─────────────────────────────────────────────

    public void RecordPayrollGeneration(double durationMs, int employeeCount, bool success)
    {
        var tags = new TagList
        {
            { "success", success.ToString().ToLower() }
        };
        _payrollGenerationMs.Record(durationMs, tags);
        _payrollGenerationCount.Add(1, tags);
        if (!success) _payrollErrorCount.Add(1);
    }

    public void RecordDbQuery(double durationMs, string operation, string entity)
    {
        _dbQueryMs.Record(durationMs, new TagList
        {
            { "db.operation", operation },
            { "db.entity",    entity }
        });
    }

    public void RecordRedisOperation(double durationMs, string operation)
    {
        _redisLatencyMs.Record(durationMs, new TagList
        {
            { "redis.operation", operation }
        });
    }

    public void RecordReport(string reportType, long rowCount, double durationMs)
    {
        var tags = new TagList { { "report.type", reportType } };
        _reportRowCount.Record(rowCount, tags);
        _reportGenerationMs.Record(durationMs, tags);
    }

    public void Dispose()
    {
        _payrollMeter.Dispose();
        _dbMeter.Dispose();
    }
}
