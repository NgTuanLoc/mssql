using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.Lab;

// After a server-side ROLLBACK (a trigger, a deadlock victim, raw SQL) the test transaction is dead
// and SqlClient runs later commands with no transaction: autocommit, so writes would become permanent.
// Refuse to send any command in that state, and any raw COMMIT (CommitGuard covers Database.CommitTransaction).
public sealed class TransactionGuard : DbCommandInterceptor
{
    static void Check(DbCommand command)
    {
        if (Regex.IsMatch(command.CommandText, @"^\s*COMMIT\b", RegexOptions.IgnoreCase))
            throw new InvalidOperationException(
                "Lab tests never commit: the test's transaction is rolled back when the test ends, so AdventureWorks " +
                "stays unchanged. Remove the COMMIT.");
        if (command.Transaction is null)
            throw new InvalidOperationException(
                "The test transaction has already ended (committed or rolled back on the server). " +
                "Further commands would autocommit and modify AdventureWorks, so the test must not continue.");
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Check(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Check(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Check(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Check(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Check(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Check(command);
        return ValueTask.FromResult(result);
    }
}
