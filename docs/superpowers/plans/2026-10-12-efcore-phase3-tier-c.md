# EF Core Track — Phase 3 (Tier C: Change Tracking & Concurrency) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add lessons E07 (change tracking internals), E08 (SaveChanges & set-based writes) and E09 (concurrency & transactions) to `efcore/`, with the harness additions they need: contexts of other types or with extra interceptors, independent second sessions, a lock timeout on every lab connection, and a dispose that can't leave a transaction open.

**Architecture:** Same lesson anatomy as phases 1–2. `LabTest<TContext>` gains `NewContext<T>(params IInterceptor[])` and `NewSessionAsync(IsolationLevel)`: a second connection with its own transaction, rolled back like the main one. Every lab connection runs `SET LOCK_TIMEOUT 3000`, so a test blocked by its own second session fails instead of hanging. `DisposeAsync` attempts every step even when one fails. E08 adds `E08Context` (a model without one trigger, for one example). E09 adds `E09Context` (`ModifiedDate` as a concurrency token, a `rowversion` shadow property backed by a column added with `ALTER TABLE` inside the test's transaction) and `RetryingContext`. Every file below was validated against the live `mssql-learn` container on 2026-10-12:
- the build has 0 warnings
- the full suite gives 169 passed and 72 skipped (the 72 are the exercise stubs)
- every check was also run against a set of tempting-but-wrong answers and failed each one for the intended reason
- AdventureWorks checksums, row counts and the `ProductCategory` schema were identical afterwards

**Tech Stack:** .NET 10 SDK (10.0.x), EF Core 10.0.12 (SqlServer, Design, Proxies), xUnit v3 4.0.2 (`xunit.v3.mtp-off`) on VSTest, SQL Server 2022 (Docker container `mssql-learn`), AdventureWorks2022.

**Spec:** `docs/superpowers/specs/2026-10-10-efcore-track-design.md` (phase 3 of its "Build-out Phases").

## Global Constraints

- Target `net10.0`. EF Core packages exactly `10.0.12`; `xunit.v3.mtp-off` `4.0.2`, `xunit.runner.visualstudio` `4.0.1`, `Microsoft.NET.Test.Sdk` `18.10.1`. No new packages in this phase. No floating versions.
- AdventureWorks2022 is never modified: every test, and every second session, runs inside a transaction that is rolled back. Schema changes a lesson needs (E09's `rowversion` column) happen inside the test's transaction. No `setup.sql`, no `ALTER DATABASE`.
- No credentials committed anywhere.
- `efcore/Model/` is scaffolder output, never hand-edited; lesson-specific model changes go in a derived context in the lesson folder (`E08Context.cs`, `E09Context.cs`).
- Tests run serially (`parallelizeTestCollections: false`).
- Lesson folder `efcore/lessons/E<NN>-<slug>/` = `README.md`, `Examples.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs` (+ `E<NN>Context.cs` if the model changes). Namespace `EfCoreLessons.E<NN>`; classes `E<NN>Examples`, `E<NN>Checks`, `E<NN>Exercises`, `E<NN>Solutions`.
- Each lesson: 5 exercises (`Ex1`–`Ex5`) + 3 challenges (`Ch1`–`Ch3`); identical prompts in `Exercises.cs` and `Solutions.cs`; a `// Why:` note after each solution.
- `dotnet build` produces 0 warnings.
- Commits: conventional prefixes (`feat:`, `docs:`), no attribution lines (disabled in the user's settings).

## Review Focus

1. **A test blocked by its own second session.** A test thread that waits on a lock held by another connection it opened would hang forever (SQL Server sees no deadlock: the waiting happens in the test). Every lab connection sets `LOCK_TIMEOUT` (3 s), so it fails with error 1222 instead. Pinned by Task 1's `A_session_is_independent_and_cannot_block_forever`.
2. **A failing test must not leave a transaction open.** If one dispose step throws (for example, a session closed while one of its commands is still running), the remaining steps must still roll back and close everything, or the leftover locks block every later test. This happened during validation and is why `DisposeAsync` attempts every step and why E09's challenge 3 check always awaits its blocked command. Pinned by Task 1 (`Session_writes_are_rolled_back_when_the_test_ends`) and Task 4, Step 9 (the whole suite runs clean after a deliberately failing E09 exercise).
3. **Schema changes inside a test.** E09 adds a `rowversion` column with `ALTER TABLE` inside the test's transaction. It must be gone after the run. Pinned by Task 5, Step 9 (checks the column doesn't exist).
4. **Concurrency checks in one transaction.** Both "users" in a conflict share the test's connection and transaction, so the second save really sees the first. Checks that run two scenarios use a fresh context for the second, because a failed save leaves its changes pending in the context. Pinned by the E09 checks in Task 4.
5. **A learner's plain `dotnet test`** still gives zero failures: 169 passed, 72 skipped, in about 20 s (E09 waits out a few deliberate 3 s lock timeouts). Pinned by Task 5, Step 5.

## Conventions used by every task

- Shell: Git Bash on Windows, bash/zsh elsewhere. Paths are relative to the repository (or worktree) root unless a step says `cd efcore`.
- Commands that need the password export it first (`docker/.env` is gitignored; a fresh worktree outside the repo has no copy until Task 0 copies it):

  ```bash
  export SA_PASSWORD=$(grep '^SA_PASSWORD=' /d/Study/Database/mssql/docker/.env | cut -d= -f2- | tr -d '\r')
  ```

- On Git Bash, prefix `docker exec ... /opt/mssql-tools18/...` with `MSYS_NO_PATHCONV=1`.

## File Map

| Action | Path |
|---|---|
| Replace | `efcore/Lab/LabTest.cs` (NewContext<T>, NewSessionAsync, LockTimeoutMs, robust DisposeAsync) |
| Replace | `efcore/Lab/LabSelfTests.cs` (three new self-tests) |
| Create | `efcore/lessons/E07-change-tracking/` (5 files) |
| Create | `efcore/lessons/E08-savechanges-and-set-based-writes/` (6 files, incl. `E08Context.cs`) |
| Create | `efcore/lessons/E09-concurrency-and-transactions/` (6 files, incl. `E09Context.cs`) |
| Modify | `efcore/README.md`, `README.md`, `CLAUDE.md`, `docs/superpowers/specs/2026-10-10-efcore-track-design.md` |

---

## Task 0: Isolate the work

- [ ] **Step 1: Create an isolated workspace** using the `superpowers:using-git-worktrees` skill, on a new branch `efcore-phase3` based on `master`. Without a worktree: `git switch -c efcore-phase3`. In a worktree outside the repo, copy the password file: `cp /d/Study/Database/mssql/docker/.env docker/.env`.

- [ ] **Step 2: Verify you are not on master**

Run: `git branch --show-current`
Expected: `efcore-phase3`

- [ ] **Step 3: Confirm the container is up**

Run: `docker ps --filter name=mssql-learn --format '{{.Status}}'`
Expected: starts with `Up`. If not, run `docker compose -f docker/docker-compose.yml up -d` from the main checkout and wait ~20 s.

- [ ] **Step 4: Confirm the phase 2 baseline passes**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build`
Expected: `0 Warning(s)`, then `Passed!  - Failed: 0, Passed: 117, Skipped: 48, Total: 165`.

- [ ] **Step 5: Record the AdventureWorks baseline** (compared again in Task 5)

```bash
export SA_PASSWORD=$(grep '^SA_PASSWORD=' /d/Study/Database/mssql/docker/.env | cut -d= -f2- | tr -d '\r')
MSYS_NO_PATHCONV=1 docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -W -s'|' -d AdventureWorks2022 -Q "SET NOCOUNT ON; SELECT (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Production.Product) AS product, (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM HumanResources.Employee) AS employee, (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Sales.SalesOrderHeader) AS soh, (SELECT COUNT(*) FROM Sales.SalesOrderDetail) AS lines, (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('Production.ProductCategory') AND name = 'Version') AS version_column;"
```

Expected: five values. Note them down. (On 2026-10-12 they were `-1058563079|259050056|-1058661271|121317|0`.)

---

## Task 1: Harness additions (other contexts, interceptors, second sessions)

**Files:**
- Replace: `efcore/Lab/LabSelfTests.cs` (first, as the failing tests)
- Replace: `efcore/Lab/LabTest.cs`

**Interfaces:**
- Consumes: `LabConnection.OpenAsync(string)`, `SqlCapture`, `TransactionGuard`, `Reference` (unchanged files in `efcore/Lab/`).
- Produces, in `LabTest<TContext>` (namespace `EfCoreLessons.Lab`), in addition to the existing `Db`, `NewContext()`, `Sql`, `Reference`:
  - `public const int LockTimeoutMs = 3000;` — every lab connection runs `SET LOCK_TIMEOUT 3000`.
  - `protected T NewContext<T>(params IInterceptor[] interceptors) where T : AdventureWorksContext` — a context of type `T` on the test's connection and transaction, captured in `Sql`, with extra interceptors.
  - `protected Task<TContext> NewSessionAsync(IsolationLevel isolation = IsolationLevel.ReadCommitted)` — a context on a second connection with its own transaction (rolled back at the end of the test); not counted in `Sql`.

- [ ] **Step 1: Write the failing self-tests.** Replace `efcore/Lab/LabSelfTests.cs` with:

```csharp
using EfCoreLessons.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.Lab;

// Pins the harness guarantees every lesson relies on.
public class LabSelfTests : LabTest
{
    [Fact]
    public async Task Sql_counts_Db_and_NewContext_commands_but_not_Reference()
    {
        await Db.Products.CountAsync();
        await NewContext().Products.CountAsync();
        await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Production.Product");

        Sql.AssertRoundTrips(2);
    }

    [Fact]
    public async Task AssertRoundTrips_failure_lists_the_captured_sql()
    {
        await Db.Products.CountAsync();

        var failure = Assert.ThrowsAny<Exception>(() => Sql.AssertRoundTrips(0));

        Assert.Contains("Expected 0 round trip(s), got 1.", failure.Message);
        Assert.Contains("FROM [Production].[Product]", failure.Message);
    }

    [Fact]
    public async Task Contexts_share_the_test_transaction()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Changed inside the test";
        await Db.SaveChangesAsync();

        var seenByOther = await NewContext().Products
            .Where(p => p.ProductId == 1).Select(p => p.Name).SingleAsync();

        Assert.Equal("Changed inside the test", seenByOther);
    }

    [Fact]
    public async Task Writes_are_rolled_back_when_the_test_ends()
    {
        var lab = new ScratchLab();
        await lab.InitializeAsync();
        await lab.RenameProduct1("Never committed");
        await lab.DisposeAsync();

        await using var connection = await LabConnection.OpenAsync(LabConnection.AdventureWorks);
        await using var command = new SqlCommand("SELECT Name FROM Production.Product WHERE ProductID = 1", connection);

        Assert.Equal("Adjustable Race", await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Commands_throw_after_the_test_transaction_was_rolled_back_on_the_server()
    {
        await Db.Database.ExecuteSqlRawAsync("ROLLBACK");

        // Without the guard this would run in autocommit and could write for real.
        var failure = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Db.Products.CountAsync());

        Assert.Contains("transaction", failure.Message);
    }

    [Fact]
    public async Task QueryPlan_compiles_without_running_or_counting()
    {
        var operators = await QueryPlan.OperatorsAsync(Db, Db.Products.Where(p => p.ProductId == 1));

        Assert.Contains("Clustered Index Seek", operators);
        Assert.Equal(0, Sql.Count);
        Assert.Equal(504, await Db.Products.CountAsync()); // SHOWPLAN is off again: queries run normally
    }

    [Fact]
    public async Task NewContext_adds_interceptors()
    {
        var saves = new SaveCounter();
        var db = NewContext<AdventureWorksContext>(saves);

        var product = await db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Intercepted";
        await db.SaveChangesAsync();

        Assert.Equal(1, saves.Count);
        Sql.AssertRoundTrips(2); // still captured, still on the test transaction
    }

    [Fact]
    public async Task A_session_is_independent_and_cannot_block_forever()
    {
        await Db.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(u => u.SetProperty(p => p.Name, "Locked by the test"));
        var session = await NewSessionAsync();

        // Its own transaction: it sees the last committed name, not the test's uncommitted one.
        Assert.Equal("Adjustable Race", await session.Products.Where(p => p.ProductId == 1).Select(p => p.Name).SingleAsync());

        // Writing the row the test has locked waits LockTimeoutMs, then fails with error 1222.
        var started = DateTime.UtcNow;
        var failure = await Assert.ThrowsAnyAsync<Exception>(() =>
            session.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(u => u.SetProperty(p => p.Name, "Blocked")));
        Assert.Equal(1222, Assert.IsType<SqlException>(failure.InnerException ?? failure).Number);
        Assert.InRange(DateTime.UtcNow - started, TimeSpan.FromMilliseconds(LockTimeoutMs - 500), TimeSpan.FromMilliseconds(LockTimeoutMs * 3));
        Sql.AssertRoundTrips(1); // the session's commands are not counted
    }

    [Fact]
    public async Task Session_writes_are_rolled_back_when_the_test_ends()
    {
        var lab = new ScratchLab();
        await lab.InitializeAsync();
        await lab.RenameProduct1InASession("Never committed either");
        await lab.DisposeAsync();

        await using var connection = await LabConnection.OpenAsync(LabConnection.AdventureWorks);
        await using var command = new SqlCommand("SELECT Name FROM Production.Product WHERE ProductID = 1", connection);

        Assert.Equal("Adjustable Race", await command.ExecuteScalarAsync());
    }

    sealed class ScratchLab : LabTest
    {
        public async Task RenameProduct1(string name)
        {
            var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
            product.Name = name;
            await Db.SaveChangesAsync();
        }

        public async Task RenameProduct1InASession(string name)
        {
            var session = await NewSessionAsync();
            await session.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(u => u.SetProperty(p => p.Name, name));
        }
    }

    sealed class SaveCounter : SaveChangesInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}

// LabConnection's password lookup; needs no database.
public class LabConnectionTests
{
    [Fact]
    public void Password_comes_from_the_nearest_docker_env()
    {
        var root = Directory.CreateTempSubdirectory("lab-env-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "docker"));
            File.WriteAllText(Path.Combine(root.FullName, "docker", ".env"), "OTHER=1\r\nSA_PASSWORD=p@ss=word!\r\n");
            var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "efcore", "bin", "Debug"));

            Assert.Equal("p@ss=word!", LabConnection.FindSaPassword(nested.FullName)); // CRLF trimmed, '=' kept
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Password_is_null_when_no_docker_env_exists()
    {
        var root = Directory.CreateTempSubdirectory("lab-env-");
        try
        {
            Assert.Null(LabConnection.FindSaPassword(root.FullName));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `cd efcore && dotnet build 2>&1 | grep -E ' error ' | sort -u | head -5`
Expected: the build fails, with `error CS...` lines naming `NewContext` (which isn't generic yet), `NewSessionAsync` and `LockTimeoutMs`.

- [ ] **Step 3: Replace `efcore/Lab/LabTest.cs`**

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
            .AddInterceptors(Guard);
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
        if (failures.Count > 0) throw new AggregateException("Disposing the lab test failed.", failures);
    }
}

public abstract class LabTest : LabTest<AdventureWorksContext>;

public sealed class Reference(AdventureWorksContext db)
{
    public Task<List<T>> Rows<T>(FormattableString sql) => db.Database.SqlQuery<T>(sql).ToListAsync();
}
```

- [ ] **Step 4: Run the self-tests**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter "LabSelfTests|LabConnectionTests"`
Expected: `Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11`; the run takes about 4 s (one deliberate lock timeout).

- [ ] **Step 5: Everything else still passes**

Run: `cd efcore && dotnet test --no-build`
Expected: `Passed!  - Failed: 0, Passed: 120, Skipped: 48, Total: 168`.

- [ ] **Step 6: Commit**

```bash
git add efcore/Lab
git commit -m "feat: add second sessions, lock timeouts and typed contexts to the EF lab"
```

---

## Task 2: Lesson E07 — Change tracking internals

**Files:**
- Create: `efcore/lessons/E07-change-tracking/Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `LabTest` (`Db`, `NewContext()`, `Sql`, `Reference`), `[Exercise]`, the scaffolded model.
- Produces: namespace `EfCoreLessons.E07`: records `PropertyChange`, `ProductEdit`, classes `E07Checks`, `E07Exercises`, `E07Solutions`, `E07Examples`. Nothing later depends on them.

- [ ] **Step 1: Write `Checks.cs`**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E07;

public record PropertyChange(string Property, object? Original, object? Current);
public record ProductEdit(int ProductId, string Name, decimal ListPrice, string? Color);

// The checks for E07. E07Exercises (yours) and E07Solutions both inherit them.
public abstract class E07Checks : LabTest
{
    protected abstract List<PropertyChange> Ex1(AdventureWorksContext db, Product product);
    protected abstract Task Ex2(AdventureWorksContext db, int productId, decimal listPrice);
    protected abstract Task Ex3(AdventureWorksContext db, ProductEdit edit);
    protected abstract bool Ex4(AdventureWorksContext db);
    protected abstract Task Ex5(AdventureWorksContext db, Product product);
    protected abstract Task Ch1(AdventureWorksContext db, Product newProduct, ProductSubcategory detachedSubcategory);
    protected abstract Task Ch2(AdventureWorksContext db, SalesOrderHeader detachedOrder, int changedDetailId);
    protected abstract Task Ch3(AdventureWorksContext db, Product staleCopy);

    // A new, valid product that belongs to no subcategory yet.
    protected static Product NewProduct(string productNumber) => new()
    {
        Name = $"E07 test product {productNumber}",
        ProductNumber = productNumber,
        SafetyStockLevel = 1,
        ReorderPoint = 1,
        StandardCost = 10m,
        ListPrice = 20m,
        DaysToManufacture = 0,
        SellStartDate = new DateTime(2026, 1, 1),
    };

    async Task<int> SubcategoryCount() =>
        (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Production.ProductSubcategory")).Single();

    static int Occurrences(string text, string part) => text.Split(part).Length - 1;

    [Exercise]
    public async Task Ex1_WhatChanged()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Adjustable Race v2";
        product.ListPrice = 12.5m;
        product.Color = null; // assigned, but the same value: not a change
        Sql.Reset();

        var changes = Ex1(Db, product);

        Assert.Equal(
            [new("ListPrice", 0m, 12.5m), new("Name", "Adjustable Race", "Adjustable Race v2")],
            changes);
        Assert.Equal(0, Sql.Count); // the tracker already knows: no query
    }

    [Exercise]
    public async Task Ex2_UpdateWithoutLoading()
    {
        await Ex2(Db, 1, 12.5m);

        Sql.AssertRoundTrips(1);
        Assert.Contains("SET [ListPrice] = @", Sql.All);
        Assert.DoesNotContain("[Name] = @", Sql.All); // only the column you changed
        var row = (await Reference.Rows<ProductEdit>($"SELECT ProductID AS ProductId, Name, ListPrice, Color FROM Production.Product WHERE ProductID = 1")).Single();
        Assert.Equal(new ProductEdit(1, "Adjustable Race", 12.5m, null), row);
    }

    [Exercise]
    public async Task Ex3_ApplyAnEdit()
    {
        await Ex3(Db, new ProductEdit(1, "Adjustable Race v2", 12.5m, null));

        Sql.AssertRoundTrips(2); // load, then save
        var update = Sql.Commands[1];
        Assert.Contains("[Name] = @", update);
        Assert.Contains("[ListPrice] = @", update);
        Assert.DoesNotContain("[Color] = @", update); // unchanged values are not written
        var row = (await Reference.Rows<ProductEdit>($"SELECT ProductID AS ProductId, Name, ListPrice, Color FROM Production.Product WHERE ProductID = 1")).Single();
        Assert.Equal(new ProductEdit(1, "Adjustable Race v2", 12.5m, null), row);
    }

    [Exercise]
    public async Task Ex4_AnythingToSave()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);

        product.Name = "Adjustable Race"; // the value it already had
        Assert.False(Ex4(Db));

        product.ListPrice = 1m;
        Assert.True(Ex4(Db));

        Sql.AssertRoundTrips(1); // only the load
    }

    [Exercise]
    public async Task Ex5_DiscardLocalChanges()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Not saved";
        Sql.Reset();

        await Ex5(Db, product);

        Sql.AssertRoundTrips(1);
        Assert.Equal(EntityState.Unchanged, Db.Entry(product).State);
        Assert.Equal("Adjustable Race", product.Name);
    }

    [Exercise]
    public async Task Ch1_NewProductInAnExistingSubcategory()
    {
        var subcategory = await NewContext().ProductSubcategories.AsNoTracking().SingleAsync(s => s.ProductSubcategoryId == 1);
        var before = await SubcategoryCount();
        var product = NewProduct("E07-CH1");
        Sql.Reset();

        await Ch1(Db, product, subcategory);

        Assert.True(product.ProductId > 0, "The new product should have been saved (it gets its ProductId from the database).");
        Assert.DoesNotContain("INSERT INTO [Production].[ProductSubcategory]", Sql.All);
        Assert.Equal(before, await SubcategoryCount());
        Assert.Equal(1, (await Reference.Rows<int>($"SELECT ProductSubcategoryID AS Value FROM Production.Product WHERE ProductID = {product.ProductId}")).Single());
    }

    [Exercise]
    public async Task Ch2_SaveADisconnectedGraph()
    {
        var order = await NewContext().SalesOrderHeaders.AsNoTracking()
            .Include(h => h.SalesOrderDetails)
            .SingleAsync(h => h.SalesOrderId == 43659);
        var lines = order.SalesOrderDetails.Count;
        var changed = order.SalesOrderDetails.OrderBy(d => d.SalesOrderDetailId).First();
        changed.OrderQty += 5;
        order.SalesOrderDetails.Add(new SalesOrderDetail
        {
            SalesOrderId = order.SalesOrderId,
            ProductId = changed.ProductId,
            SpecialOfferId = changed.SpecialOfferId,
            OrderQty = 1,
            UnitPrice = changed.UnitPrice,
        });
        Sql.Reset();

        await Ch2(Db, order, changed.SalesOrderDetailId);

        var sql = Sql.All;
        Assert.Equal(1, Occurrences(sql, "UPDATE [Sales].[SalesOrderDetail]"));    // only the changed line
        Assert.Equal(1, Occurrences(sql, "INSERT INTO [Sales].[SalesOrderDetail]")); // the new line
        Assert.DoesNotContain("UPDATE [Sales].[SalesOrderHeader]", sql);             // the header didn't change
        Assert.Equal(lines + 1, (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43659")).Single());
        Assert.Equal(changed.OrderQty, (await Reference.Rows<short>($"SELECT OrderQty AS Value FROM Sales.SalesOrderDetail WHERE SalesOrderDetailID = {changed.SalesOrderDetailId}")).Single());
    }

    [Exercise]
    public async Task Ch3_MergeIntoTheTrackedInstance()
    {
        var tracked = await Db.Products.SingleAsync(p => p.ProductId == 1);
        tracked.Name = "Renamed locally";
        var staleCopy = await NewContext().Products.AsNoTracking().SingleAsync(p => p.ProductId == 1);
        staleCopy.ListPrice = 99m;
        Sql.Reset();

        await Ch3(Db, staleCopy);

        Assert.Equal(0, Sql.Count); // no query, no save
        await Db.SaveChangesAsync();
        var row = (await Reference.Rows<ProductEdit>($"SELECT ProductID AS ProductId, Name, ListPrice, Color FROM Production.Product WHERE ProductID = 1")).Single();
        Assert.Equal(new ProductEdit(1, "Renamed locally", 99m, null), row); // both changes kept
    }
}
```

- [ ] **Step 2: Write `Exercises.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E07;

// E07 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E07Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E07Exercises : E07Checks
{
    // Exercise 1 — What changed?
    // `product` is tracked by `db` and has been edited in memory. Return its modified properties as
    // PropertyChange(property name, original value, current value), ordered by property name.
    // Rules: read it from the change tracker; no queries.
    protected override List<PropertyChange> Ex1(AdventureWorksContext db, Product product)
        => throw new NotImplementedException();

    // Exercise 2 — Update without loading
    // Set the ListPrice of product `productId` to `listPrice` without reading the product first.
    // Rules: one round trip; the UPDATE writes only ListPrice.
    protected override Task Ex2(AdventureWorksContext db, int productId, decimal listPrice)
        => throw new NotImplementedException();

    // Exercise 3 — Apply an edit
    // Load product `edit.ProductId`, copy the edit's values onto it, and save.
    // Rules: two round trips (load, save); the UPDATE writes only the columns whose values changed.
    protected override Task Ex3(AdventureWorksContext db, ProductEdit edit)
        => throw new NotImplementedException();

    // Exercise 4 — Anything to save?
    // Return whether calling SaveChanges now would write anything.
    // Rules: no round trips; don't save.
    protected override bool Ex4(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 5 — Discard local changes
    // `product` is tracked by `db` and has unsaved edits. Throw them away: the entity must be
    // Unchanged again and hold the database's current values.
    // Rules: one round trip.
    protected override Task Ex5(AdventureWorksContext db, Product product)
        => throw new NotImplementedException();

    // Challenge 1 — A new product in an existing subcategory
    // Save `newProduct` as a product of `detachedSubcategory` (an existing row, loaded by another
    // context and not tracked by `db`).
    // Rules: the subcategory must not be inserted again.
    protected override Task Ch1(AdventureWorksContext db, Product newProduct, ProductSubcategory detachedSubcategory)
        => throw new NotImplementedException();

    // Challenge 2 — Save a disconnected graph
    // `detachedOrder` and its SalesOrderDetails were loaded by another context and edited offline:
    // the detail `changedDetailId` was modified, and one new detail (SalesOrderDetailId 0) was added.
    // Save exactly that: insert the new detail, update the changed one, write nothing else.
    protected override Task Ch2(AdventureWorksContext db, SalesOrderHeader detachedOrder, int changedDetailId)
        => throw new NotImplementedException();

    // Challenge 3 — Merge into the tracked instance
    // `db` already tracks product `staleCopy.ProductId` with unsaved edits. `staleCopy` is a different
    // instance of the same product, carrying a new ListPrice. Copy that ListPrice onto the product `db`
    // tracks, keeping its other edits.
    // Rules: no round trips; don't save.
    protected override Task Ch3(AdventureWorksContext db, Product staleCopy)
        => throw new NotImplementedException();
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E07Exercises`
Expected: `Failed: 0, Passed: 0, Skipped: 8, Total: 8` (dotnet prints the summary as `Skipped!` when every test skips).

- [ ] **Step 3: Write `Solutions.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E07;

// Worked solutions for E07, each followed by a Why note. Run: dotnet test --filter E07Solutions
public class E07Solutions : E07Checks
{
    // Exercise 1 — What changed?
    // `product` is tracked by `db` and has been edited in memory. Return its modified properties as
    // PropertyChange(property name, original value, current value), ordered by property name.
    // Rules: read it from the change tracker; no queries.
    protected override List<PropertyChange> Ex1(AdventureWorksContext db, Product product)
        => db.Entry(product).Properties
            .Where(p => p.IsModified)
            .OrderBy(p => p.Metadata.Name)
            .Select(p => new PropertyChange(p.Metadata.Name, p.OriginalValue, p.CurrentValue))
            .ToList();
    // Why: when EF materializes a tracked entity it keeps a snapshot of the values it read. Entry(...)
    // runs DetectChanges for that entity, which compares current values with the snapshot. A property
    // set to the value it already had is not modified, which is why Color doesn't appear.

    // Exercise 2 — Update without loading
    // Set the ListPrice of product `productId` to `listPrice` without reading the product first.
    // Rules: one round trip; the UPDATE writes only ListPrice.
    protected override Task Ex2(AdventureWorksContext db, int productId, decimal listPrice)
    {
        var product = new Product { ProductId = productId };
        db.Products.Attach(product);                     // tracked as Unchanged: EF assumes the row exists
        product.ListPrice = listPrice;                   // DetectChanges will see this one change
        db.Entry(product).Property(p => p.ListPrice).IsModified = true; // even if listPrice happens to be 0
        return db.SaveChangesAsync();
    }
    // Why: Attach starts tracking a stub that has only its key, as Unchanged. Changing one property
    // marks just that property modified, so the UPDATE sets one column. The explicit IsModified
    // covers the case where the new value equals the stub's default (0 here). Tempting-but-wrong:
    // db.Update(new Product { ProductId = ..., ListPrice = ... }) marks every property modified and
    // writes the stub's defaults (a null Name) into the row.

    // Exercise 3 — Apply an edit
    // Load product `edit.ProductId`, copy the edit's values onto it, and save.
    // Rules: two round trips (load, save); the UPDATE writes only the columns whose values changed.
    protected override async Task Ex3(AdventureWorksContext db, ProductEdit edit)
    {
        var product = await db.Products.SingleAsync(p => p.ProductId == edit.ProductId);
        db.Entry(product).CurrentValues.SetValues(edit);
        await db.SaveChangesAsync();
    }
    // Why: CurrentValues.SetValues copies every property whose name matches, from any object (here a
    // record). Values equal to the snapshot stay unmodified, so the UPDATE contains only Name and
    // ListPrice. Loading first is what makes the minimal UPDATE possible: EF needs original values to
    // compare against.

    // Exercise 4 — Anything to save?
    // Return whether calling SaveChanges now would write anything.
    // Rules: no round trips; don't save.
    protected override bool Ex4(AdventureWorksContext db)
        => db.ChangeTracker.HasChanges();
    // Why: HasChanges runs DetectChanges and reports whether any entry is Added, Modified or Deleted.
    // Tempting-but-wrong: ChangeTracker.Entries().Any() is true as soon as anything is tracked, even if
    // nothing changed.

    // Exercise 5 — Discard local changes
    // `product` is tracked by `db` and has unsaved edits. Throw them away: the entity must be
    // Unchanged again and hold the database's current values.
    // Rules: one round trip.
    protected override Task Ex5(AdventureWorksContext db, Product product)
        => db.Entry(product).ReloadAsync();
    // Why: Reload reads the row again and overwrites both current and original values, so the entry
    // becomes Unchanged. Tempting-but-wrong: querying the product again. The query returns the instance
    // already tracked and does not overwrite it, so the edits survive.

    // Challenge 1 — A new product in an existing subcategory
    // Save `newProduct` as a product of `detachedSubcategory` (an existing row, loaded by another
    // context and not tracked by `db`).
    // Rules: the subcategory must not be inserted again.
    protected override async Task Ch1(AdventureWorksContext db, Product newProduct, ProductSubcategory detachedSubcategory)
    {
        db.ProductSubcategories.Attach(detachedSubcategory); // existing: Unchanged
        newProduct.ProductSubcategory = detachedSubcategory;
        db.Products.Add(newProduct);                         // only the new product is Added
        await db.SaveChangesAsync();
    }
    // Why: Add marks every untracked entity reachable from the one you add as Added, including the
    // detached subcategory, so SaveChanges tries to INSERT it again (and fails on its identity key).
    // Attach it first, or skip the navigation and set newProduct.ProductSubcategoryId.

    // Challenge 2 — Save a disconnected graph
    // `detachedOrder` and its SalesOrderDetails were loaded by another context and edited offline:
    // the detail `changedDetailId` was modified, and one new detail (SalesOrderDetailId 0) was added.
    // Save exactly that: insert the new detail, update the changed one, write nothing else.
    protected override async Task Ch2(AdventureWorksContext db, SalesOrderHeader detachedOrder, int changedDetailId)
    {
        db.ChangeTracker.TrackGraph(detachedOrder, node =>
            node.Entry.State = node.Entry.IsKeySet ? EntityState.Unchanged : EntityState.Added);
        var changed = detachedOrder.SalesOrderDetails.Single(d => d.SalesOrderDetailId == changedDetailId);
        db.Entry(changed).State = EntityState.Modified;
        await db.SaveChangesAsync();
    }
    // Why: TrackGraph walks the graph and lets you decide each entity's state. Entities whose generated
    // key is set already exist (Unchanged), the ones without a key are new (Added), and you mark the one
    // you know changed. Tempting-but-wrong: db.Update(detachedOrder) marks the header and every detail
    // Modified and rewrites all of them; db.Add tries to insert the existing rows.

    // Challenge 3 — Merge into the tracked instance
    // `db` already tracks product `staleCopy.ProductId` with unsaved edits. `staleCopy` is a different
    // instance of the same product, carrying a new ListPrice. Copy that ListPrice onto the product `db`
    // tracks, keeping its other edits.
    // Rules: no round trips; don't save.
    protected override async Task Ch3(AdventureWorksContext db, Product staleCopy)
    {
        var tracked = await db.Products.FindAsync(staleCopy.ProductId);
        tracked!.ListPrice = staleCopy.ListPrice;
    }
    // Why: a context tracks at most one instance per key (identity resolution), so Attach or Update with
    // staleCopy throws "another instance with the same key value is already being tracked". FindAsync
    // looks in the tracker first and only queries when the key isn't tracked, so here it costs nothing.
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E07Solutions`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 4: Write `Examples.cs`**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E07;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E07Examples --logger "console;verbosity=detailed"
public class E07Examples : LabTest
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: an entity's state moves through Added, Unchanged, Modified, Deleted and Detached.
    [Fact]
    public async Task Entity_states_through_a_lifecycle()
    {
        var category = new ProductCategory { Name = "E07 lifecycle" };
        Assert.Equal(EntityState.Detached, Db.Entry(category).State);

        Db.ProductCategories.Add(category);
        Assert.Equal(EntityState.Added, Db.Entry(category).State);

        await Db.SaveChangesAsync();                        // INSERT; the generated key flows back
        Assert.Equal(EntityState.Unchanged, Db.Entry(category).State);
        Assert.True(category.ProductCategoryId > 0);

        category.Name = "E07 lifecycle, renamed";
        Assert.Equal(EntityState.Modified, Db.Entry(category).State);
        await Db.SaveChangesAsync();                        // UPDATE

        Db.ProductCategories.Remove(category);
        Assert.Equal(EntityState.Deleted, Db.Entry(category).State);
        await Db.SaveChangesAsync();                        // DELETE
        Assert.Equal(EntityState.Detached, Db.Entry(category).State);
        Sql.AssertRoundTrips(3);
    }

    // Concept 2: changes are found by comparing with a snapshot, when DetectChanges runs.
    [Fact]
    public async Task Changes_are_detected_against_a_snapshot()
    {
        Db.ChangeTracker.AutoDetectChangesEnabled = false;
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);

        product.Name = "Changed in memory";
        Assert.Equal(EntityState.Unchanged, Db.Entry(product).State); // nothing compared yet
        Assert.Equal(0, await Db.SaveChangesAsync());                  // so nothing is saved

        Db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Modified, Db.Entry(product).State);
        Assert.Equal(1, await Db.SaveChangesAsync());
    }

    // Concept 2: DebugView prints what the tracker knows, including original values.
    [Fact]
    public async Task DebugView_shows_the_tracker()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Changed in memory";
        Db.ChangeTracker.DetectChanges();

        var view = Db.ChangeTracker.DebugView.LongView;
        Print(view);
        Assert.Contains("Product {ProductId: 1} Modified", view);
        Assert.Contains("Name: 'Changed in memory' Modified Originally 'Adjustable Race'", view);
    }

    // Concept 3: one instance per key per context, and a second instance can't be tracked.
    [Fact]
    public async Task Identity_resolution_one_instance_per_key()
    {
        var first = await Db.Products.SingleAsync(p => p.ProductId == 1);
        first.Name = "Edited";
        var second = await Db.Products.SingleAsync(p => p.ProductId == 1);

        Assert.Same(first, second);
        Assert.Equal("Edited", second.Name); // the query did not overwrite the tracked instance

        var failure = Assert.Throws<InvalidOperationException>(() => Db.Products.Attach(new Product { ProductId = 1 }));
        Assert.Contains("another instance with the same key value", failure.Message);
    }

    // Concept 4: only modified properties are written.
    [Fact]
    public async Task Only_modified_columns_are_updated()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Only this column";
        Sql.Reset();

        await Db.SaveChangesAsync();

        Assert.Contains("UPDATE [Production].[Product] SET [Name] = @p0", Sql.All);
        Assert.DoesNotContain("[ListPrice] = @", Sql.All);
    }

    // Concept 5: Attach starts as Unchanged; Update marks every property modified.
    [Fact]
    public async Task Attach_vs_Update()
    {
        var detached = await NewContext().Products.AsNoTracking().SingleAsync(p => p.ProductId == 1);
        detached.ListPrice = 9.99m;
        Sql.Reset();

        var updating = NewContext();
        updating.Products.Update(detached);
        await updating.SaveChangesAsync();
        Assert.Contains("[Name] = @", Sql.All);  // every column rewritten
        Assert.Contains("[Color] = @", Sql.All);

        Sql.Reset();
        var attaching = NewContext();
        var stub = new Product { ProductId = 1 };
        attaching.Products.Attach(stub);
        stub.ListPrice = 19.99m;
        await attaching.SaveChangesAsync();
        Assert.Contains("SET [ListPrice] = @", Sql.All); // one column
        Assert.DoesNotContain("[Name] = @", Sql.All);
    }

    // Concept 6: Add marks every untracked entity in the graph Added, existing ones included.
    [Fact]
    public async Task Add_on_a_graph_inserts_everything_it_reaches()
    {
        var subcategory = await NewContext().ProductSubcategories.AsNoTracking().SingleAsync(s => s.ProductSubcategoryId == 1);
        var product = new Product
        {
            Name = "E07 graph", ProductNumber = "E07-GRAPH", SafetyStockLevel = 1, ReorderPoint = 1,
            StandardCost = 1m, ListPrice = 2m, SellStartDate = new DateTime(2026, 1, 1),
            ProductSubcategory = subcategory,
        };

        Db.Products.Add(product);
        Print(Db.ChangeTracker.DebugView.ShortView);

        Assert.Equal(EntityState.Added, Db.Entry(subcategory).State); // the existing subcategory too
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
        Assert.Contains("IDENTITY_INSERT", failure.InnerException!.Message);
    }

    // Concept 6: TrackGraph lets you choose each entity's state.
    [Fact]
    public async Task TrackGraph_decides_per_entity()
    {
        var subcategory = await NewContext().ProductSubcategories.AsNoTracking().SingleAsync(s => s.ProductSubcategoryId == 1);
        var product = new Product
        {
            Name = "E07 track graph", ProductNumber = "E07-TRACK", SafetyStockLevel = 1, ReorderPoint = 1,
            StandardCost = 1m, ListPrice = 2m, SellStartDate = new DateTime(2026, 1, 1),
            ProductSubcategory = subcategory,
        };

        Db.ChangeTracker.TrackGraph(product, node =>
            node.Entry.State = node.Entry.IsKeySet ? EntityState.Unchanged : EntityState.Added);

        Assert.Equal(EntityState.Unchanged, Db.Entry(subcategory).State);
        Assert.Equal(EntityState.Added, Db.Entry(product).State);
        await Db.SaveChangesAsync();
        Assert.DoesNotContain("INSERT INTO [Production].[ProductSubcategory]", Sql.All);
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E07Examples`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 5: Write `README.md`**

````markdown
# E07 — Change Tracking Internals

## What you'll learn

- The five entity states and what moves an entity between them
- How EF finds changes: snapshots and `DetectChanges`, and reading them with `Entry(...)` and `ChangeTracker.DebugView`
- Original vs current values, and why the UPDATE only writes modified columns
- Identity resolution: one instance per key per context, and the "already being tracked" error
- Disconnected entities: `Attach` vs `Update`, `CurrentValues.SetValues`, and updating without loading
- Graph pitfalls: what `Add` and `Update` do to every entity they reach, and `TrackGraph`

## Builds on

- No T-SQL lesson: change tracking is EF's own machinery.
- EF lessons [E05](../E05-loading-related-data/README.md) (loading graphs) and [E06](../E06-query-performance/README.md) (tracking vs no-tracking)

## Setup

```
dotnet test --filter E07Examples --logger "console;verbosity=detailed"
```

Everything a test writes is rolled back when it ends, so these lessons change AdventureWorks freely.

## Concepts

### 1. Entity states

| State | Meaning | `SaveChanges` sends |
|---|---|---|
| `Detached` | not tracked by this context | nothing |
| `Added` | new, not in the database yet | `INSERT` |
| `Unchanged` | tracked, same as when loaded (or last saved) | nothing |
| `Modified` | tracked, at least one property changed | `UPDATE` of the modified columns |
| `Deleted` | marked for deletion | `DELETE` |

A query with tracking returns `Unchanged` entities. `Add`, `Attach`, `Update` and `Remove` set states explicitly. After a successful `SaveChanges`, Added and Modified entities become Unchanged (with database-generated values such as identity keys copied back) and Deleted ones become Detached.

Example: `Entity_states_through_a_lifecycle`.

### 2. Snapshots and `DetectChanges`

Plain entity classes don't tell EF when you set a property. Instead, EF keeps a **snapshot** of the original values of every tracked entity and compares it with the current values in `DetectChanges`. That runs automatically before `SaveChanges`, in `ChangeTracker.Entries()` and `HasChanges()`, and for a single entity in `Entry(entity)`. A property set to the value it already had is not a change.

`ChangeTracker.AutoDetectChangesEnabled = false` turns the automatic calls off. That's useful when attaching thousands of entities in a loop, as long as you call `DetectChanges()` yourself before saving. `ChangeTracker.DebugView.LongView` (or `ShortView`) prints every tracked entity with its state, and for modified properties both values (`Name: 'X' Modified Originally 'Y'`).

Examples: `Changes_are_detected_against_a_snapshot`, `DebugView_shows_the_tracker`.

### 3. Identity resolution

A context tracks at most one instance per primary key. Query the same row twice and you get the same object, and the second query does **not** overwrite its values. That's how unsaved edits survive, and also why a re-query doesn't refresh stale data (use `Entry(e).ReloadAsync()`). Trying to track a second instance with the same key (`Attach`, `Update`, `Add`) throws "another instance with the same key value is already being tracked". Find the tracked one instead: `FindAsync` checks the tracker before querying, and `DbSet.Local` lists what's tracked.

Example: `Identity_resolution_one_instance_per_key`.

### 4. Original and current values

`db.Entry(e)` exposes `OriginalValues`, `CurrentValues`, and per property `.OriginalValue`, `.CurrentValue` and `.IsModified`. The UPDATE contains only properties marked modified. `CurrentValues.SetValues(obj)` copies matching properties from any object (a DTO or record), and only real changes become modified.

Example: `Only_modified_columns_are_updated`.

### 5. Disconnected entities: `Attach` vs `Update`

Entities that come from another context, an API request or a cache are not tracked. Two ways to bring one back:

| Method | State | UPDATE |
|---|---|---|
| `Attach(e)` | `Unchanged`; then change properties | only the properties you change afterwards |
| `Update(e)` | `Modified`, every property | every column, whatever its value |

`Update` is right when the object carries the complete new row. To change one column without reading the row, attach a stub that has only the key and set the property: one round trip, one column. EF can't tell you whether the row exists until `SaveChanges` updates 0 rows (E09).

Example: `Attach_vs_Update`.

### 6. Graphs

`Add`, `Attach` and `Update` act on everything reachable from the entity you pass, through loaded navigations:

- `Add(newProduct)` with `newProduct.ProductSubcategory = existingSubcategory` marks the existing subcategory **Added** too, and SaveChanges tries to insert it.
- `Update(order)` marks the order and every line Modified, even if one line changed. A line with no key value (an unset generated key) becomes Added.

Fixes: attach the existing entities first (`Attach` before `Add`), set foreign-key properties instead of navigations (`ProductSubcategoryId = 1`), or use `ChangeTracker.TrackGraph(root, node => ...)`, which visits every entity in the graph and lets you set each state, typically from `node.Entry.IsKeySet`.

Examples: `Add_on_a_graph_inserts_everything_it_reaches`, `TrackGraph_decides_per_entity`.

## Pitfalls

- **`Update` for a one-column change.** It rewrites every column, including ones another user changed in the meantime.
- **A re-query to "refresh".** Tracked entities are not overwritten. Use `Reload`.
- **`Entries().Any()` as "has changes".** It counts unchanged entities too. Use `HasChanges()`.
- **`Add` on a graph containing existing entities.** It re-inserts them.
- **Two instances, one key.** Find and update the tracked one.

## Exercises

```
dotnet test --filter E07Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
````

- [ ] **Step 6: Prompts are identical in Exercises.cs and Solutions.cs**

```bash
cd efcore && python - lessons/E07-change-tracking <<'EOF'
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

- [ ] **Step 7: Commit**

```bash
git add efcore/lessons/E07-change-tracking
git commit -m "feat: add EF Core lesson E07 - change tracking internals"
```

- [ ] **Step 8: A wrong answer fails for the right reason.** Temporarily replace the body of `Ch3` in `Exercises.cs` (the `=> throw new NotImplementedException();` line under `Ch3`) with the "attach the other instance" answer:

```csharp
    {
        db.Products.Update(staleCopy);
        return Task.CompletedTask;
    }
```

(and remove the `=>` stub line). Run: `cd efcore && dotnet build && dotnet test --no-build --filter "FullyQualifiedName~E07Exercises.Ch3"`
Expected: `Failed: 1` with `InvalidOperationException : The instance of entity type 'Product' cannot be tracked because another instance with the same key value for {'ProductId'} is already being tracked.` Then restore and confirm a clean tree:

```bash
git checkout -- efcore/lessons/E07-change-tracking/Exercises.cs
git status --short
```

Expected: `git status --short` prints nothing.

---

## Task 3: Lesson E08 — SaveChanges & set-based writes

**Files:**
- Create: `efcore/lessons/E08-savechanges-and-set-based-writes/E08Context.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `LabTest` (`Db`, `NewContext()`, `NewContext<T>(params IInterceptor[])` from Task 1, `Sql`, `Reference`), `[Exercise]`, the scaffolded model (its `HasTrigger` declarations).
- Produces: namespace `EfCoreLessons.E08`: `E08Context`, records `ProductPrice`, `ProductStamp`, classes `E08Checks`, `E08Exercises`, `E08Solutions`, `E08Examples`. Nothing later depends on them.

- [ ] **Step 1: Write `E08Context.cs`** (a model without the `uSalesOrderHeader` trigger, for one example)

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E08;

// AdventureWorksContext whose model "forgets" the uSalesOrderHeader trigger: what a model looks
// like when nobody declared HasTrigger. Used by one example to show why the declaration matters.
public class E08Context(DbContextOptions<AdventureWorksContext> options) : AdventureWorksContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SalesOrderHeader>().Metadata.RemoveTrigger("uSalesOrderHeader");
    }
}
```

- [ ] **Step 2: Write `Checks.cs`**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.E08;

public record ProductPrice(int ProductId, decimal ListPrice);
public record ProductStamp(int ProductId, decimal ListPrice, DateTime ModifiedDate);

// The checks for E08. E08Exercises (yours) and E08Solutions both inherit them.
public abstract class E08Checks : LabTest
{
    protected abstract Task Ex1(AdventureWorksContext db, List<ProductCategory> categories);
    protected abstract Task<int> Ex2(AdventureWorksContext db, int subcategoryId, decimal percent);
    protected abstract Task<int> Ex3(AdventureWorksContext db, int salesOrderId);
    protected abstract Task<List<Product>> Ex4(AdventureWorksContext db, int subcategoryId);
    protected abstract SaveChangesInterceptor Ex5();
    protected abstract Task Ch1(AdventureWorksContext db, int salesOrderId);
    protected abstract Task Ch2(AdventureWorksContext db, int subcategoryId);
    protected abstract Task<int> Ch3(AdventureWorksContext db, int subcategoryId, decimal percent, DateTime now);

    Task<List<ProductPrice>> PricesIn(int subcategoryId) => Reference.Rows<ProductPrice>(
        $"SELECT ProductID AS ProductId, ListPrice FROM Production.Product WHERE ProductSubcategoryID = {subcategoryId} ORDER BY ProductID");

    static List<ProductPrice> Raised(List<ProductPrice> prices, decimal percent)
        => prices.Select(p => p with { ListPrice = p.ListPrice * (1 + percent / 100) }).ToList();

    async Task<int> Count(FormattableString sql) => (await Reference.Rows<int>(sql)).Single();

    [Exercise]
    public async Task Ex1_InsertInOneRoundTrip()
    {
        List<ProductCategory> categories = [new() { Name = "E08 Drones" }, new() { Name = "E08 Scooters" }, new() { Name = "E08 Skates" }];

        await Ex1(Db, categories);

        Sql.AssertRoundTrips(1);
        Assert.All(categories, c => Assert.True(c.ProductCategoryId > 0)); // generated keys came back
        Assert.Equal(3, await Count($"SELECT COUNT(*) AS Value FROM Production.ProductCategory WHERE Name LIKE 'E08 %'"));
    }

    [Exercise]
    public async Task Ex2_RaisePricesInOneStatement()
    {
        var expected = Raised(await PricesIn(1), 10m);

        var rows = await Ex2(Db, 1, 10m);

        Assert.Equal(expected.Count, rows);
        Sql.AssertRoundTrips(1);
        Assert.StartsWith("UPDATE", Sql.Commands[0].TrimStart());
        Assert.Empty(Db.ChangeTracker.Entries()); // nothing loaded
        Assert.Equal(expected, await PricesIn(1));
    }

    [Exercise]
    public async Task Ex3_DeleteInOneStatement()
    {
        var rows = await Ex3(Db, 43697);

        Assert.Equal(2, rows);
        Sql.AssertRoundTrips(1);
        Assert.StartsWith("DELETE", Sql.Commands[0].TrimStart());
        Assert.Equal(0, await Count($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderHeaderSalesReason WHERE SalesOrderID = 43697"));
    }

    [Exercise]
    public async Task Ex4_NoStaleEntities()
    {
        var tracked = await Db.Products.Where(p => p.ProductSubcategoryId == 1).ToListAsync();
        var expected = Raised(await PricesIn(1), 10m);
        Sql.Reset();

        var products = await Ex4(Db, 1);

        Sql.AssertRoundTrips(2); // the UPDATE, then one query
        Assert.Contains("UPDATE", Sql.Commands[0]);
        Assert.Equal(expected, products.OrderBy(p => p.ProductId).Select(p => new ProductPrice(p.ProductId, p.ListPrice)).ToList());
    }

    [Exercise]
    public async Task Ex5_StampModifiedDate()
    {
        var db = NewContext<AdventureWorksContext>(Ex5());
        var product = await db.Products.SingleAsync(p => p.ProductId == 1);
        var untouched = await db.Products.SingleAsync(p => p.ProductId == 2);
        var untouchedDate = untouched.ModifiedDate;
        var before = DateTime.Now;

        product.Name = "Stamped";
        var category = new ProductCategory { Name = "E08 Stamped" };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();

        var after = DateTime.Now;
        Assert.InRange(product.ModifiedDate, before, after);
        Assert.InRange(category.ModifiedDate, before, after);
        Assert.Equal(untouchedDate, untouched.ModifiedDate); // unchanged entities keep their date
        var saved = (await Reference.Rows<ProductStamp>($"SELECT ProductID AS ProductId, ListPrice, ModifiedDate FROM Production.Product WHERE ProductID = 1")).Single();
        Assert.Equal(product.ModifiedDate, saved.ModifiedDate, TimeSpan.FromMilliseconds(5)); // datetime rounds to ~3 ms
    }

    [Exercise]
    public async Task Ch1_LetTheDatabaseCascade()
    {
        await Ch1(Db, 43659);

        Sql.AssertRoundTrips(1);
        Assert.DoesNotContain("[Sales].[SalesOrderDetail]", Sql.All); // the database deleted the lines
        Assert.Equal(0, await Count($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderHeader WHERE SalesOrderID = 43659"));
        Assert.Equal(0, await Count($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43659"));
    }

    [Exercise]
    public async Task Ch2_DeleteAParentOfOptionalChildren()
    {
        var products = await Count($"SELECT COUNT(*) AS Value FROM Production.Product");
        var children = await Count($"SELECT COUNT(*) AS Value FROM Production.Product WHERE ProductSubcategoryID = 1");
        var withoutSubcategory = await Count($"SELECT COUNT(*) AS Value FROM Production.Product WHERE ProductSubcategoryID IS NULL");

        await Ch2(Db, 1);

        Assert.InRange(Sql.Count, 1, 2);
        Assert.Equal(0, await Count($"SELECT COUNT(*) AS Value FROM Production.ProductSubcategory WHERE ProductSubcategoryID = 1"));
        Assert.Equal(products, await Count($"SELECT COUNT(*) AS Value FROM Production.Product")); // no product deleted
        Assert.Equal(withoutSubcategory + children, await Count($"SELECT COUNT(*) AS Value FROM Production.Product WHERE ProductSubcategoryID IS NULL"));
    }

    [Exercise]
    public async Task Ch3_SetBasedWritesSkipInterceptors()
    {
        var now = new DateTime(2026, 10, 12, 9, 30, 0);
        var expected = Raised(await PricesIn(1), 5m);

        var rows = await Ch3(Db, 1, 5m, now);

        Assert.Equal(expected.Count, rows);
        Sql.AssertRoundTrips(1);
        var saved = await Reference.Rows<ProductStamp>($"SELECT ProductID AS ProductId, ListPrice, ModifiedDate FROM Production.Product WHERE ProductSubcategoryID = 1 ORDER BY ProductID");
        Assert.Equal(expected, saved.Select(s => new ProductPrice(s.ProductId, s.ListPrice)).ToList());
        Assert.All(saved, s => Assert.Equal(now, s.ModifiedDate));
    }
}
```

