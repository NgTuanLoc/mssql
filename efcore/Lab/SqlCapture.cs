using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.Lab;

// Records every command EF sends to SQL Server, so checks can count round trips
// and inspect the generated SQL. Each command is also printed to the test output.
public sealed class SqlCapture : DbCommandInterceptor
{
    readonly List<string> commands = [];

    public IReadOnlyList<string> Commands => commands;

    public int Count => commands.Count;

    // All captured SQL as one string, for Assert.Contains / Assert.DoesNotContain.
    public string All => string.Join(Environment.NewLine, commands);

    public void Reset() => commands.Clear();

    public void AssertRoundTrips(int expected)
    {
        if (commands.Count == expected) return;

        var message = new StringBuilder($"Expected {expected} round trip(s), got {commands.Count}.");
        for (var i = 0; i < commands.Count; i++)
            message.AppendLine().AppendLine($"[{i + 1}] {commands[i]}");
        Assert.Fail(message.ToString());
    }

    void Record(DbCommand command)
    {
        commands.Add(command.CommandText);

        var parameters = string.Join(", ", command.Parameters.Cast<DbParameter>()
            .Select(p => $"{p.ParameterName}='{p.Value}'"));
        var header = parameters.Length > 0 ? $"-- SQL #{commands.Count} ({parameters})" : $"-- SQL #{commands.Count}";
        TestContext.Current.TestOutputHelper?.WriteLine($"{header}{Environment.NewLine}{command.CommandText}{Environment.NewLine}");
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }
}
