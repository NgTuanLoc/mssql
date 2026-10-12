# EF Core Track — Phase 2 (Tier B: Loading & Performance) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add lessons E05 (loading related data) and E06 (query performance) to `efcore/`, with the two harness additions they need: the lazy-loading proxies package and a query-plan helper.

**Architecture:** Same lesson anatomy as phase 1. Abstract `E<NN>Checks` hold the assertions; `E<NN>Exercises` (stubs, reported as skipped) and `E<NN>Solutions` inherit them. E05 runs every test on `E05Context`, a derived context with lazy-loading proxies, so a navigation the learner forgot to load costs a visible round trip instead of being silently empty. E06 asserts on execution plans through the new `Lab/QueryPlan.cs`, which compiles a query with `SET SHOWPLAN_XML ON` (nothing runs) and returns its physical operators. Every file below was validated against the live `mssql-learn` container on 2026-10-12:
- the build has 0 warnings
- the full suite gives 117 passed and 48 skipped (the 48 are the exercise stubs)
- every check was also run against a set of tempting-but-wrong answers and failed each one for the intended reason

**Tech Stack:** .NET 10 SDK (10.0.x), EF Core 10.0.12 (SqlServer, Design, **Proxies**), xUnit v3 4.0.2 (`xunit.v3.mtp-off`) on VSTest, SQL Server 2022 (Docker container `mssql-learn`), AdventureWorks2022.

**Spec:** `docs/superpowers/specs/2026-10-10-efcore-track-design.md` (phase 2 of its "Build-out Phases").

## Global Constraints