- [ ] **Step 3: Write `Exercises.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.E08;

// E08 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E08Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E08Exercises : E08Checks
{
    // Exercise 1 — Insert in one round trip
    // Insert all of `categories` (new ProductCategory objects with only Name set).
    // Rules: one round trip; afterwards each object carries its database-generated ProductCategoryId.
    protected override Task Ex1(AdventureWorksContext db, List<ProductCategory> categories)
        => throw new NotImplementedException();

    // Exercise 2 — Raise prices in one statement
    // Raise the ListPrice of every product in subcategory `subcategoryId` by `percent` percent, and
    // return the number of rows updated.
    // Rules: one round trip, an UPDATE; load nothing.
    protected override Task<int> Ex2(AdventureWorksContext db, int subcategoryId, decimal percent)
        => throw new NotImplementedException();

    // Exercise 3 — Delete in one statement
    // Delete every SalesOrderHeaderSalesReason row of order `salesOrderId`, and return how many
    // rows were deleted.
    // Rules: one round trip, a DELETE.
    protected override Task<int> Ex3(AdventureWorksContext db, int salesOrderId)
        => throw new NotImplementedException();

    // Exercise 4 — No stale entities
    // The products of subcategory `subcategoryId` are already tracked by `db`. Raise their ListPrice
    // by 10% with ExecuteUpdate, then return the subcategory's products showing the new prices.
    // Rules: two round trips (the UPDATE and one query).
    protected override Task<List<Product>> Ex4(AdventureWorksContext db, int subcategoryId)
        => throw new NotImplementedException();

    // Exercise 5 — Stamp ModifiedDate
    // Return a SaveChangesInterceptor that, just before saving, sets ModifiedDate to DateTime.Now on
    // every Added or Modified entity that has a ModifiedDate property.
    // Rules: leave Unchanged entities alone; it must work for SaveChangesAsync.
    protected override SaveChangesInterceptor Ex5()
        => throw new NotImplementedException();

    // Challenge 1 — Let the database cascade
    // Delete order `salesOrderId` together with its lines (and its sales reasons).
    // Rules: one round trip. The database's foreign keys cascade from the order to both child tables.
    protected override Task Ch1(AdventureWorksContext db, int salesOrderId)
        => throw new NotImplementedException();

    // Challenge 2 — Delete a parent of optional children
    // Delete subcategory `subcategoryId`. Its products must keep existing, with no subcategory.
    // Rules: at most two round trips. The database's foreign key is NO ACTION: it won't null the
    // products' ProductSubcategoryID for you.
    protected override Task Ch2(AdventureWorksContext db, int subcategoryId)
        => throw new NotImplementedException();

    // Challenge 3 — Set-based writes skip interceptors
    // Raise the ListPrice of every product in subcategory `subcategoryId` by `percent` percent and set
    // their ModifiedDate to `now`, and return the number of rows updated.
    // Rules: one round trip.
    protected override Task<int> Ch3(AdventureWorksContext db, int subcategoryId, decimal percent, DateTime now)
        => throw new NotImplementedException();
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E08Exercises`
Expected: `Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

- [ ] **Step 4: Write `Solutions.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.E08;

