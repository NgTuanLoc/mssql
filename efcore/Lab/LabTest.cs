using System.Data;
using EfCoreLessons.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.Lab;

// Base class for every AdventureWorks test. Each test gets one connection and one
// transaction that is rolled back on dispose: nothing a test does is ever committed.
// TContext is AdventureWorksContext, or a lesson's derived context (see E04Context).
public abstract class LabTest<TContext> : IAsyncLifetime where TContext : AdventureWorksContext
{
    // How long any lab connection waits for a lock before failing with error 1222. Without it, a
    // test blocked by its own second session (NewSessionAsync) would wait forever.
    public const int LockTimeoutMs = 3000;

    readonly List<(SqlConnection Connection, SqlTransaction Transaction)> sessions = [];
    readonly List<DbContext> contexts = [];
    SqlConnection connection = null!;
    SqlTransaction transaction = null!;

    // Every command run by Db and by NewContext() contexts.
    protected SqlCapture Sql { get; } = new();

    static readonly TransactionGuard Guard = new();
    static readonly CommitGuard NoCommit = new();

    protected TContext Db { get; private set; } = null!;

    // The checks' own reference queries: same transaction, never counted in Sql.
    protected Reference Reference { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        (connection, transaction) = await OpenSessionAsync(IsolationLevel.ReadCommitted);
        Db = NewContext();
        Reference = new Reference(CreateContext<TContext>(connection, transaction, capture: false, []));
    }

    // Another context on the same connection and transaction.
    protected TContext NewContext() => NewContext<TContext>();

    // A context of another type (or with extra interceptors) on the same connection and transaction.
    protected T NewContext<T>(params IInterceptor[] interceptors) where T : AdventureWorksContext
        => CreateContext<T>(connection, transaction, capture: true, interceptors);

    // A second, independent session: its own connection and transaction (rolled back when the test
    // ends), so it can block, or be blocked by, the test's own transaction. Its commands are not
    // counted in Sql.
    protected async Task<TContext> NewSessionAsync(IsolationLevel isolation = IsolationLevel.ReadCommitted)
    {
        var (sessionConnection, sessionTransaction) = await OpenSessionAsync(isolation);
        sessions.Add((sessionConnection, sessionTransaction));
        return CreateContext<TContext>(sessionConnection, sessionTransaction, capture: false, []);
    }

    static async Task<(SqlConnection, SqlTransaction)> OpenSessionAsync(IsolationLevel isolation)
    {
        var sessionConnection = await LabConnection.OpenAsync(LabConnection.AdventureWorks);
        await using (var command = sessionConnection.CreateCommand())
        {
            command.CommandText = $"SET LOCK_TIMEOUT {LockTimeoutMs}";
            await command.ExecuteNonQueryAsync();
        }
        return (sessionConnection, (SqlTransaction)await sessionConnection.BeginTransactionAsync(isolation));
    }

    T CreateContext<T>(SqlConnection on, SqlTransaction within, bool capture, IInterceptor[] interceptors)
        where T : AdventureWorksContext
    {
        var options = new DbContextOptionsBuilder<AdventureWorksContext>().UseSqlServer(on)
            .AddInterceptors(Guard, NoCommit);
        if (capture) options.AddInterceptors(Sql);
        options.AddInterceptors(interceptors);

        // Derived lesson contexts expose the same (DbContextOptions<AdventureWorksContext>) constructor.
        var db = (T)Activator.CreateInstance(typeof(T), options.Options)!;
        db.Database.UseTransaction(within);
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
        foreach (var (sessionConnection, sessionTransaction) in sessions)
        {
            await Attempt(sessionTransaction.DisposeAsync);
            await Attempt(sessionConnection.DisposeAsync);
        }
        if (transaction != null) await Attempt(transaction.DisposeAsync);
        if (connection != null) await Attempt(connection.DisposeAsync);
        GC.SuppressFinalize(this);
        if (failures.Count > 0)
        {
            var hint = failures.Any(f => f is InvalidOperationException && f.Message.Contains("MultipleActiveResultSets"))
                ? " Hint: a NewSessionAsync command was still running when the test ended; await it (in a finally) before the test returns."
                : "";
            throw new AggregateException("Disposing the lab test failed." + hint, failures);
        }
    }
}

public abstract class LabTest : LabTest<AdventureWorksContext>;

public sealed class Reference(AdventureWorksContext db)
{
    public Task<List<T>> Rows<T>(FormattableString sql) => db.Database.SqlQuery<T>(sql).ToListAsync();
}