- Target `net10.0`. EF Core packages exactly `10.0.12`, including the new `Microsoft.EntityFrameworkCore.Proxies`; `xunit.v3.mtp-off` `4.0.2`, `xunit.runner.visualstudio` `4.0.1`, `Microsoft.NET.Test.Sdk` `18.10.1`. No floating versions.
- AdventureWorks2022 is never modified: every test runs on a connection + transaction that is rolled back. No `setup.sql`, no `ALTER DATABASE` (Query Store stays off; E06 uses the plan-cache DMVs instead).
- No credentials committed anywhere.
- `efcore/Model/` is scaffolder output, never hand-edited; lesson-specific model changes go in a derived context in the lesson folder (`E05Context.cs`).
- Tests run serially (`parallelizeTestCollections: false`).
- Lesson folder `efcore/lessons/E<NN>-<slug>/` = `README.md`, `Examples.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs` (+ `E<NN>Context.cs` if the model changes). Namespace `EfCoreLessons.E<NN>`; classes `E<NN>Examples`, `E<NN>Checks`, `E<NN>Exercises`, `E<NN>Solutions`.
- Each lesson: 5 exercises (`Ex1`–`Ex5`) + 3 challenges (`Ch1`–`Ch3`); identical prompts in `Exercises.cs` and `Solutions.cs`; a `// Why:` note after each solution.
- `dotnet build` produces 0 warnings.
- Commits: conventional prefixes (`feat:`, `docs:`), no attribution lines (disabled in the user's settings).

## Review Focus

1. **A failing E05 check must not lazy-load while reporting.** xUnit formats the values in a failed assertion, and formatting a proxy entity reads its navigations, which with lazy loading runs queries (a failure once took 7 s). The checks therefore never hand entities to a formatting assertion; they assert booleans with a hint. Pinned by Task 2, Step 9 (a wrong Ex1 answer must fail fast with the hint).
2. **Running E06 again and again.** The plan-cache and DMV assertions search the server's caches by text, which outlive the rolled-back transaction. Every such test tags its queries with a fresh GUID, so earlier runs can't match. Pinned by Task 3, Step 7 (run `E06Examples` and `E06Solutions` twice in a row).
3. **`SHOWPLAN_XML` left on.** If `QueryPlan` left the session in showplan mode, every later query in the test would return a plan instead of rows. It switches it off in `finally`. Pinned by Task 1's self-test (a normal query runs right after a plan).
4. **Plan-shape assertions on another SQL Server.** Seek/scan assertions are made only where the seek uses a covering index range against the alternative of a full scan, so the optimizer's choice doesn't depend on statistics details. Pinned by the E06 examples and checks in Task 3 (they run against this container; a different SQL Server major version could still differ).
5. **A learner's plain `dotnet test`** still gives zero failures: 117 passed, 48 skipped. Pinned by Task 4, Step 5.

## Conventions used by every task

- Shell: Git Bash on Windows, bash/zsh elsewhere. Paths are relative to the repository (or worktree) root unless a step says `cd efcore`.
- Commands that need the password export it first (`docker/.env` is gitignored; a fresh worktree outside the repo has no copy until Task 1, Step 1 copies it):

  ```bash
  export SA_PASSWORD=$(grep '^SA_PASSWORD=' /d/Study/Database/mssql/docker/.env | cut -d= -f2- | tr -d '\r')
  ```

- On Git Bash, prefix `docker exec ... /opt/mssql-tools18/...` with `MSYS_NO_PATHCONV=1`.

## File Map

| Action | Path |
|---|---|
| Modify | `efcore/EfCoreLessons.csproj` (add the Proxies package) |
| Create | `efcore/Lab/QueryPlan.cs` |
| Modify | `efcore/Lab/LabSelfTests.cs` (one new self-test) |
| Create | `efcore/lessons/E05-loading-related-data/` (6 files, incl. `E05Context.cs`) |
| Create | `efcore/lessons/E06-query-performance/` (5 files) |
| Modify | `efcore/README.md`, `README.md`, `CLAUDE.md`, `docs/superpowers/specs/2026-10-10-efcore-track-design.md` |

---

## Task 0: Isolate the work

- [ ] **Step 1: Create an isolated workspace** using the `superpowers:using-git-worktrees` skill, on a new branch `efcore-phase2` based on `master`. Without a worktree: `git switch -c efcore-phase2`.

- [ ] **Step 2: Verify you are not on master**

Run: `git branch --show-current`
Expected: `efcore-phase2`

- [ ] **Step 3: Confirm the container is up**

Run: `docker ps --filter name=mssql-learn --format '{{.Status}}'`
Expected: starts with `Up`. If not, run `docker compose -f docker/docker-compose.yml up -d` from the main checkout and wait ~20 s for SQL Server to accept logins.

- [ ] **Step 4: Confirm the phase 1 baseline passes**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build`
Expected: `0 Warning(s)`, then `Passed!  - Failed: 0, Passed: 81, Skipped: 32, Total: 113`. (In a worktree outside the repo, copy `docker/.env` first: `cp /d/Study/Database/mssql/docker/.env docker/.env`.)

- [ ] **Step 5: Record the AdventureWorks baseline** (compared again in Task 4)

```bash
export SA_PASSWORD=$(grep '^SA_PASSWORD=' /d/Study/Database/mssql/docker/.env | cut -d= -f2- | tr -d '\r')
MSYS_NO_PATHCONV=1 docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -W -s'|' -d AdventureWorks2022 -Q "SET NOCOUNT ON; SELECT (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Production.Product) AS product, (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM HumanResources.Employee) AS employee, (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Sales.SalesOrderHeader) AS soh;"
```

Expected: three integers. Note them down. (On 2026-10-12 they were `-1058563079|259050056|-1058661271`.)

---

## Task 1: Harness additions (Proxies package, QueryPlan)

**Files:**
- Modify: `efcore/EfCoreLessons.csproj`
- Modify: `efcore/Lab/LabSelfTests.cs` (first, as the failing test)
- Create: `efcore/Lab/QueryPlan.cs`

**Interfaces:**
- Consumes: `LabTest` (`Db`, `Sql`) from `efcore/Lab/LabTest.cs`.
- Produces: `public static class QueryPlan` in namespace `EfCoreLessons.Lab` with `public static Task<List<string>> OperatorsAsync(DbContext db, IQueryable query)` — the estimated plan's `PhysicalOp` values in plan order (e.g. `["Top", "Clustered Index Seek"]`), without executing the query and without being counted in `Sql`. The package `Microsoft.EntityFrameworkCore.Proxies` 10.0.12 (gives `UseLazyLoadingProxies()`), used by Task 2.

- [ ] **Step 1: Add the Proxies package.** In `efcore/EfCoreLessons.csproj`, insert this line directly above the `Microsoft.EntityFrameworkCore.SqlServer` reference:

```xml
    <PackageReference Include="Microsoft.EntityFrameworkCore.Proxies" Version="10.0.12" />
```

Run: `cd efcore && dotnet build -warnaserror`
Expected: `Build succeeded.`, `0 Warning(s)`.

- [ ] **Step 2: Write the failing self-test.** In `efcore/Lab/LabSelfTests.cs`, inside `public class LabSelfTests`, insert this test directly after `Commands_throw_after_the_test_transaction_was_rolled_back_on_the_server` (before `sealed class ScratchLab`):

```csharp
    [Fact]
    public async Task QueryPlan_compiles_without_running_or_counting()
    {
        var operators = await QueryPlan.OperatorsAsync(Db, Db.Products.Where(p => p.ProductId == 1));

        Assert.Contains("Clustered Index Seek", operators);
        Assert.Equal(0, Sql.Count);
        Assert.Equal(504, await Db.Products.CountAsync()); // SHOWPLAN is off again: queries run normally
    }
```

- [ ] **Step 3: Run the build to verify it fails**

Run: `cd efcore && dotnet build 2>&1 | grep -E ' error ' | head -3`
Expected: `error CS0103: The name 'QueryPlan' does not exist in the current context`.

- [ ] **Step 4: Write `efcore/Lab/QueryPlan.cs`**

```csharp
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCoreLessons.Lab;

// The estimated execution plan SQL Server would use for a query, without running the query.
// Uses the context's own connection and transaction; these commands are not counted in Sql.
public static class QueryPlan
{
    // The plan's physical operators in plan order, e.g. ["Top", "Clustered Index Seek"].
    public static async Task<List<string>> OperatorsAsync(DbContext db, IQueryable query)
    {
        var plan = XDocument.Parse(await ShowplanXmlAsync(db, query.ToQueryString()));
        return plan.Descendants()
            .Select(e => (string?)e.Attribute("PhysicalOp"))
            .OfType<string>()
            .ToList();
    }

    static async Task<string> ShowplanXmlAsync(DbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();

        command.CommandText = "SET SHOWPLAN_XML ON";
        await command.ExecuteNonQueryAsync();
        try
        {
            // ToQueryString turns the parameters into DECLAREd local variables, whose values the
            // optimizer doesn't sniff: the plan is compiled for "some value". Good enough to tell a
            // seek from a scan; a real execution's sniffed plan can still differ.
            command.CommandText = sql;
            return (string)(await command.ExecuteScalarAsync())!;
        }
        finally
        {
            command.CommandText = "SET SHOWPLAN_XML OFF";
            await command.ExecuteNonQueryAsync();
        }
    }
}
```

- [ ] **Step 5: Run the self-tests**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter "LabSelfTests|LabConnectionTests"`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 6: Commit**

```bash
git add efcore/EfCoreLessons.csproj efcore/Lab
git commit -m "feat: add lazy-loading proxies and a query-plan helper to the EF lab"
```

---

## Task 2: Lesson E05 — Loading related data

**Files:**
- Create: `efcore/lessons/E05-loading-related-data/E05Context.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `LabTest<TContext>` (`Db`, `NewContext()`, `Sql`, `Reference`), `[Exercise]`, the scaffolded `AdventureWorksContext` (`DbContextOptions<AdventureWorksContext>` constructor; virtual navigations), `UseLazyLoadingProxies()` from Task 1's package.
- Produces: namespace `EfCoreLessons.E05`: `E05Context`, records `OrderSummary`, `TerritoryCustomers`, `OrderRef`, classes `E05Checks`, `E05Exercises`, `E05Solutions`, `E05Examples`. Nothing later depends on them.

- [ ] **Step 1: Write `E05Context.cs`** (lazy-loading proxies on)

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E05;

// AdventureWorksContext with lazy-loading proxies switched on. Every navigation in Model/ is
// virtual, so touching an unloaded one runs a query: each hidden load shows up in Sql.
public class E05Context(DbContextOptions<AdventureWorksContext> options) : AdventureWorksContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseLazyLoadingProxies();
}
```

- [ ] **Step 2: Write `Checks.cs`**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E05;

public record OrderSummary(int SalesOrderId, string AccountNumber, int LineCount, decimal TotalDue);
public record TerritoryCustomers(string Territory, int Customers);
public record OrderRef(int CustomerId, int SalesOrderId);

// The checks for E05. E05Exercises (yours) and E05Solutions both inherit them.
// Every check runs on E05Context, where lazy loading is ON: a navigation you forgot to load
// is not silently empty, it costs a round trip, and the round-trip assertions catch it.
public abstract class E05Checks : LabTest<E05Context>
{
    protected abstract Task<List<SalesOrderHeader>> Ex1(E05Context db, int customerId);
    protected abstract Task<Customer> Ex2(E05Context db, int customerId, DateTime since);
    protected abstract Task Ex3(E05Context db, Product product);
    protected abstract Task<List<OrderSummary>> Ex4(E05Context db, DateTime orderDate);
    protected abstract Task<List<TerritoryCustomers>> Ex5(E05Context db);
    protected abstract Task<SalesPerson> Ch1(E05Context db, int salesPersonId);
    protected abstract Task<List<Customer>> Ch2(E05Context db, int minOrders);
    protected abstract Task<int> Ch3(E05Context db, SalesPerson salesPerson, int year);

    [Exercise]
    public async Task Ex1_OrdersWithLines()
    {
        var orders = await Ex1(Db, 11091);

        Sql.AssertRoundTrips(1); // before touching anything: a lazy load would count too
        Assert.True(orders.All(o => Db.Entry(o).Collection(h => h.SalesOrderDetails).IsLoaded),
            "Every order's SalesOrderDetails must be loaded by your query.");
        Assert.Equal(
            await Reference.Rows<int>($"SELECT SalesOrderID AS Value FROM Sales.SalesOrderHeader WHERE CustomerID = 11091 ORDER BY SalesOrderID"),
            orders.Select(o => o.SalesOrderId).ToList());
        Assert.Equal(
            (await Reference.Rows<int>($"""
                SELECT COUNT(*) AS Value FROM Sales.SalesOrderDetail d
                JOIN Sales.SalesOrderHeader h ON h.SalesOrderID = d.SalesOrderID
                WHERE h.CustomerID = 11091
                """)).Single(),
            orders.Sum(o => o.SalesOrderDetails.Count));
        Sql.AssertRoundTrips(1); // counting the lines ran nothing: they were already loaded
    }

    [Exercise]
    public async Task Ex2_RecentOrdersOnly()
    {
        var since = new DateTime(2014, 1, 1);
        var customer = await Ex2(Db, 11091, since);

        Sql.AssertRoundTrips(1);
        Assert.Equal(11091, customer.CustomerId);
        Assert.True(Db.Entry(customer).Collection(c => c.SalesOrderHeaders).IsLoaded);
        Assert.Equal(
            await Reference.Rows<int>($"SELECT SalesOrderID AS Value FROM Sales.SalesOrderHeader WHERE CustomerID = 11091 AND OrderDate >= {since} ORDER BY SalesOrderID"),
            customer.SalesOrderHeaders.Select(h => h.SalesOrderId).Order().ToList());
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ex3_LoadReviewsLater()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 937);
        Sql.Reset();
        Db.ChangeTracker.LazyLoadingEnabled = false; // this exercise is about loading on purpose

        await Ex3(Db, product);

        Sql.AssertRoundTrips(1);
        Assert.True(Db.Entry(product).Collection(p => p.ProductReviews).IsLoaded);
        Assert.DoesNotContain("[Production].[Product] AS", Sql.All); // the product itself was not re-read
        Assert.Equal([2, 4], product.ProductReviews.Select(r => r.Rating).Order().ToList());
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ex4_SummariesInsteadOfGraphs()
    {
        var result = await Ex4(Db, new DateTime(2012, 2, 29));

        Assert.Equal(await Reference.Rows<OrderSummary>($"""
            SELECT h.SalesOrderID AS SalesOrderId, c.AccountNumber,
                   (SELECT COUNT(*) FROM Sales.SalesOrderDetail d WHERE d.SalesOrderID = h.SalesOrderID) AS LineCount,
                   h.TotalDue
            FROM Sales.SalesOrderHeader h
            JOIN Sales.Customer c ON c.CustomerID = h.CustomerID
            WHERE h.OrderDate = '2012-02-29'
            ORDER BY h.SalesOrderID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Empty(Db.ChangeTracker.Entries());         // a projection, not tracked entities
        Assert.DoesNotContain("[Freight]", Sql.All);       // only the columns the summary needs
        Assert.DoesNotContain("[UnitPrice]", Sql.All);     // lines counted in SQL, not loaded
    }

    [Exercise]
    public async Task Ex5_NoNPlusOne()
    {
        var result = await Ex5(Db);

        Assert.Equal(await Reference.Rows<TerritoryCustomers>($"""
            SELECT t.Name AS Territory, COUNT(c.CustomerID) AS Customers
            FROM Sales.SalesTerritory t
            LEFT JOIN Sales.Customer c ON c.TerritoryID = t.TerritoryID
            GROUP BY t.Name
            ORDER BY t.Name
            """), result);
        Sql.AssertRoundTrips(1); // one query for all territories, not one per territory
    }

    [Exercise]
    public async Task Ch1_NoCartesianExplosion()
    {
        var salesPerson = await Ch1(Db, 277);

        Assert.InRange(Sql.Count, 2, 4);
        Assert.DoesNotContain("LEFT JOIN", Sql.All); // no query joins two collections to the same parent
        Assert.True(Db.Entry(salesPerson).Collection(s => s.SalesOrderHeaders).IsLoaded);
        Assert.True(Db.Entry(salesPerson).Collection(s => s.Stores).IsLoaded);
        Assert.True(Db.Entry(salesPerson).Collection(s => s.SalesPersonQuotaHistories).IsLoaded);
        var sqlCount = Sql.Count;
        Assert.Equal(473, salesPerson.SalesOrderHeaders.Count);
        Assert.Equal(76, salesPerson.Stores.Count);
        Assert.Equal(12, salesPerson.SalesPersonQuotaHistories.Count);
        Assert.Equal(sqlCount, Sql.Count); // nothing was lazy-loaded while counting
    }

    [Exercise]
    public async Task Ch2_ThreeMostRecentEach()
    {
        var customers = await Ch2(Db, 27);

        Sql.AssertRoundTrips(1);
        var expected = await Reference.Rows<OrderRef>($"""
            WITH ranked AS (
                SELECT CustomerID, SalesOrderID,
                       ROW_NUMBER() OVER (PARTITION BY CustomerID ORDER BY OrderDate DESC, SalesOrderID DESC) AS rn
                FROM Sales.SalesOrderHeader
                WHERE CustomerID IN (SELECT CustomerID FROM Sales.SalesOrderHeader GROUP BY CustomerID HAVING COUNT(*) >= 27)
            )
            SELECT CustomerID AS CustomerId, SalesOrderID AS SalesOrderId FROM ranked WHERE rn <= 3
            ORDER BY CustomerID, rn
            """);
        Assert.Equal(expected, customers.SelectMany(c => c.SalesOrderHeaders.Select(h => new OrderRef(c.CustomerId, h.SalesOrderId))).ToList());
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ch3_PartialLoadAndCount()
    {
        var salesPerson = await Db.SalesPeople.SingleAsync(s => s.BusinessEntityId == 277);
        Sql.Reset();

        var stores = await Ch3(Db, salesPerson, 2014);

        Sql.AssertRoundTrips(2);
        Assert.Equal(76, stores);
        Assert.False(Db.Entry(salesPerson).Collection(s => s.Stores).IsLoaded); // counted, not loaded
        Db.ChangeTracker.LazyLoadingEnabled = false; // a partly loaded collection is "not loaded": a lazy load would fetch all 473
        Assert.Equal(
            await Reference.Rows<int>($"SELECT SalesOrderID AS Value FROM Sales.SalesOrderHeader WHERE SalesPersonID = 277 AND YEAR(OrderDate) = 2014 ORDER BY SalesOrderID"),
            salesPerson.SalesOrderHeaders.Select(h => h.SalesOrderId).Order().ToList());
    }
}
```

- [ ] **Step 3: Write `Exercises.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E05;

// E05 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E05Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E05Exercises : E05Checks
{
    // Exercise 1 — Orders with their lines
    // Return the orders of customer `customerId`, ordered by SalesOrderId, each with its
    // SalesOrderDetails collection loaded.
    // Rules: one round trip in total, including anything the check reads afterwards.
    protected override Task<List<SalesOrderHeader>> Ex1(E05Context db, int customerId)
        => throw new NotImplementedException();

    // Exercise 2 — Only the recent orders
    // Return customer `customerId` with only the orders placed on or after `since` loaded into
    // SalesOrderHeaders.
    // Rules: one round trip.
    protected override Task<Customer> Ex2(E05Context db, int customerId, DateTime since)
        => throw new NotImplementedException();

    // Exercise 3 — Load the reviews later
    // `product` is already tracked by `db`, and lazy loading is switched off for this exercise.
    // Load its ProductReviews collection.
    // Rules: one round trip; don't query the product again.
    protected override Task Ex3(E05Context db, Product product)
        => throw new NotImplementedException();

    // Exercise 4 — Summaries instead of graphs
    // Return one OrderSummary per order placed on `orderDate`, ordered by SalesOrderId: the order's
    // SalesOrderId, the customer's AccountNumber, its number of lines, and its TotalDue.
    // Rules: one round trip; nothing tracked; don't load the lines (count them in SQL).
    protected override Task<List<OrderSummary>> Ex4(E05Context db, DateTime orderDate)
        => throw new NotImplementedException();

    // Exercise 5 — No N+1
    // Return every territory's Name and its number of customers, ordered by territory name.
    // Rules: one round trip, however many territories there are.
    protected override Task<List<TerritoryCustomers>> Ex5(E05Context db)
        => throw new NotImplementedException();

    // Challenge 1 — No cartesian explosion
    // Return sales person `salesPersonId` with SalesOrderHeaders, Stores and SalesPersonQuotaHistories
    // all loaded. Loading them in one query would return orders × stores × quotas rows
    // (473 × 76 × 12 = 431,376 for sales person 277).
    // Rules: no query joins two collections to the parent; at most 4 round trips.
    protected override Task<SalesPerson> Ch1(E05Context db, int salesPersonId)
        => throw new NotImplementedException();

    // Challenge 2 — The three most recent orders each
    // Return the customers with at least `minOrders` orders, ordered by CustomerId, each with only its
    // 3 most recent orders loaded (OrderDate descending, then SalesOrderId descending), in that order.
    // Rules: one round trip.
    protected override Task<List<Customer>> Ch2(E05Context db, int minOrders)
        => throw new NotImplementedException();

    // Challenge 3 — Load part, count the rest
    // `salesPerson` is already tracked by `db`. Load only its orders placed in calendar year `year` into
    // SalesOrderHeaders, and return its number of stores without loading them.
    // Rules: exactly two round trips; the Stores collection must stay unloaded.
    protected override Task<int> Ch3(E05Context db, SalesPerson salesPerson, int year)
        => throw new NotImplementedException();
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E05Exercises`
Expected: `Passed!  - Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

- [ ] **Step 4: Write `Solutions.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E05;

// Worked solutions for E05, each followed by a Why note. Run: dotnet test --filter E05Solutions
public class E05Solutions : E05Checks
{
    // Exercise 1 — Orders with their lines
    // Return the orders of customer `customerId`, ordered by SalesOrderId, each with its
    // SalesOrderDetails collection loaded.
    // Rules: one round trip in total, including anything the check reads afterwards.
    protected override Task<List<SalesOrderHeader>> Ex1(E05Context db, int customerId)
        => db.SalesOrderHeaders
            .Where(h => h.CustomerId == customerId)
            .OrderBy(h => h.SalesOrderId)
            .Include(h => h.SalesOrderDetails)
            .ToListAsync();
    // Why: Include adds the lines to the same query as a LEFT JOIN, ordered so EF can stitch each line to
    // its order, and marks every collection as loaded. Tempting-but-wrong: skip the Include and let lazy
    // loading fetch each order's lines when the check counts them: 1 + 28 round trips (the N+1 problem).

    // Exercise 2 — Only the recent orders
    // Return customer `customerId` with only the orders placed on or after `since` loaded into
    // SalesOrderHeaders.
    // Rules: one round trip.
    protected override Task<Customer> Ex2(E05Context db, int customerId, DateTime since)
        => db.Customers
            .Include(c => c.SalesOrderHeaders.Where(h => h.OrderDate >= since))
            .SingleAsync(c => c.CustomerId == customerId);
    // Why: a filtered Include puts the Where inside the join, so only matching orders come back, and EF
    // still marks the collection as loaded. Tempting-but-wrong: Include everything and filter the
    // collection in C#: every order crosses the wire, and the entity's collection still holds all of them.

    // Exercise 3 — Load the reviews later
    // `product` is already tracked by `db`, and lazy loading is switched off for this exercise.
    // Load its ProductReviews collection.
    // Rules: one round trip; don't query the product again.
    protected override Task Ex3(E05Context db, Product product)
        => db.Entry(product).Collection(p => p.ProductReviews).LoadAsync();
    // Why: explicit loading runs one query for exactly one navigation of an entity you already have, and
    // marks it loaded. It's the controlled version of lazy loading: you decide when the round trip
    // happens. Tempting-but-wrong: query the product again with Include. That re-reads the product row too.

    // Exercise 4 — Summaries instead of graphs
    // Return one OrderSummary per order placed on `orderDate`, ordered by SalesOrderId: the order's
    // SalesOrderId, the customer's AccountNumber, its number of lines, and its TotalDue.
    // Rules: one round trip; nothing tracked; don't load the lines (count them in SQL).
    protected override Task<List<OrderSummary>> Ex4(E05Context db, DateTime orderDate)
        => db.SalesOrderHeaders
            .Where(h => h.OrderDate == orderDate)
            .OrderBy(h => h.SalesOrderId)
            .Select(h => new OrderSummary(h.SalesOrderId, h.Customer.AccountNumber, h.SalesOrderDetails.Count(), h.TotalDue))
            .ToListAsync();
    // Why: a projection needs no Include at all. Navigations used inside Select become joins and
    // subqueries, only the selected columns are read, and nothing is tracked or lazy-loaded. When you
    // only need a few values, projection beats loading entity graphs.

    // Exercise 5 — No N+1
    // Return every territory's Name and its number of customers, ordered by territory name.
    // Rules: one round trip, however many territories there are.
    protected override Task<List<TerritoryCustomers>> Ex5(E05Context db)
        => db.SalesTerritories
            .OrderBy(t => t.Name)
            .Select(t => new TerritoryCustomers(t.Name, t.Customers.Count()))
            .ToListAsync();
    // Why: Count() inside the projection is a correlated subquery, so the database counts. Tempting-but-
    // wrong: load the territories, then read t.Customers.Count in a loop. With lazy loading on, that is
    // one extra query per territory, each loading every customer of that territory just to count them.

    // Challenge 1 — No cartesian explosion
    // Return sales person `salesPersonId` with SalesOrderHeaders, Stores and SalesPersonQuotaHistories
    // all loaded. Loading them in one query would return orders × stores × quotas rows
    // (473 × 76 × 12 = 431,376 for sales person 277).
    // Rules: no query joins two collections to the parent; at most 4 round trips.
    protected override Task<SalesPerson> Ch1(E05Context db, int salesPersonId)
        => db.SalesPeople
            .Include(s => s.SalesOrderHeaders)
            .Include(s => s.Stores)
            .Include(s => s.SalesPersonQuotaHistories)
            .AsSplitQuery()
            .SingleAsync(s => s.BusinessEntityId == salesPersonId);
    // Why: AsSplitQuery sends one query for the sales person and one per collection (4 in total), so the
    // rows add up (1 + 473 + 76 + 12) instead of multiplying. The trade-off: the queries run at
    // different moments, so without a snapshot or serializable transaction a concurrent writer can make
    // them disagree. Three explicit loads would also pass, at the same 4 round trips.

    // Challenge 2 — The three most recent orders each
    // Return the customers with at least `minOrders` orders, ordered by CustomerId, each with only its
    // 3 most recent orders loaded (OrderDate descending, then SalesOrderId descending), in that order.
    // Rules: one round trip.
    protected override Task<List<Customer>> Ch2(E05Context db, int minOrders)
        => db.Customers
            .Where(c => c.SalesOrderHeaders.Count() >= minOrders)
            .OrderBy(c => c.CustomerId)
            .Include(c => c.SalesOrderHeaders
                .OrderByDescending(h => h.OrderDate)
                .ThenByDescending(h => h.SalesOrderId)
                .Take(3))
            .ToListAsync();
    // Why: a filtered Include accepts OrderBy, Skip and Take as well as Where. EF numbers each customer's
    // orders with ROW_NUMBER() OVER (PARTITION BY CustomerID ...) and keeps rows 1-3: the top-N-per-group
    // pattern from E03, applied to loading.

    // Challenge 3 — Load part, count the rest
    // `salesPerson` is already tracked by `db`. Load only its orders placed in calendar year `year` into
    // SalesOrderHeaders, and return its number of stores without loading them.
    // Rules: exactly two round trips; the Stores collection must stay unloaded.
    protected override async Task<int> Ch3(E05Context db, SalesPerson salesPerson, int year)
    {
        var entry = db.Entry(salesPerson);
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);
        await entry.Collection(s => s.SalesOrderHeaders).Query()
            .Where(h => h.OrderDate >= start && h.OrderDate < end)
            .LoadAsync();
        return await entry.Collection(s => s.Stores).Query().CountAsync();
    }
    // Why: Collection(...).Query() is the navigation as an IQueryable: filter it and Load to fill the
    // collection with just those rows, or aggregate it to answer a question without loading anything.
    // Note the trap the check avoids: a partly loaded collection is still "not loaded", so with lazy
    // loading on, the next read of salesPerson.SalesOrderHeaders would fetch all 473 orders.
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E05Solutions`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 5: Write `Examples.cs`**

```csharp
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E05;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E05Examples --logger "console;verbosity=detailed"
// Db is an E05Context: lazy loading is on unless an example switches it off.
public class E05Examples : LabTest<E05Context>
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: with lazy loading, the first read of an unloaded navigation runs a query.
    [Fact]
    public async Task Lazy_loading_queries_on_first_touch()
    {
        var customer = await Db.Customers.SingleAsync(c => c.CustomerId == 11091);
        Print(customer.GetType().Name); // CustomerProxy: a generated subclass that intercepts navigations
        Sql.AssertRoundTrips(1);

        var orders = customer.SalesOrderHeaders.Count; // this line runs SQL
        var again = customer.SalesOrderHeaders.Count;  // loaded now: no more SQL

        Assert.Equal(28, orders);
        Assert.Equal(orders, again);
        Sql.AssertRoundTrips(2);
    }

    // Concept 1: without lazy loading, an unloaded navigation is just empty. No error, no query.
    [Fact]
    public async Task Without_lazy_loading_unloaded_navigations_are_empty()
    {
        Db.ChangeTracker.LazyLoadingEnabled = false;
        var customer = await Db.Customers.SingleAsync(c => c.CustomerId == 11091);

        Assert.Empty(customer.SalesOrderHeaders); // 28 orders exist; none were loaded
        Assert.False(Db.Entry(customer).Collection(c => c.SalesOrderHeaders).IsLoaded);
        Sql.AssertRoundTrips(1);
    }

    // Concept 2: a loop over lazy navigations is N+1 round trips. Include makes it one.
    [Fact]
    public async Task N_plus_one_and_its_fix()
    {
        var salesPeople = await Db.SalesPeople.ToListAsync();
        var lazyTotal = salesPeople.Sum(s => s.Stores.Count); // one query per sales person
        Assert.Equal(1 + salesPeople.Count, Sql.Count);       // 1 + 17

        Sql.Reset();
        var eager = await NewContext().SalesPeople.Include(s => s.Stores).ToListAsync();
        Assert.Equal(lazyTotal, eager.Sum(s => s.Stores.Count));
        Sql.AssertRoundTrips(1);
    }

    // Concept 3: Include / ThenInclude add LEFT JOINs to the same query.
    [Fact]
    public async Task Include_and_ThenInclude_join_in_one_query()
    {
        var order = await Db.SalesOrderHeaders
            .Include(h => h.SalesOrderDetails).ThenInclude(d => d.SpecialOfferProduct).ThenInclude(s => s.Product)
            .SingleAsync(h => h.SalesOrderId == 43659);

        Print(string.Join(", ", order.SalesOrderDetails.Select(d => d.SpecialOfferProduct.Product.Name)));
        Sql.AssertRoundTrips(1);
        Assert.Contains("[Production].[Product]", Sql.All);
    }

    // Concept 3: a filtered Include filters, sorts and limits the loaded collection inside the query.
    [Fact]
    public async Task Filtered_Include_with_Take_uses_ROW_NUMBER()
    {
        var customer = await Db.Customers
            .Include(c => c.SalesOrderHeaders.OrderByDescending(h => h.TotalDue).Take(2))
            .SingleAsync(c => c.CustomerId == 11091);

        Assert.Equal(2, customer.SalesOrderHeaders.Count);
        Assert.True(Db.Entry(customer).Collection(c => c.SalesOrderHeaders).IsLoaded);
        Assert.Contains("ROW_NUMBER() OVER(PARTITION BY [s].[CustomerID] ORDER BY [s].[TotalDue] DESC)", Sql.All);
        Sql.AssertRoundTrips(1);
    }

    // Concept 4: explicit loading. Load one navigation when you decide to, or query it without loading.
    [Fact]
    public async Task Explicit_loading_and_Query()
    {
        Db.ChangeTracker.LazyLoadingEnabled = false;
        var product = await Db.Products.SingleAsync(p => p.ProductId == 937);
        var reviews = Db.Entry(product).Collection(p => p.ProductReviews);

        var count = await reviews.Query().CountAsync();   // SELECT COUNT(*): nothing loaded
        Assert.Equal(2, count);
        Assert.False(reviews.IsLoaded);

        await reviews.LoadAsync();                        // SELECT the reviews: now loaded
        Assert.True(reviews.IsLoaded);
        Assert.Equal(2, product.ProductReviews.Count);
        Sql.AssertRoundTrips(3);
    }

    // Concept 5: a projection reads what it selects. No Include, no tracking, no lazy loading.
    [Fact]
    public async Task Projection_needs_no_Include()
    {
        var rows = await Db.SalesOrderHeaders
            .Where(h => h.CustomerId == 11091)
            .Select(h => new { h.SalesOrderId, Lines = h.SalesOrderDetails.Count(), h.Customer.AccountNumber })
            .ToListAsync();

        Assert.Equal(28, rows.Count);
        Assert.Empty(Db.ChangeTracker.Entries());
        Sql.AssertRoundTrips(1);
    }

    // Concept 6: two collection Includes in one query multiply the rows (a cartesian explosion).
    [Fact]
    public async Task Two_collection_Includes_multiply_rows()
    {
        var single = Db.SalesPeople
            .Where(s => s.BusinessEntityId == 277)
            .Include(s => s.SalesOrderHeaders)
            .Include(s => s.Stores);
        var sql = single.ToQueryString();
        Assert.Equal(2, sql.Split("LEFT JOIN").Length - 1);

        // The rows that single query would return: every order paired with every store.
        var rows = (await Reference.Rows<int>($"""
            SELECT COUNT(*) AS Value
            FROM Sales.SalesPerson s
            LEFT JOIN Sales.SalesOrderHeader h ON h.SalesPersonID = s.BusinessEntityID
            LEFT JOIN Sales.Store st ON st.SalesPersonID = s.BusinessEntityID
            WHERE s.BusinessEntityID = 277
            """)).Single();
        Print($"{rows:N0} rows to load 473 orders and 76 stores");
        Assert.Equal(473 * 76, rows);
    }

    // Concept 6: AsSplitQuery sends one query per collection; the rows add up instead.
    [Fact]
    public async Task AsSplitQuery_sends_one_query_per_collection()
    {
        var salesPerson = await Db.SalesPeople
            .Include(s => s.SalesOrderHeaders)
            .Include(s => s.Stores)
            .AsSplitQuery()
            .SingleAsync(s => s.BusinessEntityId == 277);

        Sql.AssertRoundTrips(3); // the sales person, its orders, its stores
        Assert.DoesNotContain("LEFT JOIN", Sql.All);
        Assert.Equal(473, salesPerson.SalesOrderHeaders.Count);
        Assert.Equal(76, salesPerson.Stores.Count);
        Sql.AssertRoundTrips(3);
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E05Examples`
Expected: `Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9`.

- [ ] **Step 6: Write `README.md`**

````markdown
# E05 — Loading Related Data

## What you'll learn

- The three ways to load a navigation (lazy, explicit, eager) and the round trips each one costs
- How to spot the N+1 problem in captured SQL, and the two fixes: `Include` or a projection
- Filtered `Include`: loading only some of a collection, including the top N per parent
- Explicit loading with `Query()`: filter, count or aggregate a navigation without loading all of it
- Why two collection `Include`s in one query multiply rows (cartesian explosion), and what `AsSplitQuery` trades for fixing it

## Builds on

- T-SQL [lesson 04](../../../lessons/04-joins-and-sets/lesson.ipynb) — joins, and why one-to-many joins repeat the "one" side
- T-SQL [lesson 13](../../../lessons/13-execution-plans-and-tuning/lesson.ipynb) — reading what a query really costs
- T-SQL [lesson 11](../../../lessons/11-transactions-errors-concurrency/lesson.ipynb) — isolation levels, for the split-query consistency trade-off
- EF lessons [E02](../E02-projections-joins-nulls/README.md) (projections, navigations as joins) and [E03](../E03-grouping-subqueries-limits/README.md) (top-N per group)

## Setup

```
dotnet test --filter E05Examples --logger "console;verbosity=detailed"
```

Every E05 test runs on `E05Context` (`E05Context.cs`): `AdventureWorksContext` with **lazy-loading proxies** switched on. The scaffolded navigations are all `virtual`, so EF generates a subclass (`CustomerProxy`) that runs a query the first time you read an unloaded navigation. That makes every hidden load visible as a round trip in the captured SQL. An example or check that needs lazy loading off sets `Db.ChangeTracker.LazyLoadingEnabled = false`.

Lazy loading needs the `Microsoft.EntityFrameworkCore.Proxies` package and `UseLazyLoadingProxies()`. Neither is on by default in EF Core.

## Concepts

### 1. Three ways to load a navigation

| Style | How | When the SQL runs |
|---|---|---|
| Eager | `Include(...)` in the query | with the query, in the same round trip (or split, see 6) |
| Explicit | `db.Entry(e).Collection(...).LoadAsync()` / `.Reference(...)` | when you call it: one round trip per call |
| Lazy | read the navigation property | the first time you touch it: one round trip per navigation per entity |

Without lazy loading, a navigation you didn't load is just empty (or `null` for a reference). There's no error and no query, which is how "the customer has no orders" bugs happen. With lazy loading the data appears, but at the cost of a round trip you can't see in the code.

Examples: `Lazy_loading_queries_on_first_touch`, `Without_lazy_loading_unloaded_navigations_are_empty`.

### 2. N+1

Load N parents, then read a navigation on each one inside a loop: that is 1 query for the parents plus N lazy loads. It looks harmless in code and grows with the data. The captured SQL shows it right away: the same query repeated with a different parameter.

Two fixes:

- **`Include`** the navigation in the first query, when you need the entities.
- **Project** what you need (`Select(t => new { t.Name, Customers = t.Customers.Count() })`), when you only need values. The database does the counting and nothing is loaded.

Example: `N_plus_one_and_its_fix`.

### 3. Include, ThenInclude, filtered Include

`Include(h => h.SalesOrderDetails).ThenInclude(d => d.SpecialOfferProduct)` walks a path; every step is a join in the same query. A **filtered Include** limits what goes into the collection:

```csharp
db.Customers.Include(c => c.SalesOrderHeaders
    .Where(h => h.OrderDate >= since)
    .OrderByDescending(h => h.OrderDate)
    .Take(3))
```

Inside a filtered Include you can use `Where`, `OrderBy`/`ThenBy`, `Skip` and `Take`. `Take` per parent is translated with `ROW_NUMBER() OVER (PARTITION BY ...)`, the top-N-per-group pattern from E03. The collection is still marked loaded, so lazy loading won't later "complete" it.

Examples: `Include_and_ThenInclude_join_in_one_query`, `Filtered_Include_with_Take_uses_ROW_NUMBER`.

### 4. Explicit loading and `Query()`

`db.Entry(product).Collection(p => p.ProductReviews)` gives you the navigation of one tracked entity:

- `.LoadAsync()` loads it and marks it loaded.
- `.Query()` returns it as an `IQueryable`. Add `Where` and call `LoadAsync()` to load only part of it, or aggregate (`CountAsync()`, `SumAsync(...)`) to answer a question without loading anything.

A collection loaded through `Query().Where(...)` is **not** marked loaded. With lazy loading on, the next read of the property loads the whole collection.

Example: `Explicit_loading_and_Query`.

### 5. Projection instead of loading

A `Select` never needs `Include`. Navigations inside the projection become joins and correlated subqueries, only the selected columns are read, and the result is not tracked, so nothing can be lazy-loaded later. For read-only screens and reports, project. Load entity graphs when you are going to change them (Tier C).

Example: `Projection_needs_no_Include`.

### 6. Cartesian explosion and `AsSplitQuery`

One query with two collection `Include`s on the same parent returns every combination: sales person 277's 473 orders × 76 stores = 35,948 rows to load 549 entities, and adding the 12 quota rows makes it 431,376. EF de-duplicates on the client, but the database still produces and sends every row.

`AsSplitQuery()` sends one query for the parents and one per collection, each joined back to the parent's filter, so the rows add up (1 + 473 + 76) instead of multiplying.

The trade-off: the split queries run one after another. Under the default `READ COMMITTED`, another session can change the data between them, and the collections can then disagree with the parent rows (T-SQL lesson 11). If that matters, run them in a `SNAPSHOT` or `SERIALIZABLE` transaction, or keep the single query. You can make splitting the default with `UseSqlServer(..., o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))` and opt out per query with `AsSingleQuery()`.

Examples: `Two_collection_Includes_multiply_rows`, `AsSplitQuery_sends_one_query_per_collection`.

## Pitfalls

- **Lazy loading in a loop.** N+1, invisible in code review. Watch the SQL log.
- **Forgetting `Include` without lazy loading.** Empty collections that look like real data.
- **Filtering a loaded collection in C#.** `Include` everything, then `.Where` in memory, ships every row. Use a filtered `Include` or `Query()`.
- **Partial explicit load plus lazy loading.** `Query().Where(...).LoadAsync()` doesn't mark the collection loaded. Reading the property later loads all of it.
- **Several collection `Include`s in one query.** Rows multiply. Split, or project.

## Exercises

```
dotnet test --filter E05Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
````

- [ ] **Step 7: Prompts are identical in Exercises.cs and Solutions.cs**

```bash
cd efcore && python - lessons/E05-loading-related-data <<'EOF'
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
git add efcore/lessons/E05-loading-related-data
git commit -m "feat: add EF Core lesson E05 - loading related data"
```

- [ ] **Step 9: A wrong answer fails fast and says why (Review Focus 1).** Temporarily replace the body of `Ex1` in `Exercises.cs` (the `=> throw new NotImplementedException();` line under `Ex1`) with the lazy-loading answer:

```csharp
        => db.SalesOrderHeaders.Where(h => h.CustomerId == customerId).OrderBy(h => h.SalesOrderId).ToListAsync();
```

Run: `cd efcore && dotnet build && dotnet test --no-build --filter "FullyQualifiedName~E05Exercises.Ex1"`
Expected: `Failed: 1`, the message `Every order's SalesOrderDetails must be loaded by your query.`, and a test duration under 2 s.

Then restore the committed file and confirm nothing is left over:

```bash
git checkout -- efcore/lessons/E05-loading-related-data/Exercises.cs
git status --short
```

Expected: `git status --short` prints nothing.

---

## Task 3: Lesson E06 — Query performance

**Files:**
- Create: `efcore/lessons/E06-query-performance/Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `LabTest` (`Db`, `NewContext()`, `Sql`, `Reference`), `[Exercise]`, `QueryPlan.OperatorsAsync(DbContext, IQueryable)` from Task 1, the scaffolded model.
- Produces: namespace `EfCoreLessons.E06`: records `OrderRef`, `ProductPrice`, classes `E06Checks`, `E06Exercises`, `E06Solutions`, `E06Examples`. Nothing later depends on them.

- [ ] **Step 1: Write `Checks.cs`**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E06;

public record OrderRef(int SalesOrderId, DateTime OrderDate, decimal TotalDue);
public record ProductPrice(int ProductId, string Name, decimal ListPrice);

// The checks for E06. E06Exercises (yours) and E06Solutions both inherit them.
public abstract class E06Checks : LabTest
{
    protected abstract Task<List<Product>> Ex1(AdventureWorksContext db, decimal minPrice);
    protected abstract Task<List<SalesOrderHeader>> Ex2(AdventureWorksContext db, int customerId);
    protected abstract Task<List<OrderRef>> Ex3(AdventureWorksContext db, int afterSalesOrderId, int pageSize);
    protected abstract IQueryable<CurrencyRate> Ex4(AdventureWorksContext db, int year);
    protected abstract Func<AdventureWorksContext, int, Task<string?>> Ex5();
    protected abstract Task<long> Ch1(AdventureWorksContext db, string tag);
    protected abstract Task<List<ProductPrice>> Ch2(AdventureWorksContext db, ProductPrice? after, int pageSize);
    protected abstract IQueryable<Person> Ch3(AdventureWorksContext db, string prefix);

    [Exercise]
    public async Task Ex1_ReadOnlyEntities()
    {
        var products = await Ex1(Db, 3000m);

        Assert.Equal(
            await Reference.Rows<int>($"SELECT ProductID AS Value FROM Production.Product WHERE ListPrice > 3000 ORDER BY ProductID"),
            products.Select(p => p.ProductId).ToList());
        Assert.Empty(Db.ChangeTracker.Entries()); // nothing tracked
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ex2_OneCustomerInstance()
    {
        var orders = await Ex2(Db, 11091);

        Assert.Equal(28, orders.Count);
        Assert.Empty(Db.ChangeTracker.Entries());
        Assert.All(orders, o => Assert.Equal(11091, o.Customer.CustomerId));
        Assert.Single(orders.Select(o => o.Customer).Distinct(ReferenceEqualityComparer.Instance)); // one shared instance
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ex3_KeysetPaging()
    {
        var pages = new List<List<OrderRef>>();
        var after = 0;
        for (var i = 0; i < 3; i++)
        {
            var page = await Ex3(Db, after, 25);
            pages.Add(page);
            after = page[^1].SalesOrderId;
        }

        Assert.Equal(await Reference.Rows<OrderRef>($"""
            SELECT SalesOrderID AS SalesOrderId, OrderDate, TotalDue
            FROM Sales.SalesOrderHeader
            ORDER BY SalesOrderID
            OFFSET 0 ROWS FETCH NEXT 75 ROWS ONLY
            """), pages.SelectMany(p => p).ToList());
        Sql.AssertRoundTrips(3);
        Assert.DoesNotContain("OFFSET", Sql.All);          // no rows skipped and thrown away
        Assert.Contains("[SalesOrderID] > @", Sql.All);    // continue from the last key seen
    }

    [Exercise]
    public async Task Ex4_SargableYear()
    {
        var query = Ex4(Db, 2013);

        Assert.Equal(
            (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Sales.CurrencyRate WHERE CurrencyRateDate >= '2013-01-01' AND CurrencyRateDate < '2014-01-01'")).Single(),
            await query.CountAsync());
        Assert.DoesNotContain("DATEPART", Sql.All);
        var operators = await QueryPlan.OperatorsAsync(Db, query.Select(r => r.CurrencyRateId));
        Assert.Contains("Index Seek", operators); // the CurrencyRateDate index is usable
    }

    [Exercise]
    public async Task Ex5_CompiledOnce()
    {
        var query = Ex5();

        Assert.Same(query, Ex5()); // compiled once and reused, not compiled per call
        Assert.Contains("Compiled", query.Target?.GetType().Name ?? ""); // built with EF.CompileAsyncQuery
        Assert.Equal("Mountain-200 Black, 38", await query(Db, 782));
        Assert.Equal("Adjustable Race", await query(Db, 1));
        Assert.Null(await query(Db, -1));
        Sql.AssertRoundTrips(3);
        Assert.Single(Sql.Commands.Distinct()); // one parameterized SQL text
    }

    [Exercise]
    public async Task Ch1_FindItInTheDmvs()
    {
        var tag = "E06Ch1-" + Guid.NewGuid().ToString("N"); // unique: no earlier run can match

        var executions = await Ch1(Db, tag);

        Assert.Equal(3, executions);
        Sql.AssertRoundTrips(4);
        Assert.All(Sql.Commands.Take(3), sql => Assert.StartsWith($"-- {tag}", sql));
        Assert.Contains("dm_exec_query_stats", Sql.Commands[3]);
    }

    [Exercise]
    public async Task Ch2_KeysetOnTwoColumns()
    {
        var all = new List<ProductPrice>();
        ProductPrice? after = null;
        while (true)
        {
            var page = await Ch2(Db, after, 50);
            all.AddRange(page);
            if (page.Count < 50) break;
            after = page[^1];
        }

        Assert.Equal(await Reference.Rows<ProductPrice>($"""
            SELECT ProductID AS ProductId, Name, ListPrice
            FROM Production.Product
            WHERE ListPrice > 0
            ORDER BY ListPrice DESC, ProductID
            """), all);
        Sql.AssertRoundTrips(7); // 304 products: six full pages and a short one
        Assert.DoesNotContain("OFFSET", Sql.All);
    }

    [Exercise]
    public async Task Ch3_PrefixThatSeeks()
    {
        var expected = (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Person.Person WHERE LastName LIKE 'sm%'")).Single();

        Assert.Equal(expected, await Ch3(Db, "sm").CountAsync());
        Assert.Equal(expected, await Ch3(Db, "SM").CountAsync());
        Assert.DoesNotContain("UPPER(", Sql.All);
        Assert.DoesNotContain("LOWER(", Sql.All);
        var operators = await QueryPlan.OperatorsAsync(Db, Ch3(Db, "Sm").Select(p => p.BusinessEntityId));
        Assert.Contains("Index Seek", operators); // IX_Person_LastName_FirstName_MiddleName is usable
    }
}
```

- [ ] **Step 2: Write `Exercises.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E06;

// E06 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E06Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E06Exercises : E06Checks
{
    // Exercise 1 — Read-only entities
    // Return the Product entities with ListPrice > minPrice, ordered by ProductId, for display only.
    // Rules: one round trip; the change tracker must stay empty.
    protected override Task<List<Product>> Ex1(AdventureWorksContext db, decimal minPrice)
        => throw new NotImplementedException();

    // Exercise 2 — One customer instance
    // Return the orders of customer `customerId` with each order's Customer loaded, untracked. All
    // the orders must share a single Customer object, not one copy per order.
    // Rules: one round trip; nothing tracked.
    protected override Task<List<SalesOrderHeader>> Ex2(AdventureWorksContext db, int customerId)
        => throw new NotImplementedException();

    // Exercise 3 — Keyset paging
    // Return the next `pageSize` orders after SalesOrderId `afterSalesOrderId`, in SalesOrderId order,
    // as OrderRef rows. The first page is requested with afterSalesOrderId = 0.
    // Rules: one round trip per page; no OFFSET.
    protected override Task<List<OrderRef>> Ex3(AdventureWorksContext db, int afterSalesOrderId, int pageSize)
        => throw new NotImplementedException();

    // Exercise 4 — A year the index can use
    // Return a query (don't run it) for the currency rates whose CurrencyRateDate falls in calendar
    // year `year`. The check counts it and inspects its plan.
    // Rules: the SQL must not apply a function to CurrencyRateDate, so its index can be used.
    protected override IQueryable<CurrencyRate> Ex4(AdventureWorksContext db, int year)
        => throw new NotImplementedException();

    // Exercise 5 — Compile it once
    // Return a compiled query that gives a product's Name by ProductId, or null when there is no such
    // product. Every call must return the same delegate.
    // Rules: build it with EF.CompileAsyncQuery; one round trip per execution.
    protected override Func<AdventureWorksContext, int, Task<string?>> Ex5()
        => throw new NotImplementedException();

    // Challenge 1 — Find it in the DMVs
    // Run db.Products.Count(p => p.ListPrice > 0) three times, tagged with `tag` (TagWith). Then read
    // from sys.dm_exec_query_stats (joined to sys.dm_exec_sql_text) how many times SQL Server has executed
    // the statement whose text contains the tag, and return that number.
    // Rules: four round trips: the three counts and one DMV query.
    protected override Task<long> Ch1(AdventureWorksContext db, string tag)
        => throw new NotImplementedException();

    // Challenge 2 — Keyset on two columns
    // Return the next `pageSize` products with ListPrice > 0 after `after` (null = first page), ordered
    // by ListPrice descending, then ProductId, as ProductPrice rows.
    // Rules: one round trip per page; no OFFSET.
    protected override Task<List<ProductPrice>> Ch2(AdventureWorksContext db, ProductPrice? after, int pageSize)
        => throw new NotImplementedException();

    // Challenge 3 — A prefix that seeks
    // Return a query (don't run it) for the people whose LastName starts with `prefix`, ignoring case
    // ("sm" and "SM" both match "Smith").
    // Rules: no UPPER/LOWER in the SQL; the IX_Person_LastName_FirstName_MiddleName index must be usable.
    protected override IQueryable<Person> Ch3(AdventureWorksContext db, string prefix)
        => throw new NotImplementedException();
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E06Exercises`
Expected: `Passed!  - Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

- [ ] **Step 3: Write `Solutions.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E06;

// Worked solutions for E06, each followed by a Why note. Run: dotnet test --filter E06Solutions
public class E06Solutions : E06Checks
{
    // Exercise 1 — Read-only entities
    // Return the Product entities with ListPrice > minPrice, ordered by ProductId, for display only.
    // Rules: one round trip; the change tracker must stay empty.
    protected override Task<List<Product>> Ex1(AdventureWorksContext db, decimal minPrice)
        => db.Products
            .AsNoTracking()
            .Where(p => p.ListPrice > minPrice)
            .OrderBy(p => p.ProductId)
            .ToListAsync();
    // Why: a tracked query stores each entity plus a snapshot of its original values so SaveChanges can
    // find changes later. For read-only results that is pure overhead in memory and CPU. AsNoTracking
    // skips it. The SQL is identical: tracking is a client-side cost.

    // Exercise 2 — One customer instance
    // Return the orders of customer `customerId` with each order's Customer loaded, untracked. All
    // the orders must share a single Customer object, not one copy per order.
    // Rules: one round trip; nothing tracked.
    protected override Task<List<SalesOrderHeader>> Ex2(AdventureWorksContext db, int customerId)
        => db.SalesOrderHeaders
            .AsNoTrackingWithIdentityResolution()
            .Include(h => h.Customer)
            .Where(h => h.CustomerId == customerId)
            .ToListAsync();
    // Why: AsNoTracking materializes a new object for every row, so the customer joined to 28 orders
    // becomes 28 identical Customer objects. AsNoTrackingWithIdentityResolution keeps a lookup for the
    // duration of the query only, so equal keys map to one instance, and still tracks nothing afterwards.

    // Exercise 3 — Keyset paging
    // Return the next `pageSize` orders after SalesOrderId `afterSalesOrderId`, in SalesOrderId order,
    // as OrderRef rows. The first page is requested with afterSalesOrderId = 0.
    // Rules: one round trip per page; no OFFSET.
    protected override Task<List<OrderRef>> Ex3(AdventureWorksContext db, int afterSalesOrderId, int pageSize)
        => db.SalesOrderHeaders
            .Where(h => h.SalesOrderId > afterSalesOrderId)
            .OrderBy(h => h.SalesOrderId)
            .Take(pageSize)
            .Select(h => new OrderRef(h.SalesOrderId, h.OrderDate, h.TotalDue))
            .ToListAsync();
    // Why: OFFSET n makes the server read and discard n rows on every page, so deep pages get slower and
    // rows shift if data changes between requests. "Key greater than the last one seen" is a seek on the
    // clustered index: page 1,000 costs the same as page 1. The price is no "jump to page N".

    // Exercise 4 — A year the index can use
    // Return a query (don't run it) for the currency rates whose CurrencyRateDate falls in calendar
    // year `year`. The check counts it and inspects its plan.
    // Rules: the SQL must not apply a function to CurrencyRateDate, so its index can be used.
    protected override IQueryable<CurrencyRate> Ex4(AdventureWorksContext db, int year)
    {
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);
        return db.CurrencyRates.Where(r => r.CurrencyRateDate >= start && r.CurrencyRateDate < end);
    }
    // Why: r.CurrencyRateDate.Year == year translates to DATEPART(year, [CurrencyRateDate]) = @year,
    // which has to be computed for every row: an index scan. A half-open range on the bare column lets
    // the optimizer seek the AK_CurrencyRate_CurrencyRateDate_... index (T-SQL lesson 12, SARGability).

    // Exercise 5 — Compile it once
    // Return a compiled query that gives a product's Name by ProductId, or null when there is no such
    // product. Every call must return the same delegate.
    // Rules: build it with EF.CompileAsyncQuery; one round trip per execution.
    static readonly Func<AdventureWorksContext, int, Task<string?>> ProductName =
        EF.CompileAsyncQuery((AdventureWorksContext db, int id) =>
            db.Products.Where(p => p.ProductId == id).Select(p => p.Name).SingleOrDefault());

    protected override Func<AdventureWorksContext, int, Task<string?>> Ex5() => ProductName;
    // Why: EF caches compiled queries anyway, but every execution still builds the expression tree and
    // looks it up in the cache. EF.CompileAsyncQuery does that work once and returns a delegate. Keep it
    // in a static field: compiling it on every call throws the saving away.

    // Challenge 1 — Find it in the DMVs
    // Run db.Products.Count(p => p.ListPrice > 0) three times, tagged with `tag` (TagWith). Then read
    // from sys.dm_exec_query_stats (joined to sys.dm_exec_sql_text) how many times SQL Server has executed
    // the statement whose text contains the tag, and return that number.
    // Rules: four round trips: the three counts and one DMV query.
    protected override async Task<long> Ch1(AdventureWorksContext db, string tag)
    {
        for (var i = 0; i < 3; i++)
            await db.Products.TagWith(tag).CountAsync(p => p.ListPrice > 0);

        var pattern = $"%{tag}%";
        return await db.Database.SqlQuery<long>($"""
            SELECT SUM(qs.execution_count) AS Value
            FROM sys.dm_exec_query_stats AS qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
            WHERE st.text LIKE {pattern}
            """).SingleAsync();
    }
    // Why: TagWith puts a "-- tag" comment at the top of the SQL, and the comment is part of the
    // statement text SQL Server stores with the plan. Searching the DMVs (or Query Store, where it is
    // enabled) for the tag finds your LINQ query's server-side statistics. Pass the pattern as a
    // parameter: pasted into the SQL text, the tag would make the DMV query match itself on its next run
    // (and EF1002 would warn about building SQL by interpolation).

    // Challenge 2 — Keyset on two columns
    // Return the next `pageSize` products with ListPrice > 0 after `after` (null = first page), ordered
    // by ListPrice descending, then ProductId, as ProductPrice rows.
    // Rules: one round trip per page; no OFFSET.
    protected override Task<List<ProductPrice>> Ch2(AdventureWorksContext db, ProductPrice? after, int pageSize)
    {
        var query = db.Products.Where(p => p.ListPrice > 0);
        if (after != null)
            query = query.Where(p => p.ListPrice < after.ListPrice
                || (p.ListPrice == after.ListPrice && p.ProductId > after.ProductId));
        return query
            .OrderByDescending(p => p.ListPrice).ThenBy(p => p.ProductId)
            .Take(pageSize)
            .Select(p => new ProductPrice(p.ProductId, p.Name, p.ListPrice))
            .ToListAsync();
    }
    // Why: the keyset predicate has to match the sort exactly: "lower price, or the same price and a
    // higher id". Comparing only ListPrice skips or repeats products that share a price across a page
    // boundary. Composing the Where only when there is a previous page keeps the first page simple.

    // Challenge 3 — A prefix that seeks
    // Return a query (don't run it) for the people whose LastName starts with `prefix`, ignoring case
    // ("sm" and "SM" both match "Smith").
    // Rules: no UPPER/LOWER in the SQL; the IX_Person_LastName_FirstName_MiddleName index must be usable.
    protected override IQueryable<Person> Ch3(AdventureWorksContext db, string prefix)
        => db.People.Where(p => p.LastName.StartsWith(prefix));
    // Why: AdventureWorks uses a case-insensitive collation, so LIKE 'sm%' already matches "Smith".
    // ToUpper()/ToLower() on the column add UPPER(...) around it, and a function on the column means a
    // scan. StartsWith becomes LIKE @prefix ESCAPE, which SQL Server can still turn into an index range seek.
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E06Solutions`
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 4: Write `Examples.cs`**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E06;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E06Examples --logger "console;verbosity=detailed"
public class E06Examples : LabTest
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    public record CachedPlan(string ObjType, int UseCounts);

    // Concept 1: tracking keeps every entity (and a snapshot of it); AsNoTracking keeps nothing.
    [Fact]
    public async Task Tracking_vs_AsNoTracking()
    {
        var tracked = await Db.Products.Where(p => p.ListPrice > 1000).ToListAsync();
        Assert.Equal(tracked.Count, Db.ChangeTracker.Entries().Count());

        var other = NewContext();
        var untracked = await other.Products.AsNoTracking().Where(p => p.ListPrice > 1000).ToListAsync();
        Assert.Equal(tracked.Count, untracked.Count);
        Assert.Empty(other.ChangeTracker.Entries());
        Assert.Equal(Sql.Commands[0], Sql.Commands[1]); // same SQL: tracking is a client-side cost
    }

    // Concept 1: AsNoTracking materializes one object per row; identity resolution shares them.
    [Fact]
    public async Task AsNoTracking_duplicates_identity_resolution_shares()
    {
        var plain = await Db.SalesOrderHeaders.AsNoTracking()
            .Include(h => h.Customer).Where(h => h.CustomerId == 11091).ToListAsync();
        var resolved = await Db.SalesOrderHeaders.AsNoTrackingWithIdentityResolution()
            .Include(h => h.Customer).Where(h => h.CustomerId == 11091).ToListAsync();

        Assert.Equal(28, plain.Select(h => h.Customer).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Single(resolved.Select(h => h.Customer).Distinct(ReferenceEqualityComparer.Instance));
        Assert.Empty(Db.ChangeTracker.Entries());
    }

    static readonly Func<AdventureWorksContext, decimal, IAsyncEnumerable<Product>> PricierThan =
        EF.CompileAsyncQuery((AdventureWorksContext db, decimal min) => db.Products.Where(p => p.ListPrice > min));

    // Concept 2: a compiled query is translated once and stored in a static field.
    [Fact]
    public async Task Compiled_query_runs_like_any_other()
    {
        var expensive = await PricierThan(Db, 3000m).ToListAsync();
        var cheaper = await PricierThan(Db, 2000m).ToListAsync();

        Assert.True(cheaper.Count > expensive.Count);
        Assert.Equal(Sql.Commands[0], Sql.Commands[1]); // one parameterized SQL text
        Assert.Contains("@min", Sql.All);
    }

    // Concept 3: keyset paging seeks the clustered index; a deep OFFSET scans and discards.
    [Fact]
    public async Task Keyset_seeks_offset_scans()
    {
        var lastSeen = 60000;
        var keyset = Db.SalesOrderHeaders.Where(h => h.SalesOrderId > lastSeen)
            .OrderBy(h => h.SalesOrderId).Take(20).Select(h => h.SalesOrderId);
        var offset = Db.SalesOrderHeaders
            .OrderBy(h => h.SalesOrderId).Skip(15000).Take(20).Select(h => h.SalesOrderId);

        var keysetPlan = await QueryPlan.OperatorsAsync(Db, keyset);
        var offsetPlan = await QueryPlan.OperatorsAsync(Db, offset);
        Print($"keyset: {string.Join(" <- ", keysetPlan)}");
        Print($"offset: {string.Join(" <- ", offsetPlan)}");

        Assert.Contains("Clustered Index Seek", keysetPlan);
        Assert.Contains("Clustered Index Scan", offsetPlan);
        Sql.AssertRoundTrips(0); // QueryPlan compiles the queries without running them
    }

    // Concept 4: streaming holds the connection's only open reader until you finish.
    [Fact]
    public async Task Streaming_holds_the_connection()
    {
        var streamed = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => // "already an open DataReader"
        {
            await foreach (var order in Db.SalesOrderHeaders.Where(h => h.CustomerId == 11091).AsAsyncEnumerable())
            {
                streamed++;
                await Db.Customers.CountAsync(); // a second query while the first is still reading
            }
        });
        Assert.Equal(1, streamed);

        var buffered = await Db.SalesOrderHeaders.Where(h => h.CustomerId == 11091).ToListAsync();
        foreach (var order in buffered)
            await Db.Customers.CountAsync(c => c.CustomerId == order.CustomerId); // fine: the reader is closed
    }

    // Concept 5: .Year on a column is DATEPART(...) on every row: no seek. A date range seeks.
    [Fact]
    public async Task Year_is_not_SARGable()
    {
        var year = 2013;
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);
        var byYear = Db.CurrencyRates.Where(r => r.CurrencyRateDate.Year == year).Select(r => r.CurrencyRateId);
        var byRange = Db.CurrencyRates.Where(r => r.CurrencyRateDate >= start && r.CurrencyRateDate < end).Select(r => r.CurrencyRateId);

        Assert.Contains("DATEPART(year, [c].[CurrencyRateDate])", byYear.ToQueryString());
        Assert.Contains("Index Scan", await QueryPlan.OperatorsAsync(Db, byYear));
        Assert.Contains("Index Seek", await QueryPlan.OperatorsAsync(Db, byRange));
        Assert.Equal(await byYear.CountAsync(), await byRange.CountAsync());
    }

    // Concept 5: any function around the column hides it from the index. StartsWith doesn't.
    [Fact]
    public async Task Functions_on_the_column_block_seeks()
    {
        var prefix = "Sm";
        var startsWith = Db.People.Where(p => p.LastName.StartsWith(prefix)).Select(p => p.BusinessEntityId);
        var substring = Db.People.Where(p => p.LastName.Substring(0, 2) == prefix).Select(p => p.BusinessEntityId);
        var upper = Db.People.Where(p => p.LastName.ToUpper().StartsWith(prefix)).Select(p => p.BusinessEntityId);

        Assert.Contains("Index Seek", await QueryPlan.OperatorsAsync(Db, startsWith)); // LIKE @prefix_startswith
        Assert.Contains("Index Scan", await QueryPlan.OperatorsAsync(Db, substring));  // SUBSTRING([LastName], ...)
        Assert.Contains("Index Scan", await QueryPlan.OperatorsAsync(Db, upper));      // UPPER([LastName]) LIKE ...
        Assert.Equal(await startsWith.CountAsync(), await substring.CountAsync());
    }

    // Concept 6: inlined constants compile one ad hoc plan per value; a parameter shares one plan.
    [Fact]
    public async Task Inlined_constants_pollute_the_plan_cache()
    {
        var tag = "E06-" + Guid.NewGuid().ToString("N");
        for (byte status = 1; status <= 5; status++)
            await Db.SalesOrderHeaders.TagWith($"{tag} constant").CountAsync(h => h.Status == EF.Constant(status));
        for (byte status = 1; status <= 5; status++)
            await Db.SalesOrderHeaders.TagWith($"{tag} parameter").CountAsync(h => h.Status == status);

        async Task<List<CachedPlan>> PlansTagged(string label)
        {
            var pattern = $"%{tag} {label}%";
            return await Db.Database.SqlQuery<CachedPlan>($"""
                SELECT cp.objtype AS ObjType, cp.usecounts AS UseCounts
                FROM sys.dm_exec_cached_plans AS cp
                CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) AS st
                WHERE st.text LIKE {pattern}
                """).ToListAsync();
        }

        var constantPlans = await PlansTagged("constant");
        var parameterPlans = await PlansTagged("parameter");
        Print($"constants: {string.Join(", ", constantPlans)}");
        Print($"parameter: {string.Join(", ", parameterPlans)}");
        Assert.Equal(5, constantPlans.Count(p => p.ObjType == "Adhoc"));         // one plan per value
        Assert.Equal(new CachedPlan("Prepared", 5), Assert.Single(parameterPlans)); // one plan, used 5 times
    }

    // Concept 7: TagWith adds a comment you can search for in logs, DMVs and Query Store.
    [Fact]
    public async Task TagWith_comments_the_SQL()
    {
        await Db.Products.TagWith("E06: product count for the dashboard").CountAsync();
        await Db.Products.TagWithCallSite().CountAsync();

        Assert.StartsWith("-- E06: product count for the dashboard", Sql.Commands[0]);
        Assert.Contains("Examples.cs:", Sql.Commands[1]); // file and line of the query
    }

    // Concept 8: in a database-first model, HasIndex is metadata the scaffolder copied from the database.
    [Fact]
    public void Scaffolded_indexes_are_model_metadata()
    {
        var person = Db.Model.FindEntityType(typeof(Person))!;
        var indexes = person.GetIndexes().Select(i => $"{i.GetDatabaseName()} ({string.Join(", ", i.Properties.Select(p => p.Name))})").ToList();
        foreach (var index in indexes) Print(index);

        Assert.Contains("IX_Person_LastName_FirstName_MiddleName (LastName, FirstName, MiddleName)", indexes);
        Assert.Equal(0, Sql.Count); // read from the model: no query
    }
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E06Examples --logger "console;verbosity=detailed" | grep -E 'keyset:|offset:|Passed!|Failed!'`
Expected: `keyset: Top <- Clustered Index Seek`, `offset: Top <- Clustered Index Scan`, and `Passed!  - Failed: 0, Passed: 10, Skipped: 0, Total: 10`.

- [ ] **Step 5: Write `README.md`**

````markdown
# E06 — Query Performance

## What you'll learn

- What change tracking costs, and when to switch it off (`AsNoTracking`, `AsNoTrackingWithIdentityResolution`)
- Compiled queries: what they save and how to hold on to them
- Keyset paging instead of `OFFSET`, including a two-column sort key
- Streaming (`AsAsyncEnumerable`) vs buffering (`ToListAsync`), and why streaming holds the connection
- Which LINQ shapes are SARGable: `.Year ==`, `Substring` and `ToUpper` vs ranges and `StartsWith`, checked against real execution plans
- How inlined constants pollute the plan cache, and how to find your EF query on the server with `TagWith`

## Builds on

- T-SQL [lesson 12](../../../lessons/12-indexes/lesson.ipynb) — indexes, seeks vs scans, SARGability
- T-SQL [lesson 13](../../../lessons/13-execution-plans-and-tuning/lesson.ipynb) — execution plans, parameter sniffing, the plan cache
- T-SQL [lesson 15](../../../lessons/15-query-lifecycle-optimizer/lesson.ipynb) — compilation and plan reuse
- Cheatsheets: [`05-indexes.md`](../../../cheatsheets/05-indexes.md), [`06-execution-plans.md`](../../../cheatsheets/06-execution-plans.md)
- EF lessons [E01](../E01-setup-and-generated-sql/README.md) (parameters vs constants) and [E03](../E03-grouping-subqueries-limits/README.md) (parameterized collections)

## Setup

```
dotnet test --filter E06Examples --logger "console;verbosity=detailed"
```

Several examples and checks look at execution plans through `QueryPlan.OperatorsAsync(db, query)` (in `Lab/QueryPlan.cs`). It compiles the query with `SET SHOWPLAN_XML ON`, so nothing runs, and returns the plan's physical operators, such as `["Top", "Clustered Index Seek"]`. The parameters become local variables, so the plan is compiled without sniffing their values. That's enough to tell a seek from a scan.

## Concepts

### 1. Change tracking costs memory and time

A tracked query registers every entity with the change tracker and keeps a snapshot of its original values, so `SaveChanges` can find what changed. The SQL is the same either way. Tracking is a client-side cost that grows with the number of rows.

| Option | Tracked? | Same key in two rows |
|---|---|---|
| default | yes | one instance (the tracker resolves identity) |
| `AsNoTracking()` | no | **a new instance per row**: 28 orders that share a customer give 28 `Customer` objects |
| `AsNoTrackingWithIdentityResolution()` | no | one instance, resolved only while the query runs |

Use `AsNoTracking` for read-only results, and the identity-resolution variant when the results contain repeated related entities you'll compare or traverse. To make no-tracking the default for a context, use `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` in its options.

Examples: `Tracking_vs_AsNoTracking`, `AsNoTracking_duplicates_identity_resolution_shares`.

### 2. Compiled queries

On every execution, EF builds the LINQ expression tree, computes a cache key from it and looks up the translated query. `EF.CompileAsyncQuery(...)` does that once and gives you a delegate that takes the context and the parameters:

```csharp
static readonly Func<AdventureWorksContext, int, Task<string?>> ProductName =
    EF.CompileAsyncQuery((AdventureWorksContext db, int id) =>
        db.Products.Where(p => p.ProductId == id).Select(p => p.Name).SingleOrDefault());
```

Store it in a `static readonly` field. Compiling inside the method that uses it does the work every time. The saving is client-side CPU, worth it on hot paths only. A query returning many rows returns `IAsyncEnumerable<T>` instead of `Task<T>`.

Examples: `Compiled_query_runs_like_any_other`.

### 3. Keyset paging

`Skip(n).Take(m)` becomes `OFFSET n ROWS FETCH NEXT m ROWS`. The server still reads the first n rows and discards them, so page 750 reads 15,000 rows to return 20, and rows shift between pages when data changes.

Keyset ("seek") paging remembers the last key returned and asks for the rows after it:

```csharp
.Where(h => h.SalesOrderId > lastSeen).OrderBy(h => h.SalesOrderId).Take(20)
```

That is a seek on the index: every page costs the same. For a multi-column sort the predicate has to match the sort exactly. With `ORDER BY ListPrice DESC, ProductId` it is `ListPrice < @lastPrice OR (ListPrice = @lastPrice AND ProductId > @lastId)`. The trade-off: no "jump to page N", only next (and previous, with the comparisons reversed).

Example: `Keyset_seeks_offset_scans`.

### 4. Streaming vs buffering

`ToListAsync()` reads every row into a list, then closes the reader. `AsAsyncEnumerable()` with `await foreach` hands you rows as they arrive, which uses less memory for big results, but **the data reader stays open until the loop finishes**. Without MARS (Multiple Active Result Sets, off by default), running another query on the same context inside the loop throws "There is already an open DataReader associated with this Connection". Buffer first when you need to query per row, or better, don't query per row at all (E05).

Example: `Streaming_holds_the_connection`.

### 5. SARGable LINQ

A predicate is SARGable when the database can use an index to find the rows, which means the column must appear bare, with no function around it. The translation decides this, so the LINQ shape matters:

| LINQ | SQL | Index on the column |
|---|---|---|
| `r.CurrencyRateDate.Year == year` | `DATEPART(year, [CurrencyRateDate]) = @year` | scan |
| `r.CurrencyRateDate >= start && r.CurrencyRateDate < end` | a range on the bare column | **seek** |
| `p.LastName.Substring(0, 2) == prefix` | `SUBSTRING([LastName], 0 + 1, 2) = @prefix` | scan |
| `p.LastName.ToUpper().StartsWith(prefix)` | `UPPER([LastName]) LIKE ...` | scan |
| `p.LastName.StartsWith(prefix)` | `[LastName] LIKE @prefix_startswith ESCAPE N'\'` | **seek** |

AdventureWorks uses a case-insensitive collation, so `ToUpper`/`ToLower` for case-insensitive matching is never needed there. It only costs the index.

Examples: `Year_is_not_SARGable`, `Functions_on_the_column_block_seeks`.

### 6. Plan-cache pollution

Every distinct SQL text gets its own plan in the cache. A parameterized query (`h.Status == status`) is one text and **one plan reused**. Inlined values (`EF.Constant(status)`, or SQL built by string concatenation) produce a new text, and a new compiled plan, for every value. That wastes compile CPU and cache memory, and it pushes useful plans out. E01's challenge 3 inlined a skewed status on purpose: reserve that for a handful of values.

Example: `Inlined_constants_pollute_the_plan_cache` (5 `Adhoc` plans vs 1 `Prepared` plan used 5 times).

### 7. Finding your query on the server

`TagWith("...")` puts a `-- ...` comment at the top of the SQL. `TagWithCallSite()` adds the file and line of the query. The comment is part of the statement text SQL Server stores, so you can find your LINQ query in:

- the plan cache and its statistics: `sys.dm_exec_cached_plans`, `sys.dm_exec_query_stats`, joined to `sys.dm_exec_sql_text` (used here)
- Query Store's `sys.query_store_query_text`, in databases where Query Store is on. It's **off** in this container's AdventureWorks, and turning it on would change the database, so the track uses the DMVs.

Examples: `TagWith_comments_the_SQL`, `Inlined_constants_pollute_the_plan_cache`.

### 8. `HasIndex` in a database-first model

The scaffolder copied the database's indexes into the model as `HasIndex(e => new { e.LastName, e.FirstName, e.MiddleName }, "IX_Person_LastName_FirstName_MiddleName")`. In a database-first project that is **metadata only**: EF doesn't create indexes unless you use migrations or `EnsureCreated`. The database is the truth, and the model tells you what it has. Adding an index for a slow query means a migration in code-first (E12) or a script in database-first, not just a `HasIndex` line.

Example: `Scaffolded_indexes_are_model_metadata`.

## Pitfalls

- **Tracking read-only results.** Memory and CPU for nothing.
- **`AsNoTracking` with repeated related entities.** Duplicate objects, and reference comparisons that silently fail.
- **Compiling a query per call.** The delegate must be static.
- **Deep `OFFSET` paging.** Slower with every page. Use keyset paging.
- **Querying inside an `await foreach`.** It throws without MARS. Buffer, or restructure.
- **Functions on indexed columns.** `.Year`, `.Date`, `Substring`, `ToUpper`: write ranges and `StartsWith` instead.
- **Inlining values that vary.** One plan per value. Keep parameters.

## Exercises

```
dotnet test --filter E06Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
````

- [ ] **Step 6: Prompts are identical in Exercises.cs and Solutions.cs**

```bash
cd efcore && python - lessons/E06-query-performance <<'EOF'
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

- [ ] **Step 7: Re-runnable (Review Focus 2).** The DMV-based tests must pass again immediately:

Run: `cd efcore && dotnet test --no-build --filter "E06Examples|E06Solutions" && dotnet test --no-build --filter "E06Examples|E06Solutions"`
Expected: both runs `Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18`.

- [ ] **Step 8: Commit**

```bash
git add efcore/lessons/E06-query-performance
git commit -m "feat: add EF Core lesson E06 - query performance"
```

---

## Task 4: Docs and final verification

**Files:**
- Modify: `efcore/README.md` (lesson table), `README.md` (EF Core Track paragraph), `CLAUDE.md` (EF Core track section), `docs/superpowers/specs/2026-10-10-efcore-track-design.md` (Tier B row, Lab Harness)

- [ ] **Step 1: `efcore/README.md` lesson table.** Change the two rows

```markdown
| E05 | Loading related data | planned |
| E06 | Query performance | planned |
```

to

```markdown
| E05 | Loading related data | available |
| E06 | Query performance | available |
```

- [ ] **Step 2: Root `README.md`.** In the `## EF Core Track` paragraph, replace `Lessons E01–E04 are available;` with `Lessons E01–E06 are available;`.

- [ ] **Step 3: `CLAUDE.md`.** In the `## EF Core track` section, directly after the bullet that starts ``- `efcore/Lab/` gives every test a connection and transaction``, add:

```markdown
- `Lab/QueryPlan.cs`: `QueryPlan.OperatorsAsync(db, query)` returns the estimated plan's physical operators (`SET SHOWPLAN_XML`, nothing executes, not counted in `Sql`), for checks that assert seek vs scan. E05 runs on `E05Context` (lazy-loading proxies on), so a missed load shows up as an extra round trip; checks there never pass entities to a formatting assertion, because formatting a proxy lazy-loads.
```

- [ ] **Step 4: Spec.** In `docs/superpowers/specs/2026-10-10-efcore-track-design.md`:
  - In the Tier B table's E06 row, replace `finding EF queries in Query Store via `TagWith`` with `finding EF queries in the plan-cache DMVs via `TagWith` (Query Store is off in the container, and enabling it would change the database)`.
  - Under `## Lab Harness`, directly after the `Reference` bullet (`- `Reference` — runs the checks' own reference queries ...` and its continuation line), add:

```markdown
- `QueryPlan.OperatorsAsync(db, query)` (added with Tier B) — the estimated plan's physical
  operators via `SET SHOWPLAN_XML`, without executing the query or counting it in `Sql`.
```

- [ ] **Step 5: Build and run everything, unfiltered (Review Focus 5)**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build`
Expected: `0 Warning(s)`, then `Passed!  - Failed: 0, Passed: 117, Skipped: 48, Total: 165`.

- [ ] **Step 6: Run the curriculum self-check**

Run: `cd efcore && dotnet test --no-build --filter "FullyQualifiedName!~Exercises"`
Expected: `Passed!  - Failed: 0, Passed: 117, Skipped: 0, Total: 117`.

- [ ] **Step 7: No newline-dependent assertions**

Run: `cd efcore && grep -nF -e 'NewLine' -e '\r\n' lessons/*/Checks.cs lessons/*/Examples.cs`
Expected: exactly one hit, `lessons/E01-setup-and-generated-sql/Examples.cs:59` (output only, from phase 1).

- [ ] **Step 8: Check the EF Core 10-specific claims (spec DoD item 5).** Every behavioral claim in the E05/E06 READMEs is asserted by an example or check, so Steps 5–6 verified them against EF Core 10.0.12. These claims have no assertion; confirm each against Microsoft Learn ("Loading related data", "Split queries", "Tracking vs. no-tracking queries", "Efficient querying", "Advanced performance topics"):
  - E05: lazy loading requires the Proxies package and `UseLazyLoadingProxies()`, and virtual navigations
  - E05: `UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)` makes splitting the default, `AsSingleQuery()` opts out
  - E05: filtered Include supports `Where`, `OrderBy`/`OrderByDescending`, `ThenBy`/`ThenByDescending`, `Skip`, `Take`
  - E06: `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` sets the default for a context
  - E06: what compiled queries save (expression-tree processing and cache lookup)

Expected: no corrections needed. If one is, fix the README sentence and re-run Step 5.

- [ ] **Step 9: AdventureWorks is unchanged.** Re-run Task 0, Step 5's query.
Expected: the same three integers recorded in Task 0.

- [ ] **Step 10: Commit**

```bash
git add efcore/README.md README.md CLAUDE.md docs/superpowers/specs/2026-10-10-efcore-track-design.md
git commit -m "docs: mark EF Core lessons E05-E06 available"
```

---

## Not in this phase

Tier C (E07–E09), Tier D (E10–E12, with `LabDatabase`) and the capstone (E13) each get their own plan, per the spec's build-out phases.