// Worked solutions for E08, each followed by a Why note. Run: dotnet test --filter E08Solutions
public class E08Solutions : E08Checks
{
    // Exercise 1 — Insert in one round trip
    // Insert all of `categories` (new ProductCategory objects with only Name set).
    // Rules: one round trip; afterwards each object carries its database-generated ProductCategoryId.
    protected override Task Ex1(AdventureWorksContext db, List<ProductCategory> categories)
    {
        db.ProductCategories.AddRange(categories);
        return db.SaveChangesAsync();
    }
    // Why: SaveChanges batches every pending change into as few commands as it can. Several inserts
    // into the same table become a single MERGE ... OUTPUT INSERTED.[ProductCategoryID], which returns
    // the identity values EF copies back into the objects. Tempting-but-wrong: SaveChanges inside the
    // loop, one round trip per row.

    // Exercise 2 — Raise prices in one statement
    // Raise the ListPrice of every product in subcategory `subcategoryId` by `percent` percent, and
    // return the number of rows updated.
    // Rules: one round trip, an UPDATE; load nothing.
    protected override Task<int> Ex2(AdventureWorksContext db, int subcategoryId, decimal percent)
    {
        var factor = 1 + percent / 100;
        return db.Products
            .Where(p => p.ProductSubcategoryId == subcategoryId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, p => p.ListPrice * factor));
    }
    // Why: ExecuteUpdate translates the query and the setters into one UPDATE ... FROM ... WHERE, run
    // immediately, without loading or tracking anything. Load-modify-save would read 32 rows and send
    // 32 updates (batched, but still one statement per row).

    // Exercise 3 — Delete in one statement
    // Delete every SalesOrderHeaderSalesReason row of order `salesOrderId`, and return how many
    // rows were deleted.
    // Rules: one round trip, a DELETE.
    protected override Task<int> Ex3(AdventureWorksContext db, int salesOrderId)
        => db.SalesOrderHeaderSalesReasons
            .Where(r => r.SalesOrderId == salesOrderId)
            .ExecuteDeleteAsync();
    // Why: ExecuteDelete is a DELETE with the query's WHERE. No entity has to be loaded first, which
    // Remove() would require.

    // Exercise 4 — No stale entities
    // The products of subcategory `subcategoryId` are already tracked by `db`. Raise their ListPrice
    // by 10% with ExecuteUpdate, then return the subcategory's products showing the new prices.
    // Rules: two round trips (the UPDATE and one query).
    protected override async Task<List<Product>> Ex4(AdventureWorksContext db, int subcategoryId)
    {
        await db.Products
            .Where(p => p.ProductSubcategoryId == subcategoryId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, p => p.ListPrice * 1.1m));
        db.ChangeTracker.Clear();
        return await db.Products.Where(p => p.ProductSubcategoryId == subcategoryId).ToListAsync();
    }
    // Why: ExecuteUpdate bypasses the change tracker, so the tracked entities keep their old prices.
    // A plain re-query doesn't fix that: identity resolution returns the tracked instances unchanged.
    // Clear() detaches everything, so the next query materializes fresh entities. (Reloading each entity
    // also works, at one round trip per entity.)

    // Exercise 5 — Stamp ModifiedDate
    // Return a SaveChangesInterceptor that, just before saving, sets ModifiedDate to DateTime.Now on
    // every Added or Modified entity that has a ModifiedDate property.
    // Rules: leave Unchanged entities alone; it must work for SaveChangesAsync.
    protected override SaveChangesInterceptor Ex5() => new ModifiedDateStamp();

    sealed class ModifiedDateStamp : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Stamp(eventData.Context!);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Stamp(eventData.Context!);
            return ValueTask.FromResult(result);
        }

        static void Stamp(DbContext db)
        {
            var now = DateTime.Now;
            foreach (var entry in db.ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
                if (entry.Metadata.FindProperty("ModifiedDate") is null) continue;
                entry.Property("ModifiedDate").CurrentValue = now;
            }
        }
    }
    // Why: SavingChanges runs inside SaveChanges, after DetectChanges, so entry states are final and a
    // value set here is saved with everything else. Async saves call SavingChangesAsync only, so override
    // both. FindProperty keeps it generic: any entity type with a ModifiedDate column gets stamped.

    // Challenge 1 — Let the database cascade
    // Delete order `salesOrderId` together with its lines (and its sales reasons).
    // Rules: one round trip. The database's foreign keys cascade from the order to both child tables.
    protected override Task Ch1(AdventureWorksContext db, int salesOrderId)
        => db.SalesOrderHeaders.Where(h => h.SalesOrderId == salesOrderId).ExecuteDeleteAsync();
    // Why: FK_SalesOrderDetail_SalesOrderHeader and FK_SalesOrderHeaderSalesReason_SalesOrderHeader are
    // ON DELETE CASCADE, so deleting the header deletes the children inside the same statement. Tempting-
    // but-wrong: load the order with Include(lines) and Remove it. EF then deletes every tracked line
    // itself (client-side cascade), one DELETE per line, after a round trip to load them.

    // Challenge 2 — Delete a parent of optional children
    // Delete subcategory `subcategoryId`. Its products must keep existing, with no subcategory.
    // Rules: at most two round trips. The database's foreign key is NO ACTION: it won't null the
    // products' ProductSubcategoryID for you.
    protected override async Task Ch2(AdventureWorksContext db, int subcategoryId)
    {
        await db.Products
            .Where(p => p.ProductSubcategoryId == subcategoryId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ProductSubcategoryId, (int?)null));
        await db.ProductSubcategories.Where(s => s.ProductSubcategoryId == subcategoryId).ExecuteDeleteAsync();
    }
    // Why: the scaffolded relationship is DeleteBehavior.ClientSetNull: EF nulls the FK of dependents
    // it tracks, and the database does nothing. Loading the subcategory with Include(products) and
    // removing it also works (EF sends 32 UPDATEs and the DELETE in one batch: 2 round trips). Removing
    // it without its products fails: the database rejects the DELETE because products still refer to it.

    // Challenge 3 — Set-based writes skip interceptors
    // Raise the ListPrice of every product in subcategory `subcategoryId` by `percent` percent and set
    // their ModifiedDate to `now`, and return the number of rows updated.
    // Rules: one round trip.
    protected override Task<int> Ch3(AdventureWorksContext db, int subcategoryId, decimal percent, DateTime now)
    {
        var factor = 1 + percent / 100;
        return db.Products
            .Where(p => p.ProductSubcategoryId == subcategoryId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(p => p.ListPrice, p => p.ListPrice * factor)
                .SetProperty(p => p.ModifiedDate, now));
    }
    // Why: ExecuteUpdate and ExecuteDelete don't go through SaveChanges, so SaveChanges interceptors
    // (like exercise 5's stamp), the change tracker and its concurrency checks never see them. Whatever
    // the interceptor would have done, the statement has to do itself: one more SetProperty.
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E08Solutions`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 5: Write `Examples.cs`**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E08;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E08Examples --logger "console;verbosity=detailed"
public class E08Examples : LabTest
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: one insert is INSERT ... OUTPUT; several into one table become a single MERGE.
    [Fact]
    public async Task Inserts_are_batched()
    {
        Db.ProductCategories.Add(new ProductCategory { Name = "E08 single" });
        await Db.SaveChangesAsync();
        Assert.Contains("INSERT INTO [Production].[ProductCategory]", Sql.Commands[0]);
        Assert.Contains("OUTPUT INSERTED.[ProductCategoryID]", Sql.Commands[0]);

        Db.ProductCategories.AddRange(new ProductCategory { Name = "E08 a" }, new ProductCategory { Name = "E08 b" }, new ProductCategory { Name = "E08 c" });
        Db.SalesReasons.Add(new SalesReason { Name = "E08 reason", ReasonType = "Other" });
        await Db.SaveChangesAsync();

        Sql.AssertRoundTrips(2);  // every change of the second save in one command
        var batch = Sql.Commands[1];
        Assert.Contains("MERGE [Production].[ProductCategory]", batch);
        Assert.Contains("INSERT INTO [Sales].[SalesReason]", batch);
    }

    // Concept 2: on a table with a trigger, EF avoids OUTPUT and reads @@ROWCOUNT instead.
    [Fact]
    public async Task Tables_with_triggers_get_no_OUTPUT_clause()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);          // no trigger
        var order = await Db.SalesOrderHeaders.SingleAsync(h => h.SalesOrderId == 43659); // uSalesOrderHeader
        product.Name = "Changed";
        order.Comment = "Changed";
        Sql.Reset();

        await Db.SaveChangesAsync();
        Print(Sql.All);

        var batch = Sql.Commands.Single();
        var orderUpdate = batch[batch.IndexOf("UPDATE [Sales].[SalesOrderHeader]")..];
        Assert.Matches(@"UPDATE \[Production\]\.\[Product\] SET \[Name\] = @p0\s+OUTPUT 1", batch);
        Assert.DoesNotContain("OUTPUT", orderUpdate);   // a trigger table: no OUTPUT clause
        Assert.Contains("@@ROWCOUNT = 1", orderUpdate); // computed columns read back with a separate SELECT
    }

    // Concept 2: without HasTrigger in the model, SQL Server rejects EF's OUTPUT clause.
    [Fact]
    public async Task Without_HasTrigger_the_update_fails()
    {
        var db = NewContext<E08Context>(); // its model has no trigger declared on SalesOrderHeader
        var order = await db.SalesOrderHeaders.SingleAsync(h => h.SalesOrderId == 43659);
        order.Comment = "Changed";

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Print(failure.InnerException!.Message);
        Assert.Contains("cannot have any enabled triggers if the statement contains an OUTPUT clause", failure.InnerException.Message);
    }

    // Concept 3: ExecuteUpdate / ExecuteDelete are one statement, run now, with nothing tracked.
    [Fact]
    public async Task ExecuteUpdate_and_ExecuteDelete_are_single_statements()
    {
        var updated = await Db.Products
            .Where(p => p.ProductSubcategoryId == 1)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, p => p.ListPrice * 1.1m));
        var deleted = await Db.SalesOrderHeaderSalesReasons
            .Where(r => r.SalesOrderId == 43697)
            .ExecuteDeleteAsync();

        Assert.Equal(32, updated);
        Assert.Equal(2, deleted);
        Sql.AssertRoundTrips(2);
        Assert.Empty(Db.ChangeTracker.Entries());
    }

    // Concept 3: ExecuteUpdate doesn't touch tracked entities, and a re-query doesn't refresh them.
    [Fact]
    public async Task ExecuteUpdate_leaves_tracked_entities_stale()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 771);
        var before = product.ListPrice;

        await Db.Products.Where(p => p.ProductId == 771).ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, 1m));
        var requeried = await Db.Products.SingleAsync(p => p.ProductId == 771);

        Assert.Same(product, requeried);
        Assert.Equal(before, requeried.ListPrice); // stale: the row now says 1.00
        await Db.Entry(product).ReloadAsync();
        Assert.Equal(1m, product.ListPrice);
    }

    // Concept 4: tracked dependents are deleted by EF; untracked ones by the database's ON DELETE CASCADE.
    [Fact]
    public async Task Client_cascade_vs_database_cascade()
    {
        var withLines = await Db.SalesOrderHeaders.Include(h => h.SalesOrderDetails).SingleAsync(h => h.SalesOrderId == 43660);
        Db.Remove(withLines);
        Sql.Reset();
        await Db.SaveChangesAsync();
        Assert.Contains("DELETE FROM [Sales].[SalesOrderDetail]", Sql.All); // EF deleted the 2 tracked lines

        var alone = await Db.SalesOrderHeaders.SingleAsync(h => h.SalesOrderId == 43661);
        Db.Remove(alone);
        Sql.Reset();
        await Db.SaveChangesAsync();
        Assert.DoesNotContain("[Sales].[SalesOrderDetail]", Sql.All);        // the database deleted the lines
        Assert.Equal(0, (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43661")).Single());
    }

    // Concept 4: ClientSetNull nulls the FKs of tracked dependents; untracked ones block the delete.
    [Fact]
    public async Task ClientSetNull_nulls_tracked_dependents()
    {
        var subcategory = await Db.ProductSubcategories.Include(s => s.Products).SingleAsync(s => s.ProductSubcategoryId == 1);
        Db.Remove(subcategory);
        Sql.Reset();

        await Db.SaveChangesAsync();

        Assert.Equal(32, Sql.All.Split("UPDATE [Production].[Product] SET [ProductSubcategoryID] = @").Length - 1);
        Assert.Contains("DELETE FROM [Production].[ProductSubcategory]", Sql.All);
        Assert.All(subcategory.Products, p => Assert.Null(p.ProductSubcategoryId));

        var other = NewContext();
        other.Remove(await other.ProductSubcategories.SingleAsync(s => s.ProductSubcategoryId == 2)); // products not loaded
        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());                 // FK violation
    }

    // Concept 5: SavingChanges, the lightweight cousin of a SaveChangesInterceptor.
    [Fact]
    public async Task SavingChanges_event_sees_the_final_states()
    {
        var states = new List<string>();
        Db.SavingChanges += (sender, _) =>
            states.AddRange(((DbContext)sender!).ChangeTracker.Entries().Select(e => $"{e.Metadata.ClrType.Name} {e.State}"));
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Changed";
        Db.ProductCategories.Add(new ProductCategory { Name = "E08 event" });

        await Db.SaveChangesAsync();

        Assert.Equal(["Product Modified", "ProductCategory Added"], states.Order());
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E08Examples`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 6: Write `README.md`**

