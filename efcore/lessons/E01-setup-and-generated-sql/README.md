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

Anywhere else — `Where`, `OrderBy`, `GroupBy` — an untranslatable call throws `InvalidOperationException` ("could not be translated") instead of silently loading the table and filtering in memory. To filter on the client on purpose, call `AsEnumerable()` explicitly, *after* everything SQL can do. Only the final `Select` (including collections nested inside it) may call .NET code.

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
