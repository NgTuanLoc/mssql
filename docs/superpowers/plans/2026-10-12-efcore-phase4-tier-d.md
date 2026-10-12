# EF Core Track — Phase 4 (Tier D: Modeling & Migrations) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the code-first lessons E10 (Fluent API modeling), E11 (inheritance & JSON columns) and E12 (migrations workflow) to `efcore/`, with the harness they need: `EfLab_` databases behind a name guard, a code-first test base, and a shared session/cleanup class.

**Architecture:** The connection/transaction/cleanup logic `LabTest` owns moves into `LabSessions`, so a second test base can reuse it. `LabDatabase` creates and drops `EfLab_<lesson>` databases and refuses any other name. `CodeFirstTest` (E10, E11) gives each test a transaction in its lesson's `EfLab_` database; `CreateModelAsync(configure)` builds a model from a configuration method (cached per method, so the learner's model and the solution's never mix) and creates its tables *inside* that transaction, so the rollback empties the database again. E12 can't roll back (migrations commit), so every E12 test drops and recreates `EfLab_E12` and applies migrations for real; the learner creates them with `dotnet ef migrations add` against their own context, the solution's are committed. Every file below was validated against the live `mssql-learn` container on 2026-10-12:
- the build has 0 warnings
- the full suite gives 228 passed and 96 skipped (the 96 are the exercise stubs), twice in a row
- every check was run against tempting-but-wrong answers; for E12 that meant real `dotnet ef` migrations scaffolded and left unedited
- AdventureWorks checksums were unchanged afterwards

**Tech Stack:** .NET 10 SDK (10.0.x), EF Core 10.0.12 (SqlServer, Design, Proxies), `dotnet-ef` 10.0.12 (local tool), xUnit v3 4.0.2 (`xunit.v3.mtp-off`) on VSTest, SQL Server 2022 (Docker container `mssql-learn`).

**Spec:** `docs/superpowers/specs/2026-10-10-efcore-track-design.md` (phase 4 of its "Build-out Phases").

## Global Constraints

- Target `net10.0`. EF Core packages exactly `10.0.12`; `xunit.v3.mtp-off` `4.0.2`, `xunit.runner.visualstudio` `4.0.1`, `Microsoft.NET.Test.Sdk` `18.10.1`. No new packages in this phase. No floating versions.
- AdventureWorks2022 is never modified. Code-first lessons use databases named `EfLab_<letters, digits, _>` only; the harness refuses to create, drop or migrate any other name.
- No credentials committed anywhere. Design-time factories read the password at run time (`LabConnection.For`), never from a literal.
- `efcore/Model/` is scaffolder output, never hand-edited.
- Tests run serially (`parallelizeTestCollections: false`).
- Lesson folder `efcore/lessons/E<NN>-<slug>/` = `README.md`, `Examples.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`. Code-first lessons add `E<NN>Model.cs` (E10, E11: the classes the exercises map); E12 adds `SolutionMigrations/` (committed) and, once the learner works, `ExerciseMigrations/`. Namespace `EfCoreLessons.E<NN>`; classes `E<NN>Examples`, `E<NN>Checks`, `E<NN>Exercises`, `E<NN>Solutions`.
- Each lesson: 5 exercises (`Ex1`–`Ex5`) + 3 challenges (`Ch1`–`Ch3`); identical prompts in `Exercises.cs` and `Solutions.cs`; a `// Why:` note after each solution.
- `dotnet build` produces 0 warnings.
- Commits: conventional prefixes (`feat:`, `docs:`), no attribution lines (disabled in the user's settings).

## Review Focus

1. **A database name that isn't a lab database.** `LabDatabase` must refuse `AdventureWorks2022`, `master`, names without the `EfLab_` prefix, and names with characters that could break out of `[...]` in DDL. Pinned by Task 1's `LabDatabaseTests` (including an injection-shaped name) — the guard runs before any SQL.
2. **Two models for one entity class.** The learner's configuration and the solution's map the same classes differently; EF caches a model per context type, so without a per-method cache key the second would silently reuse the first. Pinned by Task 1's `Each_configuration_method_gets_its_own_model`.
3. **Tables left behind.** Code-first tables are created inside the test's transaction; after the test, the database must be empty again, or the next test's `CreateTables` fails. Pinned by Task 1's `Tables_and_rows_are_gone_after_the_test` and by running the whole suite twice (Task 5, Step 6).
4. **A learner who applies scaffolded migrations unread.** The E12 checks must fail when a rename or column split loses data. Pinned by Task 4, Step 10: a real `dotnet ef migrations add` with the scaffolding left as is must fail exercise 2.
5. **Slow reruns.** Dropping a database that pooled connections still point at took ~3 s per E12 test until `LabDatabase.DropAsync` cleared the pool first; the full suite must stay around 30 s. Pinned by Task 5, Step 5 (duration in the summary line).

## Conventions used by every task

- Shell: Git Bash on Windows, bash/zsh elsewhere. Paths are relative to the repository (or worktree) root unless a step says `cd efcore`.
- Commands that need the password export it first (`docker/.env` is gitignored; a fresh worktree outside the repo has no copy until Task 0 copies it):

  ```bash
  export SA_PASSWORD=$(grep '^SA_PASSWORD=' /d/Study/Database/mssql/docker/.env | cut -d= -f2- | tr -d '\r')
  ```

- On Git Bash, prefix `docker exec ... /opt/mssql-tools18/...` with `MSYS_NO_PATHCONV=1`.
- Files in this plan have no byte-order mark; files `dotnet ef` generates do. Either compiles.

## File Map

| Action | Path |
|---|---|
| Create | `efcore/Lab/LabSessions.cs`, `LabDatabase.cs`, `CodeFirstTest.cs`, `Schema.cs`, `CodeFirstSelfTests.cs` |
| Replace | `efcore/Lab/LabTest.cs` (now built on `LabSessions`), `CommitGuard.cs` and `TransactionGuard.cs` (messages say "the database", not "AdventureWorks") |
| Create | `efcore/lessons/E10-fluent-api-modeling/` (6 files, incl. `E10Model.cs`) |
| Create | `efcore/lessons/E11-inheritance-and-json/` (6 files, incl. `E11Model.cs`) |
| Create | `efcore/lessons/E12-migrations-workflow/` (5 files + `SolutionMigrations/`, 11 files) |
| Modify | `efcore/README.md`, `README.md`, `CLAUDE.md`, `docs/superpowers/specs/2026-10-10-efcore-track-design.md` |

---

## Task 0: Isolate the work

- [ ] **Step 1: Create an isolated workspace** using the `superpowers:using-git-worktrees` skill, on a new branch `efcore-phase4` based on `master`. Without a worktree: `git switch -c efcore-phase4`. In a worktree outside the repo, copy the password file: `cp /d/Study/Database/mssql/docker/.env docker/.env`.

- [ ] **Step 2: Verify you are not on master**

Run: `git branch --show-current`
Expected: `efcore-phase4`

- [ ] **Step 3: Confirm the container is up**

Run: `docker ps --filter name=mssql-learn --format '{{.Status}}'`
Expected: starts with `Up`. If not, run `docker compose -f docker/docker-compose.yml up -d` from the main checkout and wait ~20 s.

- [ ] **Step 4: Confirm the phase 3 baseline passes**

Run: `cd efcore && dotnet tool restore && dotnet build -warnaserror && dotnet test --no-build`
Expected: `0 Warning(s)`, then `Passed!  - Failed: 0, Passed: 172, Skipped: 72, Total: 244`.

- [ ] **Step 5: Record the AdventureWorks baseline** (compared again in Task 5)

```bash
export SA_PASSWORD=$(grep '^SA_PASSWORD=' /d/Study/Database/mssql/docker/.env | cut -d= -f2- | tr -d '\r')
MSYS_NO_PATHCONV=1 docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -W -s'|' -h -1 -d AdventureWorks2022 -Q "SET NOCOUNT ON; SELECT (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Production.Product), (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM HumanResources.Employee), (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Sales.SalesOrderHeader), (SELECT COUNT(*) FROM Sales.SalesOrderDetail);"
```

Expected: four values. Note them down. (On 2026-10-12 they were `-1058563079|259050056|-1058661271|121317`.)

---

## Task 1: Code-first harness (LabSessions, LabDatabase, CodeFirstTest)

**Files:**
- Create: `efcore/Lab/CodeFirstSelfTests.cs` (first, as the failing tests)
- Create: `efcore/Lab/LabSessions.cs`, `efcore/Lab/LabDatabase.cs`, `efcore/Lab/CodeFirstTest.cs`, `efcore/Lab/Schema.cs`
- Replace: `efcore/Lab/LabTest.cs`, `efcore/Lab/CommitGuard.cs`, `efcore/Lab/TransactionGuard.cs`

**Interfaces:**
- Consumes: `LabConnection.OpenAsync(string)`, `LabConnection.For(string)`, `SqlCapture`, the two guards (existing files in `efcore/Lab/`).
- Produces (namespace `EfCoreLessons.Lab`):
  - `LabTest<TContext>` keeps every member it had (`LockTimeoutMs`, `Db`, `NewContext()`, `NewContext<T>(params IInterceptor[])`, `NewSessionAsync(IsolationLevel)`, `Sql`, `Reference`), now implemented on `LabSessions`. `Reference` now takes any `DbContext`: `public sealed class Reference(DbContext db)` with `Task<List<T>> Rows<T>(FormattableString sql)`.
  - `public static class LabDatabase`: `const string Prefix = "EfLab_"`, `string For(string lesson)`, `string Guard(string database)`, `Task EnsureExistsAsync(string)`, `Task RecreateAsync(string)`, `Task DropAsync(string)`.
  - `public abstract class CodeFirstTest(string lesson)`: `string Database`, `SqlCapture Sql`, `Reference Reference`, `Task<LabModelContext> CreateModelAsync(Action<ModelBuilder> configure)`, `LabModelContext NewContext(Action<ModelBuilder> configure)`.
  - `public sealed class LabModelContext(DbContextOptions<LabModelContext>, Action<ModelBuilder>) : DbContext`.
  - `public record ColumnInfo(string Column, string Type, int? MaxLength, bool Nullable)` and `public static class Schema` with `TablesAsync()`, `ColumnsAsync(table)`, `PrimaryKeyAsync(table)`, `ForeignKeysAsync(table)` as extension methods on `Reference`.

- [ ] **Step 1: Write the failing self-tests: `efcore/Lab/CodeFirstSelfTests.cs`**

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.Lab;

// Pins the EfLab_ name guard. Needs no database for the name checks.
public class LabDatabaseTests
{
    [Theory]
    [InlineData("AdventureWorks2022")]
    [InlineData("master")]
    [InlineData("EfLab")]
    [InlineData("EfLab_")]
    [InlineData("eflab_E10")]
    [InlineData("EfLab_E10]; DROP DATABASE [AdventureWorks2022")]
    public void Guard_refuses_anything_but_EfLab_names(string database)
        => Assert.Throws<InvalidOperationException>(() => LabDatabase.Guard(database));

    [Fact]
    public void Lesson_databases_are_EfLab_names()
        => Assert.Equal("EfLab_E10", LabDatabase.For("E10"));

    [Fact]
    public async Task Create_and_drop_refuse_AdventureWorks()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => LabDatabase.DropAsync(LabConnection.AdventureWorks));
        await Assert.ThrowsAsync<InvalidOperationException>(() => LabDatabase.RecreateAsync(LabConnection.AdventureWorks));
        await Assert.ThrowsAsync<InvalidOperationException>(() => LabDatabase.EnsureExistsAsync(LabConnection.AdventureWorks));
    }
}

// Pins what CodeFirstTest guarantees the code-first lessons.
public class CodeFirstSelfTests() : CodeFirstTest("LabSelfTest")
{
    public class Note
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
    }

    static void NoteModel(ModelBuilder model) => model.Entity<Note>();
    static void NotesInAnotherTable(ModelBuilder model) => model.Entity<Note>().ToTable("OtherNotes");

    [Fact]
    public async Task CreateModelAsync_creates_the_tables_and_counts_only_later_commands()
    {
        var db = await CreateModelAsync(NoteModel);

        db.Add(new Note { Text = "inside the test" });
        await db.SaveChangesAsync();

        Sql.AssertRoundTrips(1); // the INSERT; the CREATE TABLE was setup
        Assert.Equal(1, (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM [Note]")).Single());
    }

    [Fact]
    public void Each_configuration_method_gets_its_own_model()
    {
        var first = NewContext(NoteModel);
        var second = NewContext(NotesInAnotherTable);

        Assert.Equal("Note", first.Model.FindEntityType(typeof(Note))!.GetTableName());
        Assert.Equal("OtherNotes", second.Model.FindEntityType(typeof(Note))!.GetTableName());
    }

    [Fact]
    public async Task Commits_are_refused_here_too()
    {
        var db = await CreateModelAsync(NoteModel);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.Database.CommitTransactionAsync());
    }

    [Fact]
    public async Task Tables_and_rows_are_gone_after_the_test()
    {
        var lab = new ScratchLab();
        await lab.InitializeAsync();
        try
        {
            await lab.CreateAndFill();
        }
        finally
        {
            await lab.DisposeAsync();
        }

        await using var connection = await LabConnection.OpenAsync(Database);
        await using var command = new SqlCommand("SELECT COUNT(*) FROM sys.tables", connection);
        Assert.Equal(0, await command.ExecuteScalarAsync());
    }

    sealed class ScratchLab() : CodeFirstTest("LabSelfTest")
    {
        public async Task CreateAndFill()
        {
            var db = await CreateModelAsync(NoteModel);
            db.Add(new Note { Text = "never committed" });
            await db.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `cd efcore && dotnet build 2>&1 | grep -E ' error ' | sort -u | head -5`
Expected: the build fails with `error CS...` lines naming `LabDatabase` and `CodeFirstTest`.

- [ ] **Step 3: Write `efcore/Lab/LabSessions.cs`**

```csharp
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
```

- [ ] **Step 4: Replace `efcore/Lab/LabTest.cs`**

```csharp
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
```

- [ ] **Step 5: Write `efcore/Lab/LabDatabase.cs`**

```csharp
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace EfCoreLessons.Lab;

// The code-first lessons' own databases. Every name must start with EfLab_: the guard is what keeps
// a create, drop or migration from ever touching AdventureWorks2022 (or anything else) by mistake.
public static partial class LabDatabase
{
    public const string Prefix = "EfLab_";

    // The database for a lesson, e.g. For("E10") => "EfLab_E10".
    public static string For(string lesson) => Guard(Prefix + lesson);

    // Throws unless `database` is an EfLab_ name made only of letters, digits and underscores.
    public static string Guard(string database)
    {
        if (!SafeName().IsMatch(database))
            throw new InvalidOperationException(
                $"Refusing to create, drop or migrate database '{database}': lab databases must be named " +
                $"{Prefix}<letters, digits or _>. AdventureWorks2022 is never touched this way.");
        return database;
    }

    // Creates the database if it doesn't exist yet. Never drops anything.
    public static Task EnsureExistsAsync(string database)
        => OnMaster($"IF DB_ID(N'{Guard(database)}') IS NULL CREATE DATABASE [{database}]");

    // Drops the database (closing other connections to it) and creates it empty.
    public static async Task RecreateAsync(string database)
    {
        await DropAsync(database);
        await OnMaster($"CREATE DATABASE [{database}]");
    }

    // Drops the database if it exists, closing other connections to it first.
    public static async Task DropAsync(string database)
    {
        Guard(database);
        SqlConnection.ClearAllPools(); // idle pooled connections to it would otherwise slow the drop down
        await OnMaster($"""
            IF DB_ID(N'{database}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{database}];
            END
            """);
        SqlConnection.ClearAllPools(); // pooled connections to the dropped database are dead
    }

    static async Task OnMaster(string sql)
    {
        await using var connection = await LabConnection.OpenAsync("master");
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    [GeneratedRegex("^EfLab_[A-Za-z0-9_]+$")]
    private static partial Regex SafeName();
}
```

- [ ] **Step 6: Write `efcore/Lab/CodeFirstTest.cs`**

```csharp
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
```

- [ ] **Step 7: Write `efcore/Lab/Schema.cs`**

```csharp
namespace EfCoreLessons.Lab;

public record ColumnInfo(string Column, string Type, int? MaxLength, bool Nullable);

// Reads the schema EF actually created, for the code-first lessons' checks. Runs through Reference:
// same transaction, not counted in Sql.
public static class Schema
{
    public static Task<List<string>> TablesAsync(this Reference reference)
        => reference.Rows<string>($"SELECT name AS Value FROM sys.tables ORDER BY name");

    public static Task<List<ColumnInfo>> ColumnsAsync(this Reference reference, string table)
        => reference.Rows<ColumnInfo>($"""
            SELECT COLUMN_NAME AS [Column], DATA_TYPE AS Type, CHARACTER_MAXIMUM_LENGTH AS MaxLength,
                   CAST(CASE IS_NULLABLE WHEN 'YES' THEN 1 ELSE 0 END AS bit) AS Nullable
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = {table}
            ORDER BY ORDINAL_POSITION
            """);

    // The primary key's columns, in key order.
    public static Task<List<string>> PrimaryKeyAsync(this Reference reference, string table)
        => reference.Rows<string>($"""
            SELECT c.name AS Value
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID({table}) AND i.is_primary_key = 1
            ORDER BY ic.key_ordinal
            """);

    // For each foreign key on `table`: "Column -> ReferencedTable.ReferencedColumn ON DELETE action".
    public static Task<List<string>> ForeignKeysAsync(this Reference reference, string table)
        => reference.Rows<string>($"""
            SELECT CONCAT(c.name COLLATE DATABASE_DEFAULT, ' -> ', OBJECT_NAME(fk.referenced_object_id) COLLATE DATABASE_DEFAULT,
                          '.', rc.name COLLATE DATABASE_DEFAULT,
                          ' ON DELETE ', fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT) AS Value
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
            JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            WHERE fk.parent_object_id = OBJECT_ID({table})
            ORDER BY c.name
            """);
}
```

- [ ] **Step 8: Replace the guards' messages.** The guards now also protect `EfLab_` databases, so their messages say "the database" instead of "AdventureWorks". Replace `efcore/Lab/CommitGuard.cs`:

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.Lab;

// A commit is permanent: it would write to the database for good. Refuse every commit the test's
// transaction could receive through EF (Database.CommitTransaction); raw COMMIT text is
// refused by TransactionGuard.
public sealed class CommitGuard : DbTransactionInterceptor
{
    static InvalidOperationException Refusal() => new(
        "Lab tests never commit: the test's transaction is rolled back when the test ends, so the database " +
        "stays unchanged. Remove the commit (SaveChanges already saves inside the test's transaction).");

    public override InterceptionResult TransactionCommitting(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
        => throw Refusal();

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
        => throw Refusal();
}
```

and `efcore/Lab/TransactionGuard.cs`:

```csharp
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
                "Lab tests never commit: the test's transaction is rolled back when the test ends, so the database " +
                "stays unchanged. Remove the COMMIT.");
        if (command.Transaction is null)
            throw new InvalidOperationException(
                "The test transaction has already ended (committed or rolled back on the server). " +
                "Further commands would autocommit and modify the database, so the test must not continue.");
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
```

- [ ] **Step 9: Run the harness tests**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter "FullyQualifiedName~EfCoreLessons.Lab"`
Expected: `0 Warning(s)`, then `Passed!  - Failed: 0, Passed: 28, Skipped: 0, Total: 28` (16 existing, 12 new). The first run creates the empty `EfLab_LabSelfTest` database.

- [ ] **Step 10: Everything else still passes**

Run: `cd efcore && dotnet test --no-build`
Expected: `Passed!  - Failed: 0, Passed: 184, Skipped: 72, Total: 256`.

- [ ] **Step 11: Commit**

```bash
git add efcore/Lab
git commit -m "feat: add EfLab_ databases and a code-first test base to the EF lab"
```

---

## Task 2: Lesson E10 — Modeling with the Fluent API

**Files:**
- Create: `efcore/lessons/E10-fluent-api-modeling/E10Model.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `CodeFirstTest` (`CreateModelAsync`, `NewContext`, `Sql`, `Reference`), `Schema` extensions and `ColumnInfo`, `[Exercise]` (Task 1).
- Produces: namespace `EfCoreLessons.E10`: the model classes in `E10Model.cs`, classes `E10Checks`, `E10Exercises`, `E10Solutions`, `E10Examples`. Nothing later depends on them.

- [ ] **Step 1: Write `E10Model.cs`**

```csharp
namespace EfCoreLessons.E10;

// The classes the E10 exercises configure. They carry no attributes: every mapping decision is made
// in the Fluent API. Each exercise maps its own small set of classes, so exercises don't interfere.

// Exercise 1 (keys)
public class OrderLine
{
    public int OrderId { get; set; }
    public int LineNumber { get; set; }
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
}

// Exercise 2 (relationships and delete behavior)
public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Book> Books { get; set; } = [];
}

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int? AuthorId { get; set; }
    public Author? Author { get; set; }
}

// Exercise 3 (complex types)
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Address ShippingAddress { get; set; } = new();
}

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
}