````markdown
# E08 — SaveChanges & Set-Based Writes

## What you'll learn

- The SQL `SaveChanges` sends: batching, `MERGE ... OUTPUT` for inserts, and how generated keys come back
- Why tables with triggers need `HasTrigger` in the model, and the SQL EF uses instead of `OUTPUT`
- `ExecuteUpdate` / `ExecuteDelete`: one statement, nothing loaded, and what they bypass
- Delete behaviors: client-side cascade, `ClientSetNull`, and the database's own `ON DELETE CASCADE`
- Hooking into saves with a `SaveChangesInterceptor` (or the `SavingChanges` event) to stamp `ModifiedDate`

## Builds on

- T-SQL [lesson 08](../../../lessons/08-dml-insert-update-delete-merge/lesson.ipynb) — `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `OUTPUT`
- T-SQL [lesson 09](../../../lessons/09-ddl-and-constraints/lesson.ipynb) — foreign keys and `ON DELETE` actions
- T-SQL [lesson 10](../../../lessons/10-views-procedures-functions/lesson.ipynb) — triggers
- EF lesson [E07](../E07-change-tracking/README.md) — entity states, which decide what `SaveChanges` writes

## Setup

```
dotnet test --filter E08Examples --logger "console;verbosity=detailed"
```

`E08Context.cs` is AdventureWorksContext with the `uSalesOrderHeader` trigger removed from the model. It's used by one example to show what happens without `HasTrigger`.

## Concepts

### 1. What SaveChanges sends

`SaveChanges` runs `DetectChanges`, then turns every Added, Modified and Deleted entry into SQL and sends it in as few round trips as it can (on SQL Server, batches of up to 42 statements by default). The batch runs in a transaction: EF's own, or, when one is already open, the current one (E09).

| Change | SQL (no trigger on the table) |
|---|---|
| one insert | `INSERT ... OUTPUT INSERTED.[Id] VALUES (...)`: the identity comes back in the same statement |
| several inserts into one table | `MERGE ... USING (VALUES (...), (...)) ... OUTPUT INSERTED.[Id], i._Position`: one statement for all of them |
| update | `UPDATE ... SET <modified columns only> OUTPUT 1 WHERE [Key] = @p`: `OUTPUT 1` tells EF a row was updated |
| delete | `DELETE ... OUTPUT 1 WHERE [Key] = @p` |

Values the database generates (identity, `rowguid` and `ModifiedDate` defaults, computed columns like `SalesOrderNumber`) are read back and copied into your entities.

Example: `Inserts_are_batched`.

### 2. Triggers vs OUTPUT

SQL Server rejects an `OUTPUT` clause without `INTO` on a table that has an enabled trigger: "The target table ... cannot have any enabled triggers if the statement contains an OUTPUT clause without INTO clause". So for tables declared with a trigger, EF switches strategy. The UPDATE has no `OUTPUT`, and a separate `SELECT ... WHERE @@ROWCOUNT = 1 AND [Key] = @p` reads the generated values back.

EF can only do that if it knows about the trigger. The scaffolder recorded every AdventureWorks trigger as `tb.HasTrigger("...")` (E01, challenge 1). In a code-first model, or a database whose triggers were added later, you declare them yourself. Forget one and every save to that table fails.

Examples: `Tables_with_triggers_get_no_OUTPUT_clause`, `Without_HasTrigger_the_update_fails`.

### 3. ExecuteUpdate and ExecuteDelete

```csharp
await db.Products.Where(p => p.ProductSubcategoryId == 1)
    .ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, p => p.ListPrice * 1.1m));
