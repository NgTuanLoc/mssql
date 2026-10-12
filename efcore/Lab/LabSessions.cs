using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.Lab;

// The connections, transactions and contexts one test opens, and their cleanup. Shared by LabTest
// (AdventureWorks) and CodeFirstTest (EfLab_ databases).
internal sealed class LabSessions : IAsyncDisposable
{
    // How long any lab connection waits for a lock before failing with error 1222. Without it, a
    // test blocked by its own second session would wait forever.
    public const int LockTimeoutMs = 3000;

    static readonly TransactionGuard Guard = new();
    static readonly CommitGuard NoCommit = new();

    readonly List<(SqlConnection Connection, SqlTransaction Transaction)> sessions = [];
    readonly List<DbContext> contexts = [];

    // A connection to `database` with its own transaction, rolled back when the test ends.
    public async Task<(SqlConnection, SqlTransaction)> OpenAsync(string database, IsolationLevel isolation)
    {
        var connection = await LabConnection.OpenAsync(database);
        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SET LOCK_TIMEOUT {LockTimeoutMs}";
                await command.ExecuteNonQueryAsync();
            }
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(isolation);
            sessions.Add((connection, transaction));
            return (connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    // Options for a context on `connection`, with the guards that keep the transaction from
    // committing or being bypassed, plus `extra` interceptors (e.g. the test's SqlCapture).
    public static void UseLab(DbContextOptionsBuilder options, SqlConnection connection, params IInterceptor[] extra)
        => options.UseSqlServer(connection).AddInterceptors(Guard, NoCommit).AddInterceptors(extra);

    // Enlists the context in `transaction` and disposes it with the test.
    public T Track<T>(T db, SqlTransaction transaction) where T : DbContext
    {
        db.Database.UseTransaction(transaction);
        contexts.Add(db);
        return db;
    }

    public async ValueTask DisposeAsync()
    {
        // Every step runs even if an earlier one failed: a connection left open would keep its
        // transaction's locks and block every later test. Transactions are never committed, so
        // disposing them (or closing their connection) rolls them back.
        var failures = new List<Exception>();
        async Task Attempt(Func<ValueTask> dispose)
        {
            try { await dispose(); }
            catch (Exception ex) { failures.Add(ex); }
        }

        foreach (var db in contexts) await Attempt(db.DisposeAsync);
        // Second sessions first, the test's own (opened first) last.
        foreach (var (connection, transaction) in Enumerable.Reverse(sessions))
        {
            await Attempt(transaction.DisposeAsync);
            await Attempt(connection.DisposeAsync);
        }
        if (failures.Count > 0)
        {
            var hint = failures.Any(f => f is InvalidOperationException && f.Message.Contains("MultipleActiveResultSets"))
                ? " Hint: a NewSessionAsync command was still running when the test ended; await it (in a finally) before the test returns."
                : "";
            throw new AggregateException("Disposing the lab test failed." + hint, failures);
        }
    }
}