// Exercise 4 (value converters)
public enum ShipmentStatus { Pending, Shipped, Delivered }

public class Shipment
{
    public int Id { get; set; }
    public ShipmentStatus Status { get; set; }
}

// Exercise 5 (shadow properties)
public class Note
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
}

// Challenge 1 (named query filters)
public class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsDeleted { get; set; }
    public bool IsArchived { get; set; }
}

// Challenge 2 (backing fields)
public class Account
{
    decimal ledgerBalance; // not a name EF's conventions recognize as Balance's backing field

    public int Id { get; set; }
    public decimal Balance => ledgerBalance;

    public void Deposit(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        ledgerBalance += amount;
    }
}

// Challenge 3 (alternate keys)
public class Member
{
    public int Id { get; set; }
    public string MemberNumber { get; set; } = "";
    public List<Visit> Visits { get; set; } = [];
}

public class Visit
{
    public int Id { get; set; }
    public string MemberNumber { get; set; } = "";
    public DateTime At { get; set; }
    public Member Member { get; set; } = null!;
}
```

- [ ] **Step 2: Write `Checks.cs`**

```csharp
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E10;

// The checks for E10. E10Exercises (yours) and E10Solutions both inherit them.
// Each check builds a model from your configuration method, creates its tables in the EfLab_E10
// database inside the test's transaction, then checks the schema EF created and how it behaves.
public abstract class E10Checks() : CodeFirstTest("E10")
{
    protected abstract void Ex1(ModelBuilder model);
    protected abstract void Ex2(ModelBuilder model);
    protected abstract void Ex3(ModelBuilder model);
    protected abstract void Ex4(ModelBuilder model);
    protected abstract void Ex5(ModelBuilder model);
    protected abstract void Ch1(ModelBuilder model);
    protected abstract void Ch2(ModelBuilder model);
    protected abstract void Ch3(ModelBuilder model);

