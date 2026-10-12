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
    // How long any lab connection waits for a lock before failing with error 1222.
    public const int LockTimeoutMs = LabSessions.LockTimeoutMs;

    readonly LabSessions sessions = new();
    SqlConnection connection = null!;
    SqlTransaction transaction = null!;

    // Every command run by Db and by NewContext() contexts.
    protected SqlCapture Sql { get; } = new();

    protected TContext Db { get; private set; } = null!;

    // The checks' own reference queries: same transaction, never counted in Sql.
    protected Reference Reference { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        (connection, transaction) = await sessions.OpenAsync(LabConnection.AdventureWorks, IsolationLevel.ReadCommitted);
        Db = NewContext();
        Reference = new Reference(CreateContext<TContext>(connection, transaction));
    }

    // Another context on the same connection and transaction.
    protected TContext NewContext() => NewContext<TContext>();

    // A context of another type (or with extra interceptors) on the same connection and transaction.
    protected T NewContext<T>(params IInterceptor[] interceptors) where T : AdventureWorksContext
        => CreateContext<T>(connection, transaction, [Sql, .. interceptors]);

    // A second, independent session: its own connection and transaction (rolled back when the test
    // ends), so it can block, or be blocked by, the test's own transaction. Its commands are not
    // counted in Sql.
    protected async Task<TContext> NewSessionAsync(IsolationLevel isolation = IsolationLevel.ReadCommitted)
    {
        var (sessionConnection, sessionTransaction) = await sessions.OpenAsync(LabConnection.AdventureWorks, isolation);
        return CreateContext<TContext>(sessionConnection, sessionTransaction);
    }

    T CreateContext<T>(SqlConnection on, SqlTransaction within, params IInterceptor[] interceptors)
        where T : AdventureWorksContext
    {
        var options = new DbContextOptionsBuilder<AdventureWorksContext>();
        LabSessions.UseLab(options, on, interceptors);

        // Derived lesson contexts expose the same (DbContextOptions<AdventureWorksContext>) constructor.
        return sessions.Track((T)Activator.CreateInstance(typeof(T), options.Options)!, within);
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await sessions.DisposeAsync();
    }
}

public abstract class LabTest : LabTest<AdventureWorksContext>;

public sealed class Reference(DbContext db)
{
    public Task<List<T>> Rows<T>(FormattableString sql) => db.Database.SqlQuery<T>(sql).ToListAsync();
}
