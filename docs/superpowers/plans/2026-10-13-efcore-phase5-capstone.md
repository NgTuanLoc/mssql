# EF Core Track — Phase 5 (Capstone E13) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the capstone lesson E13 to `efcore/`: T-SQL lesson 18's reports ported to single-query LINQ, the step LINQ can't express mapped as a keyless entity over SQL in a derived context, and a transactional order placement with conflict handling.

**Architecture:** E13 is a database-first lesson on AdventureWorks (rolled-back transaction per test, as in E01–E09). It is the first lesson where the learner customizes the model, so its checks are generic: `E13Checks<TContext> : LabTest<TContext> where TContext : E13Context`. The abstract `E13Context` adds a `VendorMonths` set and one hook, `MapVendorMonths`. `E13ExerciseContext` (in `Exercises.cs`, empty hook) and `E13SolutionContext` (in `Solutions.cs`, a `ToSqlQuery` mapping with `LAG`) are separate context types, so EF caches a separate model for each. The examples use a third, `E13ExampleContext`, which maps a different report (`TerritoryMonth`) so they don't give challenge 1 away. Every file below was validated against the live `mssql-learn` container on 2026-10-13:
- the build has 0 warnings
- the full suite gives 244 passed and 104 skipped (the 104 are the exercise stubs)
- every check was run against tempting-but-wrong answers (Task 1, Step 6), and each one failed
- AdventureWorks checksums were unchanged afterwards

**Tech Stack:** .NET 10 SDK (10.0.x), EF Core 10.0.12 (SqlServer, Design, Proxies), xUnit v3 4.0.2 (`xunit.v3.mtp-off`) on VSTest, SQL Server 2022 (Docker container `mssql-learn`).

**Spec:** `docs/superpowers/specs/2026-10-10-efcore-track-design.md` (phase 5 of its "Build-out Phases"; the E13 row under "Capstone").

## Global Constraints