    [Exercise]
    public async Task Ex1_CompositeKey()
    {
        var db = await CreateModelAsync(Ex1);

        Assert.Equal(["OrderId", "LineNumber"], await Reference.PrimaryKeyAsync("OrderLine"));
        Assert.Contains(new ColumnInfo("Sku", "nvarchar", 20, false), await Reference.ColumnsAsync("OrderLine"));

        db.AddRange(
            new OrderLine { OrderId = 1, LineNumber = 1, Sku = "A", Quantity = 1 },
            new OrderLine { OrderId = 2, LineNumber = 1, Sku = "B", Quantity = 1 }); // same line number, another order
        await db.SaveChangesAsync();
        var duplicate = NewContext(Ex1);
        duplicate.Add(new OrderLine { OrderId = 1, LineNumber = 1, Sku = "C", Quantity = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync()); // the database enforces the key
    }

    [Exercise]
    public async Task Ex2_OptionalRelationshipThatSetsNull()
    {
        var db = await CreateModelAsync(Ex2);

        Assert.Equal(["AuthorId -> Author.Id ON DELETE SET_NULL"], await Reference.ForeignKeysAsync("Book"));
        Assert.Contains(new ColumnInfo("Title", "nvarchar", 200, false), await Reference.ColumnsAsync("Book"));

        db.Add(new Author { Name = "A", Books = [new Book { Title = "One" }, new Book { Title = "Two" }] });
        db.Add(new Book { Title = "Anonymous" }); // a book with no author is allowed
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Author]"); // the database, not EF, handles the books
        Assert.Equal(3, (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM [Book] WHERE AuthorId IS NULL")).Single());
    }

    [Exercise]
    public async Task Ex3_ComplexType()
    {
        var db = await CreateModelAsync(Ex3);

        Assert.Equal(["Customer"], await Reference.TablesAsync()); // no Address table
        var columns = (await Reference.ColumnsAsync("Customer")).Select(c => c.Column).ToList();
        Assert.Equal(["Id", "Name", "ShippingCity", "ShippingStreet"], columns.Order());
        Assert.NotNull(db.Model.FindEntityType(typeof(Customer))!.FindComplexProperty(nameof(Customer.ShippingAddress)));

        db.Add(new Customer { Name = "C", ShippingAddress = new Address { Street = "1 Main St", City = "Seattle" } });
        await db.SaveChangesAsync();
        var city = await NewContext(Ex3).Set<Customer>().Select(c => c.ShippingAddress.City).SingleAsync();
        Assert.Equal("Seattle", city);
    }

    [Exercise]
    public async Task Ex4_EnumAsText()
    {
        var db = await CreateModelAsync(Ex4);

        Assert.Contains(new ColumnInfo("Status", "nvarchar", 20, false), await Reference.ColumnsAsync("Shipment"));

        db.AddRange(new Shipment { Status = ShipmentStatus.Shipped }, new Shipment { Status = ShipmentStatus.Pending });
        await db.SaveChangesAsync();
        Assert.Equal(["Pending", "Shipped"], (await Reference.Rows<string>($"SELECT Status AS Value FROM [Shipment]")).Order());
        Sql.Reset();
        Assert.Equal(1, await db.Set<Shipment>().CountAsync(s => s.Status == ShipmentStatus.Shipped));
        Assert.Contains("N'Shipped'", Sql.All); // the filter compares text in SQL
    }

    [Exercise]
    public async Task Ex5_ShadowProperty()
    {
        var db = await CreateModelAsync(Ex5);

        Assert.Contains(new ColumnInfo("CreatedAt", "datetime2", null, false), await Reference.ColumnsAsync("Note"));
        Assert.True(db.Model.FindEntityType(typeof(Note))!.FindProperty("CreatedAt")!.IsShadowProperty());
        var defaultSql = (await Reference.Rows<string>(
            $"SELECT LOWER(definition) AS Value FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('Note')")).Single();
        Assert.Contains("sysutcdatetime()", defaultSql); // computed by the database at insert time, not a constant

        var before = DateTime.UtcNow.AddMinutes(-5); // generous: the database's clock, not this machine's
        db.Add(new Note { Text = "hello" });
        await db.SaveChangesAsync();
        var createdAt = await NewContext(Ex5).Set<Note>().Select(n => EF.Property<DateTime>(n, "CreatedAt")).SingleAsync();
        Assert.InRange(createdAt, before, DateTime.UtcNow.AddMinutes(5));
    }

    [Exercise]
    public async Task Ch1_NamedQueryFilters()
    {
        var db = await CreateModelAsync(Ch1);
        db.AddRange(
            new Ticket { Title = "open" },
            new Ticket { Title = "deleted", IsDeleted = true },
            new Ticket { Title = "archived", IsArchived = true },
            new Ticket { Title = "both", IsDeleted = true, IsArchived = true });
        await db.SaveChangesAsync();
        var tickets = NewContext(Ch1).Set<Ticket>();

        Assert.Equal(["open"], await tickets.Select(t => t.Title).ToListAsync());
        Assert.Equal(["deleted", "open"], (await tickets.IgnoreQueryFilters(["SoftDelete"]).Select(t => t.Title).ToListAsync()).Order());
        Assert.Equal(["archived", "open"], (await tickets.IgnoreQueryFilters(["Archived"]).Select(t => t.Title).ToListAsync()).Order());
        Assert.Equal(4, await tickets.IgnoreQueryFilters().CountAsync());
    }

    [Exercise]
    public async Task Ch2_BackingField()
    {
        var db = await CreateModelAsync(Ch2);

        var balance = Assert.Single(await Reference.ColumnsAsync("Account"), c => c.Column == "Balance");
        Assert.Equal("decimal", balance.Type);
        Assert.Equal(12, db.Model.FindEntityType(typeof(Account))!.FindProperty(nameof(Account.Balance))!.GetPrecision());

        var account = new Account();
        account.Deposit(100m);
        account.Deposit(50m);
        db.Add(account);
        await db.SaveChangesAsync();
        Assert.Equal(150m, (await NewContext(Ch2).Set<Account>().SingleAsync()).Balance); // materialized through the field
    }

    [Exercise]
    public async Task Ch3_RelationshipOnAnAlternateKey()
    {
        var db = await CreateModelAsync(Ch3);

        Assert.Equal(["MemberNumber -> Member.MemberNumber ON DELETE CASCADE"], await Reference.ForeignKeysAsync("Visit"));
        Assert.DoesNotContain(await Reference.ColumnsAsync("Visit"), c => c.Column == "MemberId"); // no shadow FK

        db.Add(new Member { MemberNumber = "M-001", Visits = [new Visit { At = new DateTime(2026, 10, 1) }] });
        await db.SaveChangesAsync();
        Assert.Equal("M-001", (await Reference.Rows<string>($"SELECT MemberNumber AS Value FROM [Visit]")).Single());
        var visit = await NewContext(Ch3).Set<Visit>().Include(v => v.Member).SingleAsync();
        Assert.Equal("M-001", visit.Member.MemberNumber);
    }
}
```

- [ ] **Step 3: Write `Exercises.cs`**

```csharp
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E10;

// E10 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E10Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E10Exercises : E10Checks
{
    // Exercise 1 — Composite key
    // Map OrderLine (E10Model.cs) with the primary key (OrderId, LineNumber), in that order. Sku is
    // required and at most 20 characters.
    protected override void Ex1(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 2 — An optional relationship that sets NULL
    // Map Author and Book: a book may have no author, and deleting an author in the database sets its
    // books' AuthorId to NULL (ON DELETE SET NULL). Book.Title is at most 200 characters.
    protected override void Ex2(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 3 — A complex type
    // Map Customer.ShippingAddress as a complex type: its values live in the Customer table (no Address
    // table, no key), in columns named ShippingStreet and ShippingCity.
    protected override void Ex3(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 4 — An enum stored as text
    // Store Shipment.Status as its name ("Shipped") in an nvarchar(20) column.
    protected override void Ex4(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 5 — A shadow property
    // Give Note a CreatedAt column (datetime2, NOT NULL) that is not a property of the Note class, filled
    // by the database with SYSUTCDATETIME() when a row is inserted.
    protected override void Ex5(ModelBuilder model)
        => throw new NotImplementedException();

    // Challenge 1 — Named query filters
    // Give Ticket two query filters that can be switched off independently: "SoftDelete" hides
    // IsDeleted tickets, "Archived" hides IsArchived tickets.
    protected override void Ch1(ModelBuilder model)
        => throw new NotImplementedException();

    // Challenge 2 — A backing field
    // Map Account.Balance, a read-only property backed by the private field ledgerBalance, as a
    // decimal(12, 2) column. Loading an account must set the field.
    protected override void Ch2(ModelBuilder model)
        => throw new NotImplementedException();

    // Challenge 3 — A relationship on an alternate key
    // Map Member and Visit so that Visit.MemberNumber refers to Member.MemberNumber (not to Member.Id),
    // and deleting a member deletes its visits.
    protected override void Ch3(ModelBuilder model)
        => throw new NotImplementedException();
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E10Exercises`
Expected: `Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

- [ ] **Step 4: Write `Solutions.cs`**

```csharp
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E10;

// Worked solutions for E10, each followed by a Why note. Run: dotnet test --filter E10Solutions
public class E10Solutions : E10Checks
{
    // Exercise 1 — Composite key
    // Map OrderLine (E10Model.cs) with the primary key (OrderId, LineNumber), in that order. Sku is
    // required and at most 20 characters.
    protected override void Ex1(ModelBuilder model)
        => model.Entity<OrderLine>(line =>
        {
            line.HasKey(l => new { l.OrderId, l.LineNumber });
            line.Property(l => l.Sku).HasMaxLength(20);
        });
    // Why: EF's key convention only recognizes a single property named Id or <Type>Id, and OrderLine
    // has neither, so without HasKey the model fails to build. A composite key is an anonymous type, in
    // column order. Sku is already required because it's a non-nullable string (nullable reference
    // types on); HasMaxLength turns nvarchar(max) into nvarchar(20).

    // Exercise 2 — An optional relationship that sets NULL
    // Map Author and Book: a book may have no author, and deleting an author in the database sets its
    // books' AuthorId to NULL (ON DELETE SET NULL). Book.Title is at most 200 characters.
    protected override void Ex2(ModelBuilder model)
    {
        model.Entity<Book>(book =>
        {
            book.Property(b => b.Title).HasMaxLength(200);
            book.HasOne(b => b.Author)
                .WithMany(a => a.Books)
                .HasForeignKey(b => b.AuthorId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
    // Why: the relationship is optional because AuthorId is int?, but the convention for optional
    // relationships is ClientSetNull: EF nulls the FK of tracked books, and the database's constraint
    // is NO ACTION, so a DELETE in SQL fails while books still point at the author. SetNull puts
    // ON DELETE SET NULL in the constraint, so the database does it for every book, tracked or not.

    // Exercise 3 — A complex type
    // Map Customer.ShippingAddress as a complex type: its values live in the Customer table (no Address
    // table, no key), in columns named ShippingStreet and ShippingCity.
    protected override void Ex3(ModelBuilder model)
        => model.Entity<Customer>().ComplexProperty(c => c.ShippingAddress, address =>
        {
            address.Property(a => a.Street).HasColumnName("ShippingStreet");
            address.Property(a => a.City).HasColumnName("ShippingCity");
        });
    // Why: Address has no key, so EF can't map it as an entity. A complex type is a value: it has no
    // identity, lives in its owner's row, and is compared by value. Its columns default to
    // ShippingAddress_Street; HasColumnName renames them. (An owned type, OwnsOne, also lives in the
    // owner's table, but it is still an entity type with a hidden key; complex types are EF 8+'s answer
    // for plain values.)

    // Exercise 4 — An enum stored as text
    // Store Shipment.Status as its name ("Shipped") in an nvarchar(20) column.
    protected override void Ex4(ModelBuilder model)
        => model.Entity<Shipment>().Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
    // Why: by default an enum is stored as its integer value. HasConversion<string>() adds a value
    // converter: names going in, Enum.Parse coming out. Queries still translate: Status == Shipped
    // becomes [Status] = N'Shipped'. Text survives reordering the enum, at the price of more bytes.

    // Exercise 5 — A shadow property
    // Give Note a CreatedAt column (datetime2, NOT NULL) that is not a property of the Note class, filled
    // by the database with SYSUTCDATETIME() when a row is inserted.
    protected override void Ex5(ModelBuilder model)
        => model.Entity<Note>().Property<DateTime>("CreatedAt").HasDefaultValueSql("SYSUTCDATETIME()");
    // Why: Property<T>("Name") declares a shadow property: part of the model and the table, but not of
    // the class. EF tracks its value in the change tracker, and queries reach it with
    // EF.Property<DateTime>(n, "CreatedAt"). HasDefaultValueSql leaves the value to the database on
    // insert, so the class stays free of audit plumbing.

    // Challenge 1 — Named query filters
    // Give Ticket two query filters that can be switched off independently: "SoftDelete" hides
    // IsDeleted tickets, "Archived" hides IsArchived tickets.
    protected override void Ch1(ModelBuilder model)
        => model.Entity<Ticket>()
            .HasQueryFilter("SoftDelete", t => !t.IsDeleted)
            .HasQueryFilter("Archived", t => !t.IsArchived);
    // Why: before EF 10 an entity type had one query filter, so disabling it switched off every rule at
    // once. Named filters (new in EF 10) are independent: IgnoreQueryFilters(["SoftDelete"]) shows
    // deleted tickets while still hiding archived ones, and IgnoreQueryFilters() switches all off.

    // Challenge 2 — A backing field
    // Map Account.Balance, a read-only property backed by the private field ledgerBalance, as a
    // decimal(12, 2) column. Loading an account must set the field.
    protected override void Ch2(ModelBuilder model)
        => model.Entity<Account>().Property(a => a.Balance)
            .HasField("ledgerBalance")
            .HasPrecision(12, 2);
    // Why: EF maps a read-only property only when it finds a backing field, and its conventions only
    // look for names like _balance, balance or m_balance. HasField names the field explicitly, and EF
    // reads and writes it directly, so Deposit stays the only way to change a balance in code.

    // Challenge 3 — A relationship on an alternate key
    // Map Member and Visit so that Visit.MemberNumber refers to Member.MemberNumber (not to Member.Id),
    // and deleting a member deletes its visits.
    protected override void Ch3(ModelBuilder model)
        => model.Entity<Visit>()
            .HasOne(v => v.Member)
            .WithMany(m => m.Visits)
            .HasForeignKey(v => v.MemberNumber)
            .HasPrincipalKey(m => m.MemberNumber)
            .OnDelete(DeleteBehavior.Cascade);
    // Why: a foreign key can point at any unique column, not only the primary key. HasPrincipalKey makes
    // MemberNumber an alternate key (a UNIQUE constraint) for the FK to reference. Without it, EF can't
    // use the string MemberNumber as an FK to the int Id, and adds a shadow MemberId column instead.
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E10Solutions`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`. The first run creates the `EfLab_E10` database.

- [ ] **Step 5: Write `Examples.cs`**

```csharp
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E10;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E10Examples --logger "console;verbosity=detailed"
// Each example builds its own model in EfLab_E10, inside the test's transaction.
public class E10Examples() : CodeFirstTest("E10")
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    public class Wallet
    {
        decimal _balance; // a name EF's conventions recognize
        public int Id { get; set; }
        public decimal Balance => _balance;
        public void Add(decimal amount) => _balance += amount;
    }

    // Concept 1: with no configuration, conventions decide tables, keys, types and relationships.
    [Fact]
    public async Task Conventions_build_a_model_from_the_classes()
    {
        var db = await CreateModelAsync(model => model.Entity<Book>());
        Print(db.Database.GenerateCreateScript());

        Assert.Equal(["Author", "Book"], await Reference.TablesAsync()); // Author found through Book.Author
        Assert.Equal(["Id"], await Reference.PrimaryKeyAsync("Book"));   // "Id" is the key by convention
        Assert.Contains(new ColumnInfo("Title", "nvarchar", -1, false), await Reference.ColumnsAsync("Book")); // nvarchar(max) (-1), required
        Assert.Equal(["AuthorId -> Author.Id ON DELETE NO_ACTION"], await Reference.ForeignKeysAsync("Book"));   // optional: ClientSetNull
    }

    // Concept 2: a navigation without an FK property gets a shadow FK, required ones cascade.
    [Fact]
    public async Task A_missing_foreign_key_property_becomes_a_shadow_property()
    {
        var db = await CreateModelAsync(model => model.Entity<Visit>().Ignore(v => v.MemberNumber));

        Assert.Equal(["MemberId -> Member.Id ON DELETE CASCADE"], await Reference.ForeignKeysAsync("Visit"));
        Assert.True(db.Model.FindEntityType(typeof(Visit))!.FindProperty("MemberId")!.IsShadowProperty());
    }

    // Concept 2: DeleteBehavior decides both what EF does to tracked dependents and the ON DELETE rule.
    [Fact]
    public async Task Delete_behaviors_in_the_schema()
    {
        await CreateModelAsync(model =>
        {
            model.Entity<Book>().HasOne(b => b.Author).WithMany(a => a.Books).OnDelete(DeleteBehavior.Restrict);
            model.Entity<Visit>().Ignore(v => v.MemberNumber)
                .HasOne(v => v.Member).WithMany(m => m.Visits).OnDelete(DeleteBehavior.Cascade);
        });

        Assert.Equal(["AuthorId -> Author.Id ON DELETE NO_ACTION"], await Reference.ForeignKeysAsync("Book")); // Restrict: no action in SQL
        Assert.Equal(["MemberId -> Member.Id ON DELETE CASCADE"], await Reference.ForeignKeysAsync("Visit"));
    }

    // Concept 3: owned type vs complex type — same columns, different kinds of model object.
    [Fact]
    public void Owned_type_vs_complex_type()
    {
        var owned = NewContext(model => model.Entity<Customer>().OwnsOne(c => c.ShippingAddress));
        var complex = NewContext(model => model.Entity<Customer>().ComplexProperty(c => c.ShippingAddress));

        var address = owned.Model.FindEntityType(typeof(Customer))!.GetNavigations().Single().TargetEntityType;
        Assert.True(address.IsOwned());               // an entity type, with a hidden key
        Assert.Contains("ShippingAddress_City", owned.Database.GenerateCreateScript());
        Assert.NotNull(complex.Model.FindEntityType(typeof(Customer))!.FindComplexProperty("ShippingAddress")); // a value, no key
        Assert.Contains("ShippingAddress_City", complex.Database.GenerateCreateScript());
    }

    // Concept 4: a value converter changes what is stored; queries still translate.
    [Fact]
    public async Task Value_converter_enum_as_string()
    {
        var db = await CreateModelAsync(model =>
            model.Entity<Shipment>().Property(s => s.Status).HasConversion<string>().HasMaxLength(20));
        db.Add(new Shipment { Status = ShipmentStatus.Delivered });
        await db.SaveChangesAsync();

        Assert.Equal("Delivered", (await Reference.Rows<string>($"SELECT Status AS Value FROM [Shipment]")).Single());
        var sql = db.Set<Shipment>().Where(s => s.Status == ShipmentStatus.Delivered).ToQueryString();
        Assert.Contains("[s].[Status] = N'Delivered'", sql);
    }

    // Concept 5: shadow properties live in the model and the tracker, not the class.
    [Fact]
    public async Task Shadow_properties_through_Entry_and_EF_Property()
    {
        var db = await CreateModelAsync(model => model.Entity<Note>().Property<string>("Author").HasMaxLength(50));
        var note = new Note { Text = "hi" };
        db.Add(note);
        db.Entry(note).Property("Author").CurrentValue = "ana";
        await db.SaveChangesAsync();

        var authors = await NewContext(model => model.Entity<Note>().Property<string>("Author").HasMaxLength(50))
            .Set<Note>().Where(n => EF.Property<string>(n, "Author") == "ana").CountAsync();
        Assert.Equal(1, authors);
    }

    // Concept 5: a backing field with a conventional name is found automatically.
    [Fact]
    public async Task Conventional_backing_field_is_found()
    {
        var db = await CreateModelAsync(model => model.Entity<Wallet>().Property(w => w.Balance).HasPrecision(12, 2));
        var wallet = new Wallet();
        wallet.Add(42m);
        db.Add(wallet);
        await db.SaveChangesAsync();

        Assert.Equal("_balance", db.Model.FindEntityType(typeof(Wallet))!.FindProperty("Balance")!.FieldInfo!.Name);
        Assert.Equal(42m, (await Reference.Rows<decimal>($"SELECT Balance AS Value FROM [Wallet]")).Single());
    }

    // Concept 6: named query filters add to every query and can be ignored one by one.
    [Fact]
    public void Named_query_filters_in_SQL()
    {
        var db = NewContext(model => model.Entity<Ticket>()
            .HasQueryFilter("SoftDelete", t => !t.IsDeleted)
            .HasQueryFilter("Archived", t => !t.IsArchived));

        var both = db.Set<Ticket>().ToQueryString();
        var onlyArchived = db.Set<Ticket>().IgnoreQueryFilters(["SoftDelete"]).ToQueryString();

        Assert.Contains("[t].[IsDeleted] = CAST(0 AS bit) AND [t].[IsArchived] = CAST(0 AS bit)", both);
        Assert.DoesNotContain("IsDeleted] =", onlyArchived);
        Assert.Contains("[t].[IsArchived] = CAST(0 AS bit)", onlyArchived);
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E10Examples`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 6: Write `README.md`**

````markdown
# E10 — Modeling with the Fluent API

## What you'll learn

- What EF's conventions infer from plain classes, and how to read the schema they produce
- Keys: composite and alternate keys, and relationships that point at an alternate key
- Relationships and delete behaviors: `Cascade`, `Restrict`, `SetNull`, `ClientSetNull`, and the `ON DELETE` rule each one produces
- Owned types vs complex types for values without identity
- Value converters, shadow properties and backing fields
- Named query filters, new in EF Core 10

## Builds on

- T-SQL [lesson 09](../../../lessons/09-ddl-and-constraints/lesson.ipynb) — tables, keys, foreign keys and `ON DELETE` actions
- EF lessons [E07](../E07-change-tracking/README.md) (shadow values live in the change tracker) and [E08](../E08-savechanges-and-set-based-writes/README.md) (delete behaviors at save time)

## Setup

```
dotnet test --filter E10Examples --logger "console;verbosity=detailed"
```

**E10 is code-first.** The classes in `E10Model.cs` have no attributes. You write the mapping in each exercise's method, `protected override void Ex1(ModelBuilder model)`, exactly as you would in `OnModelCreating`. The check builds a model from your method, creates its tables in the **`EfLab_E10`** database (created on first use) inside the test's transaction, and inspects what came out:
- the schema, read from `sys.*` / `INFORMATION_SCHEMA`
- the model metadata
- how inserts and queries behave

The rollback removes the tables again, so every test starts with an empty database. AdventureWorks isn't involved, and the harness refuses to create or drop any database whose name doesn't start with `EfLab_`.

To see the DDL your configuration produces, print `db.Database.GenerateCreateScript()`.

## Concepts

### 1. Conventions

With nothing configured, EF still builds a full model:

- **Tables** are named after the entity type (`Book`) or its `DbSet`, and every reachable class becomes an entity type: `Author` is found through `Book.Author`.
- **Keys:** a property named `Id` or `<Type>Id`.
- **Types:** `string` becomes `nvarchar(max)`, `NOT NULL` when it's non-nullable (with nullable reference types on), and `decimal` becomes `decimal(18,2)` with a warning.
- **Relationships:** a navigation plus `<Navigation>Id` makes a foreign key. A missing FK property becomes a **shadow** FK (`MemberId`). A required relationship gets `ON DELETE CASCADE`. An optional one gets `ClientSetNull` (`NO ACTION` in the database).

The Fluent API (`modelBuilder.Entity<T>()...`) overrides any of it, and it wins over data annotations. Reading the generated DDL is the fastest way to see what the conventions decided.

Examples: `Conventions_build_a_model_from_the_classes`, `A_missing_foreign_key_property_becomes_a_shadow_property`.

### 2. Keys and relationships

- `HasKey(x => new { x.OrderId, x.LineNumber })` declares a composite key, in column order.
- `HasAlternateKey(...)`, or `HasPrincipalKey(...)` on a relationship, makes another unique column a key that foreign keys can reference.
- `HasOne(...).WithMany(...).HasForeignKey(...)` configures a relationship from the dependent side. `.IsRequired(false)` makes it optional.

| `DeleteBehavior` | Tracked dependents (EF, at `SaveChanges`) | `ON DELETE` in the schema |
|---|---|---|
| `Cascade` | deleted | `CASCADE` |
| `SetNull` | FK set to null | `SET NULL` |
| `ClientSetNull` (optional default) | FK set to null | `NO ACTION` |
| `Restrict` / `NoAction` / `ClientNoAction` | left alone; the delete fails if they still exist | `NO ACTION` |

The schema rule is what protects rows EF never loaded (E08).

Example: `Delete_behaviors_in_the_schema`.

### 3. Owned types vs complex types

Both map a class without its own table into its owner's columns (`ShippingAddress_City` by default; rename with `HasColumnName`).

| | `OwnsOne` (owned entity type) | `ComplexProperty` (complex type) |
|---|---|---|
| Identity | an entity type with a hidden key | none: a value, compared by value |
| Sharing an instance between owners | not allowed | fine (it's copied) |
| JSON mapping | `.ToJson()` | `.ToJson()` (EF 10) |

Use complex types for values such as an address or money, and owned types when you need entity-like behavior (E11 maps both to JSON).

Example: `Owned_type_vs_complex_type`.

### 4. Value converters

`HasConversion(...)` changes how a property is stored. `HasConversion<string>()` stores an enum's name. You can also pass your own conversion pair (`v => v.ToString(), s => Parse(s)`) or a `ValueConverter`. EF applies the converter to parameters and literals too, so `Status == Shipped` still translates (`[Status] = N'Shipped'`).

Example: `Value_converter_enum_as_string`.

### 5. Shadow properties and backing fields

- **Shadow property:** `Property<DateTime>("CreatedAt")` is in the model and the table but not on the class. Set it through `Entry(e).Property("CreatedAt")`, read it in queries with `EF.Property<DateTime>(e, "CreatedAt")`, and let the database fill it with `HasDefaultValueSql`.
- **Backing field:** EF can read and write a field instead of the property, which keeps setters private and invariants in methods. Fields named `_balance`, `balance` or `m_balance` are found by convention. Any other name needs `HasField("ledgerBalance")`. A read-only property with no backing field EF can find is not mapped at all.

Examples: `Shadow_properties_through_Entry_and_EF_Property`, `Conventional_backing_field_is_found`.

### 6. Named query filters (EF 10)

`HasQueryFilter("SoftDelete", t => !t.IsDeleted)` adds a predicate to every query of the entity type. Before EF 10 there was one filter per type, and `IgnoreQueryFilters()` removed it entirely. Named filters can be ignored one at a time with `IgnoreQueryFilters(["SoftDelete"])`, so a soft-delete rule and a tenant or archive rule stay independent.

Example: `Named_query_filters_in_SQL`.

## Pitfalls

- **Trusting conventions without reading the DDL.** Shadow FKs, `nvarchar(max)` and `NO ACTION` deletes are all easy to miss.
- **`ClientSetNull` when the database must null the FKs.** Use `SetNull` so rows EF never loaded are covered.
- **An owned type where a value would do.** Owned types carry identity rules complex types don't have.
- **A client-side default for an audit column.** `HasDefaultValue(DateTime.UtcNow)` is evaluated once, when the model is built. Use `HasDefaultValueSql`.
- **A read-only property you expect to be mapped.** Without a findable backing field, it silently isn't.

## Exercises

```
dotnet test --filter E10Exercises
```

Edit `Exercises.cs`. Each exercise is a model configuration: write it as you would in `OnModelCreating`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
````

- [ ] **Step 7: Prompts are identical in Exercises.cs and Solutions.cs**

```bash
cd efcore && python - lessons/E10-fluent-api-modeling <<'EOF'
import pathlib, sys
def prompts(path):
    blocks, current = [], []
    for line in pathlib.Path(path).read_text(encoding="utf-8").splitlines():
        text = line.strip()
        if text.startswith(("// Exercise", "// Challenge")):
            current = [text]
        elif current and text.startswith("//"):
            current.append(text)
        elif current and text.startswith("protected override"):
            blocks.append(current)
            current = []
    return blocks
lesson = sys.argv[1]
exercises, solutions = prompts(f"{lesson}/Exercises.cs"), prompts(f"{lesson}/Solutions.cs")
assert exercises == solutions, "prompts differ between Exercises.cs and Solutions.cs"
print(f"{len(exercises)} prompts identical")
EOF
```

Expected: `8 prompts identical`.

- [ ] **Step 8: Commit**

```bash
git add efcore/lessons/E10-fluent-api-modeling
git commit -m "feat: add EF Core lesson E10 - modeling with the Fluent API"
```

- [ ] **Step 9: A wrong answer fails for the right reason.** A client-side default is evaluated once, when the model is built. Temporarily replace `Ex5`'s stub line in `Exercises.cs` (the `=> throw new NotImplementedException();` under `Ex5`) with:

```csharp
        => model.Entity<Note>().Property<DateTime>("CreatedAt").HasDefaultValue(DateTime.UtcNow);
```

Run: `cd efcore && dotnet build && dotnet test --no-build --filter "FullyQualifiedName~E10Exercises.Ex5"`
Expected: `Failed: 1` with `Assert.Contains() Failure: Sub-string not found` (the column's default is a constant, not `sysutcdatetime()`). Then restore and confirm a clean tree:

```bash
git checkout -- efcore/lessons/E10-fluent-api-modeling/Exercises.cs
git status --short
```

Expected: `git status --short` prints nothing.

---

## Task 3: Lesson E11 — Inheritance & JSON columns

**Files:**
- Create: `efcore/lessons/E11-inheritance-and-json/E11Model.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `CodeFirstTest`, `LabModelContext`, `Schema`/`ColumnInfo`, `[Exercise]` (Task 1).
- Produces: namespace `EfCoreLessons.E11`: model classes, record `KindTotal`, classes `E11Checks`, `E11Exercises`, `E11Solutions`, `E11Examples`. Nothing later depends on them.

- [ ] **Step 1: Write `E11Model.cs`**

```csharp
namespace EfCoreLessons.E11;

// The classes the E11 exercises map. No attributes: every mapping decision is in the Fluent API.

// Exercises 1-3 and challenge 3: one hierarchy, three ways to store it.
public abstract class Payment
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public class CardPayment : Payment
{
    public string Last4 { get; set; } = "";
}

public class BankTransfer : Payment
{
    public string Iban { get; set; } = "";
}

public class Voucher : Payment
{
    public string Code { get; set; } = "";
}

// Exercise 4, exercise 5 and challenges 1-2: an order whose shipping details and notes live in JSON.
public class Order
{
    public int Id { get; set; }
    public string Customer { get; set; } = "";
    public Shipping Shipping { get; set; } = new();
    public List<OrderNote> Notes { get; set; } = [];
}

public class Shipping
{
    public string City { get; set; } = "";
    public string Carrier { get; set; } = "";
}

public class OrderNote
{
    public string Text { get; set; } = "";
    public DateTime At { get; set; }
}
```

- [ ] **Step 2: Write `Checks.cs`**

```csharp
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E11;

public record KindTotal(string Kind, decimal Total);

// The checks for E11. E11Exercises (yours) and E11Solutions both inherit them.
// Exercises 1-4 are model configurations, checked against the schema they create in EfLab_E11.
// Exercise 5 and the challenges are queries against the models below.
public abstract class E11Checks() : CodeFirstTest("E11")
{
    protected abstract void Ex1(ModelBuilder model);
    protected abstract void Ex2(ModelBuilder model);
    protected abstract void Ex3(ModelBuilder model);
    protected abstract void Ex4(ModelBuilder model);
    protected abstract Task<List<int>> Ex5(LabModelContext db, string city);
    protected abstract Task<List<int>> Ch1(LabModelContext db, string word);
    protected abstract Task<int> Ch2(LabModelContext db, string city, string carrier);
    protected abstract Task<List<KindTotal>> Ch3(LabModelContext db);

    // The model exercise 5 and challenges 1-2 query: Order with its Shipping and Notes in JSON columns.
    protected static void OrderModel(ModelBuilder model) => model.Entity<Order>(order =>
    {
        order.ComplexProperty(o => o.Shipping, shipping => shipping.ToJson());
        order.ComplexCollection(o => o.Notes, notes => notes.ToJson());
    });

    // The model challenge 3 queries: the payments in one table (TPH), the type in a "Kind" column.
    protected static void PaymentsInOneTable(ModelBuilder model) => model.Entity<Payment>()
        .HasDiscriminator<string>("Kind")
        .HasValue<CardPayment>("card")
        .HasValue<BankTransfer>("bank")
        .HasValue<Voucher>("voucher");

    static Payment[] OneOfEach() =>
        [new CardPayment { Amount = 10m, Last4 = "4242" }, new BankTransfer { Amount = 100m, Iban = "DE89" }, new Voucher { Amount = 7m, Code = "XMAS" }];

    // Three orders: 1 Seattle/DHL with an urgent note, 2 Paris/DHL without notes, 3 Seattle/UPS.
    async Task<LabModelContext> OrdersAsync()
    {
        var db = await CreateModelAsync(OrderModel);
        db.AddRange(
            new Order { Customer = "a", Shipping = new() { City = "Seattle", Carrier = "DHL" }, Notes = [new() { Text = "urgent: call first", At = new DateTime(2026, 10, 1) }] },
            new Order { Customer = "b", Shipping = new() { City = "Paris", Carrier = "DHL" } },
            new Order { Customer = "c", Shipping = new() { City = "Seattle", Carrier = "UPS" }, Notes = [new() { Text = "leave at the door", At = new DateTime(2026, 10, 2) }] });
        await db.SaveChangesAsync();
        Sql.Reset();
        return NewContext(OrderModel); // nothing tracked
    }

    async Task<List<string>> ColumnNames(string table) => (await Reference.ColumnsAsync(table)).Select(c => c.Column).ToList();

    [Exercise]
    public async Task Ex1_TablePerHierarchy()
    {
        var db = await CreateModelAsync(Ex1);

        Assert.Equal(["Payments"], await Reference.TablesAsync());
        Assert.Contains(new ColumnInfo("Kind", "nvarchar", 10, false), await Reference.ColumnsAsync("Payments"));
        db.AddRange(OneOfEach());
        await db.SaveChangesAsync();
        Assert.Equal(["bank", "card", "voucher"], (await Reference.Rows<string>($"SELECT Kind AS Value FROM Payments")).Order());
        Sql.Reset();
        Assert.Equal(1, await db.Set<CardPayment>().CountAsync());
        Assert.Contains("[p].[Kind] = N'card'", Sql.All); // one table, filtered by the discriminator
    }

    [Exercise]
    public async Task Ex2_TablePerType()
    {
        var db = await CreateModelAsync(Ex2);

        Assert.Equal(["BankTransfers", "CardPayments", "Payments", "Vouchers"], await Reference.TablesAsync());
        Assert.Equal(["Id", "Last4"], await ColumnNames("CardPayments")); // Amount lives in Payments only
        db.AddRange(OneOfEach());
        await db.SaveChangesAsync();
        Sql.Reset();
        Assert.Equal(3, (await db.Set<Payment>().ToListAsync()).Count);
        Assert.Contains("LEFT JOIN [CardPayments]", Sql.All); // the base query joins every subtype table
    }

    [Exercise]
    public async Task Ex3_TablePerConcreteType()
    {
        var db = await CreateModelAsync(Ex3);

        Assert.Equal(["BankTransfers", "CardPayments", "Vouchers"], await Reference.TablesAsync()); // no table for the abstract base
        Assert.Equal(["Id", "Amount", "Last4"], await ColumnNames("CardPayments"));                // each table is complete
        var payments = OneOfEach();
        db.AddRange(payments);
        await db.SaveChangesAsync();
        Assert.Equal(3, payments.Select(p => p.Id).Distinct().Count()); // keys unique across the three tables
        Sql.Reset();
        Assert.Equal(3, (await db.Set<Payment>().ToListAsync()).Count);
        Assert.Contains("UNION ALL", Sql.All);
    }

    [Exercise]
    public async Task Ex4_JsonColumns()
    {
        var db = await CreateModelAsync(Ex4);

        Assert.Equal(["Order"], await Reference.TablesAsync());
        Assert.Equal(["Customer", "Id", "Notes", "Shipping"], (await ColumnNames("Order")).Order());
        db.Add(new Order { Customer = "a", Shipping = new() { City = "Seattle", Carrier = "DHL" }, Notes = [new() { Text = "hi", At = new DateTime(2026, 10, 1) }] });
        await db.SaveChangesAsync();
        var json = (await Reference.Rows<string>($"SELECT Shipping AS Value FROM [Order]")).Single();
        Assert.Contains("\"City\":\"Seattle\"", json);
        var loaded = await NewContext(Ex4).Set<Order>().SingleAsync();
        Assert.Equal("Seattle", loaded.Shipping.City);
        Assert.Equal("hi", Assert.Single(loaded.Notes).Text);
    }

    [Exercise]
    public async Task Ex5_FilterOnAJsonValue()
    {
        var db = await OrdersAsync();

        Assert.Equal([1, 3], await Ex5(db, "Seattle"));
        Sql.AssertRoundTrips(1);
        Assert.Contains("JSON_VALUE([o].[Shipping], '$.City')", Sql.All); // filtered inside the JSON, in SQL
    }

    [Exercise]
    public async Task Ch1_SearchAJsonCollection()
    {
        var db = await OrdersAsync();

        Assert.Equal([1], await Ch1(db, "urgent"));
        Sql.AssertRoundTrips(1);
        Assert.Contains("OPENJSON([o].[Notes]", Sql.All); // the notes array, expanded in SQL
    }

    [Exercise]
    public async Task Ch2_UpdateInsideJson()
    {
        var db = await OrdersAsync();

        Assert.Equal(2, await Ch2(db, "Seattle", "FedEx"));

        Sql.AssertRoundTrips(1);
        Assert.Contains("JSON_MODIFY(", Sql.All);
        var carriers = await Reference.Rows<string>($"SELECT JSON_VALUE(Shipping, '$.Carrier') AS Value FROM [Order] ORDER BY Id");
        Assert.Equal(["FedEx", "DHL", "FedEx"], carriers);
    }

    [Exercise]
    public async Task Ch3_TotalsPerKind()
    {
        var db = await CreateModelAsync(PaymentsInOneTable);
        db.AddRange([.. OneOfEach(), new CardPayment { Amount = 5m, Last4 = "1111" }]);
        await db.SaveChangesAsync();
        Sql.Reset();

        var totals = await Ch3(NewContext(PaymentsInOneTable));

        Assert.Equal([new KindTotal("bank", 100m), new KindTotal("card", 15m), new KindTotal("voucher", 7m)], totals);
        Sql.AssertRoundTrips(1);
        Assert.Contains("GROUP BY [p].[Kind]", Sql.All); // grouped on the discriminator column itself
    }
}
```

- [ ] **Step 3: Write `Exercises.cs`**

```csharp
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E11;

// E11 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E11Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E11Exercises : E11Checks
{
    // Exercise 1 — Table per hierarchy
    // Map Payment, CardPayment, BankTransfer and Voucher (E11Model.cs) to ONE table named Payments,
    // with the type stored in a discriminator column named Kind (nvarchar(10)) holding "card", "bank"
    // or "voucher".
    protected override void Ex1(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 2 — Table per type
    // Map the same hierarchy with TPT: a Payments table for the shared columns (Id, Amount), and one
    // table per subtype for its own columns: CardPayments, BankTransfers, Vouchers.
    protected override void Ex2(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 3 — Table per concrete type
    // Map the hierarchy with TPC: one complete table per concrete type (CardPayments, BankTransfers,
    // Vouchers, each with Id and Amount), no table for the abstract Payment, and Ids unique across all
    // three tables.
    protected override void Ex3(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 4 — JSON columns
    // Map Order so that Shipping (one object) and Notes (a list) are each stored as JSON in a single
    // column, named Shipping and Notes.
    protected override void Ex4(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 5 — Filter on a JSON value
    // Return the Ids of the orders shipped to `city`, ordered by Id. `db` uses Checks.OrderModel
    // (Shipping and Notes as JSON).
    // Rules: one round trip; the filter runs in SQL.
    protected override Task<List<int>> Ex5(LabModelContext db, string city)
        => throw new NotImplementedException();

    // Challenge 1 — Search a JSON collection
    // Return the Ids of the orders with at least one note whose Text contains `word`, ordered by Id.
    // Rules: one round trip; the search runs in SQL.
    protected override Task<List<int>> Ch1(LabModelContext db, string word)
        => throw new NotImplementedException();

    // Challenge 2 — Update inside JSON
    // Set Shipping.Carrier to `carrier` for every order shipped to `city`, in one statement, and return
    // the number of orders updated.
    // Rules: one round trip; load nothing.
    protected override Task<int> Ch2(LabModelContext db, string city, string carrier)
        => throw new NotImplementedException();

    // Challenge 3 — Totals per kind
    // `db` maps the payments with TPH (Checks.PaymentsInOneTable: discriminator column Kind). Return the
    // total Amount per Kind, ordered by Kind.
    // Rules: one round trip; group on the discriminator column itself.
    protected override Task<List<KindTotal>> Ch3(LabModelContext db)
        => throw new NotImplementedException();
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E11Exercises`
Expected: `Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

- [ ] **Step 4: Write `Solutions.cs`**

```csharp
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E11;

// Worked solutions for E11, each followed by a Why note. Run: dotnet test --filter E11Solutions
public class E11Solutions : E11Checks
{
    // Exercise 1 — Table per hierarchy
    // Map Payment, CardPayment, BankTransfer and Voucher (E11Model.cs) to ONE table named Payments,
    // with the type stored in a discriminator column named Kind (nvarchar(10)) holding "card", "bank"
    // or "voucher".
    protected override void Ex1(ModelBuilder model)
        => model.Entity<Payment>(payment =>
        {
            payment.ToTable("Payments");
            payment.HasDiscriminator<string>("Kind")
                .HasValue<CardPayment>("card")
                .HasValue<BankTransfer>("bank")
                .HasValue<Voucher>("voucher");
            payment.Property<string>("Kind").HasMaxLength(10);
        });
    // Why: TPH is EF's default for a hierarchy. Every subtype's columns go into one table (nullable,
    // since a card row has no Iban), and a discriminator column says which type a row is. Querying one
    // subtype is a single-table query with WHERE [Kind] = N'card'. HasValue registers each subtype and
    // its value; the column is a shadow property you can still configure.

    // Exercise 2 — Table per type
    // Map the same hierarchy with TPT: a Payments table for the shared columns (Id, Amount), and one
    // table per subtype for its own columns: CardPayments, BankTransfers, Vouchers.
    protected override void Ex2(ModelBuilder model)
    {
        model.Entity<Payment>().UseTptMappingStrategy().ToTable("Payments");
        model.Entity<CardPayment>().ToTable("CardPayments");
        model.Entity<BankTransfer>().ToTable("BankTransfers");
        model.Entity<Voucher>().ToTable("Vouchers");
    }
    // Why: TPT normalizes: no nullable subtype columns, and each subtype table's Id is also an FK to
    // Payments. The cost is joins: reading payments of any type LEFT JOINs every subtype table, and
    // inserting a card writes two rows. It is usually the slowest strategy for queries.

    // Exercise 3 — Table per concrete type
    // Map the hierarchy with TPC: one complete table per concrete type (CardPayments, BankTransfers,
    // Vouchers, each with Id and Amount), no table for the abstract Payment, and Ids unique across all
    // three tables.
    protected override void Ex3(ModelBuilder model)
    {
        model.Entity<Payment>().UseTpcMappingStrategy();
        model.Entity<CardPayment>().ToTable("CardPayments");
        model.Entity<BankTransfer>().ToTable("BankTransfers");
        model.Entity<Voucher>().ToTable("Vouchers");
    }
    // Why: TPC gives each concrete type a complete table, so a query for one type touches one table
    // with no discriminator and no join. Querying the base type is a UNION ALL. Keys must stay unique
    // across tables, so EF replaces IDENTITY with a shared sequence (PaymentSequence).

    // Exercise 4 — JSON columns
    // Map Order so that Shipping (one object) and Notes (a list) are each stored as JSON in a single
    // column, named Shipping and Notes.
    protected override void Ex4(ModelBuilder model)
        => model.Entity<Order>(order =>
        {
            order.ComplexProperty(o => o.Shipping, shipping => shipping.ToJson());
            order.ComplexCollection(o => o.Notes, notes => notes.ToJson());
        });
    // Why: ToJson() serializes the value into one nvarchar(max) column (on SQL Server 2022; SQL Server
    // 2025 and Azure SQL can use the native json type). Complex types are the EF 10 way to map JSON;
    // owned types with ToJson() also work, but carry entity semantics a value doesn't need.

    // Exercise 5 — Filter on a JSON value
    // Return the Ids of the orders shipped to `city`, ordered by Id. `db` uses Checks.OrderModel
    // (Shipping and Notes as JSON).
    // Rules: one round trip; the filter runs in SQL.
    protected override Task<List<int>> Ex5(LabModelContext db, string city)
        => db.Set<Order>()
            .Where(o => o.Shipping.City == city)
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .ToListAsync();
    // Why: EF translates a path into JSON like a column: o.Shipping.City becomes
    // JSON_VALUE([o].[Shipping], '$.City'). No deserializing on the client. Note that SQL Server can't
    // seek an index on a value inside JSON unless you add a computed column for it.

    // Challenge 1 — Search a JSON collection
    // Return the Ids of the orders with at least one note whose Text contains `word`, ordered by Id.
    // Rules: one round trip; the search runs in SQL.
    protected override Task<List<int>> Ch1(LabModelContext db, string word)
        => db.Set<Order>()
            .Where(o => o.Notes.Any(n => n.Text.Contains(word)))
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .ToListAsync();
    // Why: a JSON array is queryable like a collection: EF expands it with OPENJSON(...) WITH (...) and
    // turns Any into EXISTS over those rows. Loading the orders to search their notes in C# would ship
    // every order and its JSON.

    // Challenge 2 — Update inside JSON
    // Set Shipping.Carrier to `carrier` for every order shipped to `city`, in one statement, and return
    // the number of orders updated.
    // Rules: one round trip; load nothing.
    protected override Task<int> Ch2(LabModelContext db, string city, string carrier)
        => db.Set<Order>()
            .Where(o => o.Shipping.City == city)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.Shipping.Carrier, carrier));
    // Why: EF 10 lets ExecuteUpdate set a property inside a JSON column. It becomes
    // JSON_MODIFY([o].[Shipping], '$.Carrier', @p), changing that one value in place. Before EF 10 you
    // had to load, change and save each order, rewriting the whole JSON document.

    // Challenge 3 — Totals per kind
    // `db` maps the payments with TPH (Checks.PaymentsInOneTable: discriminator column Kind). Return the
    // total Amount per Kind, ordered by Kind.
    // Rules: one round trip; group on the discriminator column itself.
    protected override Task<List<KindTotal>> Ch3(LabModelContext db)
        => db.Set<Payment>()
            .GroupBy(p => EF.Property<string>(p, "Kind"))
            .OrderBy(g => g.Key)
            .Select(g => new KindTotal(g.Key, g.Sum(p => p.Amount)))
            .ToListAsync();
    // Why: the discriminator is a shadow property, so EF.Property<string>(p, "Kind") reaches it like any
    // column and the GROUP BY is on [Kind]. Tempting-but-wrong: grouping on p is CardPayment ? "card" :
    // ..., which works but makes SQL compute a CASE for every row. Order before projecting into the
    // record: EF can't compose after a positional record's constructor (E02).
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E11Solutions`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 5: Write `Examples.cs`**

```csharp
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E11;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E11Examples --logger "console;verbosity=detailed"
// Each example builds its own model in EfLab_E11, inside the test's transaction.
public class E11Examples() : CodeFirstTest("E11")
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    static void Tph(ModelBuilder model) { model.Entity<Payment>(); model.Entity<CardPayment>(); model.Entity<BankTransfer>(); model.Entity<Voucher>(); }
    static void Tpt(ModelBuilder model) { model.Entity<Payment>().UseTptMappingStrategy(); model.Entity<CardPayment>(); model.Entity<BankTransfer>(); model.Entity<Voucher>(); }
    static void Tpc(ModelBuilder model) { model.Entity<Payment>().UseTpcMappingStrategy(); model.Entity<CardPayment>(); model.Entity<BankTransfer>(); model.Entity<Voucher>(); }

    // Concept 1: with no strategy configured, a hierarchy is one table plus a Discriminator column.
    [Fact]
    public async Task Default_mapping_is_TPH()
    {
        var db = await CreateModelAsync(Tph);
        Print(db.Database.GenerateCreateScript());

        Assert.Equal(["Payment"], await Reference.TablesAsync());
        var columns = await Reference.ColumnsAsync("Payment");
        Assert.Contains(new ColumnInfo("Discriminator", "nvarchar", 13, false), columns); // EF sizes it from the type names
        Assert.Contains(new ColumnInfo("Iban", "nvarchar", -1, true), columns);           // subtype columns are nullable
    }

    // Concept 2: the same query for one subtype, in each strategy.
    [Fact]
    public void One_subtype_query_in_each_strategy()
    {
        var tph = NewContext(Tph).Set<CardPayment>().ToQueryString();
        var tpt = NewContext(Tpt).Set<CardPayment>().ToQueryString();
        var tpc = NewContext(Tpc).Set<CardPayment>().ToQueryString();
        Print($"TPH:\n{tph}\n\nTPT:\n{tpt}\n\nTPC:\n{tpc}");

        Assert.Contains("WHERE [p].[Discriminator] = N'CardPayment'", tph); // one table, filtered
        Assert.Contains("INNER JOIN [CardPayment]", tpt);                    // base table + subtype table
        Assert.DoesNotContain("JOIN", tpc);                                  // one complete table
        Assert.DoesNotContain("WHERE", tpc);
    }

    // Concept 2: the same query for the base type, in each strategy.
    [Fact]
    public void Base_type_query_in_each_strategy()
    {
        var tph = NewContext(Tph).Set<Payment>().ToQueryString();
        var tpt = NewContext(Tpt).Set<Payment>().ToQueryString();
        var tpc = NewContext(Tpc).Set<Payment>().ToQueryString();
        Print($"TPH:\n{tph}\n\nTPT:\n{tpt}\n\nTPC:\n{tpc}");

        Assert.DoesNotContain("JOIN", tph);
        Assert.Equal(3, tpt.Split("LEFT JOIN").Length - 1); // one per subtype
        Assert.Equal(2, tpc.Split("UNION ALL").Length - 1); // three tables stacked
    }

    // Concept 2: TPC keys come from a shared sequence so they stay unique across tables.
    [Fact]
    public async Task TPC_keys_come_from_a_sequence()
    {
        var db = await CreateModelAsync(Tpc);
        Assert.Contains("CREATE SEQUENCE [PaymentSequence]", db.Database.GenerateCreateScript());

        var card = new CardPayment { Amount = 1m, Last4 = "4242" };
        var voucher = new Voucher { Amount = 2m, Code = "X" };
        db.AddRange(card, voucher);
        await db.SaveChangesAsync();
        Assert.NotEqual(card.Id, voucher.Id);
    }

    // Concept 3: owned and complex types both map to one JSON column with ToJson().
    [Fact]
    public void Owned_or_complex_both_store_JSON()
    {
        var owned = NewContext(model => model.Entity<Order>(o =>
        {
            o.OwnsOne(x => x.Shipping, s => s.ToJson());
            o.OwnsMany(x => x.Notes, n => n.ToJson());
        }));
        var complex = NewContext(model => model.Entity<Order>(o =>
        {
            o.ComplexProperty(x => x.Shipping, s => s.ToJson());
            o.ComplexCollection(x => x.Notes, n => n.ToJson());
        }));

        foreach (var script in new[] { owned.Database.GenerateCreateScript(), complex.Database.GenerateCreateScript() })
        {
            Assert.Contains("[Shipping] nvarchar(max)", script); // one column for the whole object
            Assert.Contains("[Notes] nvarchar(max)", script);    // one column for the whole list
        }
    }

    // Concept 4: paths into JSON translate to JSON_VALUE and OPENJSON.
    [Fact]
    public async Task Querying_into_JSON()
    {
        var db = await CreateModelAsync(model => model.Entity<Order>(o =>
        {
            o.ComplexProperty(x => x.Shipping, s => s.ToJson());
            o.ComplexCollection(x => x.Notes, n => n.ToJson());
        }));
        db.Add(new Order
        {
            Customer = "a",
            Shipping = new() { City = "Oslo", Carrier = "Posten" },
            Notes = [new() { Text = "one", At = new DateTime(2026, 1, 1) }, new() { Text = "two", At = new DateTime(2026, 1, 2) }],
        });
        await db.SaveChangesAsync();
        Sql.Reset();

        var row = await db.Set<Order>()
            .Select(o => new { o.Shipping.City, NoteCount = o.Notes.Count })
            .SingleAsync();

        Assert.Equal("Oslo", row.City);
        Assert.Equal(2, row.NoteCount);
        Assert.Contains("JSON_VALUE([o].[Shipping], '$.City')", Sql.All);
        Assert.Contains("OPENJSON([o].[Notes]", Sql.All);
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E11Examples`
Expected: `Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6`.

- [ ] **Step 6: Write `README.md`**

````markdown
# E11 — Inheritance & JSON Columns

## What you'll learn

- The three ways to store a class hierarchy (TPH, TPT, TPC): the schema each produces, the SQL each generates, and what that costs
- Discriminators, and querying on them
- Mapping objects and lists to JSON columns with `ToJson()` (complex types in EF 10, or owned types)
- Querying into JSON (`JSON_VALUE`, `OPENJSON`) and updating inside it in place with `ExecuteUpdate` (`JSON_MODIFY`, new in EF 10)

## Builds on

- T-SQL [lesson 09](../../../lessons/09-ddl-and-constraints/lesson.ipynb) — tables, keys and constraints (the T-SQL track has no JSON lesson: this lesson introduces `JSON_VALUE`, `OPENJSON` and `JSON_MODIFY` through the SQL EF generates)
- EF lesson [E10](../E10-fluent-api-modeling/README.md) — complex types, owned types and shadow properties

## Setup

```
dotnet test --filter E11Examples --logger "console;verbosity=detailed"
```

Code-first, like E10. The classes are in `E11Model.cs`, and each model exercise is a configuration method that the check builds in the **`EfLab_E11`** database inside the test's transaction. Exercise 5 and the challenges are queries, run against the models defined at the top of `Checks.cs` (`OrderModel`, `PaymentsInOneTable`).

## Concepts

### 1. Table per hierarchy (TPH), the default

Map a hierarchy without choosing a strategy and EF uses TPH: one table for all the types, every subtype's columns included (nullable, because a card row has no `Iban`), and a **discriminator** column naming each row's type. By default the column is called `Discriminator` and holds the CLR type name, sized from those names. `HasDiscriminator<string>("Kind").HasValue<CardPayment>("card")...` renames it and picks the values. The discriminator is a shadow property, so queries can use it: `EF.Property<string>(p, "Kind")`.

Example: `Default_mapping_is_TPH`.

### 2. TPH vs TPT vs TPC

| | TPH | TPT (`UseTptMappingStrategy`) | TPC (`UseTpcMappingStrategy`) |
|---|---|---|---|
| Tables | one | one for the base, one per subtype | one per concrete type, none for an abstract base |
| Subtype columns | nullable, in the shared table | in the subtype's table | in the subtype's table, with the base columns repeated |
| Query one subtype | one table, `WHERE [Discriminator] = ...` | base `INNER JOIN` subtype | one table, no filter |
| Query the base type | one table | `LEFT JOIN` every subtype table | `UNION ALL` of every table |
| Insert one entity | one row | one row per level | one row |
| Keys | `IDENTITY` | `IDENTITY` on the base | a shared **sequence**, so keys stay unique across tables |

Rules of thumb from the EF docs: TPH is usually fastest and the default for good reason. TPC suits hierarchies where you mostly query one leaf type. TPT normalizes the schema but pays in joins on almost every query.

Examples: `One_subtype_query_in_each_strategy`, `Base_type_query_in_each_strategy`, `TPC_keys_come_from_a_sequence`.

### 3. JSON columns

`ToJson()` stores a whole object or list in one column: `nvarchar(max)` on SQL Server 2022 (SQL Server 2025 and Azure SQL can use the native `json` type).

```csharp
model.Entity<Order>(o =>
{
    o.ComplexProperty(x => x.Shipping, s => s.ToJson());   // one object
    o.ComplexCollection(x => x.Notes, n => n.ToJson());   // a list
});
```

EF 10 maps JSON through **complex types** (values, no keys). Owned types with `OwnsOne`/`OwnsMany(...).ToJson()` produce the same column, but they're entity types with hidden keys. Use JSON for data you read and write with its owner and rarely filter on. Separate tables are still better for data you join, index or update independently.

Example: `Owned_or_complex_both_store_JSON`.

### 4. Querying and updating JSON

EF translates paths into the JSON:

| LINQ | SQL |
|---|---|
| `o.Shipping.City == "Oslo"` | `JSON_VALUE([o].[Shipping], '$.City') = N'Oslo'` |
| `o.Notes.Any(n => n.Text.Contains(w))` | `EXISTS (SELECT 1 FROM OPENJSON([o].[Notes], '$') WITH (...) ...)` |
| `o.Notes.Count` | `(SELECT COUNT(*) FROM OPENJSON([o].[Notes], '$'))` |
| `ExecuteUpdate(s => s.SetProperty(o => o.Shipping.Carrier, c))` (EF 10) | `SET [Shipping] = JSON_MODIFY([Shipping], '$.Carrier', @p)` |

All of it runs in SQL, but none of it can seek an index. A `JSON_VALUE` filter scans every row unless you add a computed column on that path and index it.

Example: `Querying_into_JSON`.

## Pitfalls

- **TPT by habit.** Normalized tables feel right, and every query pays with joins.
- **TPC with `IDENTITY` in mind.** Keys come from a sequence, so don't rely on per-table identity values.
- **Filtering on a TPH subtype in C#.** `p is CardPayment` after `ToList()` loads every payment. Query `Set<CardPayment>()` or `OfType<CardPayment>()`.
- **JSON for data you search a lot.** `JSON_VALUE` filters can't use an index without a computed column.
- **Rewriting a whole JSON document to change one value.** In EF 10, `ExecuteUpdate` changes it in place.

## Exercises

```
dotnet test --filter E11Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
````

- [ ] **Step 7: Prompts are identical in Exercises.cs and Solutions.cs**

```bash
cd efcore && python - lessons/E11-inheritance-and-json <<'EOF'
import pathlib, sys
def prompts(path):
    blocks, current = [], []
    for line in pathlib.Path(path).read_text(encoding="utf-8").splitlines():
        text = line.strip()
        if text.startswith(("// Exercise", "// Challenge")):
            current = [text]
        elif current and text.startswith("//"):
            current.append(text)
        elif current and text.startswith("protected override"):
            blocks.append(current)
            current = []
    return blocks
lesson = sys.argv[1]
exercises, solutions = prompts(f"{lesson}/Exercises.cs"), prompts(f"{lesson}/Solutions.cs")
assert exercises == solutions, "prompts differ between Exercises.cs and Solutions.cs"
print(f"{len(exercises)} prompts identical")
EOF
```

Expected: `8 prompts identical`.

- [ ] **Step 8: Commit**

```bash
git add efcore/lessons/E11-inheritance-and-json
git commit -m "feat: add EF Core lesson E11 - inheritance and JSON columns"
```

- [ ] **Step 9: A wrong answer fails for the right reason.** Grouping on a type test instead of the discriminator column makes SQL compute a `CASE` per row. Temporarily replace `Ch3`'s stub line in `Exercises.cs` with:

```csharp
        => db.Set<Payment>()
            .GroupBy(p => p is CardPayment ? "card" : p is BankTransfer ? "bank" : "voucher")
            .OrderBy(g => g.Key)
            .Select(g => new KindTotal(g.Key, g.Sum(p => p.Amount)))
            .ToListAsync();
```

Run: `cd efcore && dotnet build && dotnet test --no-build --filter "FullyQualifiedName~E11Exercises.Ch3"`
Expected: `Failed: 1` with `Assert.Contains() Failure: Sub-string not found` (no `GROUP BY [p].[Kind]`). Then restore and confirm a clean tree:

```bash
git checkout -- efcore/lessons/E11-inheritance-and-json/Exercises.cs
git status --short
```

Expected: `git status --short` prints nothing.

---

## Task 4: Lesson E12 — Migrations workflow

**Files:**
- Create: `efcore/lessons/E12-migrations-workflow/Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`
- Create: `efcore/lessons/E12-migrations-workflow/SolutionMigrations/` (11 files: five migrations, their Designer files, the model snapshot)

**Interfaces:**
- Consumes: `LabDatabase` (`For`, `RecreateAsync`, `DropAsync`), `LabConnection.For/OpenAsync`, `Reference` + `Schema`/`ColumnInfo` (Task 1), `[Exercise]`; the local tool `dotnet-ef` 10.0.12.
- Produces: namespaces `EfCoreLessons.E12` (record `SplitRow`; classes `E12Checks`, `E12Exercises`, `E12Solutions`, `E12Examples`), `EfCoreLessons.E12.Exercise` (`Customer`, `E12ExerciseContext`, `E12ExerciseContextFactory`), `EfCoreLessons.E12.Solution` (`Customer`, `CustomerStatus`, `E12SolutionContext`, `E12SolutionContextFactory`), `EfCoreLessons.E12.Solution.Migrations` (the five migrations). Nothing later depends on them.

The solution's migrations were generated with `dotnet ef migrations add <Name> --context E12SolutionContext --output-dir lessons/E12-migrations-workflow/SolutionMigrations --namespace EfCoreLessons.E12.Solution.Migrations`, one per exercise, and then edited where the Why notes say (RenameEmail, AddIsActive, SplitFullName). Write them exactly as given; do not regenerate them (timestamps and hand edits would differ).

- [ ] **Step 1: Write `Checks.cs`**

```csharp
using System.Text.RegularExpressions;
using EfCoreLessons.Lab;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EfCoreLessons.E12;

public record SplitRow(string FirstName, string LastName, string ContactEmail, bool IsActive);

// The checks for E12. E12Exercises (yours) and E12Solutions both inherit them.
// Every check starts from an empty EfLab_E12 database, applies YOUR migrations (the ones in
// ExerciseMigrations/, created with `dotnet ef migrations add`) step by step, inserts rows in
// between, and checks the schema and that the data survived. Migrations commit for real, so these
// tests use a real (EfLab_) database instead of a rolled-back transaction.
public abstract class E12Checks : IAsyncLifetime
{
    protected static readonly string Database = LabDatabase.For("E12");

    // The context whose migrations are applied: E12ExerciseContext in Exercises.cs, E12SolutionContext
    // in Solutions.cs. Not an exercise: it's already written for you.
    protected abstract DbContext CreateContext(string connectionString, Func<DbContext, bool, CancellationToken, Task>? seeding = null);

    protected abstract string Ex1();
    protected abstract string Ex2();
    protected abstract string Ex3();
    protected abstract string Ex4();
    protected abstract string Ex5();
    protected abstract string Ch1(DbContext db);
    protected abstract Task<List<string>> Ch2(DbContext db);
    protected abstract Func<DbContext, bool, CancellationToken, Task> Ch3();

    public async ValueTask InitializeAsync() => await LabDatabase.RecreateAsync(Database);

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask; // the next test recreates the database
    }

    DbContext NewContext() => CreateContext(LabConnection.For(Database));

    // The full id (20261012123421_Initial) of the migration you named, or a failure that says what to do.
    static string IdOf(DbContext db, string name)
        => db.Database.GetMigrations().SingleOrDefault(m => m.EndsWith("_" + name, StringComparison.Ordinal))
           ?? throw new InvalidOperationException(
               $"No migration named '{name}' for {db.GetType().Name}. Create it with: dotnet ef migrations add {name} " +
               $"--context {db.GetType().Name} --output-dir lessons/E12-migrations-workflow/ExerciseMigrations");

    async Task MigrateToAsync(string name)
    {
        await using var db = NewContext();
        await db.GetService<IMigrator>().MigrateAsync(IdOf(db, name));
    }

    static async Task ExecuteAsync(string sql)
    {
        await using var connection = await LabConnection.OpenAsync(Database);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    async Task<List<T>> RowsAsync<T>(FormattableString sql)
    {
        await using var db = NewContext();
        return await db.Database.SqlQuery<T>(sql).ToListAsync();
    }

    async Task<List<ColumnInfo>> CustomerColumnsAsync()
    {
        await using var db = NewContext();
        return await new Reference(db).ColumnsAsync("Customers");
    }

    [Exercise]
    public async Task Ex1_InitialMigration()
    {
        await MigrateToAsync(Ex1());

        Assert.Equal(
            [new("Id", "int", null, false), new("FullName", "nvarchar", 100, false), new("Email", "nvarchar", 200, false)],
            await CustomerColumnsAsync());
    }

    [Exercise]
    public async Task Ex2_RenameWithoutLosingData()
    {
        await MigrateToAsync(Ex1());
        await ExecuteAsync("INSERT INTO Customers (FullName, Email) VALUES (N'Ada Lovelace', N'ada@example.com'), (N'Alan Turing', N'alan@example.com')");

        await MigrateToAsync(Ex2());

        var columns = await CustomerColumnsAsync();
        Assert.Contains(new ColumnInfo("ContactEmail", "nvarchar", 320, false), columns);
        Assert.DoesNotContain(columns, c => c.Column == "Email");
        Assert.Equal(["ada@example.com", "alan@example.com"],
            (await RowsAsync<string>($"SELECT ContactEmail AS Value FROM Customers")).Order()); // the data survived
    }

    [Exercise]
    public async Task Ex3_NewRequiredColumnForExistingRows()
    {
        await MigrateToAsync(Ex2());
        await ExecuteAsync("INSERT INTO Customers (FullName, ContactEmail) VALUES (N'Ada Lovelace', N'ada@example.com'), (N'Alan Turing', N'alan@example.com')");

        await MigrateToAsync(Ex3());

        Assert.Contains(new ColumnInfo("IsActive", "bit", null, false), await CustomerColumnsAsync());
        Assert.Equal([true, true], await RowsAsync<bool>($"SELECT IsActive AS Value FROM Customers")); // existing customers stay active
    }

    [Exercise]
    public async Task Ex4_SplitAColumnBothWays()
    {
        await MigrateToAsync(Ex3());
        await ExecuteAsync("INSERT INTO Customers (FullName, ContactEmail, IsActive) VALUES (N'Ada Lovelace', N'ada@example.com', 1), (N'Plato', N'plato@example.com', 0)");

        await MigrateToAsync(Ex4());

        var columns = (await CustomerColumnsAsync()).Select(c => c.Column).ToList();
        Assert.DoesNotContain("FullName", columns);
        Assert.Equal(
            [new SplitRow("Ada", "Lovelace", "ada@example.com", true), new SplitRow("Plato", "", "plato@example.com", false)],
            await RowsAsync<SplitRow>($"SELECT FirstName, LastName, ContactEmail, IsActive FROM Customers ORDER BY ContactEmail"));

        await MigrateToAsync(Ex3()); // Down must put FullName back, from the two halves
        Assert.Equal(["Ada Lovelace", "Plato"], await RowsAsync<string>($"SELECT FullName AS Value FROM Customers ORDER BY ContactEmail"));
    }

    [Exercise]
    public async Task Ex5_SeedReferenceData()
    {
        await MigrateToAsync(Ex5());

        Assert.Equal(["Active", "Suspended"], await RowsAsync<string>($"SELECT Name AS Value FROM CustomerStatus ORDER BY Id"));
    }

    [Exercise]
    public async Task Ch1_IdempotentScript()
    {
        await using var db = NewContext();
        var script = Ch1(db);
        var migrations = db.Database.GetMigrations().ToList();

        Assert.Contains("IF NOT EXISTS", script);
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline).Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
        foreach (var run in new[] { 1, 2 }) // a second run must change nothing and fail nowhere
            foreach (var batch in batches)
                await ExecuteAsync(batch);

        Assert.Equal(migrations, await RowsAsync<string>($"SELECT MigrationId AS Value FROM __EFMigrationsHistory ORDER BY MigrationId"));
    }

    [Exercise]
    public async Task Ch2_PendingMigrations()
    {
        await MigrateToAsync(Ex2());
        await using var db = NewContext();
        var all = db.Database.GetMigrations().ToList();

        var pending = await Ch2(db);

        Assert.Equal(all.SkipWhile(m => !m.EndsWith("_" + Ex2(), StringComparison.Ordinal)).Skip(1).ToList(), pending);
        Assert.NotEmpty(pending);
    }

    [Exercise]
    public async Task Ch3_IdempotentSeeding()
    {
        var seeding = Ch3();

        for (var start = 0; start < 2; start++) // two app starts: seeding runs on every Migrate
        {
            await using var db = CreateContext(LabConnection.For(Database), seeding);
            await db.Database.MigrateAsync();
        }

        Assert.Equal(1, (await RowsAsync<int>($"SELECT COUNT(*) AS Value FROM Customers WHERE ContactEmail = N'store@example.com'")).Single());
    }
}
```

- [ ] **Step 2: Write `Exercises.cs`** (the learner's starting model, and the stubs)

```csharp
using EfCoreLessons.E12.Exercise;
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EfCoreLessons.E12
{
    // E12 exercises. Unlike other lessons, most of the work here happens outside this class: you
    // change the model below (Customer, E12ExerciseContext), run `dotnet ef migrations add`, and edit
    // the migration it generates in ExerciseMigrations/. Each exercise then returns the name of the
    // migration you made. Run:
    //   dotnet test --filter E12Exercises
    // Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
    // The checks are in Checks.cs. Peek at Solutions.cs (and SolutionMigrations/) only after attempting.
    public class E12Exercises : E12Checks
    {
        protected override DbContext CreateContext(string connectionString, Func<DbContext, bool, CancellationToken, Task>? seeding = null)
        {
            var options = new DbContextOptionsBuilder<E12ExerciseContext>().UseSqlServer(connectionString);
            if (seeding != null) options.UseAsyncSeeding(seeding);
            return new E12ExerciseContext(options.Options);
        }

        // Exercise 1 — The first migration
        // E12ExerciseContext (in Exercises.cs) maps Customer to a Customers table. From efcore/, after
        // `dotnet tool restore`, create its first migration:
        //   dotnet ef migrations add Initial --context E12ExerciseContext --output-dir lessons/E12-migrations-workflow/ExerciseMigrations
        // Then return the migration's name.
        protected override string Ex1()
            => throw new NotImplementedException();

        // Exercise 2 — Rename without losing data
        // Rename Customer.Email to ContactEmail and widen it to 320 characters (in the class and in
        // E12ExerciseContext's HasMaxLength), then add a migration for it. Read what was scaffolded before
        // trusting it: existing emails must survive. Return the migration's name.
        protected override string Ex2()
            => throw new NotImplementedException();

        // Exercise 3 — A new required column for existing rows
        // Add `public bool IsActive { get; set; }` to Customer, then add a migration. Every customer
        // that already exists must end up active (IsActive = 1). Return the migration's name.
        protected override string Ex3()
            => throw new NotImplementedException();

        // Exercise 4 — Split a column, both ways
        // Replace Customer.FullName with FirstName and LastName (nvarchar(50) each), then add a migration
        // that fills them from FullName ("Ada Lovelace" -> "Ada" / "Lovelace"; a single word -> FirstName
        // only, LastName empty). Its Down must rebuild FullName from the two. Return the migration's name.
        protected override string Ex4()
            => throw new NotImplementedException();

        // Exercise 5 — Seed reference data
        // Add a CustomerStatus entity (Id, Name nvarchar(20)) to E12ExerciseContext, with rows 1 "Active"
        // and 2 "Suspended" declared in the model with HasData, then add a migration. Return its name.
        protected override string Ex5()
            => throw new NotImplementedException();

        // Challenge 1 — An idempotent script
        // Return a SQL script of all of `db`'s migrations that is safe to run against a database at any
        // migration state, any number of times: what `dotnet ef migrations script --idempotent` writes.
        // Hint: if running it fails, remember SQL Server compiles a whole batch before running any of it,
        // including the parts an IF would skip. Your own migrationBuilder.Sql calls may need fixing.
        protected override string Ch1(DbContext db)
            => throw new NotImplementedException();

        // Challenge 2 — Pending migrations
        // Return the ids of `db`'s migrations that haven't been applied to its database yet, in order.
        protected override Task<List<string>> Ch2(DbContext db)
            => throw new NotImplementedException();

        // Challenge 3 — Idempotent seeding
        // Return an async seeding function (for UseAsyncSeeding) that makes sure a customer with
        // ContactEmail "store@example.com" (FirstName "Store", LastName "Default", active) exists. It runs
        // on every MigrateAsync / EnsureCreatedAsync, so it must never add a second one.
        protected override Func<DbContext, bool, CancellationToken, Task> Ch3()
            => throw new NotImplementedException();
    }
}

namespace EfCoreLessons.E12.Exercise
{
    // YOUR model. Start here and change it as the exercises ask; each change gets a migration.
    public class Customer
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class E12ExerciseContext(DbContextOptions<E12ExerciseContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder model)
            => model.Entity<Customer>(customer =>
            {
                customer.Property(c => c.FullName).HasMaxLength(100);
                customer.Property(c => c.Email).HasMaxLength(200);
            });
    }

    // Used by `dotnet ef` at design time. migrations add never connects; database update would, to EfLab_E12.
    public class E12ExerciseContextFactory : IDesignTimeDbContextFactory<E12ExerciseContext>
    {
        public E12ExerciseContext CreateDbContext(string[] args)
            => new(new DbContextOptionsBuilder<E12ExerciseContext>()
                .UseSqlServer(LabConnection.For(LabDatabase.For("E12"))).Options);
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E12Exercises`
Expected: `Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

- [ ] **Step 3: Write `Solutions.cs`** (the solution's final model, its design-time factory, and the answers)

```csharp
using EfCoreLessons.E12.Solution;
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EfCoreLessons.E12
{
    // Worked solutions for E12, each followed by a Why note. Run: dotnet test --filter E12Solutions
    // The solution's migrations are in SolutionMigrations/ (generated with dotnet ef, then edited
    // where the Why notes say so).
    public class E12Solutions : E12Checks
    {
        protected override DbContext CreateContext(string connectionString, Func<DbContext, bool, CancellationToken, Task>? seeding = null)
        {
            var options = new DbContextOptionsBuilder<E12SolutionContext>().UseSqlServer(connectionString);
            if (seeding != null) options.UseAsyncSeeding(seeding);
            return new E12SolutionContext(options.Options);
        }

        // Exercise 1 — The first migration
        // E12ExerciseContext (in Exercises.cs) maps Customer to a Customers table. From efcore/, after
        // `dotnet tool restore`, create its first migration:
        //   dotnet ef migrations add Initial --context E12ExerciseContext --output-dir lessons/E12-migrations-workflow/ExerciseMigrations
        // Then return the migration's name.
        protected override string Ex1() => "Initial";
        // Why: `migrations add` compares the model with the last snapshot (none yet) and scaffolds the
        // operations that get from one to the other: CreateTable here. It writes three files: the
        // migration (Up/Down), its Designer.cs (the model at that point) and the context's ModelSnapshot.cs
        // (the model now). The snapshot is what the next migration diffs against, so commit all three.

        // Exercise 2 — Rename without losing data
        // Rename Customer.Email to ContactEmail and widen it to 320 characters (in the class and in
        // E12ExerciseContext's HasMaxLength), then add a migration for it. Read what was scaffolded before
        // trusting it: existing emails must survive. Return the migration's name.
        protected override string Ex2() => "RenameEmail";
        // Why: EF matches a renamed property to its old column only when nothing else about it changed.
        // Rename AND widen, and it scaffolds DropColumn("Email") + AddColumn("ContactEmail") with the
        // warning "An operation was scaffolded that may result in the loss of data". Applied as is, every
        // email is deleted. The fix is to edit the migration into RenameColumn + AlterColumn, and to write
        // Down as the reverse.

        // Exercise 3 — A new required column for existing rows
        // Add `public bool IsActive { get; set; }` to Customer, then add a migration. Every customer
        // that already exists must end up active (IsActive = 1). Return the migration's name.
        protected override string Ex3() => "AddIsActive";
        // Why: a new NOT NULL column needs a value for the rows already there, and EF scaffolds the CLR
        // default: defaultValue: false, so every existing customer silently becomes inactive. Backfill in the
        // migration (migrationBuilder.Sql("UPDATE Customers SET IsActive = 1")), or scaffold
        // defaultValue: true if new rows should default to active too. Either way, decide it explicitly.

        // Exercise 4 — Split a column, both ways
        // Replace Customer.FullName with FirstName and LastName (nvarchar(50) each), then add a migration
        // that fills them from FullName ("Ada Lovelace" -> "Ada" / "Lovelace"; a single word -> FirstName
        // only, LastName empty). Its Down must rebuild FullName from the two. Return the migration's name.
        protected override string Ex4() => "SplitFullName";
        // Why: EF can't know that two new columns come from an old one, so it scaffolds DropColumn first
        // and the names are gone before anything could copy them. Reorder by hand: add the new columns,
        // copy with migrationBuilder.Sql(...), then drop the old one. Down runs when someone rolls back
        // (dotnet ef database update <earlier>), so it needs the same care in reverse.

        // Exercise 5 — Seed reference data
        // Add a CustomerStatus entity (Id, Name nvarchar(20)) to E12ExerciseContext, with rows 1 "Active"
        // and 2 "Suspended" declared in the model with HasData, then add a migration. Return its name.
        protected override string Ex5() => "SeedStatuses";
        // Why: HasData makes the rows part of the model, so the migration contains InsertData, and
        // changing a row later scaffolds UpdateData. It suits fixed reference data with known keys. It
        // can't express "insert if missing" or anything computed at run time. That's UseSeeding (challenge 3).

        // Challenge 1 — An idempotent script
        // Return a SQL script of all of `db`'s migrations that is safe to run against a database at any
        // migration state, any number of times: what `dotnet ef migrations script --idempotent` writes.
        // Hint: if running it fails, remember SQL Server compiles a whole batch before running any of it,
        // including the parts an IF would skip. Your own migrationBuilder.Sql calls may need fixing.
        protected override string Ch1(DbContext db)
            => db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        // Why: each migration in an idempotent script is wrapped in IF NOT EXISTS (SELECT * FROM
        // __EFMigrationsHistory WHERE MigrationId = ...), so applied ones are skipped. That's the script a
        // DBA can review and run at deploy time, without the app's code or connection strings. A
        // migration bundle (`dotnet ef migrations bundle`) does the same job as an executable.

        // Challenge 2 — Pending migrations
        // Return the ids of `db`'s migrations that haven't been applied to its database yet, in order.
        protected override async Task<List<string>> Ch2(DbContext db)
            => [.. await db.Database.GetPendingMigrationsAsync()];
        // Why: EF compares the migrations compiled into the assembly with the rows in
        // __EFMigrationsHistory. Checking this at startup (and failing fast, or alerting) is safer than
        // calling Migrate() from every instance of a web app at once.

        // Challenge 3 — Idempotent seeding
        // Return an async seeding function (for UseAsyncSeeding) that makes sure a customer with
        // ContactEmail "store@example.com" (FirstName "Store", LastName "Default", active) exists. It runs
        // on every MigrateAsync / EnsureCreatedAsync, so it must never add a second one.
        protected override Func<DbContext, bool, CancellationToken, Task> Ch3()
            => async (db, _, cancellationToken) =>
            {
                var customers = db.Set<Customer>();
                if (await customers.AnyAsync(c => c.ContactEmail == "store@example.com", cancellationToken)) return;
                customers.Add(new Customer { FirstName = "Store", LastName = "Default", ContactEmail = "store@example.com", IsActive = true });
                await db.SaveChangesAsync(cancellationToken);
            };
        // Why: UseSeeding/UseAsyncSeeding (EF 9+) run after Migrate and EnsureCreated, with the
        // migration lock held, so concurrent app instances don't seed twice at once. Unlike HasData, the
        // code can query first and decide, which is what "insert if missing" needs. Being called on every
        // start is the point, so the check-then-insert must be idempotent. (Provide UseSeeding as well if
        // anything calls the synchronous Migrate.)
    }
}

namespace EfCoreLessons.E12.Solution
{
    // The solution's model after all five exercises.
    public class Customer
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string ContactEmail { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class CustomerStatus
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class E12SolutionContext(DbContextOptions<E12SolutionContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<CustomerStatus>(status =>
            {
                status.Property(s => s.Name).HasMaxLength(20);
                status.HasData(new CustomerStatus { Id = 1, Name = "Active" }, new CustomerStatus { Id = 2, Name = "Suspended" });
            });
            model.Entity<Customer>(customer =>
            {
                customer.Property(c => c.FirstName).HasMaxLength(50);
                customer.Property(c => c.LastName).HasMaxLength(50);
                customer.Property(c => c.ContactEmail).HasMaxLength(320);
            });
        }
    }

    // Used by `dotnet ef` at design time. migrations add never connects; database update would, to EfLab_E12.
    public class E12SolutionContextFactory : IDesignTimeDbContextFactory<E12SolutionContext>
    {
        public E12SolutionContext CreateDbContext(string[] args)
            => new(new DbContextOptionsBuilder<E12SolutionContext>()
                .UseSqlServer(LabConnection.For(LabDatabase.For("E12"))).Options);
    }
}
```

- [ ] **Step 4: Write the solution's migrations, `SolutionMigrations/20261012123421_Initial.cs` and `.Designer.cs`**

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
```

```csharp
// <auto-generated />
using EfCoreLessons.E12.Solution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    [DbContext(typeof(E12SolutionContext))]
    [Migration("20261012123421_Initial")]
    partial class Initial
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("EfCoreLessons.E12.Solution.Customer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.HasKey("Id");

                    b.ToTable("Customers");
                });
#pragma warning restore 612, 618
        }
    }
}
```

- [ ] **Step 5: `SolutionMigrations/20261012123440_RenameEmail.cs` and `.Designer.cs`** (hand-edited: scaffolded as drop + add)

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    /// <inheritdoc />
    public partial class RenameEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Scaffolded as DropColumn + AddColumn, which would delete every email. Rename, then widen.
            migrationBuilder.RenameColumn(
                name: "Email",
                table: "Customers",
                newName: "ContactEmail");

            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Customers",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(320)",
                oldMaxLength: 320);

            migrationBuilder.RenameColumn(
                name: "ContactEmail",
                table: "Customers",
                newName: "Email");
        }
    }
}
```

```csharp
// <auto-generated />
using EfCoreLessons.E12.Solution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    [DbContext(typeof(E12SolutionContext))]
    [Migration("20261012123440_RenameEmail")]
    partial class RenameEmail
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("EfCoreLessons.E12.Solution.Customer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ContactEmail")
                        .IsRequired()
                        .HasMaxLength(320)
                        .HasColumnType("nvarchar(320)");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.HasKey("Id");

                    b.ToTable("Customers");
                });
#pragma warning restore 612, 618
        }
    }
}
```

- [ ] **Step 6: `SolutionMigrations/20261012123502_AddIsActive.cs` and `.Designer.cs`** (hand-edited: backfill)

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // The scaffolded default would make every existing customer inactive. They were all active.
            // EXEC: in an idempotent script this compiles only when it runs, after IsActive exists.
            migrationBuilder.Sql("EXEC(N'UPDATE Customers SET IsActive = 1')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Customers");
        }
    }
}
```

