# EF Core Track — Phase 1 (Foundation + Tier A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `efcore/`: the .NET 10 / EF Core 10 xUnit project, its test harness, the scaffolded AdventureWorks model, and lessons E01–E04.

**Architecture:** One xUnit v3 project (`efcore/EfCoreLessons.csproj`). `Lab/` gives every test a connection and transaction that is rolled back, plus SQL capture and uncounted reference queries. Each lesson's abstract `E<NN>Checks` holds the assertions. Two classes inherit it: `E<NN>Exercises`, whose stubs report as skipped, and `E<NN>Solutions`, with the answers. Every file below was validated against the live `mssql-learn` container on 2026-10-10:
- the build has 0 warnings
- 80 tests pass and 32 skip
- every check was also run against a set of tempting-but-wrong answers and failed each one for the intended reason
- AdventureWorks checksums were identical before and after a full run

**Tech Stack:** .NET 10 SDK (10.0.x), EF Core 10.0.12 (SqlServer, Design), xUnit v3 4.0.2 (`xunit.v3.mtp-off`) on VSTest, SQL Server 2022 (Docker container `mssql-learn`), AdventureWorks2022.

**Spec:** `docs/superpowers/specs/2026-10-10-efcore-track-design.md` (phase 1 of its "Build-out Phases").

## Global Constraints

- Target `net10.0`. EF Core packages exactly `10.0.12`; `xunit.v3.mtp-off` `4.0.2`, `xunit.runner.visualstudio` `4.0.1`, `Microsoft.NET.Test.Sdk` `18.10.1`. No floating versions.
- `efcore/global.json` pins SDK `10.0.100` with `rollForward: latestFeature`, `allowPrerelease: false` (this machine's default SDK is an 11 preview).
- `efcore/dotnet-tools.json` pins `dotnet-ef` `10.0.12`; the global 9.0.7 tool is left alone.
- AdventureWorks2022 is never modified: every test runs on a connection + transaction that is rolled back. The EF track has no `setup.sql`.
- No credentials committed anywhere, including `Model/` (scaffold with `--no-onconfiguring`).
- `efcore/Model/` is scaffolder output, never hand-edited; lesson-specific model changes go in a derived context in the lesson folder.
- Tests run serially (`parallelizeTestCollections: false`).
- Lesson folder `efcore/lessons/E<NN>-<slug>/` = `README.md`, `Examples.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs` (+ `E<NN>Context.cs` if the model changes). Namespace `EfCoreLessons.E<NN>`; classes `E<NN>Examples`, `E<NN>Checks`, `E<NN>Exercises`, `E<NN>Solutions`.
- Each lesson: 5 exercises (`Ex1`–`Ex5`) + 3 challenges (`Ch1`–`Ch3`); identical prompts in `Exercises.cs` and `Solutions.cs`; a `// Why:` note after each solution.
- `dotnet build` produces 0 warnings.
- Commits: conventional prefixes (`feat:`, `docs:`), no attribution lines (disabled in the user's settings).

## Review Focus

1. **Container stopped** → every test fails within seconds with the "Is the container running?" hint, not one 5-second timeout per test. Pinned by Task 2, Step 6.
2. **No password available** (no `SA_PASSWORD`, no `docker/.env`; e.g. a fresh worktree outside the repo) → the nearest `docker/.env` is used, and a clear "SA password not found" error appears when none exists. Pinned by Task 2's `Password_*` self-tests.
3. **Learner runs plain `dotnet test`** (no filter) → zero failures; unattempted exercises report as skipped, not as a wall of red. Pinned by Task 7, Step 5.
4. **macOS/Linux line endings** → no check or example asserts on `\r\n`/`Environment.NewLine`. Pinned by Task 7, Step 7.
5. **Scaffolding a database that contains the T-SQL lessons' `lessonNN` schemas** → the model excludes them and contains no credentials. Pinned by Task 1, Step 6.

## Conventions used by every task

- Shell: Git Bash on Windows, bash/zsh elsewhere. Paths are relative to the repository (or worktree) root unless a step says `cd efcore`.
- Commands that need the password export it first. Adjust the path to your main checkout; `docker/.env` is gitignored, so a fresh worktree has no copy until Task 2, Step 7:

  ```bash
  export SA_PASSWORD=$(grep '^SA_PASSWORD=' /d/Study/Database/mssql/docker/.env | cut -d= -f2- | tr -d '\r')
  ```

- On Git Bash, prefix `docker exec ... /opt/mssql-tools18/...` with `MSYS_NO_PATHCONV=1`, otherwise the container path is rewritten into a Windows path.

## File Map

| Action | Path |
|---|---|
| Create | `efcore/global.json`, `efcore/EfCoreLessons.csproj`, `efcore/xunit.runner.json`, `efcore/dotnet-tools.json` |
| Generate | `efcore/Model/*.cs` (90 files, `dotnet ef dbcontext scaffold`) |
| Create | `efcore/Lab/LabConnection.cs`, `SqlCapture.cs`, `LabTest.cs`, `ExerciseAttribute.cs`, `LabSelfTests.cs` |
| Create | `efcore/lessons/E01-setup-and-generated-sql/` (5 files) |
| Create | `efcore/lessons/E02-projections-joins-nulls/` (5 files) |
| Create | `efcore/lessons/E03-grouping-subqueries-limits/` (5 files) |
| Create | `efcore/lessons/E04-raw-sql-views-functions-procs/` (6 files, incl. `E04Context.cs`) |
| Create | `efcore/README.md` |
| Modify | `.gitignore`, `README.md`, `CLAUDE.md` |

---

## Task 0: Isolate the work

- [ ] **Step 1: Create an isolated workspace** using the `superpowers:using-git-worktrees` skill, on a new branch `efcore-phase1` based on `master`. Without a worktree: `git switch -c efcore-phase1`.

- [ ] **Step 2: Verify you are not on master**

Run: `git branch --show-current`
Expected: `efcore-phase1`

- [ ] **Step 3: Confirm the container is up**

Run: `docker ps --filter name=mssql-learn --format '{{.Status}}'`
Expected: starts with `Up`. If not, run `docker compose -f docker/docker-compose.yml up -d` from the main checkout (the compose project and its volume live there).

- [ ] **Step 4: Confirm the .NET 10 SDK is installed**

Run: `dotnet --list-sdks`
Expected: a `10.0.` line (e.g. `10.0.401`).

- [ ] **Step 5: Record the AdventureWorks baseline** (compared again in Task 7)

```bash
export SA_PASSWORD=$(grep '^SA_PASSWORD=' /d/Study/Database/mssql/docker/.env | cut -d= -f2- | tr -d '\r')
MSYS_NO_PATHCONV=1 docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -W -s'|' -d AdventureWorks2022 -Q "SET NOCOUNT ON; SELECT (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Production.Product) AS product, (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM HumanResources.Employee) AS employee, (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Sales.SalesOrderHeader) AS soh;"
```

Expected: three integers. Note them down. (On 2026-10-10 they were `-1058563079|259050056|-1058661271`.)

---

## Task 1: Project skeleton, tooling pins, scaffolded model

**Files:**
- Create: `efcore/global.json`, `efcore/EfCoreLessons.csproj`, `efcore/xunit.runner.json`
- Create (via CLI): `efcore/dotnet-tools.json`
- Generate: `efcore/Model/*.cs`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: the running `mssql-learn` container with AdventureWorks2022.
- Produces: namespace `EfCoreLessons.Model`; `public partial class AdventureWorksContext : DbContext` with constructor `AdventureWorksContext(DbContextOptions<AdventureWorksContext> options)` and an overridable `protected override void OnModelCreating(ModelBuilder)`. Later tasks use these DbSets: `Products`, `People`, `Employees`, `Customers`, `Stores`, `Vendors`, `SalesOrderHeaders`, `SalesOrderDetails`, `PurchaseOrderDetails`, `SalesTerritories`, `SalesPeople`, `ProductSubcategories`, `ProductReviews`, `VSalesPeople`. Property names are PascalCase with `Id` (`SalesOrderId`, `BusinessEntityId`); `date` columns are `DateOnly`.

- [ ] **Step 1: Write `efcore/global.json`**

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

Run: `cd efcore && dotnet --version`
Expected: `10.0.` followed by a feature band (e.g. `10.0.401`), not `11.0.…`.

- [ ] **Step 2: Write `efcore/EfCoreLessons.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <!-- Lessons favor readable queries over cancellation-token plumbing. -->
    <NoWarn>$(NoWarn);xUnit1051</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.12" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
    <PackageReference Include="xunit.v3.mtp-off" Version="4.0.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="4.0.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Content Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Write `efcore/xunit.runner.json`**

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeTestCollections": false
}
```

- [ ] **Step 4: Pin `dotnet-ef` as a local tool**

Run: `cd efcore && dotnet new tool-manifest && dotnet tool install dotnet-ef --version 10.0.12`
Expected: `efcore/dotnet-tools.json` exists with exactly this content:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "10.0.12",
      "commands": [
        "dotnet-ef"
      ],
      "rollForward": false
    }
  }
}
```

- [ ] **Step 5: Ignore .NET build output.** Append to `.gitignore`:

```gitignore

# .NET build output (efcore/)
bin/
obj/
TestResults/
```

- [ ] **Step 6: Scaffold the model**

```bash
cd efcore
dotnet ef dbcontext scaffold \
  "Server=localhost,1433;Database=AdventureWorks2022;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=True" \
  Microsoft.EntityFrameworkCore.SqlServer \
  --output-dir Model --context AdventureWorksContext --no-onconfiguring --force \
  --schema Person --schema HumanResources --schema Production \
  --schema Purchasing --schema Sales --schema dbo