await db.SalesOrderHeaderSalesReasons.Where(r => r.SalesOrderId == 43697).ExecuteDeleteAsync();
```

Each is one set-based statement, sent immediately (not at `SaveChanges`), returning the number of rows affected. Nothing is loaded or tracked. They also **bypass** everything that lives in `SaveChanges`:

- the change tracker: entities already tracked keep their old values, and a re-query returns those same stale instances (identity resolution). Use `ChangeTracker.Clear()` or `Reload`.
- `SaveChanges` interceptors and the `SavingChanges` event: if the interceptor stamps `ModifiedDate`, set it in the statement yourself.
- concurrency tokens (E09) and client-side cascades.

Examples: `ExecuteUpdate_and_ExecuteDelete_are_single_statements`, `ExecuteUpdate_leaves_tracked_entities_stale`.

### 4. Deleting principals

What happens to the children when you delete a parent depends on two settings that are easy to confuse:

| Where | Setting | Effect |
|---|---|---|
| EF model | `DeleteBehavior.Cascade` | EF deletes **tracked** dependents itself (one `DELETE` each) |
| EF model | `DeleteBehavior.ClientSetNull` (scaffolded for `NO ACTION` keys) | EF sets the FK of **tracked** dependents to null |
| Database | `ON DELETE CASCADE` | the database deletes **all** dependents in the same statement |
| Database | `NO ACTION` | the DELETE fails while any dependent row still refers to the parent |

In AdventureWorks only two foreign keys cascade: order lines and order sales reasons to `SalesOrderHeader`. Everything else is `NO ACTION`. Deleting a subcategory therefore works only if no product refers to it any more. EF nulls the FKs of products it has loaded, but it can't see the ones it hasn't.

Examples: `Client_cascade_vs_database_cascade`, `ClientSetNull_nulls_tracked_dependents`.

### 5. Interceptors and events

A `SaveChangesInterceptor` (registered with `AddInterceptors(...)`, here through `NewContext<T>(interceptor)`) gets `SavingChanges` before the SQL is generated and `SavedChanges` / `SaveChangesFailed` after. Async saves call the `...Async` overrides **only**, so override both. At `SavingChanges` the states are final, which makes it the place for cross-cutting rules like stamping `ModifiedDate` or auditing. For one context instance, the `db.SavingChanges` event does the same with less ceremony.

Example: `SavingChanges_event_sees_the_final_states`.

## Pitfalls

- **`SaveChanges` in a loop.** One round trip per row. Add everything, save once.
- **A table with an undeclared trigger.** Every save to it fails. Declare `HasTrigger`.
- **Reading tracked entities after `ExecuteUpdate`.** They're stale until you clear or reload.
- **Relying on an interceptor for set-based writes.** `ExecuteUpdate` never calls it.
- **Deleting a parent whose children aren't loaded**, when the key is `NO ACTION`. FK violation. Null or delete the children first.
- **An interceptor that only overrides `SavingChanges`.** `SaveChangesAsync` skips it silently.

## Exercises

```
dotnet test --filter E08Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
````