```csharp
// <auto-generated />
using EfCoreLessons.E12.Solution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    [DbContext(typeof(E12SolutionContext))]
    [Migration("20261012123502_AddIsActive")]
    partial class AddIsActive
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("EfCoreLessons.E12.Solution.Customer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ContactEmail")
                        .IsRequired()
                        .HasMaxLength(320)
                        .HasColumnType("nvarchar(320)");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.HasKey("Id");

                    b.ToTable("Customers");
                });
#pragma warning restore 612, 618
        }
    }
}
```

- [ ] **Step 7: `SolutionMigrations/20261012123522_SplitFullName.cs` and `.Designer.cs`** (hand-edited: add, copy, drop; and the reverse)

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    /// <inheritdoc />
    public partial class SplitFullName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Scaffolded as DropColumn first, which would lose every name. Add, copy, then drop.
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // EXEC: in an idempotent script this compiles only when it runs (the columns it names
            // don't exist yet, or no longer exist, when the script is compiled).
            migrationBuilder.Sql("""
                EXEC(N'UPDATE Customers
                SET FirstName = LEFT(FullName, CHARINDEX('' '', FullName + '' '') - 1),
                    LastName = LTRIM(SUBSTRING(FullName, CHARINDEX('' '', FullName + '' '') + 1, 100))')
                """);

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Customers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("EXEC(N'UPDATE Customers SET FullName = TRIM(FirstName + '' '' + LastName)')");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Customers");
        }
    }
}
```

```csharp
// <auto-generated />
using EfCoreLessons.E12.Solution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    [DbContext(typeof(E12SolutionContext))]
    [Migration("20261012123522_SplitFullName")]
    partial class SplitFullName
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("EfCoreLessons.E12.Solution.Customer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ContactEmail")
                        .IsRequired()
                        .HasMaxLength(320)
                        .HasColumnType("nvarchar(320)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("Id");

                    b.ToTable("Customers");
                });
