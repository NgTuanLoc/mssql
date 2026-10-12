using EfCoreLessons.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.Lab;

// Base class for every AdventureWorks test. Each test gets one connection and one
// transaction that is rolled back on dispose: nothing a test does is ever committed.
// TContext is AdventureWorksContext, or a lesson's derived context (see E04Context).
public abstract class LabTest<TContext> : IAsyncLifetime where TContext : AdventureWorksContext
{
    SqlConnection? connection;
    SqlTransaction? transaction;
    readonly List<DbContext> contexts = [];

    // Every command run by Db and by NewContext() contexts.
    protected SqlCapture Sql { get; } = new();

    static readonly TransactionGuard Guard = new();

    protected TContext Db { get; private set; } = null!;

    // The checks' own reference queries: same transaction, never counted in Sql.
    protected Reference Reference { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        connection = await LabConnection.OpenAsync(LabConnection.AdventureWorks);
        transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        Db = NewContext();
        Reference = new Reference(CreateContext(capture: false));
    }

    // Another context on the same connection and transaction.
    protected TContext NewContext() => CreateContext(capture: true);

    TContext CreateContext(bool capture)
    {
        var options = new DbContextOptionsBuilder<AdventureWorksContext>().UseSqlServer(connection!)
            .AddInterceptors(Guard);
        if (capture) options.AddInterceptors(Sql);

        // Derived lesson contexts expose the same (DbContextOptions<AdventureWorksContext>) constructor.
        var db = (TContext)Activator.CreateInstance(typeof(TContext), options.Options)!;
        db.Database.UseTransaction(transaction);
        contexts.Add(db);
        return db;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var db in contexts) await db.DisposeAsync();
        }
        finally
        {
            try
            {
                if (transaction != null) await transaction.DisposeAsync(); // never committed: rolls back
            }
            finally
            {
                if (connection != null) await connection.DisposeAsync();
            }
        }
        GC.SuppressFinalize(this);
    }
}

public abstract class LabTest : LabTest<AdventureWorksContext>;

public sealed class Reference(AdventureWorksContext db)
{
    public Task<List<T>> Rows<T>(FormattableString sql) => db.Database.SqlQuery<T>(sql).ToListAsync();
}