- [ ] **Step 7: Prompts are identical in Exercises.cs and Solutions.cs**

```bash
cd efcore && python - lessons/E08-savechanges-and-set-based-writes <<'EOF'
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
git add efcore/lessons/E08-savechanges-and-set-based-writes
git commit -m "feat: add EF Core lesson E08 - SaveChanges and set-based writes"
```

- [ ] **Step 9: A wrong answer fails for the right reason.** An interceptor that overrides only the synchronous `SavingChanges` is never called by `SaveChangesAsync`. Temporarily replace `Ex5` in `Exercises.cs` (its signature line and the stub line under it) with:

```csharp
    protected override SaveChangesInterceptor Ex5() => new SyncOnly();

    sealed class SyncOnly : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            foreach (var e in eventData.Context!.ChangeTracker.Entries())
                if (e.State is EntityState.Added or EntityState.Modified) e.Property("ModifiedDate").CurrentValue = DateTime.Now;
            return result;
        }
    }
```

Run: `cd efcore && dotnet build && dotnet test --no-build --filter "FullyQualifiedName~E08Exercises.Ex5"`
Expected: `Failed: 1` with `Assert.InRange() Failure: Value not in range` (the date was never stamped). Then restore and confirm a clean tree:

```bash
git checkout -- efcore/lessons/E08-savechanges-and-set-based-writes/Exercises.cs
git status --short
```

Expected: `git status --short` prints nothing.

---

## Task 4: Lesson E09 — Concurrency & transactions

**Files:**
- Create: `efcore/lessons/E09-concurrency-and-transactions/E09Context.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `LabTest<TContext>` (`Db`, `NewContext()`, `NewContext<T>(params IInterceptor[])`, `NewSessionAsync(IsolationLevel)`, `LockTimeoutMs`, `Sql`, `Reference`) from Task 1, `[Exercise]`, the scaffolded model.
- Produces: namespace `EfCoreLessons.E09`: `E09Context` (with `static Task AddVersionColumnAsync(DbContext)`), `RetryingContext`, records `PriceAndName`, `BlockingInfo`, classes `E09Checks`, `E09Exercises`, `E09Solutions`, `E09Examples`. Nothing later depends on them.

- [ ] **Step 1: Write `E09Context.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E09;

// AdventureWorksContext with two concurrency tokens:
// - Product.ModifiedDate, the legacy-style token: it only works if every writer changes it.
// - a rowversion column "Version" on ProductCategory, as a shadow property. AdventureWorks has no
//   rowversion columns, so AddVersionColumnAsync adds one inside the test's transaction (DDL is
//   transactional in SQL Server: the rollback removes it again). Call it before touching categories.
public class E09Context(DbContextOptions<AdventureWorksContext> options) : AdventureWorksContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Product>().Property(p => p.ModifiedDate).IsConcurrencyToken();
        modelBuilder.Entity<ProductCategory>().Property<byte[]>("Version").IsRowVersion();
    }

    public static Task AddVersionColumnAsync(DbContext db)
        => db.Database.ExecuteSqlRawAsync("ALTER TABLE Production.ProductCategory ADD Version rowversion");
}

// E09Context with EF's retrying execution strategy switched on.
public class RetryingContext(DbContextOptions<AdventureWorksContext> options) : E09Context(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer(sql => sql.EnableRetryOnFailure());
}
```

- [ ] **Step 2: Write `Checks.cs`**

```csharp
using System.Data.Common;
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCoreLessons.E09;

public record PriceAndName(string Name, decimal ListPrice);
public record BlockingInfo(int BlockingSessionId, string WaitType);

// The checks for E09. E09Exercises (yours) and E09Solutions both inherit them.
// Db is an E09Context: Product.ModifiedDate is a concurrency token.
public abstract class E09Checks : LabTest<E09Context>
{
    protected abstract Task<bool> Ex1(E09Context db, Product product, decimal newPrice);
    protected abstract Task Ex2(E09Context db, Product product, decimal newPrice);
    protected abstract Task<bool> Ex3(E09Context db, int categoryId, string newName, byte[] version);
    protected abstract Task<bool> Ex4(E09Context db, Product first, decimal firstPrice, Product second, decimal secondPrice);
    protected abstract Task<bool> Ex5(E09Context session, int productId, decimal newPrice);
    protected abstract Task Ch1(E09Context db, int productId, short delta);
    protected abstract Task Ch2(E09Context db, Product product);
    protected abstract Task<BlockingInfo> Ch3(E09Context db);

    // Someone else changes the product's price, and its ModifiedDate as every writer should.
    async Task TheyChangePrice(int productId, decimal price)
    {
        var theirs = NewContext();
        var product = await theirs.Products.SingleAsync(p => p.ProductId == productId);
        product.ListPrice = price;
        product.ModifiedDate = product.ModifiedDate.AddSeconds(1);
        await theirs.SaveChangesAsync();
    }

    Task<PriceAndName> Row(int productId) => Reference.Rows<PriceAndName>(
        $"SELECT Name, ListPrice FROM Production.Product WHERE ProductID = {productId}").ContinueWith(t => t.Result.Single());

    [Exercise]
    public async Task Ex1_DetectAConflict()
    {
        var mine = await Db.Products.SingleAsync(p => p.ProductId == 1);
        await TheyChangePrice(1, 50m);
        Assert.False(await Ex1(Db, mine, 60m)); // they saved first
        Assert.Equal(50m, (await Row(1)).ListPrice);

        var fresh = NewContext(); // Db still holds the conflicting change to product 1
        var unrivalled = await fresh.Products.SingleAsync(p => p.ProductId == 2);
        var before = DateTime.Now;
        Assert.True(await Ex1(fresh, unrivalled, 70m));
        Assert.Equal(70m, (await Row(2)).ListPrice);
        Assert.True(unrivalled.ModifiedDate >= before.AddSeconds(-1), "Stamp ModifiedDate: it's the token other writers rely on.");
    }

    [Exercise]
    public async Task Ex2_YourChangeWins()
    {
        var mine = await Db.Products.SingleAsync(p => p.ProductId == 1);
        await TheyChangePrice(1, 50m);
        Sql.Reset();

        await Ex2(Db, mine, 60m);

        Assert.Equal(60m, (await Row(1)).ListPrice);
        Assert.InRange(Sql.Count, 2, 4); // the failed save, reading the current row, the save that wins
        Assert.False(Db.ChangeTracker.HasChanges());
    }