#pragma warning restore 612, 618
        }
    }
}
```

- [ ] **Step 8: `SolutionMigrations/20261012123603_SeedStatuses.cs`, `.Designer.cs`, and the model snapshot**

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EfCoreLessons.E12.Solution.Migrations
{
    /// <inheritdoc />
    public partial class SeedStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerStatus", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CustomerStatus",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Active" },
                    { 2, "Suspended" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerStatus");
        }
    }
}
```

```csharp
// <auto-generated />
using EfCoreLessons.E12.Solution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    [DbContext(typeof(E12SolutionContext))]
    [Migration("20261012123603_SeedStatuses")]
    partial class SeedStatuses
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("EfCoreLessons.E12.Solution.Customer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ContactEmail")
                        .IsRequired()
                        .HasMaxLength(320)
                        .HasColumnType("nvarchar(320)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("Id");

                    b.ToTable("Customers");
                });

            modelBuilder.Entity("EfCoreLessons.E12.Solution.CustomerStatus", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.HasKey("Id");

                    b.ToTable("CustomerStatus");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            Name = "Active"
                        },
                        new
                        {
                            Id = 2,
                            Name = "Suspended"
                        });
                });
#pragma warning restore 612, 618
        }
    }
}
```

```csharp
// <auto-generated />
using EfCoreLessons.E12.Solution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    [DbContext(typeof(E12SolutionContext))]
    partial class E12SolutionContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("EfCoreLessons.E12.Solution.Customer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ContactEmail")
                        .IsRequired()
                        .HasMaxLength(320)
                        .HasColumnType("nvarchar(320)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("Id");

                    b.ToTable("Customers");
                });

            modelBuilder.Entity("EfCoreLessons.E12.Solution.CustomerStatus", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.HasKey("Id");

                    b.ToTable("CustomerStatus");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            Name = "Active"
                        },
                        new
                        {
                            Id = 2,
                            Name = "Suspended"
                        });
                });
#pragma warning restore 612, 618
        }
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E12Solutions && dotnet ef migrations list --context E12SolutionContext`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8` (a few seconds: each test recreates `EfLab_E12`), then the five migrations listed in order, ending `20261012123603_SeedStatuses`. (`migrations list` may add "(Pending)" or connect to `EfLab_E12`; either is fine.)

- [ ] **Step 9: Write `Examples.cs`, then `README.md`**

```csharp
using EfCoreLessons.E12.Solution;
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EfCoreLessons.E12;

