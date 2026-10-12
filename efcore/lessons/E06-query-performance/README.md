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

`Skip(n).Take(m)` becomes `OFFSET n ROWS FETCH NEXT m ROWS`. The server still reads the first n rows and discards them, so skipping 15,000 rows still reads them, just to return 20, and rows shift between pages when data changes.

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