- Target `net10.0`. EF Core packages exactly `10.0.12`; `xunit.v3.mtp-off` `4.0.2`, `xunit.runner.visualstudio` `4.0.1`, `Microsoft.NET.Test.Sdk` `18.10.1`. No new packages in this phase. No floating versions.
- AdventureWorks2022 is never modified: every test runs in a transaction that is rolled back (commits are refused). E13 needs no `setup.sql` and no `EfLab_` database.
- No credentials committed anywhere.
- `efcore/Model/` is scaffolder output, never hand-edited. Lesson-specific model changes go in a context derived from `AdventureWorksContext` in the lesson folder.
- Tests run serially (`parallelizeTestCollections: false`).
- Lesson folder `efcore/lessons/E13-capstone/` = `README.md`, `Examples.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`, plus `E13Context.cs`. Namespace `EfCoreLessons.E13`; classes `E13Examples`, `E13Checks<TContext>`, `E13Exercises`, `E13Solutions`.
- 5 exercises (`Ex1`–`Ex5`) + 3 challenges (`Ch1`–`Ch3`); identical prompts in `Exercises.cs` and `Solutions.cs`; a `// Why:` note after each solution.
- `dotnet build` produces 0 warnings.
- Commits: conventional prefixes (`feat:`, `docs:`), no attribution lines (disabled in the user's settings).

## Review Focus

1. **A correct answer written differently.** Exercise 2's averages: SQL Server's `AVG` keeps its input's scale, and EF's translation feeds it a different type than lesson 18's T-SQL (money vs decimal), so an exact comparison fails correct answers. Exercise 3: EF emits `CROSS APPLY` or `ROW_NUMBER()` depending on the LINQ shape, and both are right. Pinned in Task 1's `Checks.cs` (the `Cents` comparer; the `ROW_NUMBER() || APPLY` assertion) and exercised by the solutions.
2. **Work quietly done in memory.** A `ToListAsync` halfway through a report still returns the right rows. Pinned by the round-trip and SQL-shape assertions, and proven by Task 1, Step 6: the wrong answers compute the average, the top-N cut and the growth in memory and must fail.
3. **Stock taken but no order saved.** If saving the order fails after the stock `UPDATE`, the stock must come back. Pinned by the third block of `Ch3_PlaceAnOrder` (ship method 999) and by Step 6's no-savepoint wrong answer.
4. **Rows silently dropped by an inner APPLY.** Every salesperson in AdventureWorks has orders, so lesson 18's OUTER vs CROSS APPLY difference is invisible on the real data. `Ex5_RecentOrdersPerSalesPerson` adds a salesperson with no orders inside the test's transaction. Pinned by Step 6's `CROSS APPLY` wrong answer.
5. **Writes leaking into AdventureWorks.** `Ex5` inserts a salesperson, and `Ch3` and an example change inventory and insert orders, all inside the rolled-back transaction. Pinned by Task 2, Step 6 (checksums unchanged).

## Conventions used by every task

- Shell: Git Bash on Windows, bash/zsh elsewhere. Paths are relative to the repository (or worktree) root unless a step says `cd efcore`.
- Commands that need the password export it first (`docker/.env` is gitignored; a fresh worktree outside the repo has no copy until Task 0 copies it):

  ```bash
  export SA_PASSWORD=$(grep '^SA_PASSWORD=' /d/Study/Database/mssql/docker/.env | cut -d= -f2- | tr -d '\r')
  ```

- On Git Bash, prefix `docker exec ... /opt/mssql-tools18/...` with `MSYS_NO_PATHCONV=1`.
- The `mssql-learn` container sometimes stops between sessions. If tests fail with "Cannot connect to SQL Server", run `docker compose -f docker/docker-compose.yml up -d` from the main checkout and wait until `sqlcmd ... -Q "SELECT 1"` answers before re-running.

## File Map

| Action | Path |
|---|---|
| Create | `efcore/lessons/E13-capstone/` (6 files: `E13Context.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`) |
| Modify | `efcore/README.md`, `README.md`, `CLAUDE.md`, `docs/superpowers/specs/2026-10-10-efcore-track-design.md` |

---

## Task 0: Isolate the work

- [ ] **Step 1: Create an isolated workspace** using the `superpowers:using-git-worktrees` skill, on a new branch `efcore-phase5` based on `master`. Without a worktree: `git switch -c efcore-phase5`. In a worktree outside the repo, copy the password file: `cp /d/Study/Database/mssql/docker/.env docker/.env`.

- [ ] **Step 2: Verify you are not on master**

Run: `git branch --show-current`
Expected: `efcore-phase5`

- [ ] **Step 3: Confirm the container is up**

Run: `docker ps --filter name=mssql-learn --format '{{.Status}}'`
Expected: starts with `Up`. If not, see Conventions.

- [ ] **Step 4: Confirm the phase 4 baseline passes**

Run: `cd efcore && dotnet tool restore && dotnet build -warnaserror && dotnet test --no-build`
Expected: `0 Warning(s)`, then `Passed!  - Failed: 0, Passed: 231, Skipped: 96, Total: 327`.

- [ ] **Step 5: Record the AdventureWorks baseline** (compared again in Task 2)

```bash
export SA_PASSWORD=$(grep '^SA_PASSWORD=' /d/Study/Database/mssql/docker/.env | cut -d= -f2- | tr -d '\r')
MSYS_NO_PATHCONV=1 docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -W -s'|' -h -1 -d AdventureWorks2022 -Q "SET NOCOUNT ON; SELECT (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Production.Product), (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM HumanResources.Employee), (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Sales.SalesOrderHeader), (SELECT COUNT(*) FROM Sales.SalesOrderDetail);"
```

Expected: four values. Note them down. (On 2026-10-13 they were `-1058563079|259050056|-1058661271|121317`.)

---

## Task 1: Lesson E13 — Capstone

**Files:**
- Create: `efcore/lessons/E13-capstone/E13Context.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`, `Examples.cs`, `README.md`

**Interfaces:**
- Consumes: `LabTest<TContext>` (`Db`, `Sql`, `Reference`, `NewContext()`), `SqlCapture.AssertRoundTrips/Commands/All/Count/Reset`, `Reference.Rows<T>`, `[Exercise]` (all in `efcore/Lab/`); `AdventureWorksContext` and its entities (`efcore/Model/`).
- Produces: namespace `EfCoreLessons.E13`: `VendorMonth`, abstract `E13Context`; records `ProductTotal`, `TerritoryRevenue`, `TopCustomer`, `TerritoryOrders`, `RecentOrder`, `VendorGrowth`, `VendorReportRow`, `OrderLine`, `OrderRequest`; `E13Checks<TContext>`, `E13ExerciseContext`, `E13Exercises`, `E13SolutionContext`, `E13Solutions`, `TerritoryMonth`, `E13ExampleContext`, `E13Examples`. Nothing later depends on them.

The checks' T-SQL is lesson 18's (`lessons/18-complex-queries/exercises-solutions.ipynb`, exercises 1–5 and 8), with explicit tiebreaks added where "the top N" needs one.

- [ ] **Step 1: Write `E13Context.cs`**

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E13;

// One vendor's purchases in one month, with the previous month's total (null for the vendor's first
// month). Keyless: it's a report row, not an entity you save. Challenges 1 and 2 map and query it.
public class VendorMonth
{
    public int VendorId { get; set; }
    public DateTime OrderMonth { get; set; }
    public decimal MonthTotal { get; set; }
    public decimal? PrevMonthTotal { get; set; }
}

// E13's model: the shared AdventureWorks model plus whatever MapVendorMonths adds. E13ExerciseContext
// (Exercises.cs, yours) and E13SolutionContext (Solutions.cs) each map VendorMonth their own way. They
// are different context types, so EF caches a separate model for each and they never mix.
public abstract class E13Context(DbContextOptions<AdventureWorksContext> options) : AdventureWorksContext(options)
{
    public IQueryable<VendorMonth> VendorMonths => Set<VendorMonth>();

    protected abstract void MapVendorMonths(ModelBuilder modelBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        MapVendorMonths(modelBuilder);
    }
}
```

- [ ] **Step 2: Write `Checks.cs`**

```csharp
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E13;

public record ProductTotal(string Name, decimal TotalSold);
public record TerritoryRevenue(string Territory, decimal AvgMonthlyRevenue);
public record TopCustomer(string Territory, int CustomerId, decimal LifetimeSpend);
public record TerritoryOrders(string Territory, int Orders2012, int Orders2013, int Orders2014, int TotalOrders);
public record RecentOrder(int SalesPersonId, int? SalesOrderId, DateTime? OrderDate, decimal? TotalDue);
public record VendorGrowth(DateTime OrderMonth, decimal MonthTotal, decimal? GrowthPct);
public record VendorReportRow(DateTime OrderMonth, decimal MonthTotal, decimal? GrowthPct, string ProductName, decimal ProductTotal, decimal ContributionPct);
public record OrderLine(short OrderQty, decimal UnitPrice);
public record OrderRequest(int CustomerId, int AddressId, int ShipMethodId, int ProductId, short LocationId, short Quantity);

// The checks for E13. E13Exercises (yours) and E13Solutions both inherit them. Each runs on its own
// context type: E13ExerciseContext (Exercises.cs) or E13SolutionContext (Solutions.cs).
// The reports (exercises 1-5, challenges 1-2) follow the capstone rules: ONE round trip, and every
// filter, grouping, ordering and top-N cut happens in SQL. The checks compare your rows with the
// T-SQL lesson 18 query and read the SQL you sent.
public abstract class E13Checks<TContext> : LabTest<TContext> where TContext : E13Context
{
    protected abstract Task<List<ProductTotal>> Ex1(TContext db);
    protected abstract Task<List<TerritoryRevenue>> Ex2(TContext db);
    protected abstract Task<List<TopCustomer>> Ex3(TContext db, int top);
    protected abstract Task<List<TerritoryOrders>> Ex4(TContext db);
    protected abstract Task<List<RecentOrder>> Ex5(TContext db, int top);
    protected abstract Task<List<VendorGrowth>> Ch1(TContext db, int vendorId);
    protected abstract Task<List<VendorReportRow>> Ch2(TContext db, int vendorId);
    protected abstract Task<int?> Ch3(TContext db, OrderRequest request);

    // Averages compared to the cent: SQL Server's AVG keeps the scale of its input, and different (correct)
    // queries feed it money or decimal.
    static readonly IEqualityComparer<TerritoryRevenue> Cents = EqualityComparer<TerritoryRevenue>.Create(
        (a, b) => a!.Territory == b!.Territory && Math.Round(a.AvgMonthlyRevenue, 2) == Math.Round(b.AvgMonthlyRevenue, 2));

    const int Vendor = 1580; // Litware, Inc.: purchases in most months, several products per month

    [Exercise]
    public async Task Ex1_ProductsAboveAverage()
    {
        var result = await Ex1(Db);

        Assert.Equal(await Reference.Rows<ProductTotal>($"""
            WITH ProductTotals AS (
                SELECT ProductID, SUM(LineTotal) AS TotalSold FROM Sales.SalesOrderDetail GROUP BY ProductID
            )
            SELECT p.Name, pt.TotalSold
            FROM ProductTotals AS pt JOIN Production.Product AS p ON p.ProductID = pt.ProductID
            WHERE pt.TotalSold > (SELECT AVG(TotalSold) FROM ProductTotals)
            ORDER BY pt.TotalSold DESC, p.Name
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("AVG(", Sql.All); // the average is computed by SQL Server, not in memory
    }

    [Exercise]
    public async Task Ex2_AverageMonthlyRevenue()
    {
        var result = await Ex2(Db);

        Assert.Equal(await Reference.Rows<TerritoryRevenue>($"""
            WITH MonthlyRevenue AS (
                SELECT TerritoryID, YEAR(OrderDate) AS Y, MONTH(OrderDate) AS M, SUM(TotalDue) AS Revenue
                FROM Sales.SalesOrderHeader
                GROUP BY TerritoryID, YEAR(OrderDate), MONTH(OrderDate)
            )
            SELECT st.Name AS Territory, AVG(mr.Revenue) AS AvgMonthlyRevenue
            FROM MonthlyRevenue AS mr JOIN Sales.SalesTerritory AS st ON st.TerritoryID = mr.TerritoryID
            GROUP BY st.Name
            ORDER BY AvgMonthlyRevenue DESC
            """), result, Cents);
        Sql.AssertRoundTrips(1);
        Assert.Contains("AVG(", Sql.All);
    }

    [Exercise]
    public async Task Ex3_TopCustomersPerTerritory()
    {
        var result = await Ex3(Db, 3);

        Assert.Equal(await Reference.Rows<TopCustomer>($"""
            WITH CustomerSpend AS (
                SELECT TerritoryID, CustomerID, SUM(TotalDue) AS LifetimeSpend
                FROM Sales.SalesOrderHeader
                GROUP BY TerritoryID, CustomerID
            ),
            Ranked AS (
                SELECT *, ROW_NUMBER() OVER (PARTITION BY TerritoryID ORDER BY LifetimeSpend DESC, CustomerID) AS rn
                FROM CustomerSpend
            )
            SELECT st.Name AS Territory, r.CustomerID AS CustomerId, r.LifetimeSpend
            FROM Ranked AS r JOIN Sales.SalesTerritory AS st ON st.TerritoryID = r.TerritoryID
            WHERE r.rn <= {3}
            ORDER BY st.Name, r.LifetimeSpend DESC, r.CustomerID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.True(Sql.All.Contains("ROW_NUMBER()") || Sql.All.Contains("APPLY"), "Cut the top N in SQL (E03 concept 5)."); // either shape is fine
    }

    [Exercise]
    public async Task Ex4_OrdersPerYear()
    {
        var result = await Ex4(Db);

        Assert.Equal(await Reference.Rows<TerritoryOrders>($"""
            SELECT st.Name AS Territory,
                   COUNT(CASE WHEN YEAR(soh.OrderDate) = 2012 THEN 1 END) AS Orders2012,
                   COUNT(CASE WHEN YEAR(soh.OrderDate) = 2013 THEN 1 END) AS Orders2013,
                   COUNT(CASE WHEN YEAR(soh.OrderDate) = 2014 THEN 1 END) AS Orders2014,
                   COUNT(*) AS TotalOrders
            FROM Sales.SalesOrderHeader AS soh JOIN Sales.SalesTerritory AS st ON st.TerritoryID = soh.TerritoryID
            GROUP BY st.Name
            ORDER BY Territory
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("CASE", Sql.All); // conditional aggregation, in one pass over the orders
    }

    [Exercise]
    public async Task Ex5_RecentOrdersPerSalesPerson()
    {
        // A salesperson with no orders yet (inside the test's transaction, rolled back afterwards).
        await Db.Database.ExecuteSqlAsync($"INSERT INTO Sales.SalesPerson (BusinessEntityID) VALUES ({1})");
        Sql.Reset();

        var result = await Ex5(Db, 3);

        Assert.Equal(await Reference.Rows<RecentOrder>($"""
            SELECT sp.BusinessEntityID AS SalesPersonId, recent.SalesOrderID AS SalesOrderId, recent.OrderDate, recent.TotalDue
            FROM Sales.SalesPerson AS sp
            OUTER APPLY (
                SELECT TOP ({3}) SalesOrderID, OrderDate, TotalDue
                FROM Sales.SalesOrderHeader
                WHERE SalesPersonID = sp.BusinessEntityID
                ORDER BY OrderDate DESC, SalesOrderID DESC
            ) AS recent
            ORDER BY sp.BusinessEntityID, recent.OrderDate DESC, recent.SalesOrderID DESC
            """), result);
        Assert.Contains(result, r => r.SalesPersonId == 1 && r.SalesOrderId == null); // kept, with no orders
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ch1_VendorGrowth()
    {
        var result = await Ch1(Db, Vendor);

        Assert.Equal(await Reference.Rows<VendorGrowth>($"""
            WITH VendorMonthly AS (
                SELECT DATEFROMPARTS(YEAR(poh.OrderDate), MONTH(poh.OrderDate), 1) AS OrderMonth, SUM(pod.LineTotal) AS MonthTotal
                FROM Purchasing.PurchaseOrderHeader AS poh
                JOIN Purchasing.PurchaseOrderDetail AS pod ON pod.PurchaseOrderID = poh.PurchaseOrderID
                WHERE poh.VendorID = {Vendor}
                GROUP BY DATEFROMPARTS(YEAR(poh.OrderDate), MONTH(poh.OrderDate), 1)
            ),
            WithPrevious AS (
                SELECT *, LAG(MonthTotal) OVER (ORDER BY OrderMonth) AS PrevMonthTotal FROM VendorMonthly
            )
            SELECT OrderMonth, MonthTotal,
                   ROUND(100 * (MonthTotal - PrevMonthTotal) / NULLIF(PrevMonthTotal, 0), 1) AS GrowthPct
            FROM WithPrevious
            ORDER BY OrderMonth
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("LAG(", Sql.All);
        Assert.Contains("ROUND(", Sql.All); // the growth is computed by SQL Server too
        Assert.NotNull(Db.Model.FindEntityType(typeof(VendorMonth))); // mapped in your context, as challenge 1 asks
    }

    [Exercise]
    public async Task Ch2_VendorReport()
    {
        var result = await Ch2(Db, Vendor);

        Assert.Equal(await Reference.Rows<VendorReportRow>($"""
            WITH PoLines AS (
                SELECT DATEFROMPARTS(YEAR(poh.OrderDate), MONTH(poh.OrderDate), 1) AS OrderMonth, pod.ProductID, pod.LineTotal
                FROM Purchasing.PurchaseOrderHeader AS poh
                JOIN Purchasing.PurchaseOrderDetail AS pod ON pod.PurchaseOrderID = poh.PurchaseOrderID
                WHERE poh.VendorID = {Vendor}
            ),
            VendorMonthly AS (
                SELECT OrderMonth, SUM(LineTotal) AS MonthTotal FROM PoLines GROUP BY OrderMonth
            ),
            WithGrowth AS (
                SELECT *, LAG(MonthTotal) OVER (ORDER BY OrderMonth) AS PrevMonthTotal FROM VendorMonthly
            )
            SELECT g.OrderMonth, g.MonthTotal,
                   ROUND(100 * (g.MonthTotal - g.PrevMonthTotal) / NULLIF(g.PrevMonthTotal, 0), 1) AS GrowthPct,
                   top2.ProductName, top2.ProductTotal,
                   ROUND(100 * top2.ProductTotal / g.MonthTotal, 1) AS ContributionPct
            FROM WithGrowth AS g
            CROSS APPLY (
                SELECT TOP (2) p.Name AS ProductName, SUM(pl.LineTotal) AS ProductTotal
                FROM PoLines AS pl JOIN Production.Product AS p ON p.ProductID = pl.ProductID
                WHERE pl.OrderMonth = g.OrderMonth
                GROUP BY p.Name
                ORDER BY SUM(pl.LineTotal) DESC, p.Name
            ) AS top2
            ORDER BY g.OrderMonth, top2.ProductTotal DESC, top2.ProductName
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("LAG(", Sql.All);
    }

    // Product 709 (Mountain Bike Socks, M) has 180 in stock at location 7. Customer 29825's last order
    // shipped to address 985 by ship method 5.
    static OrderRequest Socks(short quantity) => new(29825, 985, 5, 709, 7, quantity);

    Task<short> Stock() => Reference.Rows<short>($"SELECT Quantity AS Value FROM Production.ProductInventory WHERE ProductID = 709 AND LocationID = 7")
        .ContinueWith(t => t.Result.Single());

    Task<int> OrdersOfCustomer() => Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderHeader WHERE CustomerID = 29825")
        .ContinueWith(t => t.Result.Single());

    [Exercise]
    public async Task Ch3_PlaceAnOrder()
    {
        var orders = await OrdersOfCustomer();

        var id = await Ch3(Db, Socks(100));

        Assert.NotNull(id);
        Assert.Equal(80, await Stock());
        Assert.Equal(new OrderLine(100, 9.50m),
            (await Reference.Rows<OrderLine>($"SELECT OrderQty, UnitPrice FROM Sales.SalesOrderDetail WHERE SalesOrderID = {id}")).Single());
        Assert.False(Sql.Commands.Any(c => c.Contains("[ProductInventory]") && !c.TrimStart().StartsWith("UPDATE")),
            "Check and take the stock in one UPDATE: reading it first lets two buyers take the same units.");

        // A second buyer wants 100 more: only 80 left.
        Sql.Reset();
        Assert.Null(await Ch3(NewContext(), Socks(100)));
        Assert.Equal(80, await Stock());
        Assert.DoesNotContain("INSERT", Sql.All); // no order without stock
        Assert.Equal(orders + 1, await OrdersOfCustomer());

        // The stock is taken, then saving the order fails (no ship method 999): nothing may remain.
        await Assert.ThrowsAsync<DbUpdateException>(() => Ch3(NewContext(), Socks(10) with { ShipMethodId = 999 }));
        Assert.Equal(80, await Stock());
        Assert.Equal(orders + 1, await OrdersOfCustomer());
    }
}
```

- [ ] **Step 3: Write `Exercises.cs`** (the learner's context, and the stubs)

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E13;

// Your context for E13. Exercises 1-5 and challenge 3 don't need anything here. Challenge 1 asks you
// to map VendorMonth in MapVendorMonths; until you do, VendorMonth isn't part of the model.
public class E13ExerciseContext(DbContextOptions<AdventureWorksContext> options) : E13Context(options)
{
    protected override void MapVendorMonths(ModelBuilder modelBuilder)
    {
    }
}

// E13 exercises: the capstone. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E13Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
// `db` is your E13ExerciseContext: everything in AdventureWorksContext plus VendorMonths (challenge 1).
public class E13Exercises : E13Checks<E13ExerciseContext>
{
    // Exercise 1 — Products that beat the average
    // Return each product whose total sales (sum of SalesOrderDetail.LineTotal) beat the average
    // product's total sales, as ProductTotal, highest first (ties by Name).
    // Rules: one round trip; the average is computed in SQL.
    protected override Task<List<ProductTotal>> Ex1(E13ExerciseContext db)
        => throw new NotImplementedException();

    // Exercise 2 — Average monthly revenue
    // For each territory, the average of its monthly revenue (the sum of TotalDue per calendar month),
    // as TerritoryRevenue, highest first.
    // Rules: one round trip.
    protected override Task<List<TerritoryRevenue>> Ex2(E13ExerciseContext db)
        => throw new NotImplementedException();

    // Exercise 3 — Best customers per territory
    // For each territory, the `top` customers by lifetime spend (sum of TotalDue of their orders in that
    // territory), as TopCustomer, ordered by Territory, then spend (highest first), then CustomerId.
    // Rules: one round trip; the top-N cut happens in SQL.
    protected override Task<List<TopCustomer>> Ex3(E13ExerciseContext db, int top)
        => throw new NotImplementedException();

    // Exercise 4 — Orders per year, side by side
    // One TerritoryOrders row per territory: its order counts for 2012, 2013 and 2014 plus its total,
    // ordered by Territory. No PIVOT, no one-query-per-year.
    // Rules: one round trip.
    protected override Task<List<TerritoryOrders>> Ex4(E13ExerciseContext db)
        => throw new NotImplementedException();

    // Exercise 5 — Latest orders per salesperson
    // For every salesperson in Sales.SalesPerson, their `top` most recent orders (by OrderDate, ties by
    // the higher SalesOrderId), as RecentOrder, ordered by SalesPersonId, then OrderDate and SalesOrderId
    // (both newest first). A salesperson with no orders still appears once, with null order columns.
    // Rules: one round trip.
    protected override Task<List<RecentOrder>> Ex5(E13ExerciseContext db, int top)
        => throw new NotImplementedException();

    // Challenge 1 — Where LINQ runs out: month-over-month growth
    // In E13ExerciseContext.MapVendorMonths, map VendorMonth (E13Context.cs) as a keyless entity over a
    // SQL query: one row per vendor and calendar month (first day of the month) of purchases (sum of
    // PurchaseOrderDetail.LineTotal), with the previous month's total from LAG. Then return vendor
    // `vendorId`'s months from db.VendorMonths as VendorGrowth, ordered by OrderMonth, where GrowthPct is
    // 100 * (MonthTotal - PrevMonthTotal) / PrevMonthTotal rounded to 1 decimal (null for the first month).
    // Rules: one round trip; the vendor filter, the growth and the ordering are LINQ on top of your query.
    protected override Task<List<VendorGrowth>> Ch1(E13ExerciseContext db, int vendorId)
        => throw new NotImplementedException();

    // Challenge 2 — The final boss: the vendor report
    // For vendor `vendorId`, lesson 18's purchasing report: for each month, the month total and growth %
    // (as in challenge 1), and the month's top 2 products by line total (ties by product Name), each with
    // its total and its contribution % to the month total (rounded to 1 decimal). One VendorReportRow per
    // month and product, ordered by OrderMonth, then ProductTotal (highest first), then ProductName.
    // Rules: one round trip; build on db.VendorMonths.
    protected override Task<List<VendorReportRow>> Ch2(E13ExerciseContext db, int vendorId)
        => throw new NotImplementedException();

    // Challenge 3 — Place an order
    // Place `request` as a new order: take Quantity units of the product from ProductInventory at
    // LocationId, then save a SalesOrderHeader (the customer, AddressId as both bill-to and ship-to,
    // ShipMethodId, OrderDate now, DueDate a week later) with one SalesOrderDetail (SpecialOfferId 1, the
    // product's ListPrice as UnitPrice). Return the new SalesOrderId, or null when the location has fewer
    // than Quantity units: then nothing changes. If saving the order fails, the stock must be put back and
    // the exception must reach the caller.
    // Rules: checking and taking the stock is ONE statement (no read-then-write race between two buyers);
    // all-or-nothing; db already has a transaction open (the test's), so use a savepoint.
    protected override Task<int?> Ch3(E13ExerciseContext db, OrderRequest request)
        => throw new NotImplementedException();
}
```

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E13Exercises`
Expected: `0 Warning(s)`, then `Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

- [ ] **Step 4: Write `Solutions.cs`** (the solution's context, and the answers)

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCoreLessons.E13;

// The solution's context: VendorMonth mapped to a SQL query (challenge 1).
public class E13SolutionContext(DbContextOptions<AdventureWorksContext> options) : E13Context(options)
{
    protected override void MapVendorMonths(ModelBuilder modelBuilder)
        => modelBuilder.Entity<VendorMonth>().HasNoKey().ToSqlQuery("""
            SELECT VendorID AS VendorId, OrderMonth, MonthTotal,
                   LAG(MonthTotal) OVER (PARTITION BY VendorID ORDER BY OrderMonth) AS PrevMonthTotal
            FROM (
                SELECT poh.VendorID, DATEFROMPARTS(YEAR(poh.OrderDate), MONTH(poh.OrderDate), 1) AS OrderMonth,
                       SUM(pod.LineTotal) AS MonthTotal
                FROM Purchasing.PurchaseOrderHeader AS poh
                JOIN Purchasing.PurchaseOrderDetail AS pod ON pod.PurchaseOrderID = poh.PurchaseOrderID
                GROUP BY poh.VendorID, DATEFROMPARTS(YEAR(poh.OrderDate), MONTH(poh.OrderDate), 1)
            ) AS m
            """);
    // Why: ToSqlQuery makes VendorMonth a keyless entity whose "table" is this query: LINQ composes over
    // it as a subquery, like FromSql (E04), but it's named, mapped once and reusable everywhere. LAG has
    // no LINQ form (E03 concept 6), so it lives here. Partitioning by VendorID keeps each vendor's
    // previous month its own, so a filter composed on top is still correct. No ORDER BY and no CTE:
    // composed SQL can't contain either (E04 concept 2).
}

// Worked solutions for E13, each followed by a Why note. Run: dotnet test --filter E13Solutions
public class E13Solutions : E13Checks<E13SolutionContext>
{
    // Exercise 1 — Products that beat the average
    // Return each product whose total sales (sum of SalesOrderDetail.LineTotal) beat the average
    // product's total sales, as ProductTotal, highest first (ties by Name).
    // Rules: one round trip; the average is computed in SQL.
    protected override Task<List<ProductTotal>> Ex1(E13SolutionContext db)
    {
        var totals = db.SalesOrderDetails
            .GroupBy(d => d.ProductId)
            .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(d => d.LineTotal) });

        return totals
            .Where(t => t.TotalSold > totals.Average(x => x.TotalSold))
            .Join(db.Products, t => t.ProductId, p => p.ProductId, (t, p) => new { p.Name, t.TotalSold })
            .OrderByDescending(x => x.TotalSold).ThenBy(x => x.Name)
            .Select(x => new ProductTotal(x.Name, x.TotalSold))
            .ToListAsync();
    }
    // Why: `totals` is the ProductTotals CTE from lesson 18: an IQueryable is a query definition, not
    // data, so naming it costs nothing and using it twice inlines it twice, exactly like referencing a CTE
    // twice. Nothing runs until ToListAsync, so the whole pipeline is one SELECT with the average as a
    // subquery. Calling ToListAsync on `totals` first would pull every product total into memory.

    // Exercise 2 — Average monthly revenue
    // For each territory, the average of its monthly revenue (the sum of TotalDue per calendar month),
    // as TerritoryRevenue, highest first.
    // Rules: one round trip.
    protected override Task<List<TerritoryRevenue>> Ex2(E13SolutionContext db)
        => db.SalesOrderHeaders
            .GroupBy(h => new { h.TerritoryId, h.OrderDate.Year, h.OrderDate.Month })
            .Select(g => new { g.Key.TerritoryId, Revenue = g.Sum(h => h.TotalDue) })
            .Join(db.SalesTerritories, m => m.TerritoryId, t => (int?)t.TerritoryId, (m, t) => new { t.Name, m.Revenue })
            .GroupBy(x => x.Name)
            .Select(g => new { Territory = g.Key, AvgMonthlyRevenue = g.Average(x => x.Revenue) })
            .OrderByDescending(x => x.AvgMonthlyRevenue)
            .Select(x => new TerritoryRevenue(x.Territory, x.AvgMonthlyRevenue))
            .ToListAsync();
    // Why: two grain changes, two GroupBys: orders to territory-months, then territory-months to
    // territories. EF nests the first as a derived table, the same shape as lesson 18's MonthlyRevenue
    // CTE. Grouping by Year and Month (not a DateTime built from them) keeps the key simple to translate.

    // Exercise 3 — Best customers per territory
    // For each territory, the `top` customers by lifetime spend (sum of TotalDue of their orders in that
    // territory), as TopCustomer, ordered by Territory, then spend (highest first), then CustomerId.
    // Rules: one round trip; the top-N cut happens in SQL.
    protected override Task<List<TopCustomer>> Ex3(E13SolutionContext db, int top)
    {
        var spend = db.SalesOrderHeaders
            .GroupBy(h => new { h.TerritoryId, h.CustomerId })
            .Select(g => new { g.Key.TerritoryId, g.Key.CustomerId, LifetimeSpend = g.Sum(h => h.TotalDue) });

        return db.SalesTerritories
            .SelectMany(t => spend
                .Where(s => s.TerritoryId == t.TerritoryId)
                .OrderByDescending(s => s.LifetimeSpend).ThenBy(s => s.CustomerId)
                .Take(top)
                .Select(s => new { Territory = t.Name, s.CustomerId, s.LifetimeSpend }))
            .OrderBy(x => x.Territory).ThenByDescending(x => x.LifetimeSpend).ThenBy(x => x.CustomerId)
            .Select(x => new TopCustomer(x.Territory, x.CustomerId, x.LifetimeSpend))
            .ToListAsync();
    }
    // Why: the per-customer totals are a grouped query, and a correlated Take over it per territory is
    // lesson 18's top-N-per-group: EF emits CROSS APPLY (SELECT TOP(@top) ...). Other shapes, such as
    // GroupBy(...).Select(g => g.OrderBy(...).Take(top)), come out as ROW_NUMBER() <= @top. Either way the
    // cut is in SQL. The CustomerId tiebreak makes "the top 3" well defined when two spends are equal.

    // Exercise 4 — Orders per year, side by side
    // One TerritoryOrders row per territory: its order counts for 2012, 2013 and 2014 plus its total,
    // ordered by Territory. No PIVOT, no one-query-per-year.
    // Rules: one round trip.
    protected override Task<List<TerritoryOrders>> Ex4(E13SolutionContext db)
        => db.SalesOrderHeaders
            .Where(h => h.TerritoryId != null)
            .GroupBy(h => h.Territory!.Name)
            .Select(g => new
            {
                Territory = g.Key,
                Orders2012 = g.Count(h => h.OrderDate.Year == 2012),
                Orders2013 = g.Count(h => h.OrderDate.Year == 2013),
                Orders2014 = g.Count(h => h.OrderDate.Year == 2014),
                TotalOrders = g.Count(),
            })
            .OrderBy(x => x.Territory)
            .Select(x => new TerritoryOrders(x.Territory, x.Orders2012, x.Orders2013, x.Orders2014, x.TotalOrders))
            .ToListAsync();
    // Why: Count(predicate) inside a group becomes COUNT(CASE WHEN ... THEN 1 END), lesson 18's
    // conditional aggregation, so the orders are read once. Grouping by the navigation's Name is the join.
    // The aggregates go into an anonymous type first: EF can't translate through a positional record's
    // constructor, so the record is the last step.

    // Exercise 5 — Latest orders per salesperson
    // For every salesperson in Sales.SalesPerson, their `top` most recent orders (by OrderDate, ties by
    // the higher SalesOrderId), as RecentOrder, ordered by SalesPersonId, then OrderDate and SalesOrderId
    // (both newest first). A salesperson with no orders still appears once, with null order columns.
    // Rules: one round trip.
    protected override Task<List<RecentOrder>> Ex5(E13SolutionContext db, int top)
        => db.SalesPeople
            .SelectMany(
                sp => sp.SalesOrderHeaders
                    .OrderByDescending(h => h.OrderDate).ThenByDescending(h => h.SalesOrderId)
                    .Take(top)
                    .DefaultIfEmpty(),
                (sp, h) => new { sp.BusinessEntityId, SalesOrderId = (int?)h!.SalesOrderId, OrderDate = (DateTime?)h.OrderDate, TotalDue = (decimal?)h.TotalDue })
            .OrderBy(x => x.BusinessEntityId).ThenByDescending(x => x.OrderDate).ThenByDescending(x => x.SalesOrderId)
            .Select(x => new RecentOrder(x.BusinessEntityId, x.SalesOrderId, x.OrderDate, x.TotalDue))
            .ToListAsync();
    // Why: SelectMany + Take per salesperson is lesson 18's APPLY. DefaultIfEmpty is what keeps salespeople
    // with no orders (lesson 18 exercise 4): here EF emits a LEFT JOIN over ROW_NUMBER() <= @top, which
    // returns the same rows as OUTER APPLY. Without DefaultIfEmpty the join is an inner one and they
    // silently disappear. The casts to nullable types tell EF the columns can be NULL for those rows.

    // Challenge 1 — Where LINQ runs out: month-over-month growth
    // In E13ExerciseContext.MapVendorMonths, map VendorMonth (E13Context.cs) as a keyless entity over a
    // SQL query: one row per vendor and calendar month (first day of the month) of purchases (sum of
    // PurchaseOrderDetail.LineTotal), with the previous month's total from LAG. Then return vendor
    // `vendorId`'s months from db.VendorMonths as VendorGrowth, ordered by OrderMonth, where GrowthPct is
    // 100 * (MonthTotal - PrevMonthTotal) / PrevMonthTotal rounded to 1 decimal (null for the first month).
    // Rules: one round trip; the vendor filter, the growth and the ordering are LINQ on top of your query.
    protected override Task<List<VendorGrowth>> Ch1(E13SolutionContext db, int vendorId)
        => db.VendorMonths
            .Where(m => m.VendorId == vendorId)
            .OrderBy(m => m.OrderMonth)
            .Select(m => new VendorGrowth(
                m.OrderMonth,
                m.MonthTotal,
                m.PrevMonthTotal == null || m.PrevMonthTotal == 0
                    ? null
                    : Math.Round(100 * (m.MonthTotal - m.PrevMonthTotal.Value) / m.PrevMonthTotal.Value, 1)))
            .ToListAsync();
    // Why: LINQ has no LAG, so that one step is SQL, mapped in the context (see E13SolutionContext). The
    // rest is ordinary LINQ composed over it: the WHERE, the ORDER BY and the ROUND arithmetic all run in
    // SQL Server. Unlike FromSql, the mapping is written once and every query can use db.VendorMonths.

    // Challenge 2 — The final boss: the vendor report
    // For vendor `vendorId`, lesson 18's purchasing report: for each month, the month total and growth %
    // (as in challenge 1), and the month's top 2 products by line total (ties by product Name), each with
    // its total and its contribution % to the month total (rounded to 1 decimal). One VendorReportRow per
    // month and product, ordered by OrderMonth, then ProductTotal (highest first), then ProductName.
    // Rules: one round trip; build on db.VendorMonths.
    protected override Task<List<VendorReportRow>> Ch2(E13SolutionContext db, int vendorId)
    {
        var lines = db.PurchaseOrderDetails.Where(d => d.PurchaseOrder.VendorId == vendorId);

        return db.VendorMonths
            .Where(m => m.VendorId == vendorId)
            .SelectMany(m => lines
                .Where(d => d.PurchaseOrder.OrderDate >= m.OrderMonth && d.PurchaseOrder.OrderDate < m.OrderMonth.AddMonths(1))
                .GroupBy(d => d.Product.Name)
                .Select(g => new { ProductName = g.Key, ProductTotal = g.Sum(d => d.LineTotal) })
                .OrderByDescending(p => p.ProductTotal).ThenBy(p => p.ProductName)
                .Take(2)
                .Select(p => new { Month = m, p.ProductName, p.ProductTotal }))
            .OrderBy(x => x.Month.OrderMonth).ThenByDescending(x => x.ProductTotal).ThenBy(x => x.ProductName)
            .Select(x => new VendorReportRow(
                x.Month.OrderMonth,
                x.Month.MonthTotal,
                x.Month.PrevMonthTotal == null || x.Month.PrevMonthTotal == 0
                    ? null
                    : Math.Round(100 * (x.Month.MonthTotal - x.Month.PrevMonthTotal.Value) / x.Month.PrevMonthTotal.Value, 1),
                x.ProductName,
                x.ProductTotal,
                Math.Round(100 * x.ProductTotal / x.Month.MonthTotal, 1)))
            .ToListAsync();
    }
    // Why: the same pipeline as lesson 18, with each step a different tool: the mapped query does what
    // LINQ can't (LAG), a correlated SelectMany + Take does the per-month top 2 (CROSS APPLY), and the
    // arithmetic is translated LINQ. The month range (>= first day, < first day of the next month) is
    // SARGable, unlike comparing YEAR() and MONTH() of each order (E06).

    // Challenge 3 — Place an order
    // Place `request` as a new order: take Quantity units of the product from ProductInventory at
    // LocationId, then save a SalesOrderHeader (the customer, AddressId as both bill-to and ship-to,
    // ShipMethodId, OrderDate now, DueDate a week later) with one SalesOrderDetail (SpecialOfferId 1, the
    // product's ListPrice as UnitPrice). Return the new SalesOrderId, or null when the location has fewer
    // than Quantity units: then nothing changes. If saving the order fails, the stock must be put back and
    // the exception must reach the caller.
    // Rules: checking and taking the stock is ONE statement (no read-then-write race between two buyers);
    // all-or-nothing; db already has a transaction open (the test's), so use a savepoint.
    protected override async Task<int?> Ch3(E13SolutionContext db, OrderRequest request)
    {
        var transaction = db.Database.CurrentTransaction!;
        await transaction.CreateSavepointAsync("PlaceOrder");
        try
        {
            var taken = await db.ProductInventories
                .Where(i => i.ProductId == request.ProductId && i.LocationId == request.LocationId && i.Quantity >= request.Quantity)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.Quantity, i => (short)(i.Quantity - request.Quantity)));
            if (taken == 0) return null;

            var price = await db.Products.Where(p => p.ProductId == request.ProductId).Select(p => p.ListPrice).SingleAsync();
            var order = new SalesOrderHeader
            {
                CustomerId = request.CustomerId,
                BillToAddressId = request.AddressId,
                ShipToAddressId = request.AddressId,
                ShipMethodId = request.ShipMethodId,
                OrderDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(7),
            };
            order.SalesOrderDetails.Add(new SalesOrderDetail
            {
                ProductId = request.ProductId,
                SpecialOfferId = 1,
                OrderQty = request.Quantity,
                UnitPrice = price,
            });
            db.SalesOrderHeaders.Add(order);
            await db.SaveChangesAsync();
            return order.SalesOrderId;
        }
        catch
        {
            await transaction.RollbackToSavepointAsync("PlaceOrder");
            throw;
        }
    }
    // Why: the WHERE Quantity >= @qty inside the UPDATE makes check-and-take atomic: two buyers can't both
    // see 100 units and both take them, because the second UPDATE re-checks the row the first one changed
    // and matches nothing. Its row count is the answer. SaveChanges already undoes its own batch when it
    // fails, but not the stock UPDATE that ran before it; the savepoint covers both. In an app without an
    // outer transaction, BeginTransaction + Commit plays the savepoint's role (E09).
}
```

- [ ] **Step 5: Run the solutions**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter E13Solutions`
Expected: `0 Warning(s)`, then `Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

- [ ] **Step 6: Prove the checks catch tempting-but-wrong answers (Review Focus 2–4).** Create this temporary file, `efcore/lessons/E13-capstone/WrongE13.cs` (never commit it):

```csharp
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E13;

// Plausible wrong answers: every check here must FAIL.
public class E13WrongA : E13Checks<E13SolutionContext>
{
    // average computed in memory
    protected override async Task<List<ProductTotal>> Ex1(E13SolutionContext db)
    {
        var totals = await db.SalesOrderDetails.GroupBy(d => d.SpecialOfferProduct.Product.Name)
            .Select(g => new { Name = g.Key, TotalSold = g.Sum(d => d.LineTotal) }).ToListAsync();
        var avg = totals.Average(t => t.TotalSold);
        return totals.Where(t => t.TotalSold > avg).OrderByDescending(t => t.TotalSold).ThenBy(t => t.Name)
            .Select(t => new ProductTotal(t.Name, t.TotalSold)).ToList();
    }

    // average order instead of average month
    protected override Task<List<TerritoryRevenue>> Ex2(E13SolutionContext db)
        => db.SalesOrderHeaders.Where(h => h.TerritoryId != null).GroupBy(h => h.Territory!.Name)
            .Select(g => new { g.Key, A = g.Average(h => h.TotalDue) }).OrderByDescending(x => x.A)
            .Select(x => new TerritoryRevenue(x.Key, x.A)).ToListAsync();

    // top-N cut in memory
    protected override async Task<List<TopCustomer>> Ex3(E13SolutionContext db, int top)
    {
        var all = await db.SalesOrderHeaders.Where(h => h.TerritoryId != null)
            .GroupBy(h => new { h.Territory!.Name, h.CustomerId })
            .Select(g => new { g.Key.Name, g.Key.CustomerId, S = g.Sum(h => h.TotalDue) }).ToListAsync();
        return all.GroupBy(x => x.Name).OrderBy(g => g.Key)
            .SelectMany(g => g.OrderByDescending(x => x.S).ThenBy(x => x.CustomerId).Take(top))
            .Select(x => new TopCustomer(x.Name, x.CustomerId, x.S)).ToList();
    }

    // one query per territory and year
    protected override async Task<List<TerritoryOrders>> Ex4(E13SolutionContext db)
    {
        var result = new List<TerritoryOrders>();
        foreach (var t in await db.SalesTerritories.OrderBy(t => t.Name).ToListAsync())
        {
            var orders = db.SalesOrderHeaders.Where(h => h.TerritoryId == t.TerritoryId);
            result.Add(new(t.Name, await orders.CountAsync(h => h.OrderDate.Year == 2012), await orders.CountAsync(h => h.OrderDate.Year == 2013),
                await orders.CountAsync(h => h.OrderDate.Year == 2014), await orders.CountAsync()));
        }
        return result;
    }

    // CROSS APPLY drops the salesperson without orders
    protected override Task<List<RecentOrder>> Ex5(E13SolutionContext db, int top)
        => db.SalesPeople.SelectMany(sp => sp.SalesOrderHeaders.OrderByDescending(h => h.OrderDate).ThenByDescending(h => h.SalesOrderId).Take(top),
                (sp, h) => new { sp.BusinessEntityId, h.SalesOrderId, h.OrderDate, h.TotalDue })
            .OrderBy(x => x.BusinessEntityId).ThenByDescending(x => x.OrderDate).ThenByDescending(x => x.SalesOrderId)
            .Select(x => new RecentOrder(x.BusinessEntityId, x.SalesOrderId, x.OrderDate, x.TotalDue)).ToListAsync();

    // growth computed in memory
    protected override async Task<List<VendorGrowth>> Ch1(E13SolutionContext db, int vendorId)
        => (await db.VendorMonths.Where(m => m.VendorId == vendorId).OrderBy(m => m.OrderMonth).ToListAsync())
            .Select(m => new VendorGrowth(m.OrderMonth, m.MonthTotal,
                m.PrevMonthTotal is null or 0 ? null : Math.Round(100 * (m.MonthTotal - m.PrevMonthTotal.Value) / m.PrevMonthTotal.Value, 1)))
            .ToList();

    // the vendor's top 2 products overall, repeated every month
    protected override Task<List<VendorReportRow>> Ch2(E13SolutionContext db, int vendorId)
    {
        var lines = db.PurchaseOrderDetails.Where(d => d.PurchaseOrder.VendorId == vendorId);
        return db.VendorMonths.Where(m => m.VendorId == vendorId)
            .SelectMany(m => lines.GroupBy(d => d.Product.Name)
                .Select(g => new { ProductName = g.Key, ProductTotal = g.Sum(d => d.LineTotal) })
                .OrderByDescending(p => p.ProductTotal).ThenBy(p => p.ProductName).Take(2)
                .Select(p => new { Month = m, p.ProductName, p.ProductTotal }))
            .OrderBy(x => x.Month.OrderMonth).ThenByDescending(x => x.ProductTotal).ThenBy(x => x.ProductName)
            .Select(x => new VendorReportRow(x.Month.OrderMonth, x.Month.MonthTotal,
                x.Month.PrevMonthTotal == null || x.Month.PrevMonthTotal == 0 ? null : Math.Round(100 * (x.Month.MonthTotal - x.Month.PrevMonthTotal.Value) / x.Month.PrevMonthTotal.Value, 1),
                x.ProductName, x.ProductTotal, Math.Round(100 * x.ProductTotal / x.Month.MonthTotal, 1))).ToListAsync();
    }

    // no savepoint: the stock taken before a failed save stays taken
    protected override async Task<int?> Ch3(E13SolutionContext db, OrderRequest request)
    {
        var taken = await db.ProductInventories
            .Where(i => i.ProductId == request.ProductId && i.LocationId == request.LocationId && i.Quantity >= request.Quantity)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.Quantity, i => (short)(i.Quantity - request.Quantity)));
        if (taken == 0) return null;
        var price = await db.Products.Where(p => p.ProductId == request.ProductId).Select(p => p.ListPrice).SingleAsync();
        var order = new SalesOrderHeader
        {
            CustomerId = request.CustomerId, BillToAddressId = request.AddressId, ShipToAddressId = request.AddressId,
            ShipMethodId = request.ShipMethodId, OrderDate = DateTime.Now, DueDate = DateTime.Now.AddDays(7),
        };
        order.SalesOrderDetails.Add(new SalesOrderDetail { ProductId = request.ProductId, SpecialOfferId = 1, OrderQty = request.Quantity, UnitPrice = price });
        db.SalesOrderHeaders.Add(order);
        await db.SaveChangesAsync();
        return order.SalesOrderId;
    }
}

// read-then-write: right results in a single session, but two buyers could both pass the check
public class E13WrongB : E13Checks<E13SolutionContext>
{
    protected override Task<List<ProductTotal>> Ex1(E13SolutionContext db) => throw new NotImplementedException();
    protected override Task<List<TerritoryRevenue>> Ex2(E13SolutionContext db) => throw new NotImplementedException();
    protected override Task<List<TopCustomer>> Ex3(E13SolutionContext db, int top) => throw new NotImplementedException();
    protected override Task<List<TerritoryOrders>> Ex4(E13SolutionContext db) => throw new NotImplementedException();
    protected override Task<List<RecentOrder>> Ex5(E13SolutionContext db, int top) => throw new NotImplementedException();
    protected override Task<List<VendorGrowth>> Ch1(E13SolutionContext db, int vendorId) => throw new NotImplementedException();
    protected override Task<List<VendorReportRow>> Ch2(E13SolutionContext db, int vendorId) => throw new NotImplementedException();

    protected override async Task<int?> Ch3(E13SolutionContext db, OrderRequest request)
    {
        var transaction = db.Database.CurrentTransaction!;
        await transaction.CreateSavepointAsync("P");
        try
        {
            var stock = await db.ProductInventories.SingleAsync(i => i.ProductId == request.ProductId && i.LocationId == request.LocationId);
            if (stock.Quantity < request.Quantity) return null;
            stock.Quantity -= request.Quantity;
            var price = await db.Products.Where(p => p.ProductId == request.ProductId).Select(p => p.ListPrice).SingleAsync();
            var order = new SalesOrderHeader
            {
                CustomerId = request.CustomerId, BillToAddressId = request.AddressId, ShipToAddressId = request.AddressId,
                ShipMethodId = request.ShipMethodId, OrderDate = DateTime.Now, DueDate = DateTime.Now.AddDays(7),
            };
            order.SalesOrderDetails.Add(new SalesOrderDetail { ProductId = request.ProductId, SpecialOfferId = 1, OrderQty = request.Quantity, UnitPrice = price });
            db.SalesOrderHeaders.Add(order);
            await db.SaveChangesAsync();
            return order.SalesOrderId;
        }
        catch
        {
            await transaction.RollbackToSavepointAsync("P");
            throw;
        }
    }
}
```

Run: `cd efcore && dotnet build && dotnet test --no-build --filter "FullyQualifiedName~E13Wrong"`
Expected: `Failed: 9, Passed: 0, Skipped: 7, Total: 16`: all eight `E13WrongA` tests and `E13WrongB.Ch3_PlaceAnOrder` fail (the others in `E13WrongB` are stubs). The failure messages to expect: `Ex1` "Sub-string not found" (`AVG(`), `Ex3` "Cut the top N in SQL", `Ex4` "Expected 1 round trip(s), got 41", `Ex2`/`Ex5`/`Ch2` "Collections differ", `Ch1` "Sub-string not found" (`ROUND(`), `E13WrongA.Ch3` "Expected: 80", `E13WrongB.Ch3` "Check and take the stock in one UPDATE".

Then delete it: `rm efcore/lessons/E13-capstone/WrongE13.cs`, and confirm `git status --short` doesn't list it.

- [ ] **Step 7: Write `Examples.cs`**

```csharp
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E13;

// One territory's sales in one month, with the previous month's. The examples' own report row.
public class TerritoryMonth
{
    public int TerritoryId { get; set; }
    public DateTime OrderMonth { get; set; }
    public decimal Revenue { get; set; }
    public decimal? PrevRevenue { get; set; }
}

// The examples' context: TerritoryMonth mapped to a SQL query (concept 4). VendorMonth stays unmapped.
public class E13ExampleContext(DbContextOptions<AdventureWorksContext> options) : E13Context(options)
{
    protected override void MapVendorMonths(ModelBuilder modelBuilder)
        => modelBuilder.Entity<TerritoryMonth>().HasNoKey().ToSqlQuery("""
            SELECT TerritoryID AS TerritoryId, OrderMonth, Revenue,
                   LAG(Revenue) OVER (PARTITION BY TerritoryID ORDER BY OrderMonth) AS PrevRevenue
            FROM (
                SELECT TerritoryID, DATEFROMPARTS(YEAR(OrderDate), MONTH(OrderDate), 1) AS OrderMonth, SUM(TotalDue) AS Revenue
                FROM Sales.SalesOrderHeader
                GROUP BY TerritoryID, DATEFROMPARTS(YEAR(OrderDate), MONTH(OrderDate), 1)
            ) AS m
            """);
}

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E13Examples --logger "console;verbosity=detailed"
public class E13Examples : LabTest<E13ExampleContext>
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    static bool IsBlue(string? color) => color == "Blue";

    // Concept 1: a filter EF can't translate throws; it never quietly runs in memory.
    [Fact]
    public async Task Untranslatable_filters_throw()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Db.Products.Where(p => IsBlue(p.Color)).ToListAsync());

        Print(failure.Message);
        Assert.Contains("could not be translated", failure.Message);
        Assert.Equal(0, Sql.Count); // nothing was sent
    }

    // Concept 2: an IQueryable is a query, not data. Naming a step and using it twice is like a CTE
    // referenced twice: one round trip, the step inlined in both places.
    [Fact]
    public async Task A_named_IQueryable_works_like_a_CTE()
    {
        var territoryTotals = Db.SalesOrderHeaders
            .GroupBy(h => h.TerritoryId)
            .Select(g => new { TerritoryId = g.Key, Total = g.Sum(h => h.TotalDue) });

        var aboveAverage = await territoryTotals
            .Where(t => t.Total > territoryTotals.Average(x => x.Total))
            .CountAsync();

        Print($"{aboveAverage} territories above the average");
        Sql.AssertRoundTrips(1);
        Assert.Equal(2, Occurrences(Sql.All, "GROUP BY [s"));
    }

    // Concept 3: DefaultIfEmpty keeps left rows with no matches (OUTER APPLY's job). On 2014-06-30 only
    // some territories took orders; with DefaultIfEmpty every territory is still there.
    [Fact]
    public async Task DefaultIfEmpty_keeps_rows_without_matches()
    {
        var day = new DateTime(2014, 6, 30);
        var withOrders = await Db.SalesTerritories
            .SelectMany(t => t.SalesOrderHeaders.Where(h => h.OrderDate == day).OrderByDescending(h => h.SalesOrderId).Take(1))
            .CountAsync();
        var all = await Db.SalesTerritories
            .SelectMany(
                t => t.SalesOrderHeaders.Where(h => h.OrderDate == day).OrderByDescending(h => h.SalesOrderId).Take(1).DefaultIfEmpty(),
                (t, h) => new { t.Name, SalesOrderId = (int?)h!.SalesOrderId })
            .ToListAsync();

        Print($"{withOrders} territories with orders that day; {all.Count(x => x.SalesOrderId == null)} without");
        Assert.True(withOrders < 10);
        Assert.Equal(10, all.Count);
        Assert.Contains("LEFT JOIN", Sql.Commands[1]); // a left join over ROW_NUMBER(): OUTER APPLY by another route
    }

    // Concept 4: a keyless entity mapped with ToSqlQuery: SQL for the part LINQ can't say (LAG),
    // LINQ composed on top for the rest.
    [Fact]
    public async Task ToSqlQuery_maps_a_report_LINQ_can_compose_on()
    {
        var northwest = await Db.Set<TerritoryMonth>()
            .Where(m => m.TerritoryId == 1 && m.PrevRevenue != null)
            .OrderBy(m => m.OrderMonth)
            .Select(m => new { m.OrderMonth, Growth = Math.Round(100 * (m.Revenue - m.PrevRevenue!.Value) / m.PrevRevenue.Value, 1) })
            .Take(3)
            .ToListAsync();

        foreach (var month in northwest) Print(month);
        Sql.AssertRoundTrips(1);
        Assert.Contains("LAG(Revenue)", Sql.All);  // your SQL, as a subquery
        Assert.Contains(") AS [e]", Sql.All);       // ...wrapped as a derived table, like FromSql (E04 concept 2)
        Assert.Contains("ROUND(", Sql.All);         // ...with the arithmetic translated around it
    }

    // Concept 5: a conditional UPDATE checks and changes a row in one statement; its row count says
    // whether the condition held.
    [Fact]
    public async Task A_conditional_UPDATE_checks_and_takes_atomically()
    {
        Task<int> Take(short quantity) => Db.ProductInventories
            .Where(i => i.ProductId == 709 && i.LocationId == 7 && i.Quantity >= quantity) // 180 in stock
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.Quantity, i => (short)(i.Quantity - quantity)));

        Assert.Equal(1, await Take(150)); // 30 left
        Assert.Equal(0, await Take(50));  // not enough: nothing changed
        Assert.Equal(30, await Db.ProductInventories.Where(i => i.ProductId == 709 && i.LocationId == 7).Select(i => i.Quantity).SingleAsync());
    }

    static int Occurrences(string text, string value) => (text.Length - text.Replace(value, "").Length) / value.Length;
}
```

- [ ] **Step 8: Write `README.md`**

````markdown
# E13 — Capstone

## What you'll learn

- Porting a T-SQL report pipeline (CTEs, top-N per group, conditional aggregation, `APPLY`) to LINQ that runs as **one** query, entirely in SQL Server
- Recognizing the step LINQ can't express, and putting just that step in SQL: a keyless entity mapped with `ToSqlQuery` in a derived context
- A transactional write with conflict handling: an atomic check-and-take `UPDATE`, then the order, all or nothing

## Builds on

- T-SQL [lesson 18](../../../lessons/18-complex-queries/lesson.ipynb) — every report here is one of its exercises
- EF lessons [E03](../E03-grouping-subqueries-limits/README.md) (grouping, top-N, what LINQ can't say), [E04](../E04-raw-sql-views-functions-procs/README.md) (raw SQL, composing over it, derived contexts), [E08](../E08-savechanges-and-set-based-writes/README.md) (`ExecuteUpdate`, triggers) and [E09](../E09-concurrency-and-transactions/README.md) (savepoints)

## Setup

```
dotnet test --filter E13Examples --logger "console;verbosity=detailed"
```

`E13Context.cs` is an abstract context: AdventureWorks plus a `VendorMonths` set and one hook, `MapVendorMonths`. Each test class runs on its own concrete context: `E13ExerciseContext` (in `Exercises.cs`, yours to fill in for challenge 1), `E13SolutionContext` (`Solutions.cs`) and `E13ExampleContext` (`Examples.cs`). EF caches one model per context type, so the three mappings never mix.

## The rules

Exercises 1–5 and challenges 1–2 are reports. Each must:

- run as **one round trip**
- do every filter, join, grouping, ordering, top-N cut and calculation **in SQL Server**: no `ToList` halfway through to finish in memory

The checks compare your rows with lesson 18's T-SQL and read the SQL you sent. Challenge 3 is a write with its own rules.

## Concepts

### 1. EF refuses to evaluate on the client, mostly

Since EF Core 3, a `Where`, `OrderBy` or aggregate that EF can't translate throws "could not be translated" instead of quietly running in memory. The exception is the **final** `Select`: what EF can't translate there, it evaluates on the rows as they arrive. That's harmless for formatting, but it means "it ran" doesn't prove "it ran in SQL". Read the SQL (every check prints it).

The other way to end up in memory is to ask for it: a `ToListAsync` in the middle of a pipeline runs that much, and every later step works on objects, with LINQ to Objects. That's the mistake the round-trip and SQL-shape checks catch.

Example: `Untranslatable_filters_throw`.

### 2. An `IQueryable` is a CTE

Lesson 18 builds reports as a pipeline of named CTEs. In LINQ, each step is an `IQueryable` variable:

```csharp
var totals = db.SalesOrderDetails.GroupBy(d => d.ProductId)
    .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(d => d.LineTotal) });   // WITH ProductTotals AS (...)

var report = totals.Where(t => t.TotalSold > totals.Average(x => x.TotalSold));     // used twice
```

Nothing runs until the end. EF inlines each step as a derived table or subquery, the way SQL Server inlines a CTE, so a step used twice appears twice in the SQL, just like a CTE referenced twice. Grain changes (orders to months, months to territories) become nested `GroupBy`s.

Example: `A_named_IQueryable_works_like_a_CTE`.

### 3. Top-N per group and `APPLY`

| LINQ | SQL EF emits |
|---|---|
| `outer.SelectMany(o => <query correlated to o>.OrderBy(...).Take(n))` | `CROSS APPLY (SELECT TOP(n) ...)`, lesson 18's `APPLY`, when the inner query uses the outer row's columns (e.g. in its `Select`); otherwise EF rewrites it as an `INNER JOIN` over `ROW_NUMBER() OVER (PARTITION BY ...)` filtered to `<= n` |
| `GroupBy(...).Select(g => g.OrderBy(...).Take(n))` | `ROW_NUMBER()` filtered to `<= n` |
| the first, with `.DefaultIfEmpty()` after `Take` | `OUTER APPLY`, or a `LEFT JOIN` over `ROW_NUMBER()`: rows with no matches are kept |

Which shape EF picks is its business; what you control is that the cut happens in SQL and that the order is fully specified (add a tiebreak, or "the top 3" isn't well defined). Remember `DefaultIfEmpty` whenever a left row must survive without matches, and cast the inner columns to nullable types in the projection.

Example: `DefaultIfEmpty_keeps_rows_without_matches`.

### 4. Where LINQ runs out: map the SQL once

LINQ has no `LAG`/`LEAD`, `RANK` or window frames (E03 concept 6). E04 showed `FromSql` and `SqlQuery`. For a report step you'll reuse, map it instead: a keyless entity whose source is a SQL query, in a derived context:

```csharp
modelBuilder.Entity<TerritoryMonth>().HasNoKey().ToSqlQuery("""
    SELECT TerritoryID AS TerritoryId, OrderMonth, Revenue,
           LAG(Revenue) OVER (PARTITION BY TerritoryID ORDER BY OrderMonth) AS PrevRevenue
    FROM (...) AS m
    """);
```

`db.Set<TerritoryMonth>()` is then a source like any table: EF wraps the SQL as a derived table and composes `Where`, `OrderBy`, joins, `SelectMany` and arithmetic around it, all in one query. The rules from E04 concept 2 apply: no `ORDER BY` and no CTE inside it. Two more:

- **No parameters.** The query is fixed in the model, so it computes every territory (or vendor). Filter on top in LINQ, and partition the window function by that column, or `LAG` would look across territories.
- **Columns by name.** Alias the columns to the property names.

Example: `ToSqlQuery_maps_a_report_LINQ_can_compose_on`.

### 5. A transactional write: check-and-take, then save

Placing an order takes stock and then creates rows. Two traps:

- **The race.** Reading the stock, checking it in C#, then writing the new quantity lets two buyers both read 100 units and both take them. A conditional `UPDATE` re-checks the row at the moment it changes it:

  ```csharp
  var taken = await db.ProductInventories
      .Where(i => i.ProductId == id && i.LocationId == location && i.Quantity >= quantity)
      .ExecuteUpdateAsync(set => set.SetProperty(i => i.Quantity, i => (short)(i.Quantity - quantity)));
  // taken == 0: not enough stock (or no such row)
  ```

  The second buyer's `UPDATE` waits for the first one's row lock, then re-reads the row and matches nothing.
- **Half a change.** If saving the order fails after the stock was taken, the stock must come back. `SaveChanges` undoes its own batch, but not the `ExecuteUpdate` before it. Wrap both: a transaction you commit at the end, or, inside an existing transaction (this lab's), a savepoint you roll back to on failure (E09 concept 3).

Example: `A_conditional_UPDATE_checks_and_takes_atomically`.

## Pitfalls

- **`ToListAsync` in the middle of a report.** Everything after it runs in memory, often after pulling far too many rows.
- **Trusting "it ran".** An untranslatable final `Select` runs on the client silently. Read the SQL.
- **Top-N without a tiebreak.** Equal values make "the top 3" nondeterministic.
- **Forgetting `DefaultIfEmpty`.** An inner `APPLY` or join silently drops the left rows without matches.
- **A window function in a mapped query without `PARTITION BY` the column you filter on.** `LAG` then reads the previous row of a different vendor.
- **Read, check, then write.** Use one conditional `UPDATE` and its row count.

## Exercises

```
dotnet test --filter E13Exercises
```

Edit `Exercises.cs` (challenge 1 also has you fill in `E13ExerciseContext.MapVendorMonths` at the top of it). *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
````

- [ ] **Step 9: Run the lesson and check the prompts**

Run: `cd efcore && dotnet build -warnaserror && dotnet test --no-build --filter "FullyQualifiedName~E13&FullyQualifiedName!~Exercises"`
Expected: `0 Warning(s)`, then `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13` (5 examples, 8 solutions).

```bash
cd efcore && python - lessons/E13-capstone <<'EOF'
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
git add efcore/lessons/E13-capstone
git commit -m "feat: add EF Core lesson E13 - capstone"
```

---

## Task 2: Docs and final verification

**Files:**
- Modify: `efcore/README.md` (lesson table), `README.md` (EF Core Track paragraph), `CLAUDE.md` (EF Core track section), `docs/superpowers/specs/2026-10-10-efcore-track-design.md` (Lesson Anatomy, Risks)

- [ ] **Step 1: `efcore/README.md`.** In the lesson table change the row

```markdown
| E13 | Capstone | planned |
```

to

```markdown
| E13 | Capstone | available |
```

- [ ] **Step 2: Root `README.md`.** In the `## EF Core Track` paragraph, replace `Lessons E01–E12 are available;` with `All 13 lessons (E01–E13) are available;`.

- [ ] **Step 3: `CLAUDE.md`.** In the `## EF Core track` section, directly after the bullet that starts ``- Code-first lessons: `LabDatabase` creates/drops``, add:

```markdown
- E13 (capstone) is the one lesson whose learner changes the model, so its checks are generic: `E13Checks<TContext>` with `TContext : E13Context`. The abstract `E13Context` adds `VendorMonths` and a `MapVendorMonths` hook; `E13ExerciseContext` (in `Exercises.cs`, the learner fills in the hook) and `E13SolutionContext` (a `ToSqlQuery` mapping) are separate context types, so their models never mix. Examples use `E13ExampleContext`, which maps a different report so challenge 1 isn't given away.
```

- [ ] **Step 4: Spec.** In `docs/superpowers/specs/2026-10-10-efcore-track-design.md`:
  - Under `## Lesson Anatomy`, directly after the bullet that starts `- Code-first lessons (Tier D) add` (two lines), add:

```markdown
- E13 (capstone) has an abstract `E13Context`; the learner's and the solution's contexts derive
  from it, and its checks are generic (`E13Checks<TContext>`), so each runs on its own model.
```

  - Under `Still open:`, replace the `**Run time:**` bullet (three lines) with:

```markdown
- **Run time:** serial execution; with all 13 lessons, 348 tests run in ~30 s (E09's deliberate
  lock timeouts and E12's database recreation account for most of it). Revisit if a full run
  gets slow.
```

- [ ] **Step 5: Build and run everything, unfiltered, then the curriculum self-check**

Run: `cd efcore && dotnet build --no-incremental -warnaserror && dotnet test --no-build && dotnet test --no-build --filter "FullyQualifiedName!~Exercises"`
Expected: `0 Warning(s)`, then `Passed!  - Failed: 0, Passed: 244, Skipped: 104, Total: 348` in roughly 30 s, then `Passed!  - Failed: 0, Passed: 244, Skipped: 0, Total: 244`.

- [ ] **Step 6: No newline-dependent assertions; AdventureWorks unchanged (Review Focus 5)**

Run: `cd efcore && grep -nF -e 'NewLine' -e '\r\n' lessons/*/Checks.cs lessons/*/Examples.cs`
Expected: exactly one hit, `lessons/E01-setup-and-generated-sql/Examples.cs:59` (output only, from phase 1).

Then re-run Task 0, Step 5's query.
Expected: the same four values recorded in Task 0.

- [ ] **Step 7: Check the EF Core-specific claims (spec DoD item 5).** Behavioral claims in the E13 README and Why notes are asserted by examples or checks, so Step 5 verified them against EF Core 10.0.12. These have no assertion; confirm each against Microsoft Learn ("Client vs. Server Evaluation", "Keyless Entity Types", "ExecuteUpdate and ExecuteDelete", "Transactions: savepoints", and for the last item the SQL Server docs):
  - since EF Core 3.0, untranslatable expressions outside the top-level projection throw; the top-level `Select` may be evaluated on the client
  - keyless entity types can be mapped to a defining query (`ToSqlQuery`), are never tracked, and are read-only
  - `ExecuteUpdate` returns the number of rows affected
  - `SaveChanges` inside an existing transaction uses a savepoint it rolls back on failure (E09 already asserts this)
  - with `READ_COMMITTED_SNAPSHOT` on, an `UPDATE` still takes update locks on the rows it changes, so it waits for another writer's lock and then evaluates its `WHERE` against the committed row (true on SQL Server 2022; with optimized locking — SQL Server 2025 / Azure SQL — rows are qualified without U locks, but a waiting UPDATE still re-evaluates its WHERE against the committed row, so the outcome is the same) (SQL Server's "Transaction locking and row versioning guide"); the README's concept 5 relies on this

Expected: no corrections needed. If one is, fix the README sentence and re-run Step 5.

- [ ] **Step 8: Commit**

```bash
git add efcore/README.md README.md CLAUDE.md docs/superpowers/specs/2026-10-10-efcore-track-design.md
git commit -m "docs: mark EF Core lesson E13 available"
```

---

## Not in this phase

Nothing: E13 completes the spec's five build-out phases. EF Core 11 stays out of scope (spec, Tooling & Versions).