```

Expected: `Build succeeded.`, then warnings only about what the scaffolder can't map (E01 teaches them). These are:
- the `hierarchyid`/`geography` columns `HumanResources.Employee.OrganizationNode`, `Person.Address.SpatialLocation`, `Production.Document.DocumentNode` and `Production.ProductDocument.DocumentNode` ("Could not find type mapping ... Skipping column.")
- the two `IX_Employee_*` indexes and the two primary keys that use those columns
- `Unable to generate entity type for table 'Production.Document'` and `'Production.ProductDocument'`

Nothing else.

Verify the result (Review Focus 5):

```bash
cd efcore
ls Model | wc -l                                       # 90
grep -c 'tb.HasTrigger' Model/AdventureWorksContext.cs  # 10
grep -l 'lesson[0-9][0-9]' Model/*.cs                   # no output: T-SQL lesson schemas excluded
grep -l 'Password=' Model/*.cs                          # no output: no connection string committed
dotnet build -warnaserror                              # Build succeeded, 0 Warning(s)
```

- [ ] **Step 7: Commit**

```bash
git add .gitignore efcore
git commit -m "feat: add EF Core track project and scaffolded AdventureWorks model"
```

---

## Task 2: Lab harness

**Files:**
- Create: `efcore/Lab/LabSelfTests.cs` (first, as the failing test), `efcore/Lab/LabConnection.cs`, `efcore/Lab/SqlCapture.cs`, `efcore/Lab/LabTest.cs`, `efcore/Lab/ExerciseAttribute.cs`

**Interfaces:**
- Consumes: `AdventureWorksContext` (Task 1).
- Produces (used by every lesson):
  - `LabConnection.AdventureWorks` (const `"AdventureWorks2022"`), `LabConnection.OpenAsync(string database) : Task<SqlConnection>`, `LabConnection.For(string database) : string`, `internal LabConnection.FindSaPassword(string startDirectory) : string?`
  - `SqlCapture : DbCommandInterceptor` — `IReadOnlyList<string> Commands`, `int Count`, `string All`, `void Reset()`, `void AssertRoundTrips(int expected)`
  - `abstract class LabTest<TContext> : IAsyncLifetime where TContext : AdventureWorksContext` — `protected SqlCapture Sql`, `protected TContext Db`, `protected Reference Reference`, `protected TContext NewContext()`; `abstract class LabTest : LabTest<AdventureWorksContext>`. A derived lesson context must expose a public `(DbContextOptions<AdventureWorksContext>)` constructor.
  - `sealed class Reference` — `Task<List<T>> Rows<T>(FormattableString sql)`
  - `sealed class ExerciseAttribute : FactAttribute` — reports `NotImplementedException` as skipped

- [ ] **Step 1: Write the failing self-tests, `efcore/Lab/LabSelfTests.cs`**

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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

    sealed class ScratchLab : LabTest
    {
        public async Task RenameProduct1(string name)
        {
            var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
            product.Name = name;
            await Db.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `cd efcore && dotnet build 2>&1 | grep -E ' error ' | head -3`
Expected: the build fails with `error CS...` lines naming `LabTest` and `LabConnection` (they don't exist yet).

- [ ] **Step 3: Write `efcore/Lab/LabConnection.cs`**

```csharp
using Microsoft.Data.SqlClient;

namespace EfCoreLessons.Lab;

// Connection strings for the mssql-learn container. The sa password comes from the
// SA_PASSWORD environment variable, else from docker/.env (same source as scripts/).
public static class LabConnection
{
    public const string AdventureWorks = "AdventureWorks2022";

    static readonly Lazy<string> SaPassword = new(ReadSaPassword);
    static SqlException? unreachable;

    public static string For(string database) => new SqlConnectionStringBuilder
    {
        DataSource = "localhost,1433",
        InitialCatalog = database,
        UserID = "sa",
        Password = SaPassword.Value,
        TrustServerCertificate = true,
        ConnectTimeout = 5,
    }.ConnectionString;

    // Opens a connection. After the first failure every later call fails at once,
    // instead of each test waiting out its own connect timeout.
    public static async Task<SqlConnection> OpenAsync(string database)
    {
        if (unreachable != null) throw Unreachable(unreachable);

        var connection = new SqlConnection(For(database));
        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch (SqlException ex)
        {
            unreachable = ex;
            await connection.DisposeAsync();
            throw Unreachable(ex);
        }
    }

    // The SA_PASSWORD line of the nearest docker/.env at or above startDirectory, or null.
    internal static string? FindSaPassword(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir != null; dir = dir.Parent)
        {
            var envFile = Path.Combine(dir.FullName, "docker", ".env");
            if (!File.Exists(envFile)) continue;

            var line = File.ReadLines(envFile).FirstOrDefault(l => l.StartsWith("SA_PASSWORD="));
            if (line != null) return line["SA_PASSWORD=".Length..].Trim();
        }
        return null;
    }

    static string ReadSaPassword()
    {
        var fromEnv = Environment.GetEnvironmentVariable("SA_PASSWORD");
        if (!string.IsNullOrEmpty(fromEnv)) return fromEnv;

        return FindSaPassword(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "SA password not found. Set SA_PASSWORD or create docker/.env from docker/.env.example.");
    }

    static InvalidOperationException Unreachable(SqlException ex) => new(
        $"Cannot connect to SQL Server on localhost,1433: {ex.Message} " +
        "Is the container running? Start it with: docker compose -f docker/docker-compose.yml up -d", ex);
}
```

- [ ] **Step 4: Write `efcore/Lab/SqlCapture.cs`, `efcore/Lab/LabTest.cs`, `efcore/Lab/ExerciseAttribute.cs`**

```csharp
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
```

```csharp
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
        var options = new DbContextOptionsBuilder<AdventureWorksContext>().UseSqlServer(connection!);
        if (capture) options.AddInterceptors(Sql);

        // Derived lesson contexts expose the same (DbContextOptions<AdventureWorksContext>) constructor.
        var db = (TContext)Activator.CreateInstance(typeof(TContext), options.Options)!;
        db.Database.UseTransaction(transaction);
        contexts.Add(db);
        return db;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var db in contexts) await db.DisposeAsync();
        if (transaction != null) await transaction.DisposeAsync(); // never committed: rolls back
        if (connection != null) await connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

public abstract class LabTest : LabTest<AdventureWorksContext>;

public sealed class Reference(AdventureWorksContext db)
{
    public Task<List<T>> Rows<T>(FormattableString sql) => db.Database.SqlQuery<T>(sql).ToListAsync();
}
```

```csharp
using System.Runtime.CompilerServices;

namespace EfCoreLessons.Lab;

// A check for one exercise. While the exercise still throws NotImplementedException
// the check is reported as skipped (not attempted) rather than failed.
public sealed class ExerciseAttribute : FactAttribute
{
    public ExerciseAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        SkipExceptions = [typeof(NotImplementedException)];
    }
}
```

- [ ] **Step 5: Run the self-tests to verify they pass**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter LabSelfTests`
Expected: the build succeeds; `Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6`.

- [ ] **Step 6: Verify the container-down experience (Review Focus 1).** This briefly stops the container and restarts it at the end:

```bash
cd efcore
docker stop mssql-learn
dotnet test --no-build --filter LabSelfTests 2>&1 | grep -E 'Cannot connect|Failed!'
docker start mssql-learn
until MSYS_NO_PATHCONV=1 docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -Q "SELECT 1" >/dev/null 2>&1; do sleep 2; done
```

Expected: 4 failures, each `System.InvalidOperationException : Cannot connect to SQL Server on localhost,1433: ... Is the container running? ...`; the 2 password tests still pass; the run's `Duration` is under ~10 s (one connect timeout, then fail-fast). The `until` loop returns once SQL Server answers again.

- [ ] **Step 7: Give the workspace its own `docker/.env` (worktree only) and verify the fallback**

```bash
cp /d/Study/Database/mssql/docker/.env docker/.env   # skip when working in the main checkout
unset SA_PASSWORD
cd efcore && dotnet test --no-build --filter LabSelfTests
```

Expected: `Passed: 6`. The password now comes from `docker/.env`. Re-export `SA_PASSWORD` (see Conventions) if a later command needs it.

- [ ] **Step 8: Commit**

```bash
git add efcore/Lab
git commit -m "feat: add EF Core lab harness"
```

---

## Task 3: Lesson E01 — Setup & seeing the SQL

**Files:**
- Create: `efcore/lessons/E01-setup-and-generated-sql/Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `LabTest`, `Sql`, `Reference.Rows<T>`, `[Exercise]` (Task 2); `AdventureWorksContext` DbSets `Employees`, `Products`, `SalesOrderHeaders` (Task 1).
- Produces: nothing later tasks depend on (records live in namespace `EfCoreLessons.E01`).

- [ ] **Step 1: Write the checks — the failing spec — `Checks.cs`**

```csharp
using System.Globalization;
using EfCoreLessons.Lab;
using EfCoreLessons.Model;

namespace EfCoreLessons.E01;

public record EmployeeTitle(int BusinessEntityId, string JobTitle);
public record ProductPrice(int ProductId, string Name, decimal ListPrice);
public record ProductLabel(string Name, string Price);
public record ProductRef(int ProductId, string Name);

// The checks for E01. E01Exercises (yours) and E01Solutions both inherit them.
public abstract class E01Checks : LabTest
{
    protected abstract Task<List<EmployeeTitle>> Ex1(AdventureWorksContext db);
    protected abstract Task<List<ProductPrice>> Ex2(AdventureWorksContext db, int page, int pageSize);
    protected abstract Task<bool> Ex3(AdventureWorksContext db, decimal minPrice);
    protected abstract string Ex4(AdventureWorksContext db, int customerId);
    protected abstract Task<List<ProductLabel>> Ex5(AdventureWorksContext db, decimal minPrice);
    protected abstract List<string> Ch1(AdventureWorksContext db);
    protected abstract Task<ProductRef?> Ch2(AdventureWorksContext db, string productNumberPrefix);
    protected abstract Task<int> Ch3(AdventureWorksContext db, byte status);

    [Exercise]
    public async Task Ex1_Managers()
    {
        var result = await Ex1(Db);

        Assert.Equal(await Reference.Rows<EmployeeTitle>($"""
            SELECT BusinessEntityID AS BusinessEntityId, JobTitle
            FROM HumanResources.Employee
            WHERE JobTitle LIKE '%Manager%'
            ORDER BY JobTitle, BusinessEntityID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.DoesNotContain("[BirthDate]", Sql.All); // only the columns you return
    }

    [Exercise]
    public async Task Ex2_DeterministicPaging()
    {
        var result = await Ex2(Db, page: 3, pageSize: 10);

        Assert.Equal(await Reference.Rows<ProductPrice>($"""
            SELECT ProductID AS ProductId, Name, ListPrice
            FROM Production.Product
            WHERE ListPrice > 0
            ORDER BY ListPrice DESC, ProductID
            OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("OFFSET", Sql.All);                                         // paged in SQL
        Assert.Contains("ORDER BY [p].[ListPrice] DESC, [p].[ProductID]", Sql.All); // tie-breaker
    }

    [Exercise]
    public async Task Ex3_AnyAbovePrice()
    {
        Assert.True(await Ex3(Db, 3000m));
        Assert.False(await Ex3(Db, 4000m));

        Sql.AssertRoundTrips(2);
        Assert.Contains("EXISTS", Sql.All);
        Assert.DoesNotContain("COUNT", Sql.All);
    }

    [Exercise]
    public void Ex4_SqlWithoutRunningIt()
    {
        var sql = Ex4(Db, 29825);

        Assert.Equal(0, Sql.Count);                 // ToQueryString never touches the database
        Assert.Contains("DECLARE @", sql);
        Assert.Contains("29825", sql);
        Assert.Contains("[CustomerID] = @", sql);   // a parameter, not an inlined literal
        Assert.Contains("TOP(", sql);
        Assert.Contains("ORDER BY [s].[OrderDate] DESC", sql);
    }

    [Exercise]
    public async Task Ex5_FormattedPrices()
    {
        var result = await Ex5(Db, 3000m);

        var us = CultureInfo.GetCultureInfo("en-US");
        var expected = (await Reference.Rows<ProductPrice>($"""
                SELECT ProductID AS ProductId, Name, ListPrice
                FROM Production.Product
                WHERE ListPrice > 3000
                ORDER BY ListPrice DESC, Name
                """))
            .Select(p => new ProductLabel(p.Name, p.ListPrice.ToString("C", us)))
            .ToList();
        Assert.Equal(expected, result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("WHERE", Sql.All);                 // filtered in SQL
        Assert.DoesNotContain("[ProductNumber]", Sql.All); // only the columns the labels need
    }

    [Exercise]
    public async Task Ch1_TablesWithTriggers()
    {
        var result = Ch1(Db);

        var expected = (await Reference.Rows<string>($"""
                SELECT DISTINCT OBJECT_SCHEMA_NAME(parent_id) + '.' + OBJECT_NAME(parent_id) AS Value
                FROM sys.triggers
                WHERE parent_class = 1
                  AND OBJECT_SCHEMA_NAME(parent_id) IN ('Person', 'HumanResources', 'Production', 'Purchasing', 'Sales', 'dbo')
                """))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(expected, result);
        Assert.Equal(0, Sql.Count); // answered from the model, not a query
    }

    [Exercise]
    public async Task Ch2_ExactlyOneByPrefix()
    {
        Assert.Equal(new ProductRef(782, "Mountain-200 Black, 38"), await Ch2(Db, "BK-M68B-38"));
        Assert.Null(await Ch2(Db, "BK_M68B-38")); // '_' is literal here, not a LIKE wildcard
        await Assert.ThrowsAsync<InvalidOperationException>(() => Ch2(Db, "BK-M68B")); // three matches

        Sql.AssertRoundTrips(3);
        Assert.All(Sql.Commands, sql => Assert.Contains("TOP(2)", sql));
    }

    [Exercise]
    public async Task Ch3_PlanPerStatus()
    {
        var counts = new List<int>();
        for (byte status = 1; status <= 6; status++)
            counts.Add(await Ch3(Db, status));

        Assert.Equal([0, 0, 0, 0, 31465, 0], counts);      // every AdventureWorks order is Status 5 (shipped)
        Assert.Equal(6, Sql.Commands.Distinct().Count());  // six SQL texts: one plan per value
        Assert.DoesNotContain("@", Sql.All);               // the status is inlined, not a parameter
    }
}
```

- [ ] **Step 2: Write the exercise stubs, `Exercises.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E01;

// E01 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E01Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E01Exercises : E01Checks
{
    // Exercise 1 — Managers
    // Return every employee whose JobTitle contains "Manager" as EmployeeTitle rows,
    // ordered by JobTitle, then BusinessEntityId.
    // Rules: one round trip; the SQL selects only the two columns you return.
    protected override Task<List<EmployeeTitle>> Ex1(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 2 — Deterministic paging
    // Return page `page` (1-based, `pageSize` rows per page) of the products with ListPrice > 0,
    // ordered by ListPrice descending. Many prices tie, so break ties by ProductId.
    // Rules: one round trip; the paging happens in SQL (OFFSET/FETCH), not in memory.
    protected override Task<List<ProductPrice>> Ex2(AdventureWorksContext db, int page, int pageSize)
        => throw new NotImplementedException();

    // Exercise 3 — Is there any?
    // Return whether any product has a ListPrice above `minPrice`.
    // Rules: one round trip per call; the SQL uses EXISTS, never COUNT.
    protected override Task<bool> Ex3(AdventureWorksContext db, decimal minPrice)
        => throw new NotImplementedException();

    // Exercise 4 — SQL without running it
    // Return the SQL EF would run for "the 10 most recent orders (SalesOrderId, OrderDate, TotalDue)
    // of customer `customerId`, newest first" — without executing the query.
    // Rules: zero round trips; customerId stays a parameter.
    protected override string Ex4(AdventureWorksContext db, int customerId)
        => throw new NotImplementedException();

    // Exercise 5 — Filter on the server, format on the client
    // Return the products with ListPrice > minPrice as ProductLabel(Name, Price), where Price is the
    // ListPrice formatted as US currency ("$3,578.27"), ordered by ListPrice descending, then Name.
    // Rules: one round trip; the WHERE runs in SQL; the SQL selects only the columns you need.
    // Hint: SQL Server can't apply a .NET "C" format string, but the final Select may run .NET code.
    protected override Task<List<ProductLabel>> Ex5(AdventureWorksContext db, decimal minPrice)
        => throw new NotImplementedException();

    // Challenge 1 — Model archaeology
    // Using only EF's metadata (db.Model), return "Schema.Table" for every entity type whose table
    // has triggers, sorted with StringComparer.Ordinal.
    // Rules: zero round trips. Hint: the scaffolder recorded each trigger with HasTrigger.
    protected override List<string> Ch1(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Challenge 2 — Exactly one
    // Return the product whose ProductNumber starts with `productNumberPrefix`: null when none does,
    // InvalidOperationException when more than one does. The prefix is literal text: "BK_M68B-38"
    // must not match "BK-M68B-38" ('_' is a LIKE wildcard).
    // Rules: one round trip per call, reading at most two rows.
    protected override Task<ProductRef?> Ch2(AdventureWorksContext db, string productNumberPrefix)
        => throw new NotImplementedException();

    // Challenge 3 — A plan per value
    // Count the orders with the given Status. The column is extremely skewed (all 31,465 orders have
    // Status 5), the classic parameter-sniffing setup from T-SQL lesson 13. Make EF inline the status
    // so each value gets its own SQL text, and therefore its own cached plan.
    // Rules: one round trip per call; no parameters in the SQL.
    protected override Task<int> Ch3(AdventureWorksContext db, byte status)
        => throw new NotImplementedException();
}
```

- [ ] **Step 3: Verify every exercise reports as not attempted**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E01Exercises`
Expected: the build succeeds; `Skipped! - Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

- [ ] **Step 4: Write the solutions, `Solutions.cs`**

```csharp
using System.Globalization;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E01;

// Worked solutions for E01, each followed by a Why note. Run: dotnet test --filter E01Solutions
public class E01Solutions : E01Checks
{
    // Exercise 1 — Managers
    // Return every employee whose JobTitle contains "Manager" as EmployeeTitle rows,
    // ordered by JobTitle, then BusinessEntityId.
    // Rules: one round trip; the SQL selects only the two columns you return.
    protected override Task<List<EmployeeTitle>> Ex1(AdventureWorksContext db)
        => db.Employees
            .Where(e => e.JobTitle.Contains("Manager"))
            .OrderBy(e => e.JobTitle).ThenBy(e => e.BusinessEntityId)
            .Select(e => new EmployeeTitle(e.BusinessEntityId, e.JobTitle))
            .ToListAsync();
    // Why: projecting into the record before ToListAsync makes EF select just those two columns, and a
    // projection is never tracked. Contains("Manager") on a literal becomes LIKE N'%Manager%'.
    // Tempting-but-wrong: ToListAsync() first, then Select — every Employee column crosses the wire and
    // every entity gets tracked, only for the projection to throw most of it away.

    // Exercise 2 — Deterministic paging
    // Return page `page` (1-based, `pageSize` rows per page) of the products with ListPrice > 0,
    // ordered by ListPrice descending. Many prices tie, so break ties by ProductId.
    // Rules: one round trip; the paging happens in SQL (OFFSET/FETCH), not in memory.
    protected override Task<List<ProductPrice>> Ex2(AdventureWorksContext db, int page, int pageSize)
        => db.Products
            .Where(p => p.ListPrice > 0)
            .OrderByDescending(p => p.ListPrice).ThenBy(p => p.ProductId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ProductPrice(p.ProductId, p.Name, p.ListPrice))
            .ToListAsync();
    // Why: Skip/Take after an OrderBy become OFFSET/FETCH, and EF sends both numbers as parameters, so
    // every page reuses one plan. ThenBy(ProductId) makes the order total. Without it, rows with the same
    // price may swap between pages after a re-plan, with no error (T-SQL lesson 02, challenge 1).

    // Exercise 3 — Is there any?
    // Return whether any product has a ListPrice above `minPrice`.
    // Rules: one round trip per call; the SQL uses EXISTS, never COUNT.
    protected override Task<bool> Ex3(AdventureWorksContext db, decimal minPrice)
        => db.Products.AnyAsync(p => p.ListPrice > minPrice);
    // Why: Any becomes SELECT CASE WHEN EXISTS (...), and SQL Server stops at the first matching row.
    // Tempting-but-wrong: CountAsync(...) > 0 counts every matching row just to compare the total with zero.

    // Exercise 4 — SQL without running it
    // Return the SQL EF would run for "the 10 most recent orders (SalesOrderId, OrderDate, TotalDue)
    // of customer `customerId`, newest first" — without executing the query.
    // Rules: zero round trips; customerId stays a parameter.
    protected override string Ex4(AdventureWorksContext db, int customerId)
        => db.SalesOrderHeaders
            .Where(h => h.CustomerId == customerId)
            .OrderByDescending(h => h.OrderDate)
            .Take(10)
            .Select(h => new { h.SalesOrderId, h.OrderDate, h.TotalDue })
            .ToQueryString();
    // Why: ToQueryString renders the SQL, with a DECLARE per parameter, without opening a connection. It
    // is the quickest way to inspect a query while you write it. customerId is a captured variable, so
    // EF sends it as @customerId instead of inlining it.

    // Exercise 5 — Filter on the server, format on the client
    // Return the products with ListPrice > minPrice as ProductLabel(Name, Price), where Price is the
    // ListPrice formatted as US currency ("$3,578.27"), ordered by ListPrice descending, then Name.
    // Rules: one round trip; the WHERE runs in SQL; the SQL selects only the columns you need.
    // Hint: SQL Server can't apply a .NET "C" format string, but the final Select may run .NET code.
    protected override Task<List<ProductLabel>> Ex5(AdventureWorksContext db, decimal minPrice)
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        return db.Products
            .Where(p => p.ListPrice > minPrice)
            .OrderByDescending(p => p.ListPrice).ThenBy(p => p.Name)
            .Select(p => new ProductLabel(p.Name, p.ListPrice.ToString("C", us)))
            .ToListAsync();
    }
    // Why: EF translates everything it can and runs only the untranslatable part of the final Select in
    // .NET. The WHERE and ORDER BY stay in SQL, and only Name and ListPrice cross the wire.
    // Tempting-but-wrong: AsEnumerable() or ToListAsync() before the Where. It returns the right labels,
    // but pulls all 504 products into memory to filter them there.

    // Challenge 1 — Model archaeology
    // Using only EF's metadata (db.Model), return "Schema.Table" for every entity type whose table
    // has triggers, sorted with StringComparer.Ordinal.
    // Rules: zero round trips. Hint: the scaffolder recorded each trigger with HasTrigger.
    protected override List<string> Ch1(AdventureWorksContext db)
        => db.Model.GetEntityTypes()
            .Where(e => e.GetDeclaredTriggers().Any())
            .Select(e => $"{e.GetSchema()}.{e.GetTableName()}")
            .Order(StringComparer.Ordinal)
            .ToList();
    // Why: db.Model is EF's complete mapping metadata (tables, columns, keys, indexes, triggers), and
    // reading it needs no query. The scaffolder declared the triggers because SaveChanges normally reads
    // generated values back with an OUTPUT clause, which SQL Server rejects on a table with triggers.
    // E08 covers this in depth.

    // Challenge 2 — Exactly one
    // Return the product whose ProductNumber starts with `productNumberPrefix`: null when none does,
    // InvalidOperationException when more than one does. The prefix is literal text: "BK_M68B-38"
    // must not match "BK-M68B-38" ('_' is a LIKE wildcard).
    // Rules: one round trip per call, reading at most two rows.
    protected override Task<ProductRef?> Ch2(AdventureWorksContext db, string productNumberPrefix)
        => db.Products
            .Where(p => p.ProductNumber.StartsWith(productNumberPrefix))
            .Select(p => new ProductRef(p.ProductId, p.Name))
            .SingleOrDefaultAsync();
    // Why: SingleOrDefault reads TOP(2): one row to return, plus a second to prove no other match exists.
    // It throws when the second row is there. StartsWith on a variable escapes the LIKE wildcards in the
    // value (ESCAPE N'\'), so '_' stays literal.
    // Tempting-but-wrong: EF.Functions.Like(p.ProductNumber, prefix + "%") passes '_' through as a
    // wildcard, so "BK_M68B-38" matches. FirstOrDefault reads TOP(1) and never notices duplicates.

    // Challenge 3 — A plan per value
    // Count the orders with the given Status. The column is extremely skewed (all 31,465 orders have
    // Status 5), the classic parameter-sniffing setup from T-SQL lesson 13. Make EF inline the status
    // so each value gets its own SQL text, and therefore its own cached plan.
    // Rules: one round trip per call; no parameters in the SQL.
    protected override Task<int> Ch3(AdventureWorksContext db, byte status)
        => db.SalesOrderHeaders.CountAsync(h => h.Status == EF.Constant(status));
    // Why: EF.Constant inlines the value, so status 1 and status 5 produce different SQL text, and SQL
    // Server compiles and caches a plan for each. That is the "plan per value" answer to parameter
    // sniffing. The cost is one plan-cache entry per distinct value, so reserve it for a handful of
    // skewed values, never for an ID column.
}
```

- [ ] **Step 5: Verify the solutions pass**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E01Solutions`
Expected: the build succeeds; `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 6: Write the worked examples, `Examples.cs`, and run them**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace EfCoreLessons.E01;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E01Examples --logger "console;verbosity=detailed"
public class E01Examples : LabTest
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: hierarchyid/geography columns and two whole tables never made it into the model.
    [Fact]
    public void Model_is_missing_what_the_scaffolder_could_not_map()
    {
        var employee = Db.Model.FindEntityType(typeof(Employee))!;
        Assert.Null(employee.FindProperty("OrganizationNode"));                     // hierarchyid
        Assert.NotNull(employee.FindProperty(nameof(Employee.OrganizationLevel)));  // computed column, still mapped

        var tables = Db.Model.GetEntityTypes().Select(e => e.GetTableName() ?? e.GetViewName()).ToList();
        Assert.DoesNotContain("Document", tables);          // hierarchyid primary key
        Assert.DoesNotContain("ProductDocument", tables);
        Print($"{tables.Count} entity types in the model");
    }

    // Concept 2: ToQueryString renders the SQL, with DECLAREs for parameters, without executing it.
    [Fact]
    public void ToQueryString_shows_sql_without_a_round_trip()
    {
        var customerId = 29825;
        var query = Db.SalesOrderHeaders
            .Where(h => h.CustomerId == customerId)
            .OrderByDescending(h => h.OrderDate)
            .Select(h => new { h.SalesOrderId, h.OrderDate, h.TotalDue });

        Print(query.ToQueryString());
        Assert.Equal(0, Sql.Count);
    }

    // Concept 2: LogTo is EF's built-in logging, the way to see SQL in any app.
    [Fact]
    public async Task LogTo_prints_sql_from_any_context()
    {
        var log = new List<string>();
        var options = new DbContextOptionsBuilder<AdventureWorksContext>()
            .UseSqlServer(Db.Database.GetDbConnection())
            .LogTo(log.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
            .EnableSensitiveDataLogging() // parameter values in the log: development only
            .Options;
        await using var logged = new AdventureWorksContext(options);
        logged.Database.UseTransaction(Db.Database.CurrentTransaction!.GetDbTransaction());

        var minPrice = 3000m;
        await logged.Products.CountAsync(p => p.ListPrice > minPrice);

        Print(string.Join(Environment.NewLine, log));
        Assert.Contains(log, line => line.Contains("@minPrice='3000'"));
    }

    // Concept 2: TagWith prepends a comment you can search for in the plan cache or Query Store.
    [Fact]
    public async Task TagWith_labels_the_sql()
    {
        await Db.Products.TagWith("E01: products over 1000").CountAsync(p => p.ListPrice > 1000);

        Assert.StartsWith("-- E01: products over 1000", Sql.Commands[0]);
    }

    // Concept 3: Take is TOP, Skip+Take is OFFSET/FETCH; EF parameterizes both numbers.
    [Fact]
    public async Task Take_and_Skip_become_TOP_and_OFFSET_FETCH()
    {
        await Db.Products.OrderBy(p => p.ProductId).Take(5).Select(p => p.Name).ToListAsync();
        await Db.Products.OrderBy(p => p.ProductId).Skip(10).Take(5).Select(p => p.Name).ToListAsync();

        Assert.Contains("SELECT TOP(@", Sql.Commands[0]);
        Assert.Contains("OFFSET @", Sql.Commands[1]);
        Assert.Contains("ROWS FETCH NEXT @", Sql.Commands[1]);
    }

    // Concept 3: First reads TOP(1); Single reads TOP(2) so it can throw when a second row exists.
    [Fact]
    public async Task First_reads_one_row_Single_reads_two()
    {
        var mountain200 = Db.Products.Where(p => p.ProductNumber.StartsWith("BK-M68B")).Select(p => p.Name);

        Print(await mountain200.FirstAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => mountain200.SingleAsync());

        Assert.Contains("TOP(1)", Sql.Commands[0]);
        Assert.Contains("TOP(2)", Sql.Commands[1]);
    }

    // Concept 3: Any is EXISTS (stops at the first match), Count is COUNT(*).
    [Fact]
    public async Task Any_is_EXISTS_Count_is_COUNT()
    {
        Assert.True(await Db.Products.AnyAsync(p => p.ListPrice > 3000));
        Assert.Equal(504, await Db.Products.CountAsync());

        Assert.Contains("EXISTS", Sql.Commands[0]);
        Assert.Contains("COUNT(*)", Sql.Commands[1]);
    }

    // Concept 3: a literal pattern is inlined; a variable becomes an escaped LIKE parameter.
    [Fact]
    public async Task String_methods_become_LIKE()
    {
        var part = "Mountain";
        await Db.Products.CountAsync(p => p.Name.Contains("Mountain"));
        await Db.Products.CountAsync(p => p.Name.Contains(part));

        Assert.Contains("LIKE N'%Mountain%'", Sql.Commands[0]);
        Assert.Contains("LIKE @part_contains ESCAPE N'\\'", Sql.Commands[1]);
    }

    // Concept 4: variables become parameters, literals are inlined, EF.Constant/EF.Parameter override.
    [Fact]
    public async Task Variables_are_parameters_literals_are_inlined()
    {
        var minPrice = 1000m;
        await Db.Products.CountAsync(p => p.ListPrice > minPrice);
        await Db.Products.CountAsync(p => p.ListPrice > 1000m);
        await Db.Products.CountAsync(p => p.ListPrice > EF.Constant(minPrice));
        await Db.Products.CountAsync(p => p.ListPrice > EF.Parameter(1000m));

        Assert.Contains("> @minPrice", Sql.Commands[0]);
        Assert.Contains("> 1000.0", Sql.Commands[1]);
        Assert.Contains("> 1000.0", Sql.Commands[2]);
        Assert.Contains("> @p", Sql.Commands[3]);
    }

    // Concept 5: an untranslatable call in Where throws; EF won't silently filter in memory.
    [Fact]
    public async Task Untranslatable_Where_throws()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.Products.Where(p => IsShort(p.Name)).ToListAsync());

        Print(error.Message);
        Assert.Equal(0, Sql.Count);
    }

    // Concept 5: in the final Select the same call is fine: EF fetches Name and runs IsShort in .NET.
    [Fact]
    public async Task Untranslatable_final_Select_runs_on_the_client()
    {
        var rows = await Db.Products
            .Where(p => p.ProductSubcategoryId == 1)
            .Select(p => new { p.Name, Short = IsShort(p.Name) })
            .ToListAsync();

        Assert.Equal(32, rows.Count);
        Assert.DoesNotContain("[p].[ProductNumber]", Sql.Commands[0]); // just Name crosses the wire
    }

    static bool IsShort(string name) => name.Length < 20;
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E01Examples`
Expected: the build succeeds; `Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11`.

- [ ] **Step 7: Write the lesson text, `README.md`**

````markdown
# E01 — Setup & Seeing the SQL

## What you'll learn

- How `Model/` was scaffolded from AdventureWorks, and what the scaffolder quietly left out
- Three ways to see the SQL EF generates — `ToQueryString`, `LogTo`, interceptors — and how to label a query with `TagWith`
- How the basic LINQ operators map to T-SQL (`WHERE`, `ORDER BY`, `TOP`, `OFFSET/FETCH`, `EXISTS`, `LIKE`)
- When EF sends a parameter and when it inlines a constant, and how `EF.Parameter` / `EF.Constant` override that
- Where EF runs code on the client — and where it refuses to

## Builds on

- T-SQL [lesson 01](../../../lessons/01-setup-and-basics/lesson.ipynb) and [lesson 02](../../../lessons/02-tsql-core-select/lesson.ipynb) — `SELECT`, `WHERE`, `ORDER BY`, `TOP`, `OFFSET/FETCH`, `LIKE`
- T-SQL [lesson 13](../../../lessons/13-execution-plans-and-tuning/lesson.ipynb) and [lesson 15](../../../lessons/15-query-lifecycle-optimizer/lesson.ipynb) — plan caching and parameter sniffing (concept 4)

## Setup

From `efcore/`, once per machine: `dotnet tool restore`. Then run this lesson's examples with their SQL:

```
dotnet test --filter E01Examples --logger "console;verbosity=detailed"
```

Every example and check runs inside a transaction that is rolled back, so nothing here changes AdventureWorks.

## Concepts

### 1. The model

`Model/` was generated once with `dotnet ef dbcontext scaffold` (exact command in [`efcore/README.md`](../../README.md)). It holds one class per table or view plus `AdventureWorksContext`, whose `OnModelCreating` describes every column, key, index and relationship with the Fluent API.

Things worth knowing before you query it:

| Database | Model |
|---|---|
| `SalesOrderID`, `BusinessEntityID` | `SalesOrderId`, `BusinessEntityId` (the scaffolder PascalCases names) |
| `Person.Person` | entity `Person`, DbSet `People` (DbSet names are pluralized) |
| Views (`Sales.vSalesPerson`, …) | keyless entities (`VSalesPerson`) — read-only, never tracked (E04) |
| Tables with triggers | declared with `HasTrigger` (why that matters: E08) |
| `date` columns | `DateOnly`; `datetime` columns stay `DateTime` |

What the scaffolder **skipped**, because EF Core has no mapping for the type without extra packages (warnings printed during scaffolding):

- `HumanResources.Employee.OrganizationNode` (`hierarchyid`) — the computed `OrganizationLevel` survives
- `Person.Address.SpatialLocation` (`geography`)
- The whole `Production.Document` and `Production.ProductDocument` tables — their primary key is a `hierarchyid`, so no entity could be generated

`db.Model` exposes all of this as metadata you can query without touching the database (example `Model_is_missing_what_the_scaffolder_could_not_map`, challenge 1).

### 2. Seeing the SQL

| Tool | Runs the query? | Use it for |
|---|---|---|
| `query.ToQueryString()` | no | inspecting one query while you write it; shows parameters as `DECLARE`s |
| `optionsBuilder.LogTo(...)` | yes | seeing everything a context executes in a real app |
| `DbCommandInterceptor` | yes | programmatic capture — this is how the Lab's `Sql` works |

`EnableSensitiveDataLogging()` adds parameter values to the log. Development only: it writes data into logs.

`query.TagWith("...")` prepends `-- ...` to the SQL text. That comment survives into the plan cache and Query Store, which is how you find "the EF query behind the slow page" later (E06).

Examples: `ToQueryString_shows_sql_without_a_round_trip`, `LogTo_prints_sql_from_any_context`, `TagWith_labels_the_sql`.

### 3. LINQ → T-SQL

| LINQ | T-SQL |
|---|---|
| `Where` | `WHERE` |
| `OrderBy` / `ThenBy` (`Descending`) | `ORDER BY` |
| `Take(n)` | `TOP(@p)` |
| `Skip(m).Take(n)` | `OFFSET @p ROWS FETCH NEXT @p1 ROWS ONLY` |
| `First` / `FirstOrDefault` | `TOP(1)` |
| `Single` / `SingleOrDefault` | `TOP(2)` — the second row is how EF knows to throw |
| `Any` | `CASE WHEN EXISTS (...)` |
| `Count` | `COUNT(*)` |
| `Contains` / `StartsWith` on a literal | `LIKE N'%x%'` / `LIKE N'x%'` |
| `Contains` / `StartsWith` on a variable | `LIKE @x_contains ESCAPE N'\'` — EF escapes `%`, `_` and `[` in the value |
| `Select(x => new Dto(...))` | only the columns the projection uses |

Note that EF parameterizes the numbers in `Take` and `Skip` even when you write literals: every page of a paged query shares one plan.

Examples: `Take_and_Skip_become_TOP_and_OFFSET_FETCH`, `First_reads_one_row_Single_reads_two`, `Any_is_EXISTS_Count_is_COUNT`, `String_methods_become_LIKE`.

### 4. Parameters vs constants

```csharp
var minPrice = 1000m;
db.Products.Where(p => p.ListPrice > minPrice)   // WHERE [p].[ListPrice] > @minPrice
db.Products.Where(p => p.ListPrice > 1000m)      // WHERE [p].[ListPrice] > 1000.0
```

A captured variable becomes a parameter; a literal in the lambda is inlined. That changes how SQL Server caches plans:

- **Parameter** — one SQL text, one cached plan, reused for every value. The plan is compiled for the first value it sees (parameter sniffing, T-SQL lesson 13).
- **Constant** — a new SQL text per value, so a new plan per value. Fine for a handful of values, plan-cache pollution for thousands.

Override the default per value: `EF.Constant(minPrice)` inlines a variable, `EF.Parameter(1000m)` parameterizes a literal.

Example: `Variables_are_parameters_literals_are_inlined`.

### 5. Client evaluation

EF translates the whole query to SQL, with one exception: the **final** `Select` may call .NET code EF can't translate. EF fetches the columns that code needs and runs it on the client.

Anywhere else — `Where`, `OrderBy`, `GroupBy`, a nested projection — an untranslatable call throws `InvalidOperationException` ("could not be translated") instead of silently loading the table and filtering in memory. To filter on the client on purpose, call `AsEnumerable()` explicitly, *after* everything SQL can do.

Examples: `Untranslatable_Where_throws`, `Untranslatable_final_Select_runs_on_the_client`.

## Pitfalls

- **Projecting after materializing.** `(await q.ToListAsync()).Select(...)` fetches every column and tracks every entity. Put the `Select` before `ToListAsync`.
- **`Count() > 0` instead of `Any()`.** Counts every row just to compare with zero.
- **Paging without a unique tie-breaker.** `OrderBy(p => p.ListPrice)` alone lets tied rows move between pages.
- **`EF.Functions.Like(col, prefix + "%")` with user input.** Unlike `StartsWith`, it does not escape wildcards in the value.

## Exercises

Open `Exercises.cs` and replace each `throw new NotImplementedException()` with your query. Run:

```
dotnet test --filter E01Exercises
```

*Skipped* means not attempted yet, *failed* means wrong (the message says why), *passed* means done. The checks live in `Checks.cs`. Peek at `Solutions.cs` only after attempting.
````

- [ ] **Step 8: Verify the prompts are identical in `Exercises.cs` and `Solutions.cs`**

```bash
cd efcore && python - lessons/E01-setup-and-generated-sql <<'EOF'
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

- [ ] **Step 9: Commit**

```bash
git add efcore/lessons/E01-setup-and-generated-sql
git commit -m "feat: add EF Core lesson E01 - setup and seeing the SQL"
```

---

## Task 4: Lesson E02 — Projections, joins & nulls

**Files:**
- Create: `efcore/lessons/E02-projections-joins-nulls/Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `LabTest`, `Sql`, `Reference.Rows<T>`, `[Exercise]` (Task 2); DbSets `SalesOrderHeaders`, `VSalesPeople`, `SalesTerritories`, `SalesPeople`, `SalesOrderDetails`, `PurchaseOrderDetails`, `Products`, `People`, `Vendors`, `Stores`, `ProductReviews` (Task 1).
- Produces: nothing later tasks depend on (namespace `EfCoreLessons.E02`).

- [ ] **Step 1: Write the checks, `Checks.cs`**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;

namespace EfCoreLessons.E02;

public record OrderWithNames(int SalesOrderId, string? CustomerLastName, string? TerritoryName);
public record SalesPersonCountry(int BusinessEntityId, string LastName, string? CountryRegionCode);
public record NotRedCounts(int CSharpSemantics, int SqlSemantics);
public record OrderParty(int SalesOrderId, string Customer, string? SalesPerson);
public record NamedParty(string Name, string Kind);
public record ProductRating(int ProductId, int? Rating);

// The checks for E02. E02Exercises (yours) and E02Solutions both inherit them.
public abstract class E02Checks : LabTest
{
    static readonly DateTime LeapDay = new(2012, 2, 29);

    protected abstract Task<List<OrderWithNames>> Ex1(AdventureWorksContext db, DateTime orderDate);
    protected abstract Task<List<SalesPersonCountry>> Ex2(AdventureWorksContext db);
    protected abstract Task<List<int>> Ex3(AdventureWorksContext db);
    protected abstract Task<NotRedCounts> Ex4(AdventureWorksContext db);
    protected abstract Task<List<int>> Ex5(AdventureWorksContext db, string lastName);
    protected abstract Task<List<OrderParty>> Ch1(AdventureWorksContext db, DateTime orderDate);
    protected abstract Task<List<NamedParty>> Ch2(AdventureWorksContext db, string text);
    protected abstract Task<List<ProductRating>> Ch3(AdventureWorksContext db, int minRating);

    [Exercise]
    public async Task Ex1_OrdersWithNames()
    {
        var result = await Ex1(Db, LeapDay);

        Assert.Equal(await Reference.Rows<OrderWithNames>($"""
            SELECT h.SalesOrderID AS SalesOrderId, p.LastName AS CustomerLastName, t.Name AS TerritoryName
            FROM Sales.SalesOrderHeader h
            JOIN Sales.Customer c ON c.CustomerID = h.CustomerID
            LEFT JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
            LEFT JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
            WHERE h.OrderDate = '2012-02-29'
            ORDER BY h.SalesOrderID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.DoesNotContain("[Freight]", Sql.All);   // no whole-entity loading
        Assert.Empty(Db.ChangeTracker.Entries());
    }

    [Exercise]
    public async Task Ex2_SalesPeopleWithCountry()
    {
        var result = await Ex2(Db);

        Assert.Equal(await Reference.Rows<SalesPersonCountry>($"""
            SELECT v.BusinessEntityID AS BusinessEntityId, v.LastName, t.CountryRegionCode
            FROM Sales.vSalesPerson v
            LEFT JOIN Sales.SalesTerritory t ON t.Name = v.TerritoryName
            ORDER BY v.BusinessEntityID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("LEFT JOIN", Sql.All);
    }

    [Exercise]
    public async Task Ex3_SoldButNeverPurchased()
    {
        var result = await Ex3(Db);

        Assert.Equal(await Reference.Rows<int>($"""
            SELECT ProductID AS Value FROM Sales.SalesOrderDetail
            EXCEPT
            SELECT ProductID FROM Purchasing.PurchaseOrderDetail
            ORDER BY Value
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("EXCEPT", Sql.All);
    }

    [Exercise]
    public async Task Ex4_NotRedTwoWays()
    {
        var result = await Ex4(Db);

        var cSharp = (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Production.Product WHERE Color <> 'Red' OR Color IS NULL")).Single();
        var tSql = (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Production.Product WHERE Color <> 'Red'")).Single();
        Assert.Equal(new NotRedCounts(cSharp, tSql), result);
        Assert.InRange(Sql.Count, 1, 2);
        Assert.All(Sql.Commands, sql => Assert.Contains("COUNT(", sql)); // counted in SQL, no rows fetched
    }

    [Exercise]
    public async Task Ex5_LastNameIgnoringCase()
    {
        var result = await Ex5(Db, "SMITH");

        Assert.Equal(await Reference.Rows<int>($"""
            SELECT BusinessEntityID AS Value FROM Person.Person WHERE LastName = 'Smith' ORDER BY BusinessEntityID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.DoesNotContain("UPPER(", Sql.All);
        Assert.DoesNotContain("LOWER(", Sql.All);
    }

    [Exercise]
    public async Task Ch1_OrderParties()
    {
        var result = await Ch1(Db, LeapDay);

        Assert.Equal(await Reference.Rows<OrderParty>($"""
            SELECT h.SalesOrderID AS SalesOrderId,
                   COALESCE(s.Name, p.LastName + N', ' + p.FirstName) AS Customer,
                   sp.FirstName + N' ' + sp.LastName AS SalesPerson
            FROM Sales.SalesOrderHeader h
            JOIN Sales.Customer c ON c.CustomerID = h.CustomerID
            LEFT JOIN Sales.Store s ON s.BusinessEntityID = c.StoreID
            LEFT JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
            LEFT JOIN Person.Person sp ON sp.BusinessEntityID = h.SalesPersonID
            WHERE h.OrderDate = '2012-02-29'
            ORDER BY h.SalesOrderID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("CASE", Sql.All); // the strings are built by SQL, not in memory
    }

    [Exercise]
    public async Task Ch2_VendorsAndStores()
    {
        var result = await Ch2(Db, "Bike");

        Assert.Equal(await Reference.Rows<NamedParty>($"""
            SELECT Name, N'Vendor' AS Kind FROM Purchasing.Vendor WHERE Name LIKE N'%Bike%'
            UNION ALL
            SELECT Name, N'Store' FROM Sales.Store WHERE Name LIKE N'%Bike%'
            ORDER BY Name, Kind
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("UNION ALL", Sql.All);
    }

    [Exercise]
    public async Task Ch3_RatingsWithoutLosingProducts()
    {
        var result = await Ch3(Db, 4);

        Assert.Equal(await Reference.Rows<ProductRating>($"""
            SELECT p.ProductID AS ProductId, r.Rating
            FROM Production.Product p
            LEFT JOIN Production.ProductReview r ON r.ProductID = p.ProductID AND r.Rating >= 4
            ORDER BY p.ProductID, r.Rating
            """), result);
        Sql.AssertRoundTrips(1);
    }
}
```

- [ ] **Step 2: Write the exercise stubs, `Exercises.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E02;

// E02 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E02Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
public class E02Exercises : E02Checks
{
    // Exercise 1 — Orders with names
    // For the orders placed on `orderDate` (OrderDate has no time part), return OrderWithNames:
    // SalesOrderId, the customer's person LastName, and the territory Name; ordered by SalesOrderId.
    // Rules: one round trip; navigations only, no whole entities loaded; nothing tracked.
    protected override Task<List<OrderWithNames>> Ex1(AdventureWorksContext db, DateTime orderDate)
        => throw new NotImplementedException();

    // Exercise 2 — A join without a navigation
    // Return every sales person from the VSalesPeople view (Sales.vSalesPerson) with the
    // CountryRegionCode of their territory, matching VSalesPerson.TerritoryName to SalesTerritory.Name.
    // Sales people without a territory must still appear, with a null code. Order by BusinessEntityId.
    // Rules: one round trip; the SQL uses a LEFT JOIN.
    protected override Task<List<SalesPersonCountry>> Ex2(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 3 — Sold but never purchased
    // Return the distinct ProductIds that appear in SalesOrderDetails but never in
    // PurchaseOrderDetails (AdventureWorks manufactures them), ascending.
    // Rules: one round trip; the SQL uses EXCEPT.
    protected override Task<List<int>> Ex3(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 4 — "Not red", two ways
    // Count the products that are not red twice: CSharpSemantics, where a NULL Color counts as
    // "not red", and SqlSemantics, where NULL colors are excluded, as in hand-written T-SQL.
    // Rules: at most two round trips; both counts computed by SQL Server.
    protected override Task<NotRedCounts> Ex4(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 5 — Case-insensitive, for free
    // Return the BusinessEntityIds of people whose LastName equals `lastName` ignoring case
    // (the check passes "SMITH"), ascending.
    // Rules: one round trip; no UPPER() or LOWER() in the SQL.
    protected override Task<List<int>> Ex5(AdventureWorksContext db, string lastName)
        => throw new NotImplementedException();

    // Challenge 1 — Who's on the order?
    // For the orders placed on `orderDate`, return OrderParty ordered by SalesOrderId, where:
    //   Customer    = the store's Name when the customer has a store, else "LastName, FirstName"
    //                 of the customer's person;
    //   SalesPerson = "FirstName LastName" of the order's sales person, or null for online orders
    //                 (they have none).
    // Rules: one round trip; let SQL produce both strings.
    protected override Task<List<OrderParty>> Ch1(AdventureWorksContext db, DateTime orderDate)
        => throw new NotImplementedException();

    // Challenge 2 — One list, two sources
    // Return all vendors (Kind = "Vendor") and all stores (Kind = "Store") whose Name contains `text`,
    // ordered by Name, then Kind. One row per vendor or store: two stores that share a name are
    // two rows.
    // Rules: one round trip.
    protected override Task<List<NamedParty>> Ch2(AdventureWorksContext db, string text)
        => throw new NotImplementedException();

    // Challenge 3 — Good reviews, all products
    // Return every product with each of its reviews rated at least `minRating`, ordered by
    // ProductId, then Rating. A product with no such review appears once with a null Rating —
    // all 504 products must be in the result.
    // Rules: one round trip.
    protected override Task<List<ProductRating>> Ch3(AdventureWorksContext db, int minRating)
        => throw new NotImplementedException();
}
```

- [ ] **Step 3: Verify every exercise reports as not attempted**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E02Exercises`
Expected: the build succeeds; `Skipped! - Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

- [ ] **Step 4: Write the solutions, `Solutions.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E02;

// Worked solutions for E02, each followed by a Why note. Run: dotnet test --filter E02Solutions
public class E02Solutions : E02Checks
{
    // Exercise 1 — Orders with names
    // For the orders placed on `orderDate` (OrderDate has no time part), return OrderWithNames:
    // SalesOrderId, the customer's person LastName, and the territory Name; ordered by SalesOrderId.
    // Rules: one round trip; navigations only, no whole entities loaded; nothing tracked.
    protected override Task<List<OrderWithNames>> Ex1(AdventureWorksContext db, DateTime orderDate)
        => db.SalesOrderHeaders
            .Where(h => h.OrderDate == orderDate)
            .OrderBy(h => h.SalesOrderId)
            .Select(h => new OrderWithNames(h.SalesOrderId, h.Customer.Person!.LastName, h.Territory!.Name))
            .ToListAsync();
    // Why: each navigation in the projection becomes a join, and EF picks the join type from the model.
    // Customer is required (INNER JOIN), while Customer.Person and Territory are optional (LEFT JOIN).
    // The `!` only silences the compiler; in SQL a missing Person is just NULL.
    // Tempting-but-wrong: Include(h => h.Customer).ThenInclude(c => c.Person) and mapping afterwards. That
    // loads and tracks three full entities per row to read two strings.

    // Exercise 2 — A join without a navigation
    // Return every sales person from the VSalesPeople view (Sales.vSalesPerson) with the
    // CountryRegionCode of their territory, matching VSalesPerson.TerritoryName to SalesTerritory.Name.
    // Sales people without a territory must still appear, with a null code. Order by BusinessEntityId.
    // Rules: one round trip; the SQL uses a LEFT JOIN.
    protected override Task<List<SalesPersonCountry>> Ex2(AdventureWorksContext db)
        => db.VSalesPeople
            .LeftJoin(db.SalesTerritories, v => v.TerritoryName, t => t.Name, (v, t) => new { v, t })
            .OrderBy(x => x.v.BusinessEntityId)
            .Select(x => new SalesPersonCountry(x.v.BusinessEntityId, x.v.LastName, x.t == null ? null : x.t.CountryRegionCode))
            .ToListAsync();
    // Why: keyless entities (views) have no navigations, and the match is on names, not a foreign key, so
    // only an explicit join works. EF 10's LeftJoin says "keep every left row" directly. The older
    // GroupJoin + SelectMany(DefaultIfEmpty) pattern produces the same LEFT JOIN. The join yields an
    // anonymous pair and the record comes last, because EF can't order by a positional record's
    // constructor arguments (concept 1).
    // Tempting-but-wrong: Join, which is an INNER JOIN and silently drops the three sales people
    // without a territory.

    // Exercise 3 — Sold but never purchased
    // Return the distinct ProductIds that appear in SalesOrderDetails but never in
    // PurchaseOrderDetails (AdventureWorks manufactures them), ascending.
    // Rules: one round trip; the SQL uses EXCEPT.
    protected override Task<List<int>> Ex3(AdventureWorksContext db)
        => db.SalesOrderDetails.Select(d => d.ProductId)
            .Except(db.PurchaseOrderDetails.Select(d => d.ProductId))
            .OrderBy(id => id)
            .ToListAsync();
    // Why: Except is set difference, and EXCEPT already returns distinct values, so no Distinct() is
    // needed. Ordering after a set operation is fine: EF wraps the EXCEPT in a subquery and orders that.
    // Tempting-but-wrong: loading both ID lists and calling Except in memory. That's two round trips and
    // ships 121,317 + 8,845 rows to compare 190 IDs.

    // Exercise 4 — "Not red", two ways
    // Count the products that are not red twice: CSharpSemantics, where a NULL Color counts as
    // "not red", and SqlSemantics, where NULL colors are excluded, as in hand-written T-SQL.
    // Rules: at most two round trips; both counts computed by SQL Server.
    protected override async Task<NotRedCounts> Ex4(AdventureWorksContext db)
        => new(
            await db.Products.CountAsync(p => p.Color != "Red"),
            await db.Products.CountAsync(p => p.Color != null && p.Color != "Red"));
    // Why: EF gives != C# semantics by adding `OR [Color] IS NULL`, which is why the plain comparison
    // counts the 248 colorless products too (466). To get the T-SQL answer (218), exclude NULL explicitly.
    // This is the mirror image of T-SQL lesson 02, challenge 2: there you had to add `OR Color IS NULL`;
    // with EF you have to take it away.

    // Exercise 5 — Case-insensitive, for free
    // Return the BusinessEntityIds of people whose LastName equals `lastName` ignoring case
    // (the check passes "SMITH"), ascending.
    // Rules: one round trip; no UPPER() or LOWER() in the SQL.
    protected override Task<List<int>> Ex5(AdventureWorksContext db, string lastName)
        => db.People
            .Where(p => p.LastName == lastName)
            .OrderBy(p => p.BusinessEntityId)
            .Select(p => p.BusinessEntityId)
            .ToListAsync();
    // Why: LastName uses the case-insensitive SQL_Latin1_General_CP1_CI_AS collation, so plain equality
    // already ignores case, and it can seek the IX_Person_LastName_FirstName_MiddleName index.
    // Tempting-but-wrong: string.Equals(..., StringComparison.OrdinalIgnoreCase) throws because it can't be
    // translated. p.LastName.ToUpper() == lastName.ToUpper() works but wraps the column in UPPER(), which
    // turns the index seek into a scan.

    // Challenge 1 — Who's on the order?
    // For the orders placed on `orderDate`, return OrderParty ordered by SalesOrderId, where:
    //   Customer    = the store's Name when the customer has a store, else "LastName, FirstName"
    //                 of the customer's person;
    //   SalesPerson = "FirstName LastName" of the order's sales person, or null for online orders
    //                 (they have none).
    // Rules: one round trip; let SQL produce both strings.
    protected override Task<List<OrderParty>> Ch1(AdventureWorksContext db, DateTime orderDate)
        => db.SalesOrderHeaders
            .Where(h => h.OrderDate == orderDate)
            .OrderBy(h => h.SalesOrderId)
            .Select(h => new OrderParty(
                h.SalesOrderId,
                h.Customer.Store != null
                    ? h.Customer.Store.Name
                    : h.Customer.Person!.LastName + ", " + h.Customer.Person.FirstName,
                h.SalesPerson != null
                    ? h.SalesPerson.BusinessEntity.BusinessEntity.FirstName + " " + h.SalesPerson.BusinessEntity.BusinessEntity.LastName
                    : null))
            .ToListAsync();
    // Why: the conditional operator becomes a CASE expression and + becomes string concatenation, so SQL
    // builds both strings. SalesPerson -> Employee -> Person is a chain of navigations after an optional
    // one, and EF keeps the whole chain as LEFT JOINs so online orders survive with a null SalesPerson.
    // Tempting-but-wrong: Include the entities and build the strings in memory, which loads five entity
    // types for two strings.

    // Challenge 2 — One list, two sources
    // Return all vendors (Kind = "Vendor") and all stores (Kind = "Store") whose Name contains `text`,
    // ordered by Name, then Kind. One row per vendor or store: two stores that share a name are
    // two rows.
    // Rules: one round trip.
    protected override Task<List<NamedParty>> Ch2(AdventureWorksContext db, string text)
        => db.Vendors.Where(v => v.Name.Contains(text)).Select(v => new { v.Name, Kind = "Vendor" })
            .Concat(db.Stores.Where(s => s.Name.Contains(text)).Select(s => new { s.Name, Kind = "Store" }))
            .OrderBy(x => x.Name).ThenBy(x => x.Kind)
            .Select(x => new NamedParty(x.Name, x.Kind))
            .ToListAsync();
    // Why: Concat is UNION ALL and keeps every row. Both sides project the same anonymous shape, so the
    // columns line up, and the record is created after the set operation. EF refuses a set operation over
    // a constructor projection ("set operation after client projection").
    // Tempting-but-wrong: Union, which is UNION and removes duplicates, so one of the two "Friendly
    // Bike Shop" stores disappears and the DISTINCT costs a sort you didn't need.

    // Challenge 3 — Good reviews, all products
    // Return every product with each of its reviews rated at least `minRating`, ordered by
    // ProductId, then Rating. A product with no such review appears once with a null Rating —
    // all 504 products must be in the result.
    // Rules: one round trip.
    protected override Task<List<ProductRating>> Ch3(AdventureWorksContext db, int minRating)
        => db.Products
            .LeftJoin(db.ProductReviews.Where(r => r.Rating >= minRating),
                p => p.ProductId, r => r.ProductId,
                (p, r) => new { p.ProductId, Rating = r == null ? (int?)null : r.Rating })
            .OrderBy(x => x.ProductId).ThenBy(x => x.Rating)
            .Select(x => new ProductRating(x.ProductId, x.Rating))
            .ToListAsync();
    // Why: the rating filter belongs to the right-hand side of the join (it's the ON clause in T-SQL), so
    // it filters the reviews before they're joined. Products without a matching review keep their row.
    // Tempting-but-wrong: LeftJoin(db.ProductReviews, ...).Where(x => x.Rating >= minRating). The WHERE
    // runs after the join, NULL >= 4 is never true, and the "left" join quietly becomes an inner join that
    // returns 3 rows instead of 504.
}
```

- [ ] **Step 5: Verify the solutions pass**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E02Solutions`
Expected: the build succeeds; `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 6: Write the worked examples, `Examples.cs`, and run them**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCoreLessons.E02;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E02Examples --logger "console;verbosity=detailed"
public class E02Examples : LabTest
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: an entity query selects every column and tracks; a projection selects what it uses.
    [Fact]
    public async Task Projection_selects_only_the_columns_it_uses()
    {
        await Db.Products.Where(p => p.ListPrice > 3000).ToListAsync();
        var tracked = Db.ChangeTracker.Entries().Count();
        Db.ChangeTracker.Clear();

        await Db.Products.Where(p => p.ListPrice > 3000).Select(p => new { p.ProductId, p.Name }).ToListAsync();

        Assert.Contains("[p].[WeightUnitMeasureCode]", Sql.Commands[0]);
        Assert.DoesNotContain("[p].[WeightUnitMeasureCode]", Sql.Commands[1]);
        Assert.True(tracked > 0);
        Assert.Empty(Db.ChangeTracker.Entries());
    }

    // Concept 2: required relationship -> INNER JOIN, optional relationship -> LEFT JOIN.
    [Fact]
    public async Task Required_navigation_is_INNER_JOIN_optional_is_LEFT_JOIN()
    {
        await Db.SalesOrderHeaders
            .Where(h => h.SalesOrderId == 43659)
            .Select(h => new { h.Customer.AccountNumber, Territory = h.Territory!.Name })
            .SingleAsync();

        Assert.Contains("INNER JOIN [Sales].[Customer]", Sql.All);
        Assert.Contains("LEFT JOIN [Sales].[SalesTerritory]", Sql.All);
    }

    // Concept 3: keyless views have no navigations, so join them explicitly.
    [Fact]
    public async Task Join_when_there_is_no_navigation()
    {
        var rows = await Db.VSalesPeople
            .Join(Db.SalesPeople, v => v.BusinessEntityId, s => s.BusinessEntityId,
                (v, s) => new { v.LastName, s.CommissionPct })
            .ToListAsync();

        Assert.Equal(17, rows.Count);
        Assert.Contains("INNER JOIN", Sql.All);
    }

    // Concept 3: EF 10's LeftJoin/RightJoin, and the GroupJoin pattern they replace.
    [Fact]
    public async Task LeftJoin_RightJoin_and_GroupJoin()
    {
        var left = await Db.VSalesPeople
            .LeftJoin(Db.SalesTerritories, v => v.TerritoryName, t => t.Name,
                (v, t) => new { v.LastName, Code = t == null ? null : t.CountryRegionCode })
            .ToListAsync();

        var right = await Db.SalesTerritories
            .RightJoin(Db.VSalesPeople, t => t.Name, v => v.TerritoryName,
                (t, v) => new { v.LastName, Code = t == null ? null : t.CountryRegionCode })
            .ToListAsync();

        var classic = await Db.VSalesPeople
            .GroupJoin(Db.SalesTerritories, v => v.TerritoryName, t => t.Name, (v, ts) => new { v, ts })
            .SelectMany(x => x.ts.DefaultIfEmpty(), (x, t) => new { x.v.LastName, Code = t == null ? null : t.CountryRegionCode })
            .ToListAsync();

        Assert.Equal(17, left.Count);
        Assert.Equal(3, left.Count(x => x.Code == null)); // sales people without a territory
        Assert.Equal(17, right.Count);
        Assert.Equal(17, classic.Count);
        Assert.Contains("LEFT JOIN", Sql.Commands[0]);
        Assert.Contains("RIGHT JOIN", Sql.Commands[1]);
        Assert.Contains("LEFT JOIN", Sql.Commands[2]);
    }

    // Concept 4: two different stores are both called "Friendly Bike Shop".
    [Fact]
    public async Task Union_removes_duplicates_Concat_keeps_them()
    {
        var stores = Db.Stores.Where(s => s.Name == "Friendly Bike Shop").Select(s => s.Name);
        var vendors = Db.Vendors.Where(v => v.Name == "Friendly Bike Shop").Select(v => v.Name);

        var union = await stores.Union(vendors).ToListAsync();
        var concat = await stores.Concat(vendors).ToListAsync();

        Assert.Single(union);
        Assert.Equal(2, concat.Count);
        Assert.DoesNotContain("UNION ALL", Sql.Commands[0]);
        Assert.Contains("UNION", Sql.Commands[0]);
        Assert.Contains("UNION ALL", Sql.Commands[1]);
    }

    // Concept 1: anything composed after a positional-record projection can't be translated.
    [Fact]
    public async Task Composing_after_a_record_projection_fails()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.Products.Select(p => new ProductName(p.ProductId, p.Name)).OrderBy(x => x.Name).ToListAsync());
        Print(error.Message);

        var works = await Db.Products
            .Select(p => new { p.ProductId, p.Name })
            .OrderBy(x => x.Name)
            .Select(x => new ProductName(x.ProductId, x.Name))
            .ToListAsync();
        Assert.Equal(504, works.Count);
    }

    record ProductName(int ProductId, string Name);

    // Concept 5: C# says null != "Red"; EF adds OR IS NULL to agree. Hand-written T-SQL returns 218.
    [Fact]
    public async Task Not_equal_keeps_NULL_rows_unlike_TSQL()
    {
        Assert.Equal(466, await Db.Products.CountAsync(p => p.Color != "Red"));
        Assert.Equal(218, await Db.Products.CountAsync(p => p.Color != null && p.Color != "Red"));

        Assert.Contains("[p].[Color] <> N'Red' OR [p].[Color] IS NULL", Sql.Commands[0]);
    }

    // Concept 5: EF looks at the parameter's value; a null variable becomes IS NULL.
    [Fact]
    public async Task Null_variable_becomes_IS_NULL()
    {
        string? color = null;
        var noColor = await Db.Products.CountAsync(p => p.Color == color);

        Assert.Equal(248, noColor);
        Assert.Contains("[p].[Color] IS NULL", Sql.All);
    }

    // Concept 5: comparing two nullable columns treats NULL = NULL as true, like C#.
    [Fact]
    public async Task Two_nullable_columns_compare_with_null_compensation()
    {
        await Db.Products.CountAsync(p => p.SizeUnitMeasureCode == p.WeightUnitMeasureCode);

        Assert.Contains("OR ([p].[SizeUnitMeasureCode] IS NULL AND [p].[WeightUnitMeasureCode] IS NULL)", Sql.All);
    }

    // Concept 5: UseRelationalNulls turns the compensation off: same LINQ, T-SQL's answer.
    [Fact]
    public async Task UseRelationalNulls_switches_to_TSQL_semantics()
    {
        var options = new DbContextOptionsBuilder<AdventureWorksContext>()
            .UseSqlServer(Db.Database.GetDbConnection(), sql => sql.UseRelationalNulls())
            .Options;
        await using var relational = new AdventureWorksContext(options);
        relational.Database.UseTransaction(Db.Database.CurrentTransaction!.GetDbTransaction());

        Assert.Equal(218, await relational.Products.CountAsync(p => p.Color != "Red"));
    }

    // Concept 6: the column's collation is case-insensitive, so == ignores case; StringComparison throws.
    [Fact]
    public async Task Collation_decides_case_sensitivity()
    {
        Assert.Equal(103, await Db.People.CountAsync(p => p.LastName == "SMITH"));
        Assert.Equal(0, await Db.People.CountAsync(p =>
            EF.Functions.Collate(p.LastName, "SQL_Latin1_General_CP1_CS_AS") == "SMITH"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.People.CountAsync(p => string.Equals(p.LastName, "SMITH", StringComparison.OrdinalIgnoreCase)));
        Print(error.Message);
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E02Examples`
Expected: the build succeeds; `Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11`.

- [ ] **Step 7: Write the lesson text, `README.md`**

````markdown
# E02 — Projections, Joins & Nulls

## What you'll learn

- How a projection decides which columns EF fetches, and why projections are never tracked
- How navigations turn into joins: `INNER JOIN` for required relationships, `LEFT JOIN` for optional ones
- Explicit joins for when no navigation exists: `Join`, EF 10's `LeftJoin` / `RightJoin`, and the classic `GroupJoin` + `DefaultIfEmpty` pattern
- Set operations: `Union`, `Concat`, `Intersect`, `Except`
- How EF makes SQL `NULL` behave like C# `null` — and how that differs from the T-SQL you wrote by hand
- Why string comparisons follow the database collation, not `StringComparison`

## Builds on

- T-SQL [lesson 02](../../../lessons/02-tsql-core-select/lesson.ipynb) — `NULL` and three-valued logic (challenge 2 is the mirror of exercise 4 here)
- T-SQL [lesson 03](../../../lessons/03-data-types-and-expressions/lesson.ipynb) — collations and string comparison
- T-SQL [lesson 04](../../../lessons/04-joins-and-sets/lesson.ipynb) — joins, set operations, common join mistakes
- Cheatsheet: [`03-joins-and-sets.md`](../../../cheatsheets/03-joins-and-sets.md)

## Setup

```
dotnet test --filter E02Examples --logger "console;verbosity=detailed"
```

## Concepts

### 1. Projections

`Select` decides what crosses the wire. Query an entity and EF selects every mapped column and tracks the result. Project into a DTO, record or anonymous type and EF selects only the columns the projection uses, and tracks nothing.

```csharp
db.Products.Where(p => p.ListPrice > 3000).ToListAsync();                        // 25 columns, tracked
db.Products.Where(p => p.ListPrice > 3000).Select(p => new { p.Name }).ToListAsync(); // 1 column, untracked
```

**Project into your DTO last.** EF can look inside anonymous types (`new { p.Name }`) and member initializers (`new Dto { Name = p.Name }`), but not inside a constructor call. A positional record (`new ProductRef(p.ProductId, p.Name)`) is a black box. Anything composed after it fails with "could not be translated", and that includes `OrderBy`, `Where`, and set operations ("set operation after client projection"). Use anonymous types for the intermediate steps, and make the record the final `Select`.

Examples: `Projection_selects_only_the_columns_it_uses`, `Composing_after_a_record_projection_fails`.

### 2. Navigations become joins

Using a navigation inside a query makes EF join for you. The join type follows the relationship:

| Navigation | FK column | Join |
|---|---|---|
| `SalesOrderHeader.Customer` | `CustomerID int NOT NULL` (required) | `INNER JOIN` |
| `SalesOrderHeader.Territory` | `TerritoryID int NULL` (optional) | `LEFT JOIN` |

In SQL, `h.Territory!.Name` on an order without a territory is simply `NULL`. The same expression in LINQ-to-Objects would throw `NullReferenceException`. The `!` only silences the C# compiler: EF still emits a `LEFT JOIN` and returns `null`.

Example: `Required_navigation_is_INNER_JOIN_optional_is_LEFT_JOIN`.

### 3. Explicit joins

You need an explicit join when there is no navigation to follow. That happens with keyless views (they have no relationships), with columns that aren't foreign keys, and with a filtered right-hand side.

| Operator | SQL |
|---|---|
| `Join` | `INNER JOIN` |
| `LeftJoin` / `RightJoin` (new in .NET 10 / EF 10) | `LEFT JOIN` / `RIGHT JOIN` |
| `GroupJoin(...).SelectMany(g => g.DefaultIfEmpty(), ...)` | `LEFT JOIN` (the pre-EF 10 way, still common in existing code) |

There is no `FullJoin` operator. Emulate one with a `Union` of a left and a right join, or use raw SQL (E04).

Examples: `Join_when_there_is_no_navigation`, `LeftJoin_RightJoin_and_GroupJoin`.

### 4. Set operations

| LINQ | SQL |
|---|---|
| `Union` | `UNION` (removes duplicates) |
| `Concat` | `UNION ALL` (keeps them) |
| `Intersect` | `INTERSECT` |
| `Except` | `EXCEPT` |

Both sides must project the same shape: same members, same types, same order. You can keep composing after a set operation (`OrderBy`, `Take`): EF wraps the set operation in a subquery.

Example: `Union_removes_duplicates_Concat_keeps_them`.

### 5. C# null semantics on top of SQL NULL

T-SQL uses three-valued logic: `Color <> 'Red'` is UNKNOWN when `Color` is `NULL`, so those rows disappear (T-SQL lesson 02). C# says `null != "Red"` is `true`. EF sides with C# and rewrites the SQL to make the answers match:

| LINQ | SQL EF generates |
|---|---|
| `p.Color != "Red"` | `[p].[Color] <> N'Red' OR [p].[Color] IS NULL` |
| `p.Color == color` with `color == null` | `[p].[Color] IS NULL` (EF checks the parameter's value) |
| `a.X == a.Y` (both nullable) | `a.X = a.Y OR (a.X IS NULL AND a.Y IS NULL)` |

So the same "not red" filter returns 466 products from EF and 218 from the hand-written T-SQL. To get T-SQL's semantics, either add `&& p.Color != null` yourself or configure `UseRelationalNulls()` on the context. Don't flip `UseRelationalNulls()` globally on an existing codebase: it silently changes the results of every query that compares nullable values.

Examples: `Not_equal_keeps_NULL_rows_unlike_TSQL`, `Null_variable_becomes_IS_NULL`, `Two_nullable_columns_compare_with_null_compensation`, `UseRelationalNulls_switches_to_TSQL_semantics`.

### 6. String comparison follows the collation

`p.LastName == "SMITH"` matches "Smith" because AdventureWorks uses a case-insensitive collation (`SQL_Latin1_General_CP1_CI_AS`). Case sensitivity is a property of the column's collation, not of your C#.

- `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)` and the other `StringComparison` overloads don't translate. They throw.
- `ToUpper()` / `ToLower()` translate to `UPPER()` / `LOWER()`. On a case-insensitive column they change nothing, except that an index on the column can no longer be used for a seek.
- For an explicit collation, use `EF.Functions.Collate(p.LastName, "SQL_Latin1_General_CP1_CS_AS") == "SMITH"`.

Example: `Collation_decides_case_sensitivity`.

## Pitfalls

- **Composing after a record projection.** `.Select(x => new Dto(...)).OrderBy(d => d.Name)` throws. Order, filter and combine on anonymous shapes, then project into the record.
- **Filtering the right side of a left join after the join.** `LeftJoin(...).Where(x => x.Rating >= 4)` throws away the unmatched rows, which turns it back into an inner join. Filter the right-hand source before joining (challenge 3).
- **`Union` where you meant `Concat`.** `Union` silently drops legitimate duplicates, and the `DISTINCT` it implies costs a sort.
- **Assuming EF nulls behave like your hand-written T-SQL.** They don't, by design.
- **`ToLower()` for case-insensitive search.** It's redundant on a CI collation, and it breaks index seeks.

## Exercises

```
dotnet test --filter E02Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
````

- [ ] **Step 8: Verify the prompts are identical in `Exercises.cs` and `Solutions.cs`**

```bash
cd efcore && python - lessons/E02-projections-joins-nulls <<'EOF'
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

- [ ] **Step 9: Commit**

```bash
git add efcore/lessons/E02-projections-joins-nulls
git commit -m "feat: add EF Core lesson E02 - projections, joins and nulls"
```

---

## Task 5: Lesson E03 — Grouping, subqueries & where LINQ runs out

**Files:**
- Create: `efcore/lessons/E03-grouping-subqueries-limits/Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `LabTest`, `Sql`, `Reference.Rows<T>`, `[Exercise]` (Task 2); DbSets `SalesOrderHeaders`, `SalesOrderDetails`, `Products`, `ProductSubcategories`, `SalesTerritories`, `Customers` (Task 1).
- Produces: nothing later tasks depend on (namespace `EfCoreLessons.E03`).

- [ ] **Step 1: Write the checks, `Checks.cs`**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;

namespace EfCoreLessons.E03;

public record TerritorySales(string Territory, int Orders, int Customers, decimal Total);
public record CustomerOrderMix(int CustomerId, int Total, int Online, int Offline);
public record ProductRef(int ProductId, string Name);
public record TerritoryTopOrder(string Territory, int SalesOrderId, decimal TotalDue);
public record OrderRunningTotal(int SalesOrderId, DateTime OrderDate, decimal TotalDue, decimal RunningTotal);
public record RollupRow(string? Group, string? Territory, decimal Total);
public record SubcategoryTopPrice(int ProductSubcategoryId, int ProductId, decimal ListPrice);

// The checks for E03. E03Exercises (yours) and E03Solutions both inherit them.
public abstract class E03Checks : LabTest
{
    static readonly DateTime From = new(2013, 1, 1);
    static readonly DateTime To = new(2014, 1, 1);

    protected abstract Task<List<TerritorySales>> Ex1(AdventureWorksContext db, DateTime from, DateTime to);
    protected abstract Task<List<CustomerOrderMix>> Ex2(AdventureWorksContext db, int minOrders);
    protected abstract Task<List<ProductRef>> Ex3(AdventureWorksContext db);
    protected abstract Task<List<ProductRef>> Ex4(AdventureWorksContext db, List<int> productIds);
    protected abstract Task<List<TerritoryTopOrder>> Ex5(AdventureWorksContext db, DateTime from, DateTime to);
    protected abstract Task<List<OrderRunningTotal>> Ch1(AdventureWorksContext db, int customerId);
    protected abstract Task<List<RollupRow>> Ch2(AdventureWorksContext db, DateTime from, DateTime to);
    protected abstract Task<List<SubcategoryTopPrice>> Ch3(AdventureWorksContext db);

    [Exercise]
    public async Task Ex1_TerritorySales()
    {
        var result = await Ex1(Db, From, To);

        Assert.Equal(await Reference.Rows<TerritorySales>($"""
            SELECT t.Name AS Territory, COUNT(*) AS Orders, COUNT(DISTINCT h.CustomerID) AS Customers, SUM(h.TotalDue) AS Total
            FROM Sales.SalesOrderHeader h
            JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
            WHERE h.OrderDate >= '2013-01-01' AND h.OrderDate < '2014-01-01'
            GROUP BY t.Name
            ORDER BY Total DESC
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("GROUP BY", Sql.All);
        Assert.Contains("COUNT(DISTINCT", Sql.All);
    }

    [Exercise]
    public async Task Ex2_LoyalCustomers()
    {
        var result = await Ex2(Db, 25);

        Assert.Equal(await Reference.Rows<CustomerOrderMix>($"""
            SELECT CustomerID AS CustomerId, COUNT(*) AS Total,
                   COUNT(CASE WHEN OnlineOrderFlag = 1 THEN 1 END) AS Online,
                   COUNT(CASE WHEN OnlineOrderFlag = 0 THEN 1 END) AS Offline
            FROM Sales.SalesOrderHeader
            GROUP BY CustomerID
            HAVING COUNT(*) >= 25
            ORDER BY Total DESC, CustomerId
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("HAVING", Sql.All); // groups filtered in SQL, not in memory
    }

    [Exercise]
    public async Task Ex3_NeverOrdered()
    {
        var result = await Ex3(Db);

        Assert.Equal(await Reference.Rows<ProductRef>($"""
            SELECT p.ProductID AS ProductId, p.Name
            FROM Production.Product p
            WHERE p.FinishedGoodsFlag = 1
              AND NOT EXISTS (SELECT 1 FROM Sales.SalesOrderDetail d WHERE d.ProductID = p.ProductID)
            ORDER BY p.ProductID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("EXISTS", Sql.All);
        Assert.DoesNotContain("COUNT", Sql.All);
    }

    [Exercise]
    public async Task Ex4_OneSqlForAnyListSize()
    {
        foreach (var ids in new List<int>[] { [707, 708, 709], [710, 711, 712, 713, 714, 715, 716], [.. Enumerable.Range(717, 12)] })
        {
            var csv = string.Join(",", ids);
            Assert.Equal(await Reference.Rows<ProductRef>($"""
                SELECT ProductID AS ProductId, Name FROM Production.Product
                WHERE ProductID IN (SELECT CAST(value AS int) FROM STRING_SPLIT({csv}, ','))
                ORDER BY ProductID
                """), await Ex4(Db, ids));
        }

        Sql.AssertRoundTrips(3);
        Assert.Single(Sql.Commands.Distinct()); // 3, 7 and 12 IDs: one SQL text, one plan
        Assert.Contains("OPENJSON", Sql.All);
    }

    [Exercise]
    public async Task Ex5_TopThreePerTerritory()
    {
        var result = await Ex5(Db, From, To);

        Assert.Equal(await Reference.Rows<TerritoryTopOrder>($"""
            SELECT Territory, SalesOrderId, TotalDue
            FROM (
                SELECT t.Name AS Territory, h.SalesOrderID AS SalesOrderId, h.TotalDue,
                       ROW_NUMBER() OVER (PARTITION BY h.TerritoryID ORDER BY h.TotalDue DESC, h.SalesOrderID) AS rn
                FROM Sales.SalesOrderHeader h
                JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
                WHERE h.OrderDate >= '2013-01-01' AND h.OrderDate < '2014-01-01'
            ) ranked
            WHERE rn <= 3
            ORDER BY Territory, TotalDue DESC, SalesOrderId
            """), result);
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ch1_RunningTotal()
    {
        var result = await Ch1(Db, 11091);

        Assert.Equal(await Reference.Rows<OrderRunningTotal>($"""
            SELECT SalesOrderID AS SalesOrderId, OrderDate, TotalDue,
                   SUM(TotalDue) OVER (ORDER BY OrderDate, SalesOrderID ROWS UNBOUNDED PRECEDING) AS RunningTotal
            FROM Sales.SalesOrderHeader
            WHERE CustomerID = {11091}
            ORDER BY OrderDate, SalesOrderID
            """), result);
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ch2_RollupInLinq()
    {
        var result = await Ch2(Db, From, To);

        var expected = await Reference.Rows<RollupRow>($"""
            SELECT t.[Group] AS [Group], t.Name AS Territory, SUM(h.TotalDue) AS Total
            FROM Sales.SalesOrderHeader h
            JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
            WHERE h.OrderDate >= '2013-01-01' AND h.OrderDate < '2014-01-01'
            GROUP BY ROLLUP (t.[Group], t.Name)
            """);
        Assert.Equal( // any row order is fine
            expected.OrderBy(r => r.Group).ThenBy(r => r.Territory),
            result.OrderBy(r => r.Group).ThenBy(r => r.Territory));
        Sql.AssertRoundTrips(1);
        Assert.Contains("UNION ALL", Sql.All);
        Assert.DoesNotContain("ROLLUP", Sql.All); // LINQ only: no raw SQL
    }

    [Exercise]
    public async Task Ch3_TopPriceWithTies()
    {
        var result = await Ch3(Db);

        Assert.Equal(await Reference.Rows<SubcategoryTopPrice>($"""
            SELECT ProductSubcategoryID AS ProductSubcategoryId, ProductID AS ProductId, ListPrice
            FROM (
                SELECT ProductSubcategoryID, ProductID, ListPrice,
                       RANK() OVER (PARTITION BY ProductSubcategoryID ORDER BY ListPrice DESC) AS r
                FROM Production.Product
                WHERE ProductSubcategoryID IS NOT NULL
            ) ranked
            WHERE r = 1
            ORDER BY ProductSubcategoryId, ProductId
            """), result);
        Sql.AssertRoundTrips(1);
    }
}
```

- [ ] **Step 2: Write the exercise stubs, `Exercises.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E03;

// E03 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E03Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
public class E03Exercises : E03Checks
{
    // Exercise 1 — Territory scoreboard
    // For orders with from <= OrderDate < to, return one TerritorySales row per territory:
    // territory Name, number of orders, number of distinct customers, and sum of TotalDue;
    // ordered by Total descending.
    // Rules: one round trip; grouped and counted in SQL (GROUP BY, COUNT(DISTINCT ...)).
    protected override Task<List<TerritorySales>> Ex1(AdventureWorksContext db, DateTime from, DateTime to)
        => throw new NotImplementedException();

    // Exercise 2 — Loyal customers
    // Return customers with at least `minOrders` orders as CustomerOrderMix: CustomerId, Total
    // orders, Online orders (OnlineOrderFlag) and Offline orders; ordered by Total descending,
    // then CustomerId.
    // Rules: one round trip; the minimum is applied in SQL (HAVING).
    protected override Task<List<CustomerOrderMix>> Ex2(AdventureWorksContext db, int minOrders)
        => throw new NotImplementedException();

    // Exercise 3 — Never ordered
    // Return finished-goods products (FinishedGoodsFlag) that appear on no SalesOrderDetail,
    // ordered by ProductId. Note: Product has no navigation to SalesOrderDetail.
    // Rules: one round trip; the SQL uses EXISTS, never COUNT.
    protected override Task<List<ProductRef>> Ex3(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 4 — One plan for every list
    // Return the products whose ProductId is in `productIds`, ordered by ProductId. The check calls
    // this with lists of 3, 7 and 12 IDs, and all three calls must send the same SQL text, so they
    // share one cached plan (EF 10's default pads lists to different sizes).
    // Rules: one round trip per call; the same SQL for every list size.
    protected override Task<List<ProductRef>> Ex4(AdventureWorksContext db, List<int> productIds)
        => throw new NotImplementedException();

    // Exercise 5 — Top three per territory
    // For orders with from <= OrderDate < to, return each territory's 3 largest orders by TotalDue
    // (ties broken by SalesOrderId), ordered by Territory, then TotalDue descending, then SalesOrderId.
    // Rules: one round trip.
    protected override Task<List<TerritoryTopOrder>> Ex5(AdventureWorksContext db, DateTime from, DateTime to)
        => throw new NotImplementedException();

    // Challenge 1 — Running total
    // Return customer `customerId`'s orders ordered by OrderDate, then SalesOrderId, each with the
    // running total of TotalDue up to and including that order.
    // Rules: one round trip. (LINQ has no SUM() OVER — see concept 6.)
    protected override Task<List<OrderRunningTotal>> Ch1(AdventureWorksContext db, int customerId)
        => throw new NotImplementedException();

    // Challenge 2 — ROLLUP without ROLLUP
    // For orders with from <= OrderDate < to, return what GROUP BY ROLLUP(Territory.Group,
    // Territory.Name) would return: one row per (Group, Territory) with the total TotalDue, one
    // subtotal row per Group (Territory = null), and one grand-total row (both null). Any order.
    // Rules: one round trip; LINQ only (no raw SQL).
    protected override Task<List<RollupRow>> Ch2(AdventureWorksContext db, DateTime from, DateTime to)
        => throw new NotImplementedException();

    // Challenge 3 — Most expensive, ties included
    // For every product subcategory, return the products whose ListPrice equals the highest ListPrice
    // in that subcategory. All tied products, not just one. Skip products without a subcategory.
    // Order by ProductSubcategoryId, then ProductId.
    // Rules: one round trip.
    protected override Task<List<SubcategoryTopPrice>> Ch3(AdventureWorksContext db)
        => throw new NotImplementedException();
}
```

- [ ] **Step 3: Verify every exercise reports as not attempted**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E03Exercises`
Expected: the build succeeds; `Skipped! - Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

- [ ] **Step 4: Write the solutions, `Solutions.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E03;

// Worked solutions for E03, each followed by a Why note. Run: dotnet test --filter E03Solutions
public class E03Solutions : E03Checks
{
    // Exercise 1 — Territory scoreboard
    // For orders with from <= OrderDate < to, return one TerritorySales row per territory:
    // territory Name, number of orders, number of distinct customers, and sum of TotalDue;
    // ordered by Total descending.
    // Rules: one round trip; grouped and counted in SQL (GROUP BY, COUNT(DISTINCT ...)).
    protected override Task<List<TerritorySales>> Ex1(AdventureWorksContext db, DateTime from, DateTime to)
        => db.SalesOrderHeaders
            .Where(h => h.OrderDate >= from && h.OrderDate < to)
            .GroupBy(h => h.Territory!.Name)
            .Select(g => new
            {
                Territory = g.Key,
                Orders = g.Count(),
                Customers = g.Select(h => h.CustomerId).Distinct().Count(),
                Total = g.Sum(h => h.TotalDue),
            })
            .OrderByDescending(x => x.Total)
            .Select(x => new TerritorySales(x.Territory, x.Orders, x.Customers, x.Total))
            .ToListAsync();
    // Why: grouping by a navigation property makes EF join SalesTerritory and GROUP BY its Name. Inside the
    // group, Select(...).Distinct().Count() becomes COUNT(DISTINCT CustomerID). The half-open date range
    // (>= from, < to) is the pattern from T-SQL lesson 02; E06 shows why it beats OrderDate.Year == 2013.
    // Tempting-but-wrong: loading the year's orders and grouping them in memory, which ships ~14,000 rows
    // to produce 10.

    // Exercise 2 — Loyal customers
    // Return customers with at least `minOrders` orders as CustomerOrderMix: CustomerId, Total
    // orders, Online orders (OnlineOrderFlag) and Offline orders; ordered by Total descending,
    // then CustomerId.
    // Rules: one round trip; the minimum is applied in SQL (HAVING).
    protected override Task<List<CustomerOrderMix>> Ex2(AdventureWorksContext db, int minOrders)
        => db.SalesOrderHeaders
            .GroupBy(h => h.CustomerId)
            .Where(g => g.Count() >= minOrders)
            .Select(g => new
            {
                CustomerId = g.Key,
                Total = g.Count(),
                Online = g.Count(h => h.OnlineOrderFlag),
                Offline = g.Count(h => !h.OnlineOrderFlag),
            })
            .OrderByDescending(x => x.Total).ThenBy(x => x.CustomerId)
            .Select(x => new CustomerOrderMix(x.CustomerId, x.Total, x.Online, x.Offline))
            .ToListAsync();
    // Why: a Where between GroupBy and Select filters groups, so it becomes HAVING COUNT(*) >= @minOrders.
    // Count(predicate) becomes COUNT(CASE WHEN ... THEN 1 END): three counts in one pass over the table.
    // Tempting-but-wrong: Where(g => ...) after the Select works too, but filtering the groups before
    // projecting says what you mean and gives the same HAVING.

    // Exercise 3 — Never ordered
    // Return finished-goods products (FinishedGoodsFlag) that appear on no SalesOrderDetail,
    // ordered by ProductId. Note: Product has no navigation to SalesOrderDetail.
    // Rules: one round trip; the SQL uses EXISTS, never COUNT.
    protected override Task<List<ProductRef>> Ex3(AdventureWorksContext db)
        => db.Products
            .Where(p => p.FinishedGoodsFlag && !db.SalesOrderDetails.Any(d => d.ProductId == p.ProductId))
            .OrderBy(p => p.ProductId)
            .Select(p => new ProductRef(p.ProductId, p.Name))
            .ToListAsync();
    // Why: with no navigation, reference the other DbSet inside the lambda. EF turns !Any(...) into a
    // correlated NOT EXISTS, which stops probing a product at its first order line.
    // Tempting-but-wrong: db.SalesOrderDetails.Count(d => d.ProductId == p.ProductId) == 0 counts every line
    // just to compare with zero. Loading the ordered IDs first and using Contains is an extra round trip.

    // Exercise 4 — One plan for every list
    // Return the products whose ProductId is in `productIds`, ordered by ProductId. The check calls
    // this with lists of 3, 7 and 12 IDs, and all three calls must send the same SQL text, so they
    // share one cached plan (EF 10's default pads lists to different sizes).
    // Rules: one round trip per call; the same SQL for every list size.
    protected override Task<List<ProductRef>> Ex4(AdventureWorksContext db, List<int> productIds)
        => db.Products
            .Where(p => EF.Parameter(productIds).Contains(p.ProductId))
            .OrderBy(p => p.ProductId)
            .Select(p => new ProductRef(p.ProductId, p.Name))
            .ToListAsync();
    // Why: EF.Parameter sends the whole list as one JSON string and reads it with OPENJSON, so the SQL text
    // never depends on the list's length. The trade-off is that the optimizer can't see the values and has
    // to guess the row count. The default (3 parameters, then 10, then 20) gives it more to work with, at
    // the price of one plan per bucket.
    // Tempting-but-wrong: EF.Constant(productIds) inlines the IDs, which means one SQL text and one plan
    // per distinct list.

    // Exercise 5 — Top three per territory
    // For orders with from <= OrderDate < to, return each territory's 3 largest orders by TotalDue
    // (ties broken by SalesOrderId), ordered by Territory, then TotalDue descending, then SalesOrderId.
    // Rules: one round trip.
    protected override Task<List<TerritoryTopOrder>> Ex5(AdventureWorksContext db, DateTime from, DateTime to)
        => db.SalesTerritories
            .SelectMany(t => t.SalesOrderHeaders
                .Where(h => h.OrderDate >= from && h.OrderDate < to)
                .OrderByDescending(h => h.TotalDue).ThenBy(h => h.SalesOrderId)
                .Take(3)
                .Select(h => new { Territory = t.Name, h.SalesOrderId, h.TotalDue }))
            .OrderBy(x => x.Territory).ThenByDescending(x => x.TotalDue).ThenBy(x => x.SalesOrderId)
            .Select(x => new TerritoryTopOrder(x.Territory, x.SalesOrderId, x.TotalDue))
            .ToListAsync();
    // Why: SelectMany over the territory's orders with Take(3) becomes CROSS APPLY (SELECT TOP(3) ...), the
    // APPLY pattern from T-SQL lesson 18, in one round trip. Grouping orders by TerritoryId and taking 3 per
    // group is equally valid; EF translates that shape with ROW_NUMBER() instead.
    // Tempting-but-wrong: a loop that queries each territory's top 3 is N+1 round trips.

    // Challenge 1 — Running total
    // Return customer `customerId`'s orders ordered by OrderDate, then SalesOrderId, each with the
    // running total of TotalDue up to and including that order.
    // Rules: one round trip. (LINQ has no SUM() OVER — see concept 6.)
    protected override async Task<List<OrderRunningTotal>> Ch1(AdventureWorksContext db, int customerId)
    {
        var orders = await db.SalesOrderHeaders
            .Where(h => h.CustomerId == customerId)
            .OrderBy(h => h.OrderDate).ThenBy(h => h.SalesOrderId)
            .Select(h => new { h.SalesOrderId, h.OrderDate, h.TotalDue })
            .ToListAsync();

        var running = 0m;
        return orders
            .Select(o => new OrderRunningTotal(o.SalesOrderId, o.OrderDate, o.TotalDue, running += o.TotalDue))
            .ToList();
    }
    // Why: SQL does the filtering and ordering, and C# finishes the running sum in one pass. That's fine
    // for 28 rows. A correlated subquery (sum of every earlier order, for each order) also works in one
    // round trip, but it's O(n²) on the server. For big sets, write the window function in raw SQL (E04
    // composes LINQ on top of it).

    // Challenge 2 — ROLLUP without ROLLUP
    // For orders with from <= OrderDate < to, return what GROUP BY ROLLUP(Territory.Group,
    // Territory.Name) would return: one row per (Group, Territory) with the total TotalDue, one
    // subtotal row per Group (Territory = null), and one grand-total row (both null). Any order.
    // Rules: one round trip; LINQ only (no raw SQL).
    protected override Task<List<RollupRow>> Ch2(AdventureWorksContext db, DateTime from, DateTime to)
    {
        var orders = db.SalesOrderHeaders.Where(h => h.OrderDate >= from && h.OrderDate < to);

        var perTerritory = orders
            .GroupBy(h => new { h.Territory!.Group, h.Territory.Name })
            .Select(g => new { Group = (string?)g.Key.Group, Territory = (string?)g.Key.Name, Total = g.Sum(h => h.TotalDue) });
        var perGroup = orders
            .GroupBy(h => h.Territory!.Group)
            .Select(g => new { Group = (string?)g.Key, Territory = (string?)null, Total = g.Sum(h => h.TotalDue) });
        var grandTotal = orders
            .GroupBy(h => 1)
            .Select(g => new { Group = (string?)null, Territory = (string?)null, Total = g.Sum(h => h.TotalDue) });

        return perTerritory.Concat(perGroup).Concat(grandTotal)
            .Select(x => new RollupRow(x.Group, x.Territory, x.Total))
            .ToListAsync();
    }
    // Why: ROLLUP(a, b) is GROUP BY (a, b), then GROUP BY a, then the grand total, stacked with UNION ALL.
    // Concat sends all three as one statement. The three shapes must match exactly, hence the (string?)
    // casts on the null columns. Real ROLLUP reads the table once, while this reads it three times, so it's
    // fine at this size and a reason to reach for raw SQL (E04) at scale.

    // Challenge 3 — Most expensive, ties included
    // For every product subcategory, return the products whose ListPrice equals the highest ListPrice
    // in that subcategory. All tied products, not just one. Skip products without a subcategory.
    // Order by ProductSubcategoryId, then ProductId.
    // Rules: one round trip.
    protected override Task<List<SubcategoryTopPrice>> Ch3(AdventureWorksContext db)
        => db.Products
            .Where(p => p.ProductSubcategory != null
                && p.ListPrice == p.ProductSubcategory.Products.Max(x => x.ListPrice))
            .OrderBy(p => p.ProductSubcategoryId).ThenBy(p => p.ProductId)
            .Select(p => new SubcategoryTopPrice(p.ProductSubcategoryId!.Value, p.ProductId, p.ListPrice))
            .ToListAsync();
    // Why: comparing each product with its subcategory's MAX (a correlated subquery) keeps every product at
    // the top price. That's what RANK() = 1 does in T-SQL lesson 07.
    // Tempting-but-wrong: GroupBy(p => p.ProductSubcategoryId)
    //     .Select(g => g.OrderByDescending(p => p.ListPrice).Select(p => new { ... }).First()).
    // It translates (ROW_NUMBER() ... <= 1) but keeps one product per subcategory and silently drops the
    // ties: 37 rows instead of 94.
}
```

- [ ] **Step 5: Verify the solutions pass**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E03Solutions`
Expected: the build succeeds; `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 6: Write the worked examples, `Examples.cs`, and run them**

```csharp
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E03;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E03Examples --logger "console;verbosity=detailed"
public class E03Examples : LabTest
{
    static readonly DateTime From = new(2013, 1, 1);
    static readonly DateTime To = new(2014, 1, 1);

    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: key + aggregates in the projection -> GROUP BY.
    [Fact]
    public async Task GroupBy_with_aggregates_is_GROUP_BY()
    {
        var groups = await Db.SalesOrderHeaders
            .Where(h => h.OrderDate >= From && h.OrderDate < To)
            .GroupBy(h => h.Territory!.Group)
            .Select(g => new { Group = g.Key, Orders = g.Count(), Total = g.Sum(h => h.TotalDue) })
            .ToListAsync();

        Assert.Equal(3, groups.Count); // North America, Europe, Pacific
        Assert.Contains("GROUP BY [s0].[Group]", Sql.All);
    }

    // Concept 1: a predicate inside Count/Sum becomes CASE inside the aggregate.
    [Fact]
    public async Task Conditional_aggregates_become_CASE()
    {
        var mix = await Db.SalesOrderHeaders
            .GroupBy(h => 1)
            .Select(g => new
            {
                Online = g.Count(h => h.OnlineOrderFlag),
                Offline = g.Count(h => !h.OnlineOrderFlag),
                OnlineTotal = g.Sum(h => h.OnlineOrderFlag ? h.TotalDue : 0),
            })
            .SingleAsync();

        Print(mix);
        Assert.Equal(31465, mix.Online + mix.Offline);
        Assert.Contains("COUNT(CASE", Sql.All);
    }

    // Concept 1: Where after GroupBy filters groups -> HAVING.
    [Fact]
    public async Task Where_after_GroupBy_is_HAVING()
    {
        var loyal = await Db.SalesOrderHeaders
            .GroupBy(h => h.CustomerId)
            .Where(g => g.Count() >= 25)
            .Select(g => new { CustomerId = g.Key, Orders = g.Count() })
            .ToListAsync();

        Assert.Equal(14, loyal.Count);
        Assert.Contains("HAVING COUNT(*) >= 25", Sql.All);
    }

    // Concept 2: returning the groups themselves: ORDER BY + grouping on the client.
    [Fact]
    public async Task GroupBy_as_last_operator_groups_on_the_client()
    {
        var byColor = await Db.Products.Where(p => p.Color != null).GroupBy(p => p.Color).ToListAsync();

        Assert.Equal(9, byColor.Count);
        Assert.DoesNotContain("GROUP BY", Sql.All);
        Assert.Contains("ORDER BY [p].[Color]", Sql.All);
    }

    // Concept 3: an aggregate over a navigation is a correlated scalar subquery.
    [Fact]
    public async Task Navigation_aggregate_is_a_correlated_subquery()
    {
        var customers = await Db.Customers
            .Where(c => c.CustomerId < 11005)
            .Select(c => new { c.CustomerId, Orders = c.SalesOrderHeaders.Count() })
            .ToListAsync();

        Print(string.Join(", ", customers));
        Assert.Contains("WHERE [c].[CustomerID] = [s].[CustomerID]) AS [Orders]", Sql.All);
    }

    // Concept 3: Any -> EXISTS, !Any -> NOT EXISTS, All -> NOT EXISTS of the negated predicate.
    [Fact]
    public async Task Any_and_All_are_EXISTS_and_NOT_EXISTS()
    {
        var neverOrdered = await Db.Products
            .CountAsync(p => p.FinishedGoodsFlag && !Db.SalesOrderDetails.Any(d => d.ProductId == p.ProductId));
        var allPricey = await Db.ProductSubcategories
            .Where(s => s.Products.All(p => p.ListPrice > 100))
            .Select(s => s.Name)
            .ToListAsync();

        Print($"{neverOrdered} finished goods never ordered; all over $100: {string.Join(", ", allPricey)}");
        Assert.Contains("NOT EXISTS", Sql.Commands[0]);
        Assert.Contains("NOT EXISTS", Sql.Commands[1]);
        Assert.Contains("<= 100.0", Sql.Commands[1]); // All(> 100) became "no product <= 100"
    }

    // Concept 3: Contains over another query -> IN (SELECT ...).
    [Fact]
    public async Task Contains_on_a_subquery_is_IN()
    {
        var bulkOrdered = Db.SalesOrderDetails.Where(d => d.OrderQty > 30).Select(d => d.ProductId);
        await Db.Products.Where(p => bulkOrdered.Contains(p.ProductId)).Select(p => p.Name).ToListAsync();

        Assert.Matches(@"IN \(\s+SELECT", Sql.All);
        Sql.AssertRoundTrips(1);
    }

    // Concept 4: EF 10 pads list parameters; EF.Parameter forces OPENJSON, EF.Constant inlines.
    [Fact]
    public async Task Parameterized_collections_are_padded()
    {
        var five = new List<int> { 1, 2, 3, 4, 5 };
        var six = new List<int> { 1, 2, 3, 4, 5, 6 };
        var eleven = Enumerable.Range(1, 11).ToList();
        var many = Enumerable.Range(1, 3000).ToList();

        await Db.Products.CountAsync(p => five.Contains(p.ProductId));
        await Db.Products.CountAsync(p => six.Contains(p.ProductId));
        await Db.Products.CountAsync(p => eleven.Contains(p.ProductId));
        await Db.Products.CountAsync(p => many.Contains(p.ProductId));
        await Db.Products.CountAsync(p => EF.Parameter(six).Contains(p.ProductId));
        await Db.Products.CountAsync(p => EF.Constant(six).Contains(p.ProductId));

        Assert.Contains("@five5)", Sql.Commands[0]);      // exactly five parameters
        Assert.Contains("@six10)", Sql.Commands[1]);      // six values, padded to ten parameters
        Assert.Contains("@eleven20)", Sql.Commands[2]);   // eleven values, padded to twenty
        Assert.Contains("OPENJSON(@many)", Sql.Commands[3]); // too many for parameters: one JSON parameter
        Assert.Contains("OPENJSON(@six)", Sql.Commands[4]);
        Assert.Contains("IN (1, 2, 3, 4, 5, 6)", Sql.Commands[5]);
    }

    // Concept 5: the two top-N-per-group shapes.
    [Fact]
    public async Task Top_N_per_group_CROSS_APPLY_or_ROW_NUMBER()
    {
        await Db.SalesTerritories
            .SelectMany(t => t.SalesOrderHeaders
                .Where(h => h.OrderDate >= From && h.OrderDate < To)
                .OrderByDescending(h => h.TotalDue)
                .Take(3)
                .Select(h => new { t.Name, h.SalesOrderId, h.TotalDue }))
            .ToListAsync();

        await Db.SalesOrderHeaders
            .Where(h => h.OrderDate >= From && h.OrderDate < To)
            .GroupBy(h => h.TerritoryId)
            .Select(g => g.OrderByDescending(h => h.TotalDue).Take(3).Select(h => h.SalesOrderId))
            .ToListAsync();

        Assert.Contains("CROSS APPLY", Sql.Commands[0]);
        Assert.Contains("ROW_NUMBER() OVER(PARTITION BY [s0].[TerritoryID] ORDER BY [s0].[TotalDue] DESC)", Sql.Commands[1]);
    }

    // Concept 6: no LINQ for OVER(...). Fetch ordered rows once, finish the running total in C#.
    [Fact]
    public async Task Window_functions_have_no_LINQ_form()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.Products.OrderBy(p => p.ProductId).Select((p, index) => new { p.Name, index }).ToListAsync());

        var orders = await Db.SalesOrderHeaders
            .Where(h => h.CustomerId == 11091)
            .OrderBy(h => h.OrderDate).ThenBy(h => h.SalesOrderId)
            .Select(h => new { h.SalesOrderId, h.TotalDue })
            .ToListAsync();
        var running = 0m;
        var withRunningTotal = orders.Select(o => new { o.SalesOrderId, Running = running += o.TotalDue }).ToList();

        Assert.Equal(28, withRunningTotal.Count);
        Sql.AssertRoundTrips(1);
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E03Examples`
Expected: the build succeeds; `Passed!  - Failed: 0, Passed: 10, Skipped: 0, Total: 10`.

- [ ] **Step 7: Write the lesson text, `README.md`**

````markdown
# E03 — Grouping, Subqueries & Where LINQ Runs Out

## What you'll learn

- Which `GroupBy` shapes become `GROUP BY`, including conditional aggregates and `HAVING`
- What happens when `GroupBy` is the last operator (spoiler: no `GROUP BY` at all)
- How subqueries appear: correlated scalar subqueries, `EXISTS` / `NOT EXISTS`, `IN (SELECT ...)`
- How EF 10 sends a list parameter (`ids.Contains(x)`), and how to choose a different translation
- Top-N per group: the two shapes EF generates (`CROSS APPLY` and `ROW_NUMBER()`)
- What LINQ can't express at all — window functions, `ROLLUP`, recursive CTEs — and what to do instead

## Builds on

- T-SQL [lesson 05](../../../lessons/05-aggregations-and-grouping/lesson.ipynb) — `GROUP BY`, `HAVING`, `ROLLUP`
- T-SQL [lesson 06](../../../lessons/06-subqueries-and-ctes/lesson.ipynb) — correlated subqueries, `EXISTS` vs `IN`, recursive CTEs
- T-SQL [lesson 07](../../../lessons/07-window-functions/lesson.ipynb) — `ROW_NUMBER`, `RANK`, running totals
- Cheatsheet: [`04-window-functions.md`](../../../cheatsheets/04-window-functions.md)

## Setup

```
dotnet test --filter E03Examples --logger "console;verbosity=detailed"
```

## Concepts

### 1. GroupBy that becomes GROUP BY

`GroupBy` translates when the projection uses only the key and aggregates over the group:

```csharp
db.SalesOrderHeaders
    .GroupBy(h => h.Territory!.Group)
    .Select(g => new { Group = g.Key, Orders = g.Count(), Total = g.Sum(h => h.TotalDue) })
```

| LINQ inside the group | SQL |
|---|---|
| `g.Count()`, `g.Sum(x)`, `g.Average(x)`, `g.Min(x)`, `g.Max(x)` | `COUNT(*)`, `SUM`, `AVG`, `MIN`, `MAX` |
| `g.Count(h => h.OnlineOrderFlag)` | `COUNT(CASE WHEN ... THEN 1 END)` — a conditional aggregate |
| `g.Select(h => h.CustomerId).Distinct().Count()` | `COUNT(DISTINCT ...)` |
| `.Where(g => g.Count() > 20)` after `GroupBy` | `HAVING COUNT(*) > 20` |

You can group by a navigation (`h.Territory!.Group`) or by an anonymous key (`new { h.Territory!.Group, h.Territory.Name }`); EF adds the join. Group by a constant (`GroupBy(h => 1)`) to aggregate the whole set into one row.

Examples: `GroupBy_with_aggregates_is_GROUP_BY`, `Conditional_aggregates_become_CASE`, `Where_after_GroupBy_is_HAVING`.

### 2. GroupBy as the last operator

`db.Products.GroupBy(p => p.Color).ToListAsync()` returns the groups themselves, and SQL has no type for "a group of rows". EF translates it as `ORDER BY Color`, streams **every** row back, and builds the groups on the client. That's fine for small sets, but it's not an aggregation. If you only need counts or sums, select them inside the query.

Example: `GroupBy_as_last_operator_groups_on_the_client`.

### 3. Subqueries

| LINQ | SQL |
|---|---|
| `c.SalesOrderHeaders.Count()` inside a `Select` | correlated scalar subquery `(SELECT COUNT(*) ... WHERE [c].[CustomerID] = [s].[CustomerID])` |
| `.Any(predicate)` on a related set | `EXISTS (SELECT 1 ...)` |
| `!x.Any(...)` | `NOT EXISTS (...)` |
| `.All(predicate)` | `NOT EXISTS (... WHERE NOT predicate)` |
| `otherQuery.Select(o => o.Id).Contains(x.Id)` | `x.Id IN (SELECT ...)` |

Use a navigation when the model has one. When it doesn't, reference another `DbSet` inside the lambda (`!db.SalesOrderDetails.Any(d => d.ProductId == p.ProductId)`) and EF still produces a correlated subquery. `Product` has no navigation to `SalesOrderDetail`, because the order line's foreign key points at `SpecialOfferProduct`.

Examples: `Navigation_aggregate_is_a_correlated_subquery`, `Any_and_All_are_EXISTS_and_NOT_EXISTS`, `Contains_on_a_subquery_is_IN`.

### 4. List parameters (`ids.Contains(x)`)

A `List<int>` used in `Contains` is a *parameterized collection*. EF 10's default sends one parameter per element and pads the list to a bucket size, repeating the last value. Similar-sized lists then share a SQL text and a cached plan:

| Elements | SQL (EF 10 default) |
|---|---|
| 1 | `= @ids1` |
| 2–5 | `IN (@ids1, ..., @idsN)` — exact |
| 6–10 | `IN (@ids1, ..., @ids10)` — padded to 10 |
| 11–20 | padded to 20 (and so on) |
| too many for parameters (3,000 in our test) | one JSON parameter read with `OPENJSON` |

Override per query:

- `EF.Parameter(ids).Contains(x)` — always one JSON parameter and `OPENJSON`. One SQL text for every list size, but the optimizer can't see the values, so its row estimate is a guess.
- `EF.Constant(ids).Contains(x)` — values inlined as literals: `IN (1, 2, 3)`. The optimizer sees everything, and every distinct list compiles a new plan.

Example: `Parameterized_collections_are_padded`.

### 5. Top-N per group

"The 3 biggest orders per territory" has two LINQ shapes, and EF translates each differently:

| LINQ | SQL |
|---|---|
| `db.SalesTerritories.SelectMany(t => t.SalesOrderHeaders.OrderByDescending(...).Take(3) ...)` | `CROSS APPLY (SELECT TOP(3) ...)` |
| `orders.GroupBy(h => h.TerritoryId).Select(g => g.OrderByDescending(...).Take(3))` | `ROW_NUMBER() OVER(PARTITION BY ...)` filtered to `row <= 3` |

Both are a single round trip. `Take(1)` per group picks one row even when several tie. For "the top value, ties included", compare against the group's `Max` instead (challenge 3).

Example: `Top_N_per_group_CROSS_APPLY_or_ROW_NUMBER`.

### 6. Where LINQ runs out

EF Core 10 has no LINQ form for:

- window functions with an `OVER` clause you write yourself — running totals, `LAG`/`LEAD`, `RANK` (EF uses `ROW_NUMBER` internally, but you can't ask for it), and the index overload `Select((x, i) => ...)`
- `GROUPING SETS`, `ROLLUP`, `CUBE`, `PIVOT`
- recursive CTEs

Your options, from simplest:

1. **Finish on the client.** Fetch the ordered, filtered rows in one query, then compute the running total in C#. Fine when the result is small.
2. **Emulate in LINQ.** `ROLLUP` is a `Concat` (`UNION ALL`) of grouped queries at each level, sent in one round trip.
3. **Write the SQL.** Raw SQL with the window function, composed with LINQ where possible (E04).

Example: `Window_functions_have_no_LINQ_form`.

## Pitfalls

- **Fetching rows to aggregate them in memory.** `(await q.ToListAsync()).GroupBy(...)` ships every row. Aggregate in the query.
- **`Count(...) == 0` instead of `!Any(...)`.** It counts every match just to check that there are none.
- **`Take(1)` per group when ties matter.** It silently drops tied rows.
- **N+1 per group.** Looping over territories and querying each one's top orders is N+1 round trips for something SQL does in one.

## Exercises

```
dotnet test --filter E03Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
````

- [ ] **Step 8: Verify the prompts are identical in `Exercises.cs` and `Solutions.cs`**

```bash
cd efcore && python - lessons/E03-grouping-subqueries-limits <<'EOF'
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

- [ ] **Step 9: Commit**

```bash
git add efcore/lessons/E03-grouping-subqueries-limits
git commit -m "feat: add EF Core lesson E03 - grouping, subqueries and where LINQ runs out"
```

---

## Task 6: Lesson E04 — Raw SQL, views, functions & procedures

**Files:**
- Create: `efcore/lessons/E04-raw-sql-views-functions-procs/E04Context.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `LabTest<TContext>`, `Sql`, `Reference.Rows<T>`, `[Exercise]` (Task 2); `AdventureWorksContext` constructor and `OnModelCreating` (Task 1); DbSets `People`, `Products`, `VSalesPeople`.
- Produces (lesson-internal): `E04Context(DbContextOptions<AdventureWorksContext>)`, `IQueryable<ContactInformation> GetContactInformation(int personId)` (maps `dbo.ufnGetContactInformation`), `static int GetStock(int productId)` (maps `dbo.ufnGetStock`). It's the template for every later lesson that needs model changes.

- [ ] **Step 1: Write the lesson's derived context, `E04Context.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E04;

// One row returned by dbo.ufnGetContactInformation. Keyless: a function returns rows, not entities.
public class ContactInformation
{
    public int PersonId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? JobTitle { get; set; }
    public string? BusinessEntityType { get; set; }
}

// E04's model: the shared AdventureWorks model plus two function mappings. A derived context gets
// its own cached model, so these mappings never leak into other lessons. Model/ stays untouched.
public class E04Context(DbContextOptions<AdventureWorksContext> options) : AdventureWorksContext(options)
{
    // dbo.ufnGetContactInformation(@PersonID) is table-valued: it returns IQueryable and composes with LINQ.
    public IQueryable<ContactInformation> GetContactInformation(int personId)
        => FromExpression(() => GetContactInformation(personId));

    // dbo.ufnGetStock(@ProductID) is scalar. The body never runs: EF translates calls inside a query.
    public static int GetStock(int productId)
        => throw new NotSupportedException("GetStock can only be used inside a LINQ query.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContactInformation>().HasNoKey();
        modelBuilder.HasDbFunction(typeof(E04Context).GetMethod(nameof(GetContactInformation), [typeof(int)])!)
            .HasName("ufnGetContactInformation").HasSchema("dbo");
        modelBuilder.HasDbFunction(typeof(E04Context).GetMethod(nameof(GetStock), [typeof(int)])!)
            .HasName("ufnGetStock").HasSchema("dbo");
    }
}
```

- [ ] **Step 2: Write the checks, `Checks.cs`**

```csharp
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E04;

public record PersonName(int BusinessEntityId, string FirstName, string LastName);
public record CustomerOrderTotal(int CustomerId, int SalesOrderId, DateTime OrderDate, decimal TotalDue, decimal RunningTotal);
public record OrderRunningTotal(int SalesOrderId, DateTime OrderDate, decimal TotalDue, decimal RunningTotal);
public record PersonContact(int PersonId, string? JobTitle, string? BusinessEntityType);
public record ProductStock(int ProductId, string Name, int Stock);
public record ManagerName(int RecursionLevel, string ManagerFirstName, string ManagerLastName);
public record ProductPrice(int ProductId, decimal ListPrice);
public record BomComponent(int ComponentId, int Level);
public record ProductRef(int ProductId, string Name);

// The checks for E04. E04Exercises (yours) and E04Solutions both inherit them.
// This lesson's contexts are E04Context: the shared model plus two function mappings.
public abstract class E04Checks : LabTest<E04Context>
{
    protected abstract Task<List<PersonName>> Ex1(E04Context db, string lastName);
    protected abstract Task<List<OrderRunningTotal>> Ex2(E04Context db, int customerId);
    protected abstract Task<List<PersonContact>> Ex3(E04Context db, List<int> personIds);
    protected abstract Task<List<ProductStock>> Ex4(E04Context db, int top);
    protected abstract Task<List<string>> Ex5(E04Context db, int employeeId);
    protected abstract Task<List<ProductPrice>> Ch1(E04Context db, int subcategoryId, decimal factor);
    protected abstract Task<List<BomComponent>> Ch2(E04Context db, int productId, int minLevel);
    protected abstract Task<List<ProductRef>> Ch3(E04Context db, string sortColumn);

    [Exercise]
    public async Task Ex1_SoundsLike()
    {
        Assert.Equal(await Reference.Rows<PersonName>($"""
            SELECT BusinessEntityID AS BusinessEntityId, FirstName, LastName
            FROM Person.Person
            WHERE SOUNDEX(LastName) = SOUNDEX({"Smyth"})
            ORDER BY LastName, FirstName, BusinessEntityID
            """), await Ex1(Db, "Smyth"));

        var hostile = "x' OR 1=1 --";
        Assert.Equal(await Reference.Rows<PersonName>($"""
            SELECT BusinessEntityID AS BusinessEntityId, FirstName, LastName
            FROM Person.Person
            WHERE SOUNDEX(LastName) = SOUNDEX({hostile})
            ORDER BY LastName, FirstName, BusinessEntityID
            """), await Ex1(Db, hostile)); // the input stays data, never SQL

        Sql.AssertRoundTrips(2);
        Assert.All(Sql.Commands, sql => Assert.Contains("SOUNDEX", sql));
        Assert.DoesNotContain("OR 1=1", Sql.All);
    }

    [Exercise]
    public async Task Ex2_RunningTotalInSql()
    {
        var result = await Ex2(Db, 11091);

        Assert.Equal(await Reference.Rows<OrderRunningTotal>($"""
            SELECT SalesOrderID AS SalesOrderId, OrderDate, TotalDue,
                   SUM(TotalDue) OVER (ORDER BY OrderDate, SalesOrderID ROWS UNBOUNDED PRECEDING) AS RunningTotal
            FROM Sales.SalesOrderHeader
            WHERE CustomerID = {11091}
            ORDER BY OrderDate, SalesOrderID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("OVER", Sql.All);         // computed by SQL Server
        Assert.Contains("PARTITION BY", Sql.All); // ...for every customer,
        Assert.Contains("FROM (", Sql.All);       // ...with the customer filter composed on top in LINQ
    }

    [Exercise]
    public async Task Ex3_ContactsViaFunction()
    {
        var ids = new List<int> { 1, 291, 1491, 1699 }; // an employee, a store contact, a vendor contact, a consumer
        var csv = string.Join(",", ids);

        var result = await Ex3(Db, ids);

        Assert.Equal(await Reference.Rows<PersonContact>($"""
            SELECT c.PersonID AS PersonId, c.JobTitle, c.BusinessEntityType
            FROM Person.Person p
            CROSS APPLY dbo.ufnGetContactInformation(p.BusinessEntityID) c
            WHERE p.BusinessEntityID IN (SELECT CAST(value AS int) FROM STRING_SPLIT({csv}, ','))
            ORDER BY c.PersonID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("CROSS APPLY", Sql.All);
    }

    [Exercise]
    public async Task Ex4_MostStocked()
    {
        var result = await Ex4(Db, 10);

        Assert.Equal(await Reference.Rows<ProductStock>($"""
            SELECT TOP (10) ProductID AS ProductId, Name, dbo.ufnGetStock(ProductID) AS Stock
            FROM Production.Product
            ORDER BY Stock DESC, ProductID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("[dbo].[ufnGetStock]", Sql.All);
        Assert.Contains("TOP(", Sql.All); // the top-N cut happens in SQL
    }

    [Exercise]
    public async Task Ex5_ManagementChain()
    {
        var result = await Ex5(Db, 100);

        var expected = (await Reference.Rows<ManagerName>($"EXEC dbo.uspGetEmployeeManagers @BusinessEntityID = {100}"))
            .OrderBy(m => m.RecursionLevel)
            .Select(m => $"{m.ManagerFirstName} {m.ManagerLastName}")
            .ToList();
        Assert.Equal(expected, result);
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ch1_FreshAfterExecuteSql()
    {
        var tracked = await Db.Products.Where(p => p.ProductSubcategoryId == 1).ToListAsync(); // already in the change tracker
        var before = tracked.ToDictionary(p => p.ProductId, p => p.ListPrice);
        Sql.Reset();

        var result = await Ch1(Db, 1, 1.10m);

        var expected = await Reference.Rows<ProductPrice>($"""
            SELECT ProductID AS ProductId, ListPrice FROM Production.Product
            WHERE ProductSubcategoryID = 1
            ORDER BY ProductID
            """);
        Assert.All(expected, e => Assert.NotEqual(before[e.ProductId], e.ListPrice)); // the UPDATE ran
        Assert.Equal(expected, result);                                              // ...and you returned the new prices
        Sql.AssertRoundTrips(2);                                                     // one UPDATE, one SELECT
    }

    [Exercise]
    public async Task Ch2_BillOfMaterials()
    {
        var result = await Ch2(Db, 800, 2);

        Assert.Equal(await Reference.Rows<BomComponent>($"""
            WITH Bom AS (
                SELECT ComponentID, 1 AS Lvl FROM Production.BillOfMaterials
                WHERE ProductAssemblyID = {800} AND EndDate IS NULL
                UNION ALL
                SELECT b.ComponentID, Bom.Lvl + 1 FROM Production.BillOfMaterials b
                JOIN Bom ON b.ProductAssemblyID = Bom.ComponentID
                WHERE b.EndDate IS NULL
            )
            SELECT DISTINCT ComponentID AS ComponentId, Lvl AS Level FROM Bom
            WHERE Lvl >= {2}
            ORDER BY Level, ComponentId
            """), result);
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ch3_SortByUserChoice()
    {
        foreach (var column in new[] { "Name", "ListPrice", "ProductNumber" })
        {
            var expected = column switch
            {
                "Name" => await Reference.Rows<ProductRef>($"SELECT TOP (5) ProductID AS ProductId, Name FROM Production.Product ORDER BY Name, ProductID"),
                "ListPrice" => await Reference.Rows<ProductRef>($"SELECT TOP (5) ProductID AS ProductId, Name FROM Production.Product ORDER BY ListPrice, ProductID"),
                _ => await Reference.Rows<ProductRef>($"SELECT TOP (5) ProductID AS ProductId, Name FROM Production.Product ORDER BY ProductNumber, ProductID"),
            };
            Assert.Equal(expected, await Ch3(Db, column));
        }

        await Assert.ThrowsAnyAsync<ArgumentException>(() => Ch3(Db, "Name; DROP TABLE Production.Product --"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Ch3(Db, "Color")); // a real column, but not on the list
        Sql.AssertRoundTrips(3); // rejected choices never reach the database
    }
}
```

- [ ] **Step 3: Write the exercise stubs, `Exercises.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E04;

// E04 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E04Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// `db` is an E04Context: everything in AdventureWorksContext plus GetContactInformation and GetStock.
public class E04Exercises : E04Checks
{
    // Exercise 1 — Sounds like
    // Return the people whose LastName sounds like `lastName` according to SQL Server's SOUNDEX,
    // ordered by LastName, FirstName, then BusinessEntityId. LINQ has no SOUNDEX: write that part in
    // SQL. The check also passes a hostile value ("x' OR 1=1 --"), which must be treated as a name.
    // Rules: one round trip per call; the name is a parameter, never part of the SQL text.
    protected override Task<List<PersonName>> Ex1(E04Context db, string lastName)
        => throw new NotImplementedException();

    // Exercise 2 — Window function in SQL, filter in LINQ
    // Return customer `customerId`'s orders with a running total, as in E03 challenge 1, but let
    // SQL Server compute it: write raw SQL that returns CustomerOrderTotal rows for ALL customers
    // (SUM(...) OVER (PARTITION BY CustomerID ...)), then filter to the one customer and order by
    // OrderDate, SalesOrderId with LINQ composed on top.
    // Rules: one round trip; the customer filter is LINQ, not part of your SQL.
    protected override Task<List<OrderRunningTotal>> Ex2(E04Context db, int customerId)
        => throw new NotImplementedException();

    // Exercise 3 — A table-valued function per row
    // For the people in `personIds`, return their dbo.ufnGetContactInformation rows (mapped as
    // db.GetContactInformation) as PersonContact, ordered by PersonId.
    // Rules: one round trip; the SQL uses CROSS APPLY.
    protected override Task<List<PersonContact>> Ex3(E04Context db, List<int> personIds)
        => throw new NotImplementedException();

    // Exercise 4 — A scalar function in ORDER BY
    // Return the `top` products with the most stock according to dbo.ufnGetStock (mapped as
    // E04Context.GetStock), ties broken by ProductId.
    // Rules: one round trip; both the function call and the top-N cut happen in SQL.
    protected override Task<List<ProductStock>> Ex4(E04Context db, int top)
        => throw new NotImplementedException();

    // Exercise 5 — Up the chain
    // Return "FirstName LastName" of each manager returned by dbo.uspGetEmployeeManagers for
    // employee `employeeId`, nearest manager first (ascending RecursionLevel). ManagerName (Checks.cs)
    // matches the columns you need.
    // Rules: one round trip.
    protected override Task<List<string>> Ex5(E04Context db, int employeeId)
        => throw new NotImplementedException();

    // Challenge 1 — Stale after raw SQL
    // The check has already loaded (and is tracking) the products of `subcategoryId`. Multiply their
    // ListPrice by `factor` with ONE raw SQL command, then return every product in the subcategory with
    // its NEW price, ordered by ProductId.
    // Rules: exactly two round trips: one UPDATE, one SELECT.
    protected override Task<List<ProductPrice>> Ch1(E04Context db, int subcategoryId, decimal factor)
        => throw new NotImplementedException();

    // Challenge 2 — Exploding a bill of materials
    // Return every distinct (ComponentId, Level) needed to build product `productId`, walking
    // Production.BillOfMaterials recursively (current rows only: EndDate IS NULL; the product's direct
    // components are level 1), keeping levels >= `minLevel`, ordered by Level, then ComponentId.
    // Rules: one round trip. (You'll need a recursive CTE. Can you compose LINQ over it?)
    protected override Task<List<BomComponent>> Ch2(E04Context db, int productId, int minLevel)
        => throw new NotImplementedException();

    // Challenge 3 — Sort by the user's choice
    // Return the first 5 products as ProductRef, sorted ascending by the column the user picked
    // (`sortColumn` is "Name", "ListPrice" or "ProductNumber"), ties broken by ProductId. Any other
    // value, including SQL fragments, must throw ArgumentException without touching the database.
    // Rules: one round trip per valid call.
    protected override Task<List<ProductRef>> Ch3(E04Context db, string sortColumn)
        => throw new NotImplementedException();
}
```

- [ ] **Step 4: Verify every exercise reports as not attempted**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E04Exercises`
Expected: the build succeeds; `Skipped! - Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

- [ ] **Step 5: Write the solutions, `Solutions.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E04;

// Worked solutions for E04, each followed by a Why note. Run: dotnet test --filter E04Solutions
public class E04Solutions : E04Checks
{
    // Exercise 1 — Sounds like
    // Return the people whose LastName sounds like `lastName` according to SQL Server's SOUNDEX,
    // ordered by LastName, FirstName, then BusinessEntityId. LINQ has no SOUNDEX: write that part in
    // SQL. The check also passes a hostile value ("x' OR 1=1 --"), which must be treated as a name.
    // Rules: one round trip per call; the name is a parameter, never part of the SQL text.
    protected override Task<List<PersonName>> Ex1(E04Context db, string lastName)
        => db.People
            .FromSql($"SELECT * FROM Person.Person WHERE SOUNDEX(LastName) = SOUNDEX({lastName})")
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.BusinessEntityId)
            .Select(p => new PersonName(p.BusinessEntityId, p.FirstName, p.LastName))
            .ToListAsync();
    // Why: FromSql receives a FormattableString, so {lastName} becomes @p0 and the hostile value is just
    // an odd name that sounds like nothing. Only the part LINQ can't say (SOUNDEX) is SQL. Ordering and
    // projection are LINQ composed on top, which also keeps ORDER BY out of the raw SQL, where it would
    // break the subquery.
    // Tempting-but-wrong: FromSqlRaw("... SOUNDEX('" + lastName + "')"). The hostile input closes the quote
    // and rewrites the query; here it produces a syntax error, and with luckier input it returns other
    // people's rows.

    // Exercise 2 — Window function in SQL, filter in LINQ
    // Return customer `customerId`'s orders with a running total, as in E03 challenge 1, but let
    // SQL Server compute it: write raw SQL that returns CustomerOrderTotal rows for ALL customers
    // (SUM(...) OVER (PARTITION BY CustomerID ...)), then filter to the one customer and order by
    // OrderDate, SalesOrderId with LINQ composed on top.
    // Rules: one round trip; the customer filter is LINQ, not part of your SQL.
    protected override Task<List<OrderRunningTotal>> Ex2(E04Context db, int customerId)
        => db.Database
            .SqlQuery<CustomerOrderTotal>($"""
                SELECT CustomerID AS CustomerId, SalesOrderID AS SalesOrderId, OrderDate, TotalDue,
                       SUM(TotalDue) OVER (PARTITION BY CustomerID ORDER BY OrderDate, SalesOrderID
                                           ROWS UNBOUNDED PRECEDING) AS RunningTotal
                FROM Sales.SalesOrderHeader
                """)
            .Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.OrderDate).ThenBy(x => x.SalesOrderId)
            .Select(x => new OrderRunningTotal(x.SalesOrderId, x.OrderDate, x.TotalDue, x.RunningTotal))
            .ToListAsync();
    // Why: SqlQuery<T> results compose like a table. EF wraps the SQL as a derived table and adds
    // WHERE/ORDER BY outside it. PARTITION BY CustomerID is what makes the outer filter correct: each
    // customer's running total restarts at their first order. Without the partition you'd get a running
    // total across all customers, filtered afterwards. The query is reusable for any customer, and SQL
    // Server can push the CustomerId predicate into the windowed subquery because it's on the partitioning
    // column.

    // Exercise 3 — A table-valued function per row
    // For the people in `personIds`, return their dbo.ufnGetContactInformation rows (mapped as
    // db.GetContactInformation) as PersonContact, ordered by PersonId.
    // Rules: one round trip; the SQL uses CROSS APPLY.
    protected override Task<List<PersonContact>> Ex3(E04Context db, List<int> personIds)
        => db.People
            .Where(p => personIds.Contains(p.BusinessEntityId))
            .SelectMany(p => db.GetContactInformation(p.BusinessEntityId))
            .OrderBy(c => c.PersonId)
            .Select(c => new PersonContact(c.PersonId, c.JobTitle, c.BusinessEntityType))
            .ToListAsync();
    // Why: a mapped TVF is an IQueryable, so SelectMany can call it per outer row, which is exactly what
    // CROSS APPLY means. Everything stays one statement: the filter, the function, the ordering.
    // Tempting-but-wrong: looping over personIds and calling db.GetContactInformation(id) for each, one
    // round trip per person.

    // Exercise 4 — A scalar function in ORDER BY
    // Return the `top` products with the most stock according to dbo.ufnGetStock (mapped as
    // E04Context.GetStock), ties broken by ProductId.
    // Rules: one round trip; both the function call and the top-N cut happen in SQL.
    protected override Task<List<ProductStock>> Ex4(E04Context db, int top)
        => db.Products
            .Select(p => new { p.ProductId, p.Name, Stock = E04Context.GetStock(p.ProductId) })
            .OrderByDescending(x => x.Stock).ThenBy(x => x.ProductId)
            .Take(top)
            .Select(x => new ProductStock(x.ProductId, x.Name, x.Stock))
            .ToListAsync();
    // Why: the static stub is mapped to dbo.ufnGetStock, so EF writes the function call into the SELECT and
    // ORDER BY, and TOP(@p) keeps only `top` rows. The body of GetStock never runs.
    // Tempting-but-wrong: selecting the stock for all 504 products and sorting in memory. One round trip,
    // but it ships 504 rows to keep 10, and it calls the UDF 504 times for nothing.

    // Exercise 5 — Up the chain
    // Return "FirstName LastName" of each manager returned by dbo.uspGetEmployeeManagers for
    // employee `employeeId`, nearest manager first (ascending RecursionLevel). ManagerName (Checks.cs)
    // matches the columns you need.
    // Rules: one round trip.
    protected override async Task<List<string>> Ex5(E04Context db, int employeeId)
    {
        var chain = await db.Database
            .SqlQuery<ManagerName>($"EXEC dbo.uspGetEmployeeManagers @BusinessEntityID = {employeeId}")
            .ToListAsync();

        return chain
            .OrderBy(m => m.RecursionLevel)
            .Select(m => $"{m.ManagerFirstName} {m.ManagerLastName}")
            .ToList();
    }
    // Why: SqlQuery maps the procedure's columns onto ManagerName by name and ignores the ones it doesn't
    // need. EXEC can't be wrapped in a subquery, so ordering happens after materializing, in memory. That's
    // fine for a three-row management chain.
    // Tempting-but-wrong: .OrderBy(m => m.RecursionLevel) before ToListAsync throws "non-composable SQL".

    // Challenge 1 — Stale after raw SQL
    // The check has already loaded (and is tracking) the products of `subcategoryId`. Multiply their
    // ListPrice by `factor` with ONE raw SQL command, then return every product in the subcategory with
    // its NEW price, ordered by ProductId.
    // Rules: exactly two round trips: one UPDATE, one SELECT.
    protected override async Task<List<ProductPrice>> Ch1(E04Context db, int subcategoryId, decimal factor)
    {
        await db.Database.ExecuteSqlAsync(
            $"UPDATE Production.Product SET ListPrice = ListPrice * {factor} WHERE ProductSubcategoryID = {subcategoryId}");

        return await db.Products
            .Where(p => p.ProductSubcategoryId == subcategoryId)
            .OrderBy(p => p.ProductId)
            .Select(p => new ProductPrice(p.ProductId, p.ListPrice))
            .ToListAsync();
    }
    // Why: ExecuteSql goes straight to the database, so the tracked Product instances still hold the old
    // prices. A projection reads column values from the result set and never consults the change tracker,
    // so it sees the new prices. (AsNoTracking() on an entity query works too.)
    // Tempting-but-wrong: db.Products.Where(...).ToListAsync() and mapping afterwards. Identity resolution
    // returns the already-tracked instances, unchanged, with every price stale. Calling Entry(p).Reload()
    // per product fixes it at the cost of one round trip each. E08 shows ExecuteUpdate, the LINQ way to
    // write the same UPDATE.

    // Challenge 2 — Exploding a bill of materials
    // Return every distinct (ComponentId, Level) needed to build product `productId`, walking
    // Production.BillOfMaterials recursively (current rows only: EndDate IS NULL; the product's direct
    // components are level 1), keeping levels >= `minLevel`, ordered by Level, then ComponentId.
    // Rules: one round trip. (You'll need a recursive CTE. Can you compose LINQ over it?)
    protected override Task<List<BomComponent>> Ch2(E04Context db, int productId, int minLevel)
        => db.Database
            .SqlQuery<BomComponent>($"""
                WITH Bom AS (
                    SELECT ComponentID, 1 AS Lvl FROM Production.BillOfMaterials
                    WHERE ProductAssemblyID = {productId} AND EndDate IS NULL
                    UNION ALL
                    SELECT b.ComponentID, Bom.Lvl + 1 FROM Production.BillOfMaterials b
                    JOIN Bom ON b.ProductAssemblyID = Bom.ComponentID
                    WHERE b.EndDate IS NULL
                )
                SELECT DISTINCT ComponentID AS ComponentId, Lvl AS Level FROM Bom
                WHERE Lvl >= {minLevel}
                ORDER BY Level, ComponentId
                """)
            .ToListAsync();
    // Why: no, you can't compose over it. A CTE can't sit inside a derived table, so EF refuses to compose
    // over SQL that starts with WITH ("non-composable SQL"). The filter, DISTINCT and ORDER BY therefore
    // live inside the SQL, parameterized like any other value, and the query runs exactly as written.
    // Materializing everything and filtering in memory also works, but ships the levels you don't want.

    // Challenge 3 — Sort by the user's choice
    // Return the first 5 products as ProductRef, sorted ascending by the column the user picked
    // (`sortColumn` is "Name", "ListPrice" or "ProductNumber"), ties broken by ProductId. Any other
    // value, including SQL fragments, must throw ArgumentException without touching the database.
    // Rules: one round trip per valid call.
    protected override Task<List<ProductRef>> Ch3(E04Context db, string sortColumn)
    {
        IOrderedQueryable<Product> ordered = sortColumn switch
        {
            "Name" => db.Products.OrderBy(p => p.Name),
            "ListPrice" => db.Products.OrderBy(p => p.ListPrice),
            "ProductNumber" => db.Products.OrderBy(p => p.ProductNumber),
            _ => throw new ArgumentException($"Cannot sort by '{sortColumn}'.", nameof(sortColumn)),
        };
        return ordered.ThenBy(p => p.ProductId)
            .Take(5)
            .Select(p => new ProductRef(p.ProductId, p.Name))
            .ToListAsync();
    }
    // Why: parameters carry values, never identifiers, so `ORDER BY @column` can't work, and concatenating
    // the user's text into ORDER BY is injection. Mapping each allowed choice to a lambda means user input
    // never becomes SQL text, and anything else fails before a connection is touched. If you need raw SQL,
    // the same rule applies: pick from a fixed set of column names, never splice the input.
}
```

- [ ] **Step 6: Verify the solutions pass**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E04Solutions`
Expected: the build succeeds; `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 7: Write the worked examples, `Examples.cs`, and run them.** The injection demo deliberately triggers EF's EF1003 analyzer, so it's suppressed for those three lines only. Any other EF1002/EF1003 warning is a real bug.

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E04;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E04Examples --logger "console;verbosity=detailed"
public class E04Examples : LabTest<E04Context>
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    record ColorCount(string? Color, int Products);

    // Concept 1: every {hole} in FromSql becomes a DbParameter.
    [Fact]
    public async Task FromSql_turns_interpolated_values_into_parameters()
    {
        var color = "Red";
        var red = await Db.Products.FromSql($"SELECT * FROM Production.Product WHERE Color = {color}").ToListAsync();

        Assert.Equal(38, red.Count);
        Assert.Contains("WHERE Color = @p0", Sql.All);
    }

    // Concept 1: concatenating input into FromSqlRaw lets the input rewrite the query.
    [Fact]
    public async Task FromSqlRaw_with_concatenation_is_injectable()
    {
        var input = "x' OR 1=1 --";

#pragma warning disable EF1003 // deliberately injectable: this example demonstrates the attack
        var injected = await Db.Products
            .FromSqlRaw("SELECT * FROM Production.Product WHERE Color = '" + input + "'")
            .CountAsync();
#pragma warning restore EF1003
        var safe = await Db.Products
            .FromSql($"SELECT * FROM Production.Product WHERE Color = {input}")
            .CountAsync();

        Assert.Equal(504, injected); // OR 1=1 matched every product
        Assert.Equal(0, safe);       // the whole input was compared as one value
    }

    // Concept 2: LINQ on top of FromSql wraps your SQL in a subquery.
    [Fact]
    public async Task LINQ_composes_over_FromSql_as_a_subquery()
    {
        var color = "Black";
        var names = await Db.Products
            .FromSql($"SELECT * FROM Production.Product WHERE Color = {color}")
            .Where(p => p.ListPrice > 1000)
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync();

        Print(string.Join(", ", names));
        Assert.Contains("FROM (", Sql.All);
        Assert.Contains("SELECT * FROM Production.Product WHERE Color = @p0", Sql.All);
    }

    // Concept 2: EXEC/WITH are refused by EF; a trailing ORDER BY is refused by SQL Server.
    [Fact]
    public async Task Some_SQL_cannot_be_composed()
    {
        var notComposable = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.Database.SqlQuery<ManagerName>($"EXEC dbo.uspGetEmployeeManagers @BusinessEntityID = {100}")
                .OrderBy(m => m.RecursionLevel)
                .ToListAsync());
        Print(notComposable.Message);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Db.Products.FromSql($"SELECT * FROM Production.Product ORDER BY Name")
                .Where(p => p.ListPrice > 0)
                .ToListAsync());
    }

    // Concept 3: SqlQuery<T> for shapes outside the model; scalars need a column named Value.
    [Fact]
    public async Task SqlQuery_maps_unmapped_types_and_scalars()
    {
        var byColor = await Db.Database
            .SqlQuery<ColorCount>($"SELECT Color, COUNT(*) AS Products FROM Production.Product GROUP BY Color")
            .ToListAsync();
        var total = await Db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Production.Product")
            .SingleAsync();

        Assert.Equal(10, byColor.Count); // 9 colors + NULL
        Assert.Equal(504, total);
    }

    // Concept 4: views are keyless: read-only and never tracked.
    [Fact]
    public async Task Views_are_keyless_read_only_entities()
    {
        var top = await Db.VSalesPeople.OrderByDescending(v => v.SalesYtd).FirstAsync();

        Print($"{top.FirstName} {top.LastName}: {top.SalesYtd:C}");
        Assert.Empty(Db.ChangeTracker.Entries());
        Assert.Throws<InvalidOperationException>(() => Db.VSalesPeople.Add(new VSalesPerson()));
    }

    // Concept 5: a mapped TVF is a queryable source; SelectMany over it becomes CROSS APPLY.
    [Fact]
    public async Task Table_valued_function_composes_like_a_table()
    {
        var contacts = await Db.People
            .Where(p => p.BusinessEntityId <= 5)
            .SelectMany(p => Db.GetContactInformation(p.BusinessEntityId), (p, c) => new { p.BusinessEntityId, c.JobTitle })
            .ToListAsync();

        Assert.Equal(5, contacts.Count);
        Assert.Contains("CROSS APPLY [dbo].[ufnGetContactInformation]([p].[BusinessEntityID])", Sql.All);
    }

    // Concept 5: a mapped scalar UDF translates into a function call inside the SQL.
    [Fact]
    public async Task Scalar_function_maps_to_a_CLR_method()
    {
        var stock = await Db.Products
            .Where(p => p.ProductId <= 4)
            .Select(p => new { p.ProductId, Stock = E04Context.GetStock(p.ProductId) })
            .ToListAsync();

        Print(string.Join(", ", stock));
        Assert.Contains("[dbo].[ufnGetStock]([p].[ProductID])", Sql.All);
        Assert.Throws<NotSupportedException>(() => E04Context.GetStock(1)); // never runs in .NET
    }

    // Concept 6: a procedure's rows map by column name; columns T doesn't have are ignored.
    [Fact]
    public async Task Stored_procedure_results_via_SqlQuery()
    {
        var chain = await Db.Database
            .SqlQuery<ManagerName>($"EXEC dbo.uspGetEmployeeManagers @BusinessEntityID = {100}")
            .ToListAsync();

        Print(string.Join(" -> ", chain.OrderBy(m => m.RecursionLevel).Select(m => $"{m.ManagerFirstName} {m.ManagerLastName}")));
        Assert.Equal(3, chain.Count);
    }

    // Concept 6: ExecuteSql returns rows affected, or -1 when the batch uses SET NOCOUNT ON.
    [Fact]
    public async Task ExecuteSql_returns_rows_affected()
    {
        var subcategory = 1;
        var updated = await Db.Database.ExecuteSqlAsync(
            $"UPDATE Production.Product SET ListPrice = ListPrice * 1.10 WHERE ProductSubcategoryID = {subcategory}");
        var viaProc = await Db.Database.ExecuteSqlAsync(
            $"EXEC HumanResources.uspUpdateEmployeePersonalInfo @BusinessEntityID = {1}, @NationalIDNumber = {"295847284"}, @BirthDate = {new DateTime(1969, 1, 29)}, @MaritalStatus = {"S"}, @Gender = {"M"}");

        Assert.Equal(32, updated);
        Assert.Equal(-1, viaProc); // the procedure runs SET NOCOUNT ON
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E04Examples`
Expected: the build succeeds; `Passed!  - Failed: 0, Passed: 10, Skipped: 0, Total: 10`.

- [ ] **Step 8: Write the lesson text, `README.md`**

````markdown
# E04 — Raw SQL, Views, Functions & Procedures

## What you'll learn

- The raw-SQL APIs — `FromSql`, `FromSqlRaw`, `SqlQuery`, `ExecuteSql` — and which ones protect you from SQL injection
- How LINQ composes on top of your SQL, and which SQL can't be composed
- Querying unmapped shapes and scalars with `Database.SqlQuery<T>`
- Views as keyless entities: what you can and can't do with them
- Mapping table-valued and scalar functions so they're usable inside LINQ
- Calling stored procedures, and why raw commands leave the change tracker stale

## Builds on

- T-SQL [lesson 10](../../../lessons/10-views-procedures-functions/lesson.ipynb) — views, procedures, scalar vs table-valued functions (and the scalar UDF performance trap)
- T-SQL [lesson 18](../../../lessons/18-complex-queries/lesson.ipynb) — CTE pipelines and `APPLY`
- E03 concept 6 — this lesson is the escape hatch for what LINQ can't express

## Setup

```
dotnet test --filter E04Examples --logger "console;verbosity=detailed"
```

This lesson's tests use `E04Context` (in this folder), a context derived from `AdventureWorksContext` that adds two function mappings. Lesson-specific model changes always go in a derived context like this, never in `Model/`.

## Concepts

### 1. The raw-SQL APIs

| API | Returns | Takes | Parameterized? |
|---|---|---|---|
| `db.Products.FromSql($"... {x}")` | entities (`IQueryable<Product>`) | `FormattableString` | yes: each `{x}` becomes a `DbParameter` |
| `db.Products.FromSqlRaw("... {0}", x)` | entities | `string` + values | the `{0}` placeholders are; anything you concatenate or interpolate into the string is not |
| `db.Database.SqlQuery<T>($"...")` | any type, including `int` | `FormattableString` | yes |
| `db.Database.ExecuteSql($"...")` | rows affected (`int`) | `FormattableString` | yes |

`FromSql` looks like string interpolation but isn't. The compiler hands EF a `FormattableString`, and EF turns every hole into a parameter. `FromSqlRaw` gets a finished string, so `FromSqlRaw("... WHERE Color = '" + input + "'")` is classic SQL injection. EF's analyzers flag both mistakes at build time: **EF1002** for interpolating into `FromSqlRaw`/`ExecuteSqlRaw`, **EF1003** for concatenating. Treat those warnings as bugs. Parameters protect **values** only. Table and column names can't be parameters, so if users choose a column, map their choice to a fixed list yourself (challenge 3).

`FromSql` must return every column of the entity, and they're matched by name. `SELECT *` from the entity's own table is the easy way.

Examples: `FromSql_turns_interpolated_values_into_parameters`, `FromSqlRaw_with_concatenation_is_injectable`.

### 2. LINQ composes over your SQL as a subquery

```csharp
db.Products.FromSql($"SELECT * FROM Production.Product WHERE Color = {color}")
    .Where(p => p.ListPrice > 100)
    .OrderBy(p => p.Name)
```

```sql
SELECT ... FROM (SELECT * FROM Production.Product WHERE Color = @p0) AS [p]
WHERE [p].[ListPrice] > 100.0
ORDER BY [p].[Name]
```

That wrapping decides what your SQL may contain if you want to compose on top of it:

| Your SQL | Composable? |
|---|---|
| a plain `SELECT` | yes |
| ends with `ORDER BY` (no `TOP`/`OFFSET`) | no: SQL Server rejects `ORDER BY` in a derived table. Put the ordering in LINQ. |
| starts with `WITH` (a CTE) or `EXEC` | no: EF refuses with "non-composable SQL". Materialize first (`ToListAsync`), then finish in memory. |

Composition also changes performance. A filter you compose on top runs **after** your SQL. For example, a window function partitioned by customer is computed for every customer before the outer `WHERE` keeps one. SQL Server can often push a predicate on the partitioning column down into the subquery, but it's the optimizer's choice, not a guarantee.

Examples: `LINQ_composes_over_FromSql_as_a_subquery`, `Some_SQL_cannot_be_composed`.

### 3. `SqlQuery<T>`: unmapped results

`db.Database.SqlQuery<T>(...)` maps result columns onto `T`'s properties by name. `T` can be a record or a class that isn't in the model, or a scalar such as `int` or `string`. For a scalar, the column must be aliased `Value`: `SqlQuery<int>($"SELECT COUNT(*) AS Value FROM ...")`. It composes with LINQ just like `FromSql`.

Example: `SqlQuery_maps_unmapped_types_and_scalars`.

### 4. Views are keyless entities

The scaffolder mapped every view as a keyless entity (`HasNoKey().ToView("vSalesPerson", "Sales")`). Keyless entities:

- are never tracked: queries return plain objects, so `AsNoTracking` is implied
- can't be added, attached, updated or deleted: without a key, EF can't identify a row
- have no navigations: join them explicitly (E02)

Example: `Views_are_keyless_read_only_entities`.

### 5. Mapping database functions

See `E04Context.cs`:

```csharp
// table-valued: returns IQueryable<T>, composes like a table
public IQueryable<ContactInformation> GetContactInformation(int personId)
    => FromExpression(() => GetContactInformation(personId));
// scalar: a static stub EF translates into a function call
public static int GetStock(int productId) => throw new NotSupportedException();

modelBuilder.HasDbFunction(typeof(E04Context).GetMethod(nameof(GetContactInformation), [typeof(int)])!)
    .HasName("ufnGetContactInformation").HasSchema("dbo");
```

A mapped table-valued function is a queryable source. Combine it per row with `SelectMany` and EF emits `CROSS APPLY`. A mapped scalar function can appear anywhere in a query: `E04Context.GetStock(p.ProductId)` becomes `[dbo].[ufnGetStock]([p].[ProductID])`. Remember the scalar-UDF trap from T-SQL lesson 10: a non-inlined scalar UDF runs once per row. SQL Server 2019+ inlines the ones it can.

Examples: `Table_valued_function_composes_like_a_table`, `Scalar_function_maps_to_a_CLR_method`.

### 6. Stored procedures and commands

- **Procedures that return rows:** `SqlQuery<T>($"EXEC dbo.uspGetEmployeeManagers @BusinessEntityID = {id}")`. Columns are matched by name, and extra columns are ignored. An `EXEC` isn't composable, so order and filter after materializing.
- **Commands:** `ExecuteSql($"UPDATE ... {x}")` returns the rows affected. It reports `-1` when the batch runs `SET NOCOUNT ON`, which AdventureWorks' procedures do.
- **The change tracker doesn't see raw commands.** After an `ExecuteSql` UPDATE, entities that are already tracked still hold the old values. A tracking query won't fix that either: identity resolution hands back the tracked instance as it is. Project the columns, use `AsNoTracking`, or reload explicitly (challenge 1).

Examples: `Stored_procedure_results_via_SqlQuery`, `ExecuteSql_returns_rows_affected`.

## Pitfalls

- **Interpolating into `FromSqlRaw`.** `FromSqlRaw($"... {input}")` builds the string before EF sees it, so it's injectable. Use `FromSql`.
- **`ORDER BY` inside SQL you compose on.** Order in LINQ instead.
- **Composing over `EXEC` or a CTE.** Materialize first.
- **Trusting tracked entities after `ExecuteSql`.** They're stale until reloaded.
- **Treating the `-1` from `SET NOCOUNT ON` procedures as "nothing happened".** It means "not reported".

## Exercises

```
dotnet test --filter E04Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
````

- [ ] **Step 9: Verify the prompts are identical in `Exercises.cs` and `Solutions.cs`**

```bash
cd efcore && python - lessons/E04-raw-sql-views-functions-procs <<'EOF'
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

- [ ] **Step 10: Commit**

```bash
git add efcore/lessons/E04-raw-sql-views-functions-procs
git commit -m "feat: add EF Core lesson E04 - raw SQL, views, functions and procedures"
```

---

## Task 7: Track docs and final verification

**Files:**
- Create: `efcore/README.md`
- Modify: `README.md` (Prerequisites list; new "EF Core Track" section), `CLAUDE.md` ("What this is" paragraph; new "EF Core track" section)

**Interfaces:**
- Consumes: everything above. Commands and file names in the docs must match Tasks 1–6 exactly.
- Produces: the track's entry points for learners (`efcore/README.md`) and for agents (`CLAUDE.md`).

- [ ] **Step 1: Write `efcore/README.md`**

````markdown
# EF Core Practice Track

A practice track for **EF Core 10** on the same Docker SQL Server and AdventureWorks2022 database as the T-SQL lessons. It's aimed at people who have used EF Core and want depth: predicting the SQL it generates, avoiding loading and performance traps, controlling change tracking and concurrency, and designing and migrating models.

Exercises are xUnit tests. Each one checks **how** your query ran (round trips, the shape of the generated SQL, change-tracker state), not just the rows it returned.

## Prerequisites

- The `mssql-learn` container running, with AdventureWorks2022 restored (see the [root README](../README.md))
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). `global.json` pins 10.0.x, so an installed .NET 11 preview SDK is ignored here.
- An editor with a test runner (Visual Studio, Rider, or VS Code with C# Dev Kit), or just the CLI

The sa password is read from the `SA_PASSWORD` environment variable, falling back to `docker/.env`, the same file the helper scripts use. Nothing to configure if the container already works.

## First run

From this folder:

```
dotnet tool restore                  # local dotnet-ef 10.0.12 (see dotnet-tools.json)
dotnet test --filter E01Examples     # should pass
```

## The practice loop

Each lesson in `lessons/` has five files:

| File | What it is |
|---|---|
| `README.md` | concepts, pitfalls, links to the T-SQL lessons it builds on |
| `Examples.cs` | worked examples, as tests that pass and print their SQL |
| `Checks.cs` | the assertions for each exercise (read them, they're the spec) |
| `Exercises.cs` | **the only file you edit**: replace each `throw new NotImplementedException()` |
| `Solutions.cs` | worked answers, each with a `Why:` note; peek after attempting |

```
dotnet test --filter E01Exercises    # skipped = not attempted, failed = wrong, passed = done
dotnet test --filter E01Examples --logger "console;verbosity=detailed"   # examples with their SQL
dotnet test --filter "FullyQualifiedName!~Exercises"                     # self-check: all examples + solutions
```

Every exercise check prints the SQL your code sent, so a failing round-trip check shows you the N+1 directly. Keep `Exercises.cs` compiling: one half-written exercise that doesn't build stops the whole project's tests from running, so comment it out until it compiles.

## How the database is used

- **AdventureWorks2022 is never modified.** Each test runs on its own connection inside a transaction that is rolled back when the test ends, so writes, updates and deletes in exercises disappear afterwards. No setup scripts, and tests can run in any order.
- **The code-first lessons (E10–E12, planned) will use their own `EfLab_<lesson>` databases.** The tests will create and drop them, and the harness will refuse to touch any database whose name doesn't start with `EfLab_`, so AdventureWorks can't be dropped by accident.
- Tests run one at a time (`xunit.runner.json`), which keeps SQL capture and blocking demos predictable.

## Lessons

| # | Lesson | Status |
|---|---|---|
| E01 | Setup & seeing the SQL | available |
| E02 | Projections, joins & nulls | available |
| E03 | Grouping, subqueries & where LINQ runs out | available |
| E04 | Raw SQL, views, functions & procedures | available |
| E05 | Loading related data | planned |
| E06 | Query performance | planned |
| E07 | Change tracking internals | planned |
| E08 | SaveChanges & set-based writes | planned |
| E09 | Concurrency & transactions | planned |
| E10 | Modeling with the Fluent API | planned |
| E11 | Inheritance & JSON columns | planned |
| E12 | Migrations workflow | planned |
| E13 | Capstone | planned |

Design: [`docs/superpowers/specs/2026-10-10-efcore-track-design.md`](../docs/superpowers/specs/2026-10-10-efcore-track-design.md).

## The model

`Model/` is the AdventureWorks model, generated once and committed. It is never edited by hand. Shared corrections go in partial classes outside `Model/`, and lesson-specific changes go in a context derived from `AdventureWorksContext` (see `lessons/E04-*/E04Context.cs`). To regenerate it, from this folder:

```bash
# macOS / Linux
SA_PASSWORD=$(grep '^SA_PASSWORD=' ../docker/.env | cut -d= -f2-)
dotnet ef dbcontext scaffold \
  "Server=localhost,1433;Database=AdventureWorks2022;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=True" \
  Microsoft.EntityFrameworkCore.SqlServer \
  --output-dir Model --context AdventureWorksContext --no-onconfiguring --force \
  --schema Person --schema HumanResources --schema Production \
  --schema Purchasing --schema Sales --schema dbo
