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
