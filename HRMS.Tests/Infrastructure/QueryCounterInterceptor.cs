// Fix 7: N+1 Regression Tests — query counting interceptor.
// Wraps EF Core's DbCommandInterceptor to count SQL statements executed against
// a SQLite test database. Use with TestHelpers.CreateSqliteDb(interceptor) to
// assert that service methods do not regress to N+1 query patterns.
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HRMS.Tests.Infrastructure;

/// <summary>
/// Counts the number of SQL commands executed during a test so that N+1 regressions
/// can be detected by asserting <c>QueryCount &lt; expectedThreshold</c>.
/// </summary>
public sealed class QueryCounterInterceptor : DbCommandInterceptor
{
    private int _count;
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _commands = new();

    /// <summary>Text of every SQL command executed since the last reset (diagnostics).</summary>
    public IReadOnlyList<string> Commands => _commands.ToArray();

    /// <summary>Total number of SQL commands executed since the interceptor was created.</summary>
    public int QueryCount => _count;

    /// <summary>
    /// Number of read (SELECT) statements executed since the last reset.
    /// N+1 regressions are a *read* pattern: one extra round-trip per row processed.
    /// INSERT/UPDATE/DELETE counts necessarily grow with the number of rows written
    /// (SQLite executes one statement per row), so read count is the meaningful budget.
    /// </summary>
    public int ReadQueryCount =>
        _commands.Count(c => c.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));

    /// <summary>Reset the counter between logical operations within a single test.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _count, 0);
        _commands.Clear();
    }

    private void Record(DbCommand command)
    {
        Interlocked.Increment(ref _count);
        _commands.Enqueue(command.CommandText);
    }

    // ── Synchronous path ────────────────────────────────────────────────────

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return result;
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Record(command);
        return result;
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Record(command);
        return result;
    }

    // ── Asynchronous path ────────────────────────────────────────────────────

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return new ValueTask<InterceptionResult<DbDataReader>>(result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return new ValueTask<InterceptionResult<object>>(result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return new ValueTask<InterceptionResult<int>>(result);
    }
}