```

```powershell
# Windows
$pw = (Select-String '^SA_PASSWORD=' ../docker/.env).Line.Split('=', 2)[1]
dotnet ef dbcontext scaffold "Server=localhost,1433;Database=AdventureWorks2022;User Id=sa;Password=$pw;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer --output-dir Model --context AdventureWorksContext --no-onconfiguring --force --schema Person --schema HumanResources --schema Production --schema Purchasing --schema Sales --schema dbo
```

`--no-onconfiguring` keeps the connection string out of the code, and the `--schema` list keeps the T-SQL lessons' `lessonNN` tables out of the model. E01 explains what the scaffolder skipped.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Every test fails with "Cannot connect to SQL Server on localhost,1433" | Start the container: `docker compose -f docker/docker-compose.yml up -d` (from the repo root) |
| "SA password not found" | Set `SA_PASSWORD`, or create `docker/.env` from `docker/.env.example`. A fresh git worktree doesn't have the gitignored `docker/.env`, so copy it over. |
| `A compatible .NET SDK was not found` | Install the .NET 10 SDK |
| Nothing runs, build errors in `Exercises.cs` | A half-written exercise doesn't compile. Fix it or comment it out. |
| Checks fail on row values that look right | Your AdventureWorks data may have drifted. `scripts/*/reset-db` restores a clean copy (and wipes the T-SQL lessons' `lessonNN` schemas). |
````

- [ ] **Step 2: Update the root `README.md`.** In `## Prerequisites`, after the `- **Windows only:** PowerShell 7+` line, add:

```markdown
- **EF Core track only:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
```

Then insert this section directly before `## Helper Scripts`:

```markdown
## EF Core Track

[`efcore/`](efcore/README.md) is a separate practice track for **EF Core 10** on the same container and AdventureWorks database. It's for people who have used EF Core and want depth: generated SQL, loading and performance, change tracking and concurrency, modeling and migrations. Lessons are xUnit exercises whose checks verify *how* a query ran, not just its rows. Lessons E01–E04 are available; start with [`efcore/README.md`](efcore/README.md).

```

- [ ] **Step 3: Update `CLAUDE.md`.** Replace the paragraph under `## What this is` with:

```markdown
A self-paced MSSQL 2022 / T-SQL learning curriculum: lesson content (Jupyter notebooks + SQL) plus a Docker setup for the database the lessons run against. The T-SQL lessons have no application code, build, or test suite. The separate EF Core practice track in `efcore/` is a .NET 10 xUnit project (see "EF Core track" below). Design specs and implementation plans live in `docs/superpowers/`.
```

Then insert this section directly before `## Commit style`:

````markdown
## EF Core track

A separate practice track in `efcore/` (spec: `docs/superpowers/specs/2026-10-10-efcore-track-design.md`): one xUnit project, `efcore/EfCoreLessons.csproj`, on .NET 10 and EF Core 10.0.12. Package versions are pinned exactly; `efcore/global.json` pins the SDK to 10.0.x and `efcore/dotnet-tools.json` pins `dotnet-ef`.

```bash
cd efcore
dotnet test --filter "FullyQualifiedName!~Exercises"   # examples + solutions + harness self-tests: must all pass
dotnet test --filter E01Exercises                       # the learner's loop: unattempted exercises report as skipped
```

- Lessons live in `efcore/lessons/E<NN>-<slug>/`: `README.md`, `Examples.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`, plus `E<NN>Context.cs` when the lesson changes the model. Abstract `E<NN>Checks` holds one `[Exercise]` test per exercise and an abstract method for each. `E<NN>Exercises` implements them as `throw new NotImplementedException()` (reported as skipped) and `E<NN>Solutions` with the answers. Prompts are identical in both files, and each solution is followed by a `// Why:` comment.
- 5 exercises (`Ex1`–`Ex5`) and 3–4 challenges (`Ch1`–`Ch4`) per lesson. Checks assert behavior where it is the point (round trips via `Sql.AssertRoundTrips`, SQL shape, change-tracker state) and compare rows with a reference T-SQL query via `Reference.Rows<T>` where practical.
- `efcore/Lab/` gives every test a connection and transaction that is rolled back, so the track never modifies AdventureWorks and needs no `setup.sql`. Code-first lessons (E10–E12) will use `EfLab_<lesson>` databases instead.
- `efcore/Model/` is scaffolder output (command in `efcore/README.md`). Never hand-edit it; lesson-specific model changes go in a derived context in the lesson folder.
- EF can't translate through a positional record's constructor: compose (`OrderBy`, set operations) on anonymous types and project into the record last.
- A lesson is done when the build has no new warnings, its examples and solutions pass, its exercises all report as skipped, and every EF Core 10-specific claim has been checked against the docs.

````

- [ ] **Step 4: Build clean**

Run: `cd efcore && dotnet build -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 5: Run everything, unfiltered (Review Focus 3)**

Run: `cd efcore && dotnet test --no-build`
Expected: `Passed!  - Failed: 0, Passed: 80, Skipped: 32, Total: 112`: the 32 skips are the untouched exercises.

- [ ] **Step 6: Run the curriculum self-check**

Run: `cd efcore && dotnet test --no-build --filter "FullyQualifiedName!~Exercises"`
Expected: `Passed!  - Failed: 0, Passed: 80, Skipped: 0, Total: 80`.

- [ ] **Step 7: No newline-dependent assertions (Review Focus 4)**

Run: `cd efcore && grep -nF -e 'NewLine' -e '\r\n' lessons/*/Checks.cs lessons/*/Examples.cs`
Expected: exactly one hit, `lessons/E01-setup-and-generated-sql/Examples.cs:59: Print(string.Join(Environment.NewLine, log));`. That line is output only, not an assertion.

- [ ] **Step 8: Check the EF Core 10-specific claims (spec DoD item 5).** Every behavioral claim in the E01–E04 READMEs is asserted by an example named next to it, so Steps 5–6 already verified them against EF Core 10.0.12. Three claims have no example. Confirm each against Microsoft Learn ("What's new in EF Core 10", "Querying data", "SQL queries"):
  - E04's note that SQL Server 2019+ inlines eligible scalar UDFs
  - E04's EF1002/EF1003 analyzer IDs (also visible in the build output if you temporarily concatenate into a `FromSqlRaw`)
  - E02's advice not to switch `UseRelationalNulls()` on globally in an existing codebase

Expected: no corrections needed. If one is, fix the README and re-run Step 5.

- [ ] **Step 9: AdventureWorks is unchanged.** Re-run Task 0, Step 5's query.
Expected: the same three integers recorded in Task 0.

- [ ] **Step 10: Commit**

```bash
git add efcore/README.md README.md CLAUDE.md
git commit -m "docs: describe the EF Core track in READMEs and CLAUDE.md"
```

---

## Not in this phase

Tiers B–D and the capstone (E05–E13), the `Proxies` package (phase 2), and `LabDatabase` for code-first lessons (phase 4) each get their own plan, per the spec's build-out phases.