// Worked examples for the README's concepts, using the solution's migrations (SolutionMigrations/).
//   dotnet test --filter E12Examples --logger "console;verbosity=detailed"
// Each example starts from an empty EfLab_E12 database.
public class E12Examples : IAsyncLifetime
{
    static readonly string Database = LabDatabase.For("E12");

    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    public async ValueTask InitializeAsync() => await LabDatabase.RecreateAsync(Database);

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    static E12SolutionContext NewContext() => new(new DbContextOptionsBuilder<E12SolutionContext>()
        .UseSqlServer(LabConnection.For(Database)).Options);

    // Concept 2: __EFMigrationsHistory records which migrations a database has.
    [Fact]
    public async Task The_history_table_records_applied_migrations()
    {
        await using var db = NewContext();
        Assert.Equal(5, (await db.Database.GetPendingMigrationsAsync()).Count());

        await db.Database.MigrateAsync();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Print(string.Join("\n", applied));
        Assert.Equal(db.Database.GetMigrations(), applied);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    // Concept 2: migrating to an earlier target runs the Down methods in reverse order.
    [Fact]
    public async Task Migrating_down_runs_Down()
    {
        await using var db = NewContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync();

        await migrator.MigrateAsync(Migration.InitialDatabase); // "0": before the first migration

        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        var tables = await new Reference(db).TablesAsync();
        Assert.Equal(["__EFMigrationsHistory"], tables); // every table a migration created is gone
    }

    // Concept 3: each migration operation becomes provider-specific SQL.
    [Fact]
    public void Operations_become_SQL()
    {
        using var db = NewContext();
        var generator = db.GetService<IMigrationsSqlGenerator>();

        var rename = new RenameColumnOperation { Table = "Customers", Name = "Email", NewName = "ContactEmail" };
        var add = new AddColumnOperation
        {
            Table = "Customers", Name = "IsActive", ClrType = typeof(bool), ColumnType = "bit", IsNullable = false, DefaultValue = false,
        };
        var sql = string.Join("\n", generator.Generate([rename, add]).Select(c => c.CommandText));
        Print(sql);

        Assert.Contains("EXEC sp_rename N'[Customers].[Email]', N'ContactEmail', 'COLUMN';", sql);
        Assert.Contains("ALTER TABLE [Customers] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);", sql);
    }

    // Concept 4: an idempotent script checks the history table before every migration.
    [Fact]
    public void Idempotent_script_guards_every_migration()
    {
        using var db = NewContext();
        var migrator = db.GetService<IMigrator>();

        var plain = migrator.GenerateScript();
        var idempotent = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.DoesNotContain("IF NOT EXISTS", plain);
        foreach (var id in db.Database.GetMigrations())
            Assert.Contains($"WHERE [MigrationId] = N'{id}'", idempotent);
    }

    // Concept 4: a script can cover a range of migrations, e.g. what production still needs.
    [Fact]
    public void Script_for_a_range_of_migrations()
    {
        using var db = NewContext();
        var ids = db.Database.GetMigrations().ToList();

        var script = db.GetService<IMigrator>().GenerateScript(fromMigration: ids[2], toMigration: ids[4]);

        Assert.Contains("FirstName", script);       // SplitFullName (ids[3])
        Assert.Contains("CustomerStatus", script);  // SeedStatuses (ids[4])
        Assert.DoesNotContain("sp_rename", script); // RenameEmail (ids[1]) is before the range
    }

    // Concept 5: EnsureCreated builds today's schema with no history, so migrations can't follow it.
    [Fact]
    public async Task EnsureCreated_and_migrations_dont_mix()
    {
        await LabDatabase.DropAsync(Database); // EnsureCreated creates the database itself
        await using var db = NewContext();
        await db.Database.EnsureCreatedAsync();

        Assert.Equal(5, (await db.Database.GetPendingMigrationsAsync()).Count()); // no history table at all
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
        Print(failure.Message);
        Assert.Contains("There is already an object named 'Customers'", failure.Message);
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E12Examples`
Expected: `Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6`.

````markdown
# E12 — Migrations Workflow

## What you'll learn

- The migrations workflow: change the model, `migrations add`, **read what was scaffolded**, apply
- What a migration consists of (`Up`, `Down`, the Designer file, the model snapshot) and how `__EFMigrationsHistory` tracks a database
- The data-loss traps: renames scaffolded as drop + add, defaults for new required columns, splitting a column, and how to edit a migration to keep the data
- Custom SQL in migrations, and why it needs `EXEC` to survive an idempotent script
- `HasData` vs `UseSeeding` for data
- Applying migrations: idempotent scripts and bundles at deploy time vs `Migrate()` at run time

## Builds on

- T-SQL [lesson 09](../../../lessons/09-ddl-and-constraints/lesson.ipynb) — `ALTER TABLE`, constraints, and what schema changes do to existing rows
- EF lesson [E10](../E10-fluent-api-modeling/README.md) — the model that migrations turn into schema

## Setup

```
dotnet tool restore                       # once: the pinned dotnet-ef 10.0.12
dotnet test --filter E12Examples --logger "console;verbosity=detailed"
```

**E12 works differently from the other lessons.** Your model lives in `Exercises.cs`: `Customer` and `E12ExerciseContext`, in namespace `EfCoreLessons.E12.Exercise`. Each exercise asks you to:
1. change that model
2. create a migration from `efcore/`:

   ```
   dotnet ef migrations add <Name> --context E12ExerciseContext --output-dir lessons/E12-migrations-workflow/ExerciseMigrations
   ```

3. read and, where needed, edit the generated migration in `ExerciseMigrations/`
4. return the migration's name from the exercise method

The checks apply your migrations to a fresh **`EfLab_E12`** database, one step at a time, inserting rows in between to see whether the data survives. Migrations commit for real, so E12 uses that disposable database (dropped and recreated per test) instead of a rolled-back transaction. AdventureWorks is never involved.

Made a mistake? `dotnet ef migrations remove --context E12ExerciseContext` deletes the last migration and restores the snapshot, as long as no database you care about has applied it. `dotnet ef migrations list --context E12ExerciseContext` shows the chain.

The solution's migrations are in `SolutionMigrations/`. They were generated exactly like this, then edited where the Why notes say.

## Concepts

### 1. The workflow and the files

`migrations add` compares your current model with the **model snapshot** (the model as of the last migration) and scaffolds the operations in between:

| File | Contents |
|---|---|
| `<timestamp>_<Name>.cs` | `Up` (apply) and `Down` (revert): `CreateTable`, `AddColumn`, `RenameColumn`, `Sql(...)` and so on |
| `<timestamp>_<Name>.Designer.cs` | the full model at that migration |
| `<Context>ModelSnapshot.cs` | the model now, which the next `migrations add` diffs against |

Commit all three. The timestamp orders migrations, and the class name must be unique per context.

### 2. Applying, tracking, reverting

`__EFMigrationsHistory` lists the ids of the migrations a database has applied. `Migrate()` (or `dotnet ef database update`) applies the pending ones in order. `MigrateAsync("<earlier id>")` runs `Down` methods back to that point, and `"0"` reverts them all. `GetAppliedMigrations()` and `GetPendingMigrations()` compare the assembly with the history table.

Examples: `The_history_table_records_applied_migrations`, `Migrating_down_runs_Down`.

### 3. Scaffolding is a draft: the data-loss traps

EF scaffolds a migration from the *schema* difference. It can't know what you meant to do with the data:

| Model change | Scaffolded | What happens to the data | Fix in the migration |
|---|---|---|---|
| rename a property, nothing else | `RenameColumn` | kept | none |
| rename **and** change it (here: widen) | `DropColumn` + `AddColumn`, "may result in the loss of data" | **deleted** | `RenameColumn` + `AlterColumn` |
| add a required (`NOT NULL`) column | `AddColumn(..., defaultValue: <CLR default>)` | existing rows get `false` / `0` / `""` | choose the default, or backfill with `Sql(...)` |
| replace one column with two | `DropColumn` first | **deleted** before anything can copy it | add, copy with `Sql(...)`, then drop |

Write `Down` with the same care: it runs when someone rolls back. Each operation also maps to provider SQL; a rename is `EXEC sp_rename`.

Example: `Operations_become_SQL`.

### 4. Scripts, custom SQL and deploying

Applying migrations in production is usually a deploy step, not app code:

- **`dotnet ef migrations script --idempotent`** (`GenerateScript(options: Idempotent)`) wraps each migration in `IF NOT EXISTS (... __EFMigrationsHistory ...)`, so it runs safely against a database at any state, and a DBA can review it. `--from`/`--to` (`GenerateScript(from, to)`) covers a range.
- **`dotnet ef migrations bundle`** builds a self-contained executable that applies the pending migrations, so the deploy server needs no SDK.
- **`Migrate()` at startup** is simplest. EF 9+ takes a database-wide lock while migrating, but running schema changes from every app instance, with app permissions, is still risky. Prefer checking `GetPendingMigrations()` at startup and failing fast.

The idempotent script has a catch. SQL Server **compiles a whole batch before running any of it**, including statements inside an `IF` that will be skipped. Custom SQL that names a column created (or dropped) by the same migration fails to compile in the script, even though `Migrate()`, which runs statement by statement, works fine. Wrap such SQL in `EXEC(N'...')` so it compiles only when it runs.

Examples: `Idempotent_script_guards_every_migration`, `Script_for_a_range_of_migrations`.

### 5. Data: `HasData` vs `UseSeeding`

| | `HasData` | `UseSeeding` / `UseAsyncSeeding` (EF 9+) |
|---|---|---|
| Where | model configuration | context options |
| Becomes | `InsertData`/`UpdateData` in migrations | code that runs after every `Migrate`/`EnsureCreated` |
| Good for | fixed reference rows with known keys | "insert if missing", computed or environment-specific data |
| Must be | stable (keys given explicitly) | idempotent (it runs on every start) |

### 6. `EnsureCreated` is not a migration

`EnsureCreated()` creates the current schema directly, with no history table. That's fine for tests and prototypes (E10–E11 use the same machinery), but a database created that way can't be migrated later: `Migrate()` tries to create tables that already exist. Pick one per database.

Example: `EnsureCreated_and_migrations_dont_mix`.

## Pitfalls

- **Applying a scaffolded migration without reading it.** The "may result in the loss of data" warning is easy to scroll past.
- **Editing the model snapshot or Designer files by hand.** Change the model and re-scaffold (or `migrations remove`) instead.
- **Removing or editing a migration a shared database already applied.** Add a new migration instead.
- **Custom SQL without `EXEC` in an idempotent script.** It works locally and fails on deploy.
- **Calling `Migrate()` from every web server at startup.** Use a deploy step (script or bundle).
- **Mixing `EnsureCreated` and migrations on one database.**

## Exercises

```
dotnet test --filter E12Exercises
```

Do them in order: each builds on the model and migrations of the one before. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs` and `SolutionMigrations/`.
````

- [ ] **Step 10: Prompts are identical, commit, then play the learner (Review Focus 4)**

```bash
cd efcore && python - lessons/E12-migrations-workflow <<'EOF'
import pathlib, sys
def prompts(path):
    blocks, current = [], []
    for line in pathlib.Path(path).read_text(encoding="utf-8").splitlines():
        text = line.strip()
        if text.startswith(("// Exercise", "// Challenge")):
            current = [text]
        elif current and text.startswith("//"):
            current.append(text)
        elif current and text.startswith("protected override"):
            blocks.append(current)
            current = []
    return blocks
lesson = sys.argv[1]
exercises, solutions = prompts(f"{lesson}/Exercises.cs"), prompts(f"{lesson}/Solutions.cs")
assert exercises == solutions, "prompts differ between Exercises.cs and Solutions.cs"
print(f"{len(exercises)} prompts identical")
EOF
```

Expected: `8 prompts identical`. Commit:

```bash
git add efcore/lessons/E12-migrations-workflow
git commit -m "feat: add EF Core lesson E12 - migrations workflow"
```

Now follow exercises 1 and 2 as a learner who doesn't read the scaffolded migration. From `efcore/`:

```bash
dotnet ef migrations add Initial --context E12ExerciseContext --output-dir lessons/E12-migrations-workflow/ExerciseMigrations
```

In `lessons/E12-migrations-workflow/Exercises.cs`: in class `Customer` (namespace `EfCoreLessons.E12.Exercise`) rename `Email` to `ContactEmail`; in `E12ExerciseContext.OnModelCreating` change `customer.Property(c => c.Email).HasMaxLength(200);` to `customer.Property(c => c.ContactEmail).HasMaxLength(320);`; change `Ex1`'s and `Ex2`'s bodies to `=> "Initial";` and `=> "RenameEmail";`. Then:

```bash
dotnet ef migrations add RenameEmail --context E12ExerciseContext --output-dir lessons/E12-migrations-workflow/ExerciseMigrations
dotnet build && dotnet test --no-build --filter "FullyQualifiedName~E12Exercises.Ex1|FullyQualifiedName~E12Exercises.Ex2"
```

Expected: the second `migrations add` prints `An operation was scaffolded that may result in the loss of data`; `ExerciseMigrations/` holds the two migrations **and** `E12ExerciseContextModelSnapshot.cs`; the test run shows Ex1 passed and Ex2 failed with `Assert.Equal() Failure` (the emails are gone). Then restore the learner's starting state and confirm a clean tree:

```bash
cd .. && git checkout -- efcore/lessons/E12-migrations-workflow/Exercises.cs
rm -rf efcore/lessons/E12-migrations-workflow/ExerciseMigrations
git status --short
```

Expected: `git status --short` prints nothing.

---

## Task 5: Docs and final verification

**Files:**
- Modify: `efcore/README.md` (database bullet, lesson table), `README.md` (EF Core Track paragraph), `CLAUDE.md` (EF Core track section), `docs/superpowers/specs/2026-10-10-efcore-track-design.md` (EfLab_ databases, lesson anatomy, Lab Harness, Risks)

- [ ] **Step 1: `efcore/README.md`.** Replace the bullet

```markdown
- **The code-first lessons (E10–E12, planned) will use their own `EfLab_<lesson>` databases.** The tests will create and drop them, and the harness will refuse to touch any database whose name doesn't start with `EfLab_`, so AdventureWorks can't be dropped by accident.
```

with

```markdown
- **The code-first lessons (E10–E12) use their own `EfLab_<lesson>` databases**, created on first use. E10 and E11 create each test's tables inside a rolled-back transaction, so their databases stay empty; E12 drops and recreates `EfLab_E12` for every test because migrations commit. The harness refuses to create, drop or migrate any database whose name doesn't start with `EfLab_`, so AdventureWorks can't be dropped by accident. The `EfLab_*` databases are disposable: delete them whenever you like (`reset-db` doesn't touch them).
```

Then in the lesson table change the three rows

```markdown
| E10 | Modeling with the Fluent API | planned |
| E11 | Inheritance & JSON columns | planned |
| E12 | Migrations workflow | planned |
```

to

```markdown
| E10 | Modeling with the Fluent API | available |
| E11 | Inheritance & JSON columns | available |
| E12 | Migrations workflow (uses `dotnet ef`) | available |
```

- [ ] **Step 2: Root `README.md`.** In the `## EF Core Track` paragraph, replace `Lessons E01–E09 are available;` with `Lessons E01–E12 are available;`.

- [ ] **Step 3: `CLAUDE.md`.** In the `## EF Core track` section, replace the sentence `Code-first lessons (E10–E12) will use `EfLab_<lesson>` databases instead.` (end of the bullet that starts ``- `efcore/Lab/` gives every test``) with `Code-first lessons (E10–E12) use `EfLab_<lesson>` databases instead (see below).`, and directly after the bullet that starts ``- `LabTest` also offers`` add:

```markdown
- Code-first lessons: `LabDatabase` creates/drops `EfLab_<name>` databases and refuses any other name. E10 and E11 derive from `CodeFirstTest("E<NN>")`; each exercise is a `ModelBuilder` configuration method, and `CreateModelAsync(configure)` builds that model (cached per configuration method, so exercise and solution models never mix) and creates its tables inside the test's rolled-back transaction. The classes they map live in `E<NN>Model.cs`. `Schema` reads the resulting tables/columns/keys through `Reference`. E12 (migrations) can't roll back: each test recreates `EfLab_E12`; the learner's migrations go in `ExerciseMigrations/` (created with `dotnet ef migrations add ... --context E12ExerciseContext`), the solution's are committed in `SolutionMigrations/`. Custom SQL in a migration is wrapped in `EXEC(N'...')` so idempotent scripts compile.
```

- [ ] **Step 4: Spec.** In `docs/superpowers/specs/2026-10-10-efcore-track-design.md`:
  - Under `### `EfLab_*` — code-first lessons (E10–E12)`, replace the first bullet (`- Databases named `EfLab_<lesson>` ... stale ones are harmless.`, three lines) with:

```markdown
- Databases named `EfLab_<lesson>` (e.g. `EfLab_E10`), created on first use by the tests
  themselves. E10–E11 create each test's tables inside the test's rolled-back transaction
  (`IRelationalDatabaseCreator.CreateTables`), so the database stays empty between tests; E12
  drops and recreates `EfLab_E12` per test, because migrations commit. They need no reset
  script; stale ones are harmless.
```

  - Under `## Lesson Anatomy`, directly after the bullet that starts `- Lessons that need model changes add a sixth file` (two lines), add:

```markdown
- Code-first lessons (Tier D) add `E<NN>Model.cs`: the plain classes the exercises map. E12
  also has `SolutionMigrations/` (committed) and the learner's `ExerciseMigrations/`.
```

  - Under `## Lab Harness`, replace the paragraph that starts `` `LabDatabase` (added with Tier D) `` (five lines, ending `the Tier D plan picks the mechanism.`) with:

```markdown
`LabDatabase` (added with Tier D) creates and drops `EfLab_<lesson>` databases and enforces the
`EfLab_` guard (letters, digits and `_` only, checked before any SQL). `CodeFirstTest` gives
code-first tests a rolled-back transaction in their lesson's database, and builds each model
from a configuration method with a model-cache key of (method, declaring type), so the
learner's model (Exercises) and the solution's never share EF's model cache.
```

  - Under `Still open:`, replace the `**Run time:**` bullet (three lines) with:

```markdown
- **Run time:** serial execution; after Tier D, 324 tests run in ~30 s (E09's deliberate lock
  timeouts and E12's database recreation account for most of it). Revisit if a full run gets
  slow.
```

- [ ] **Step 5: Build and run everything, unfiltered**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build`
Expected: `0 Warning(s)`, then `Passed!  - Failed: 0, Passed: 228, Skipped: 96, Total: 324`, with a duration of roughly 30 s (Review Focus 5).

- [ ] **Step 6: Run it again (Review Focus 3), and the curriculum self-check**

Run: `cd efcore && dotnet test --no-build && dotnet test --no-build --filter "FullyQualifiedName!~Exercises"`
Expected: again `Passed: 228, Skipped: 96, Total: 324` (the `EfLab_` databases from the first run don't get in the way), then `Passed!  - Failed: 0, Passed: 228, Skipped: 0, Total: 228`.

- [ ] **Step 7: No newline-dependent assertions**

Run: `cd efcore && grep -nF -e 'NewLine' -e '\r\n' lessons/*/Checks.cs lessons/*/Examples.cs`
Expected: exactly one hit, `lessons/E01-setup-and-generated-sql/Examples.cs:59` (output only, from phase 1).

- [ ] **Step 8: Check the EF Core 10-specific claims (spec DoD item 5).** Behavioral claims in the E10–E12 READMEs are asserted by examples or checks, so Steps 5–6 verified them against EF Core 10.0.12. These have no assertion; confirm each against Microsoft Learn ("Modeling: Keys, Relationships, Owned Entity Types, Complex types (What's new in EF 8/10)", "Value Conversions", "Shadow and Indexer Properties", "Backing Fields", "Global Query Filters", "Inheritance", "JSON columns", "Migrations Overview / Managing Migrations / Applying Migrations", "Data Seeding"):
  - E10: complex types can be shared between owners and are compared by value; owned types can't be shared
  - E10: backing-field conventions (`_<camel>`, `<camel>`, `m_<camel>`) and read-only properties without a backing field not being mapped
  - E10: named query filters are new in EF 10, and `IgnoreQueryFilters([...])` ignores them by name
  - E11: the EF docs' performance guidance (TPH usually fastest; TPC for querying leaf types; TPT slowest)
  - E11: SQL Server 2025 / Azure SQL can use the native `json` type
  - E12: EF 9+ takes a database-wide lock during `Migrate`; `UseSeeding`/`UseAsyncSeeding` run after `Migrate`/`EnsureCreated`; migration bundles (`dotnet ef migrations bundle`)
  - E12: idempotent scripts and custom SQL needing `EXEC` (the "Custom migration operations" / "Applying migrations: idempotent SQL scripts" pages)

Expected: no corrections needed. If one is, fix the README sentence and re-run Step 5.

- [ ] **Step 9: AdventureWorks is unchanged; only EfLab_ databases were added.** Re-run Task 0, Step 5's query, then:

```bash
MSYS_NO_PATHCONV=1 docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -W -h -1 -Q "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name"
```

Expected: the same four values recorded in Task 0; the databases listed are `AdventureWorks2022` plus only `EfLab_` names (`EfLab_E10`, `EfLab_E11`, `EfLab_E12`, `EfLab_LabSelfTest`).

- [ ] **Step 10: Commit**

```bash
git add efcore/README.md README.md CLAUDE.md docs/superpowers/specs/2026-10-10-efcore-track-design.md
git commit -m "docs: mark EF Core lessons E10-E12 available"
```

---

## Not in this phase

The capstone (E13) gets its own plan, per the spec's build-out phases.