    [Exercise]
    public async Task Ex3_RowversionFromAForm()
    {
        await E09Context.AddVersionColumnAsync(Db);
        var version = (await Reference.Rows<byte[]>($"SELECT Version AS Value FROM Production.ProductCategory WHERE ProductCategoryID = 1")).Single();
        await NewContext().ProductCategories.Where(c => c.ProductCategoryId == 2)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Name, "Parts")); // a different row: version 1 unaffected
        Sql.Reset();

        Assert.True(await Ex3(Db, 1, "Bicycles", version));
        Sql.AssertRoundTrips(1); // no read before the update
        var stale = version;
        Assert.False(await Ex3(NewContext(), 1, "Cycles", stale)); // the first rename changed the version
        Assert.Equal("Bicycles", (await Reference.Rows<string>($"SELECT Name AS Value FROM Production.ProductCategory WHERE ProductCategoryID = 1")).Single());
    }

    [Exercise]
    public async Task Ex4_KeepTheFirstChange()
    {
        var first = await Db.Products.SingleAsync(p => p.ProductId == 1);
        var second = await Db.Products.SingleAsync(p => p.ProductId == 2);

        var saved = await Ex4(Db, first, 10m, second, -1m); // CK_Product_ListPrice rejects a negative price

        Assert.False(saved);
        Assert.Equal(10m, (await Row(1)).ListPrice);
        Assert.Equal(0m, (await Row(2)).ListPrice);
        Assert.False(Db.ChangeTracker.HasChanges(), "Nothing may be left pending after the failed save.");
        Assert.Equal(504, await Db.Products.CountAsync()); // the transaction is still usable
    }

    [Exercise]
    public async Task Ex5_GiveUpOnALock()
    {
        await Db.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, 1m));
        var session = await NewSessionAsync();

        var started = DateTime.UtcNow;
        Assert.False(await Ex5(session, 1, 5m)); // the test's transaction holds the row
        Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(LockTimeoutMs - 500), "It should have waited for the lock first.");

        Assert.True(await Ex5(session, 2, 5m));  // nobody holds this one
    }

    [Exercise]
    public async Task Ch1_NoLostUpdate()
    {
        var original = (await Reference.Rows<short>($"SELECT SafetyStockLevel AS Value FROM Production.Product WHERE ProductID = 1")).Single();
        var db = NewContext<E09Context>(new InterfereOnce(productId: 1, theirDelta: 10));

        await Ch1(db, 1, 5);

        Assert.Equal(original + 10 + 5, (await Reference.Rows<short>($"SELECT SafetyStockLevel AS Value FROM Production.Product WHERE ProductID = 1")).Single());
    }

    [Exercise]
    public async Task Ch2_MergeBothChanges()
    {
        var mine = await Db.Products.SingleAsync(p => p.ProductId == 1);
        mine.Name = "Adjustable Race (mine)";
        await TheyChangePrice(1, 50m);

        await Ch2(Db, mine);

        Assert.Equal(new PriceAndName("Adjustable Race (mine)", 50m), await Row(1)); // my name, their price
        Assert.False(Db.ChangeTracker.HasChanges());
    }

    [Exercise]
    public async Task Ch3_WhoIsBlocking()
    {
        await Db.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, 1m));
        var mySessionId = (await Reference.Rows<int>($"SELECT CAST(@@SPID AS int) AS Value")).Single();
        var session = await NewSessionAsync();
        var blocked = session.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, 2m));
        try
        {
            await Task.Delay(500); // let it start waiting
            Sql.Reset();

            var info = await Ch3(Db);

            Sql.AssertRoundTrips(1);
            Assert.Equal(mySessionId, info.BlockingSessionId);
            Assert.StartsWith("LCK_M_", info.WaitType);
        }
        finally
        {
            // Always wait for the blocked write to give up (after LockTimeoutMs): a session can't be
            // closed cleanly while one of its commands is still running.
            await Assert.ThrowsAnyAsync<Exception>(() => blocked);
        }
    }

    // Plays "another user": the first time db saves, it bumps the product's SafetyStockLevel (and
    // ModifiedDate, as every writer must) directly in the database, just before db's own UPDATE runs.
    sealed class InterfereOnce(int productId, int theirDelta) : SaveChangesInterceptor
    {
        bool done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (done) return result;
            done = true;
            var database = eventData.Context!.Database;
            await using DbCommand command = database.GetDbConnection().CreateCommand();
            command.Transaction = database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = $"""
                UPDATE Production.Product
                SET SafetyStockLevel = SafetyStockLevel + {theirDelta}, ModifiedDate = DATEADD(second, 1, ModifiedDate)
                WHERE ProductID = {productId}
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }
    }
}
```

- [ ] **Step 3: Write `Exercises.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E09;

// E09 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E09Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E09Exercises : E09Checks
{
    // Exercise 1 — Detect a conflict
    // `product` is tracked by `db`. Set its ListPrice to `newPrice`, stamp ModifiedDate with
    // DateTime.Now, and save. Return true if saved, false if someone else changed the product since
    // it was loaded (ModifiedDate is the concurrency token in E09Context).
    protected override Task<bool> Ex1(E09Context db, Product product, decimal newPrice)
        => throw new NotImplementedException();

    // Exercise 2 — Your change wins
    // Like exercise 1, but on a conflict your price must win: overwrite whatever the other writer saved.
    // Rules: at most 4 round trips; nothing pending afterwards.
    protected override Task Ex2(E09Context db, Product product, decimal newPrice)
        => throw new NotImplementedException();

    // Exercise 3 — Rowversion from a form
    // A web form rendered category `categoryId` with its rowversion `version`. Rename the category to
    // `newName` only if nobody changed it since: return true if saved, false on a conflict.
    // Rules: one round trip (don't read the category first). The rowversion is the shadow property
    // "Version" (see E09Context).
    protected override Task<bool> Ex3(E09Context db, int categoryId, string newName, byte[] version)
        => throw new NotImplementedException();

    // Exercise 4 — Keep the first change
    // Set `first`'s ListPrice to `firstPrice` and `second`'s to `secondPrice` (both are tracked). The
    // database may reject the second change. If it does, the first must still be saved. Return whether
    // the second change was saved.
    // Rules: nothing may be left pending afterwards.
    protected override Task<bool> Ex4(E09Context db, Product first, decimal firstPrice, Product second, decimal secondPrice)
        => throw new NotImplementedException();

    // Exercise 5 — Give up on a lock
    // From `session` (a second, independent session), set product `productId`'s ListPrice to `newPrice`
    // with ExecuteUpdate. If the row is locked by another transaction and the lock timeout expires,
    // return false instead of throwing; otherwise return true.
    // Hint: lock timeouts are SQL Server error 1222. Look at the exception EF actually throws.
    protected override Task<bool> Ex5(E09Context session, int productId, decimal newPrice)
        => throw new NotImplementedException();

    // Challenge 1 — No lost update
    // Increase product `productId`'s SafetyStockLevel by `delta` and stamp ModifiedDate. Another user may
    // change the same product between your read and your save. Retry on conflicts (up to 3 attempts)
    // so that neither change is lost.
    protected override Task Ch1(E09Context db, int productId, short delta)
        => throw new NotImplementedException();

    // Challenge 2 — Merge both changes
    // `product` is tracked and has your unsaved edits. Another user has saved changes to other
    // properties in the meantime. Save so that both survive: your value wherever you changed a
    // property, theirs everywhere else.
    // Rules: nothing pending afterwards.
    protected override Task Ch2(E09Context db, Product product)
        => throw new NotImplementedException();

    // Challenge 3 — Who is blocking?
    // Right now another session is blocked, waiting for a lock that `db`'s transaction holds. Return
    // the blocked request's blocking_session_id and wait_type, read from sys.dm_exec_requests.
    // Rules: one round trip.
    protected override Task<BlockingInfo> Ch3(E09Context db)
        => throw new NotImplementedException();
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E09Exercises`
Expected: `Failed: 0, Passed: 0, Skipped: 8, Total: 8`. The run takes about 4 s: challenge 3's check waits for the blocked command it started even though the stub throws.

- [ ] **Step 4: Write `Solutions.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E09;

// Worked solutions for E09, each followed by a Why note. Run: dotnet test --filter E09Solutions
public class E09Solutions : E09Checks
{
    // Exercise 1 — Detect a conflict
    // `product` is tracked by `db`. Set its ListPrice to `newPrice`, stamp ModifiedDate with
    // DateTime.Now, and save. Return true if saved, false if someone else changed the product since
    // it was loaded (ModifiedDate is the concurrency token in E09Context).
    protected override async Task<bool> Ex1(E09Context db, Product product, decimal newPrice)
    {
        product.ListPrice = newPrice;
        product.ModifiedDate = DateTime.Now;
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
    // Why: a concurrency token's original value goes into the UPDATE's WHERE clause. If another writer
    // changed it, the UPDATE matches 0 rows, and EF turns "expected 1 row, got 0" into
    // DbUpdateConcurrencyException. ModifiedDate only works as a token if every writer updates it, which
    // is why you stamp it too.

    // Exercise 2 — Your change wins
    // Like exercise 1, but on a conflict your price must win: overwrite whatever the other writer saved.
    // Rules: at most 4 round trips; nothing pending afterwards.
    protected override async Task Ex2(E09Context db, Product product, decimal newPrice)
    {
        product.ListPrice = newPrice;
        product.ModifiedDate = DateTime.Now;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException conflict)
        {
            foreach (var entry in conflict.Entries)
                entry.OriginalValues.SetValues((await entry.GetDatabaseValuesAsync())!);
            await db.SaveChangesAsync();
        }
    }
    // Why: "client wins" means: pretend you loaded the row after the other writer saved it. Copying the
    // database values into OriginalValues refreshes the token, so the retry's WHERE matches. Current
    // values stay yours, and properties that differ from the new originals are written. Store wins would
    // be entry.ReloadAsync() instead.

    // Exercise 3 — Rowversion from a form
    // A web form rendered category `categoryId` with its rowversion `version`. Rename the category to
    // `newName` only if nobody changed it since: return true if saved, false on a conflict.
    // Rules: one round trip (don't read the category first). The rowversion is the shadow property
    // "Version" (see E09Context).
    protected override async Task<bool> Ex3(E09Context db, int categoryId, string newName, byte[] version)
    {
        var category = new ProductCategory { ProductCategoryId = categoryId };
        db.ProductCategories.Attach(category);
        db.Entry(category).Property("Version").OriginalValue = version;
        category.Name = newName;
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
    // Why: with a rowversion the database changes the token on every write, so no writer can forget it.
    // In a disconnected flow the version travels with the form, and you hand it back to EF as the
    // property's original value: that's what goes into WHERE [Version] = @p. (For a rowversion, which EF
    // never writes, setting the current value on the Unchanged stub happens to end up in the WHERE too.
    // Don't rely on that for a token EF does write, such as ModifiedDate: there the current value is the
    // new value to SET, and the WHERE still compares the original.)

    // Exercise 4 — Keep the first change
    // Set `first`'s ListPrice to `firstPrice` and `second`'s to `secondPrice` (both are tracked). The
    // database may reject the second change. If it does, the first must still be saved. Return whether
    // the second change was saved.
    // Rules: nothing may be left pending afterwards.
    protected override async Task<bool> Ex4(E09Context db, Product first, decimal firstPrice, Product second, decimal secondPrice)
    {
        first.ListPrice = firstPrice;
        await db.SaveChangesAsync();

        second.ListPrice = secondPrice;
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            await db.Entry(second).ReloadAsync(); // undo the rejected change locally
            return false;
        }
    }
    // Why: when a transaction is already open, SaveChanges sets a savepoint first and rolls back to it if
    // any statement fails, so a failed save never leaves half its batch behind. That works against you
    // if both changes go into one SaveChanges: the second's failure undoes the first. Two saves, two
    // savepoints. The failed entity stays Modified in memory, so reload it, or the next save retries it.

    // Exercise 5 — Give up on a lock
    // From `session` (a second, independent session), set product `productId`'s ListPrice to `newPrice`
    // with ExecuteUpdate. If the row is locked by another transaction and the lock timeout expires,
    // return false instead of throwing; otherwise return true.
    // Hint: lock timeouts are SQL Server error 1222. Look at the exception EF actually throws.
    protected override async Task<bool> Ex5(E09Context session, int productId, decimal newPrice)
    {
        try
        {
            await session.Products.Where(p => p.ProductId == productId)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, newPrice));
            return true;
        }
        catch (Exception ex) when ((ex as SqlException ?? ex.InnerException as SqlException)?.Number == 1222)
        {
            return false;
        }
    }
    // Why: the session waits for the lock (here LockTimeoutMs, SET LOCK_TIMEOUT; by default forever),
    // then SQL Server cancels the statement with error 1222. EF classifies 1222 as transient and wraps it
    // in an InvalidOperationException that suggests EnableRetryOnFailure, so `catch (SqlException)` alone
    // misses it. Filter on the number, wherever the SqlException sits.

    // Challenge 1 — No lost update
    // Increase product `productId`'s SafetyStockLevel by `delta` and stamp ModifiedDate. Another user may
    // change the same product between your read and your save. Retry on conflicts (up to 3 attempts)
    // so that neither change is lost.
    protected override async Task Ch1(E09Context db, int productId, short delta)
    {
        var product = await db.Products.SingleAsync(p => p.ProductId == productId);
        for (var attempt = 1; ; attempt++)
        {
            product.SafetyStockLevel += delta;
            product.ModifiedDate = DateTime.Now;
            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3)
            {
                await db.Entry(product).ReloadAsync(); // their value, then add mine again
            }
        }
    }
    // Why: an increment is a read-modify-write. On a conflict, "client wins" would write your stale
    // total and erase their increment: the classic lost update. Reload, re-apply the change to fresh
    // data, and save again. (For a pure increment, ExecuteUpdate with SafetyStockLevel + delta avoids
    // the race entirely, because the database does the read and the write in one statement.)

    // Challenge 2 — Merge both changes
    // `product` is tracked and has your unsaved edits. Another user has saved changes to other
    // properties in the meantime. Save so that both survive: your value wherever you changed a
    // property, theirs everywhere else.
    // Rules: nothing pending afterwards.
    protected override async Task Ch2(E09Context db, Product product)
    {
        product.ModifiedDate = DateTime.Now;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException conflict)
        {
            foreach (var entry in conflict.Entries)
            {
                var databaseValues = (await entry.GetDatabaseValuesAsync())!;
                foreach (var property in entry.Properties)
                {
                    if (!property.IsModified)
                        property.CurrentValue = databaseValues[property.Metadata];
                    property.OriginalValue = databaseValues[property.Metadata];
                }
            }
            await db.SaveChangesAsync();
        }
    }
    // Why: the conflict exception gives you three versions of each row: original (what you loaded),
    // current (what you want) and the database's (what they saved). A field-level merge keeps your value
    // for the properties you modified and takes theirs for the rest, then refreshes all originals so
    // the token check passes. Client wins would have written your stale ListPrice over theirs.

    // Challenge 3 — Who is blocking?
    // Right now another session is blocked, waiting for a lock that `db`'s transaction holds. Return
    // the blocked request's blocking_session_id and wait_type, read from sys.dm_exec_requests.
    // Rules: one round trip.
    protected override Task<BlockingInfo> Ch3(E09Context db)
        => db.Database.SqlQuery<BlockingInfo>($"""
            SELECT CAST(blocking_session_id AS int) AS BlockingSessionId, wait_type AS WaitType
            FROM sys.dm_exec_requests
            WHERE blocking_session_id = @@SPID
            """).SingleAsync();
    // Why: sys.dm_exec_requests lists the requests running right now. A blocked one shows who blocks it
    // (blocking_session_id) and what it waits for (LCK_M_X / LCK_M_U: an exclusive or update lock). Under
    // READ_COMMITTED_SNAPSHOT, which AdventureWorks has on, readers don't wait for writers, so blocking
    // like this is between writers (T-SQL lesson 17).
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E09Solutions`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8` (about 7 s: two deliberate lock timeouts).

- [ ] **Step 5: Write `Examples.cs`**

```csharp
using System.Data;
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E09;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E09Examples --logger "console;verbosity=detailed"
// Db is an E09Context: Product.ModifiedDate is a concurrency token.
public class E09Examples : LabTest<E09Context>
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: a concurrency token goes into the WHERE clause; 0 rows updated means a conflict.
    [Fact]
    public async Task ModifiedDate_as_a_concurrency_token()
    {
        var mine = await Db.Products.SingleAsync(p => p.ProductId == 1);
        var theirContext = NewContext();
        var theirs = await theirContext.Products.SingleAsync(p => p.ProductId == 1);
        theirs.ListPrice = 50m;
        theirs.ModifiedDate = DateTime.Now;
        await theirContext.SaveChangesAsync();

        mine.ListPrice = 60m;
        mine.ModifiedDate = DateTime.Now;
        var conflict = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Db.SaveChangesAsync());

        Print(conflict.Message);
        Assert.Contains("[ModifiedDate] = @", Sql.Commands[^1]); // WHERE [ProductID] = @p AND [ModifiedDate] = @p
        Assert.Same(mine, conflict.Entries.Single().Entity);
    }

    // Concept 2: a rowversion is compared on UPDATE and refreshed from OUTPUT INSERTED.
    [Fact]
    public async Task Rowversion_is_checked_and_refreshed()
    {
        await E09Context.AddVersionColumnAsync(Db);
        var category = await Db.ProductCategories.SingleAsync(c => c.ProductCategoryId == 1);
        var before = (byte[])Db.Entry(category).Property("Version").CurrentValue!;

        category.Name = "Bicycles";
        await Db.SaveChangesAsync();

        var after = (byte[])Db.Entry(category).Property("Version").CurrentValue!;
        Assert.Contains("[Version] = @", Sql.Commands[^1]);
        Assert.Contains("OUTPUT INSERTED.[Version]", Sql.Commands[^1]);
        Assert.NotEqual(before, after); // the database changed it; EF read it back
    }

    // Concept 3: resolving a conflict — store wins (reload) or client wins (refresh the originals).
    [Fact]
    public async Task Store_wins_or_client_wins()
    {
        var storeWins = await Db.Products.SingleAsync(p => p.ProductId == 1);
        var other = NewContext();
        var clientWins = await other.Products.SingleAsync(p => p.ProductId == 1);
        await Db.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set
            .SetProperty(p => p.ListPrice, 50m)
            .SetProperty(p => p.ModifiedDate, p => p.ModifiedDate.AddSeconds(1)));

        storeWins.ListPrice = 60m;
        var conflict = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Db.SaveChangesAsync());
        await conflict.Entries.Single().ReloadAsync();                    // theirs: 50, my edit discarded
        Assert.Equal(50m, storeWins.ListPrice);

        clientWins.ListPrice = 70m;
        conflict = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
        var entry = conflict.Entries.Single();
        entry.OriginalValues.SetValues((await entry.GetDatabaseValuesAsync())!); // pretend I loaded theirs
        await other.SaveChangesAsync();                                           // mine: 70
        Assert.Equal(70m, (await Reference.Rows<decimal>($"SELECT ListPrice AS Value FROM Production.Product WHERE ProductID = 1")).Single());
    }

    // Concept 4: inside an open transaction SaveChanges uses a savepoint: a failed batch leaves nothing behind.
    [Fact]
    public async Task A_failed_SaveChanges_rolls_back_its_whole_batch()
    {
        var first = await Db.Products.SingleAsync(p => p.ProductId == 1);
        var second = await Db.Products.SingleAsync(p => p.ProductId == 2);
        first.ListPrice = 10m;  // fine on its own
        second.ListPrice = -1m; // CK_Product_ListPrice: ListPrice >= 0

        await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());

        var prices = await Reference.Rows<decimal>($"SELECT ListPrice AS Value FROM Production.Product WHERE ProductID IN (1, 2) ORDER BY ProductID");
        Assert.Equal([0m, 0m], prices);                       // the first UPDATE was undone too
        Assert.Equal(504, await Db.Products.CountAsync());    // and the transaction is still usable
    }

    // Concept 4: savepoints you create yourself on the current transaction.
    [Fact]
    public async Task Savepoints_you_create()
    {
        var transaction = Db.Database.CurrentTransaction!; // the test's transaction, as UseTransaction set it
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);

        await transaction.CreateSavepointAsync("BeforeRename");
        product.Name = "Renamed";
        await Db.SaveChangesAsync();
        await transaction.RollbackToSavepointAsync("BeforeRename");

        Assert.Equal("Adjustable Race", (await Reference.Rows<string>($"SELECT Name AS Value FROM Production.Product WHERE ProductID = 1")).Single());
        Assert.Equal("Renamed", product.Name); // the entity doesn't know: reload it or clear the tracker
    }

    // Concept 4: SQL Server has no nested transactions, and EF won't start a second one.
    [Fact]
    public async Task BeginTransaction_inside_a_transaction_throws()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Database.BeginTransactionAsync());

        Assert.Contains("already in a transaction", failure.Message);
    }

    // Concept 5: with READ_COMMITTED_SNAPSHOT, readers see the last committed version; READ UNCOMMITTED sees dirty data.
    [Fact]
    public async Task Readers_and_isolation_levels()
    {
        await Db.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.Name, "Uncommitted"));

        var readCommitted = await NewSessionAsync();
        var readUncommitted = await NewSessionAsync(IsolationLevel.ReadUncommitted);

        Assert.Equal("Adjustable Race", await NameOf1(readCommitted));  // no waiting: the row version store
        Assert.Equal("Uncommitted", await NameOf1(readUncommitted));    // a dirty read
    }

    // Concept 6: writers block writers until the lock timeout, then error 1222, wrapped by EF.
    [Fact]
    public async Task Writers_block_writers_until_the_lock_timeout()
    {
        await Db.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.Name, "Locked"));
        var session = await NewSessionAsync();

        var failure = await Assert.ThrowsAnyAsync<Exception>(() =>
            session.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.Name, "Mine")));

        Print(failure.Message);
        Assert.IsType<InvalidOperationException>(failure);                    // "likely due to a transient failure"
        Assert.Equal(1222, Assert.IsType<SqlException>(failure.InnerException).Number);
    }

    // Concept 7: a retrying execution strategy refuses to run inside a transaction it didn't start.
    [Fact]
    public async Task Retrying_strategy_rejects_user_transactions()
    {
        var retrying = NewContext<RetryingContext>(); // EnableRetryOnFailure, on the test's transaction

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => retrying.Products.CountAsync());

        Print(failure.Message);
        Assert.Contains("does not support user-initiated transactions", failure.Message);
    }

    static Task<string> NameOf1(E09Context session)
        => session.Products.Where(p => p.ProductId == 1).Select(p => p.Name).SingleAsync();
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E09Examples`
Expected: `Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9`.

- [ ] **Step 6: Write `README.md`**

````markdown
# E09 — Concurrency & Transactions

## What you'll learn

- Optimistic concurrency: concurrency tokens, the legacy `ModifiedDate` style vs `rowversion`, and `DbUpdateConcurrencyException`
- Resolving conflicts: store wins, client wins, and a field-by-field merge; avoiding lost updates
- Transactions in EF: `SaveChanges`' own transaction, its automatic savepoint inside yours, savepoints you create, and why there's no nesting
- Isolation levels in practice on a `READ_COMMITTED_SNAPSHOT` database: who waits for whom
- Blocking and lock timeouts, seen from EF and from `sys.dm_exec_requests`
- Why a retrying execution strategy and your own transactions don't mix

## Builds on

- T-SQL [lesson 11](../../../lessons/11-transactions-errors-concurrency/lesson.ipynb) — transactions, savepoints, isolation levels
- T-SQL [lesson 17](../../../lessons/17-locking-blocking-waits/lesson.ipynb) — locks, blocking, waits, `sys.dm_exec_requests`
- EF lessons [E07](../E07-change-tracking/README.md) (original vs current values) and [E08](../E08-savechanges-and-set-based-writes/README.md) (what `SaveChanges` sends)

## Setup

```
dotnet test --filter E09Examples --logger "console;verbosity=detailed"
```

`E09Context.cs` makes `Product.ModifiedDate` a concurrency token and maps a `rowversion` column, `Version`, on `ProductCategory` as a shadow property. AdventureWorks has no rowversion columns, so `E09Context.AddVersionColumnAsync(db)` adds one with `ALTER TABLE` **inside the test's transaction**. DDL is transactional in SQL Server, so the rollback removes it again. `RetryingContext` turns on `EnableRetryOnFailure`.

Some examples and checks open a second, independent session with `NewSessionAsync(isolationLevel)` (its own connection and transaction, also rolled back). Every lab connection runs with `SET LOCK_TIMEOUT 3000`, so a blocked statement fails after 3 seconds instead of waiting forever. Expect a few deliberate 3-second waits in this lesson.

## Concepts

### 1. Optimistic concurrency and tokens

EF assumes conflicts are rare and checks for them only when it writes: a property marked `IsConcurrencyToken()` (or `[ConcurrencyCheck]`) puts its **original** value into the UPDATE's or DELETE's WHERE clause. If someone changed the row since you read it, the statement matches 0 rows, and EF throws `DbUpdateConcurrencyException`. Nothing is locked while the user edits.

| Token | Who changes it | Risk |
|---|---|---|
| a normal column such as `ModifiedDate` (`IsConcurrencyToken()`) | your code, on every write | a writer that forgets to change it (a script, an `ExecuteUpdate`, another app) overwrites silently |
| `rowversion` (`IsRowVersion()`, a `byte[]`) | the database, on every write, automatically | none of the above. EF reads the new value back with `OUTPUT INSERTED.[Version]` |

In a web app the token has to survive the round trip to the browser. Render the version with the form, and on post set it as the property's **`OriginalValue`** before saving.

Examples: `ModifiedDate_as_a_concurrency_token`, `Rowversion_is_checked_and_refreshed`.

### 2. Resolving a conflict

`DbUpdateConcurrencyException.Entries` gives you the conflicting entries, and each has three sets of values: `OriginalValues` (what you read), `CurrentValues` (what you want) and `await entry.GetDatabaseValuesAsync()` (what's there now).

| Strategy | Code | Result |
|---|---|---|
| Store wins | `await entry.ReloadAsync()` | your edit is discarded |
| Client wins | `entry.OriginalValues.SetValues(databaseValues)`, save again | their edit is overwritten |
| Merge | per property: keep yours if `IsModified`, else take theirs; refresh all originals; save again | both survive |

For read-modify-write updates (stock levels, counters), "client wins" is a **lost update**: it writes a total computed from stale data. Reload, re-apply your change, and retry. Or let the database do the arithmetic in one statement (`ExecuteUpdate(... x.Stock + delta)`).

Example: `Store_wins_or_client_wins`.

### 3. Transactions and savepoints

- `SaveChanges` always runs in a transaction. With none open, it starts and commits its own, so a batch is all-or-nothing.
- With one already open (`Database.BeginTransaction()`, or `UseTransaction(...)` as this lab does), `SaveChanges` sets a **savepoint** first and rolls back to it if anything fails. The whole batch is undone, including statements that had succeeded, and the outer transaction stays usable. The failed entities are still Added or Modified in the tracker until you reload, detach or fix them.
- `Database.CurrentTransaction.CreateSavepointAsync(name)` / `RollbackToSavepointAsync(name)` set your own savepoints. Rolling back changes the database, not the tracked entities.
- There is no nesting. `BeginTransaction` while a transaction is open throws "The connection is already in a transaction". SQL Server's nested `BEGIN TRAN` only increments a counter, so EF doesn't pretend.

Examples: `A_failed_SaveChanges_rolls_back_its_whole_batch`, `Savepoints_you_create`, `BeginTransaction_inside_a_transaction_throws`.

### 4. Isolation levels on this database

AdventureWorks here has `READ_COMMITTED_SNAPSHOT` ON (and `ALLOW_SNAPSHOT_ISOLATION` OFF). Under the default `READ COMMITTED`, a reader therefore doesn't wait for a writer: it gets the last committed version of the row from the version store.

| Session isolation | Reading a row another transaction has changed but not committed |
|---|---|
| `READ COMMITTED` (with RCSI) | returns the last committed value, no waiting |
| `READ UNCOMMITTED` | returns the uncommitted value: a dirty read |
| `REPEATABLE READ` / `SERIALIZABLE` | waits for the lock (here: until the 3 s timeout) |

Pass the level to `BeginTransaction(IsolationLevel...)`, or here `NewSessionAsync(IsolationLevel...)`.

Example: `Readers_and_isolation_levels`.

### 5. Blocking and lock timeouts

Writers still block writers. An UPDATE of a row another transaction has updated waits for its exclusive lock. With `LOCK_TIMEOUT` set, SQL Server gives up with **error 1222** ("Lock request time out period exceeded"). EF classifies 1222 as transient and throws an `InvalidOperationException` ("likely due to a transient failure") with the `SqlException` as `InnerException`, so `catch (SqlException)` misses it. While it waits, the blocked request is visible in `sys.dm_exec_requests` with `blocking_session_id` and a `LCK_M_*` `wait_type`.

Example: `Writers_block_writers_until_the_lock_timeout`.

### 6. Execution strategies vs your transactions

`UseSqlServer(..., o => o.EnableRetryOnFailure())` retries operations that fail with transient errors, but it can only retry what it controls. Inside a transaction you opened, it can't replay the earlier statements, so it refuses: "The configured execution strategy ... does not support user-initiated transactions". The fix is to run the whole unit through the strategy: `db.Database.CreateExecutionStrategy().ExecuteAsync(async () => { using var tx = ...; ...; await tx.CommitAsync(); })`. (This lab can't show that fix: every test already runs inside the transaction that guarantees the rollback.)

Example: `Retrying_strategy_rejects_user_transactions`.

## Pitfalls

- **A `ModifiedDate` token that some writers don't update.** Their changes go unnoticed. Prefer `rowversion` where you can add one.
- **Setting a disconnected token as the current value.** For a token EF writes, the WHERE still compares the original. Set `OriginalValue`.
- **"Client wins" for increments.** Lost updates. Reload and re-apply, or do the arithmetic in SQL.
- **Two changes in one `SaveChanges` when the first must survive.** A failure undoes the whole batch.
- **`catch (SqlException)` around EF calls.** EF often wraps it. Check `InnerException`.
- **Assuming readers block on this database.** With RCSI they don't, but writers do.

## Exercises

```
dotnet test --filter E09Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
````

- [ ] **Step 7: Prompts are identical in Exercises.cs and Solutions.cs**

```bash
cd efcore && python - lessons/E09-concurrency-and-transactions <<'EOF'
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
git add efcore/lessons/E09-concurrency-and-transactions
git commit -m "feat: add EF Core lesson E09 - concurrency and transactions"
```

- [ ] **Step 9: A failing exercise leaves nothing behind (Review Focus 2).** EF wraps the lock-timeout `SqlException` in an `InvalidOperationException`, so `catch (SqlException)` misses it. Temporarily replace the body of `Ex5` in `Exercises.cs` (the stub line under its signature) with:

```csharp
    {
        try
        {
            await session.Products.Where(p => p.ProductId == productId).ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, newPrice));
            return true;
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 1222) { return false; }
    }
```

and add `async` to its signature (`protected override async Task<bool> Ex5(...)`). Run: `cd efcore && dotnet build && dotnet test --no-build --filter "FullyQualifiedName~E09Exercises.Ex5"`
Expected: `Failed: 1` with `System.InvalidOperationException : An exception has been raised that is likely due to a transient failure.`

Then, still with the wrong answer in place, run the whole suite once: `cd efcore && dotnet test --no-build`
Expected: exactly `Failed: 1` (this exercise) — no lock timeouts anywhere else, proving the failed test released everything. Then restore and confirm a clean tree:

```bash
git checkout -- efcore/lessons/E09-concurrency-and-transactions/Exercises.cs
git status --short
```

Expected: `git status --short` prints nothing.

---

## Task 5: Docs and final verification

**Files:**
- Modify: `efcore/README.md` (lesson table; "How the database is used"), `README.md` (EF Core Track paragraph), `CLAUDE.md` (EF Core track section), `docs/superpowers/specs/2026-10-10-efcore-track-design.md` (Lab Harness; Risks)

- [ ] **Step 1: `efcore/README.md`.** In the lesson table change the three rows

```markdown
| E07 | Change tracking internals | planned |
| E08 | SaveChanges & set-based writes | planned |
| E09 | Concurrency & transactions | planned |
```

to

```markdown
| E07 | Change tracking internals | available |
| E08 | SaveChanges & set-based writes | available |
| E09 | Concurrency & transactions | available |
```

Then, under `## How the database is used`, directly after the bullet that starts `- **AdventureWorks2022 is never modified.**`, add:

```markdown
- **Some lessons open a second session** (E09: blocking, isolation levels) with its own connection and transaction, also rolled back. Every lab connection waits at most 3 seconds for a lock (`SET LOCK_TIMEOUT 3000`), so a test blocked by its own second session fails instead of hanging. E09's examples and solutions wait out a few of these timeouts on purpose.
```

- [ ] **Step 2: Root `README.md`.** In the `## EF Core Track` paragraph, replace `Lessons E01–E06 are available;` with `Lessons E01–E09 are available;`.

- [ ] **Step 3: `CLAUDE.md`.** In the `## EF Core track` section, directly after the bullet that starts ``- `Lab/QueryPlan.cs`:``, add:

```markdown
- `LabTest` also offers `NewContext<T>(params IInterceptor[])` (another context type, or extra interceptors, on the test's transaction) and `NewSessionAsync(isolationLevel)` (a second connection with its own rolled-back transaction, not counted in `Sql`). Every lab connection runs `SET LOCK_TIMEOUT 3000`. A check that starts a command it doesn't await (to create blocking) must await it in a `finally`: a session can't be closed while one of its commands runs. Schema a lesson needs (E09's rowversion column) is added with DDL inside the test's transaction, never with a script.
```

- [ ] **Step 4: Spec.** In `docs/superpowers/specs/2026-10-10-efcore-track-design.md`:
  - Under `## Lab Harness`, directly after the `QueryPlan.OperatorsAsync(db, query)` bullet and its continuation line, add:

```markdown
- `NewContext<T>(params IInterceptor[])` and `NewSessionAsync(isolationLevel)` (added with Tier C) — a
  context of another type or with extra interceptors on the test's transaction; and a second,
  independent session with its own rolled-back transaction. Every lab connection runs
  `SET LOCK_TIMEOUT 3000`, and disposal attempts every step even if one fails, so a failing test
  can't leave a transaction (and its locks) open.
```

  - Under `Still open:`, replace the `**Run time:**` bullet (both of its lines) with:

```markdown
- **Run time:** serial execution plus per-test database recreation in Tier D; acceptable for
  now (after Tier C, 241 tests run in ~20 s, most of it E09's deliberate lock timeouts),
  revisit if a full run gets slow.
```

- [ ] **Step 5: Build and run everything, unfiltered (Review Focus 5)**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build`
Expected: `0 Warning(s)`, then `Passed!  - Failed: 0, Passed: 169, Skipped: 72, Total: 241`.

- [ ] **Step 6: Run the curriculum self-check**

Run: `cd efcore && dotnet test --no-build --filter "FullyQualifiedName!~Exercises"`
Expected: `Passed!  - Failed: 0, Passed: 169, Skipped: 0, Total: 169`.

- [ ] **Step 7: No newline-dependent assertions**

Run: `cd efcore && grep -nF -e 'NewLine' -e '\r\n' lessons/*/Checks.cs lessons/*/Examples.cs`
Expected: exactly one hit, `lessons/E01-setup-and-generated-sql/Examples.cs:59` (output only, from phase 1).

- [ ] **Step 8: Check the EF Core 10-specific claims (spec DoD item 5).** Behavioral claims in the E07–E09 READMEs are asserted by examples or checks, so Steps 5–6 verified them against EF Core 10.0.12. These have no assertion; confirm each against Microsoft Learn ("Change Tracking in EF Core", "Change Detection and Notifications", "Explicitly Tracking Entities", "Efficient Updating", "ExecuteUpdate and ExecuteDelete", "Cascade Delete", "Interceptors", "Handling Concurrency Conflicts", "Transactions", "Connection Resiliency"):
  - E07: `Update` marks an entity with an unset generated key as Added
  - E08: SQL Server's default maximum batch size is 42 statements
  - E08: `ExecuteUpdate`/`ExecuteDelete` bypass the change tracker and SaveChanges interceptors
  - E08: the scaffolder maps `NO ACTION` foreign keys to `DeleteBehavior.ClientSetNull`
  - E09: `SaveChanges` creates a savepoint when a transaction is already in progress, and rolls back to it on failure
  - E09: the fix for retrying strategies is `CreateExecutionStrategy().ExecuteAsync(...)` around the whole transaction

Expected: no corrections needed. If one is, fix the README sentence and re-run Step 5.

- [ ] **Step 9: AdventureWorks is unchanged (Review Focus 3).** Re-run Task 0, Step 5's query.
Expected: the same five values recorded in Task 0 (the last one, `version_column`, is `0`: E09's `rowversion` column was rolled back).

- [ ] **Step 10: Commit**

```bash
git add efcore/README.md README.md CLAUDE.md docs/superpowers/specs/2026-10-10-efcore-track-design.md
git commit -m "docs: mark EF Core lessons E07-E09 available"
```

---

## Not in this phase

Tier D (E10–E12, with `LabDatabase` and `EfLab_*` databases) and the capstone (E13) each get their own plan, per the spec's build-out phases.
