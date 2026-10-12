using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCoreLessons.Lab;

// Base class for the code-first lessons (E10, E11). Each test runs in the lesson's EfLab_ database
// inside one transaction that is rolled back on dispose. CreateModelAsync builds a model from a
// configuration method and creates its tables inside that transaction: DDL is transactional in SQL
// Server, so the rollback leaves the database empty again for the next test.
public abstract class CodeFirstTest(string lesson) : IAsyncLifetime
{
    readonly LabSessions sessions = new();
    SqlConnection connection = null!;
    SqlTransaction transaction = null!;

    // The lesson's database, e.g. EfLab_E10.
    protected string Database { get; } = LabDatabase.For(lesson);

    // Every command run by contexts from CreateModelAsync and NewContext.
    protected SqlCapture Sql { get; } = new();

    // The checks' own queries (schema, rows): same transaction, never counted in Sql.
    protected Reference Reference { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await LabDatabase.EnsureExistsAsync(Database);
        (connection, transaction) = await sessions.OpenAsync(Database, IsolationLevel.ReadCommitted);
        Reference = new Reference(Track(new LabModelContext(Options(), _ => { })));
    }

    // A context whose model is built by `configure`, with that model's tables created in the test's
    // transaction. The model is cached per configuration method, so the learner's configuration
    // (in E<NN>Exercises) and the solution's never share a model.
    protected async Task<LabModelContext> CreateModelAsync(Action<ModelBuilder> configure)
    {
        var db = NewContext(configure);
        await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        Sql.Reset(); // the CREATE TABLEs are setup, not the test's round trips
        return db;
    }

    // Another context with the same model, on the same connection and transaction (tables not created again).
    protected LabModelContext NewContext(Action<ModelBuilder> configure)
        => Track(new LabModelContext(Options(Sql), configure));

    DbContextOptions<LabModelContext> Options(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<LabModelContext>()
            .ReplaceService<IModelCacheKeyFactory, LabModelContext.CacheKeyFactory>();
        LabSessions.UseLab(options, connection, interceptors);
        return options.Options;
    }

    T Track<T>(T db) where T : DbContext => sessions.Track(db, transaction);

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await sessions.DisposeAsync();
    }
}

// A DbContext whose model comes from a configuration method passed in.
public sealed class LabModelContext(DbContextOptions<LabModelContext> options, Action<ModelBuilder> configure)
    : DbContext(options)
{
    internal Action<ModelBuilder> Configure => configure;

    protected override void OnModelCreating(ModelBuilder modelBuilder) => configure(modelBuilder);

    // EF caches one model per key. Keying on the configuration method (and the class it belongs to)
    // gives every exercise, solution and example its own model.
    internal sealed class CacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            var configure = ((LabModelContext)context).Configure;
            return (configure.Method, configure.Target?.GetType(), designTime);
        }
    }
}
