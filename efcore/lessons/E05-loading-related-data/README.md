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

A `Select` never needs `Include`. Navigations inside the projection become joins and correlated subqueries, only the selected columns are read, and a projection of plain values (no entity instances in it) is not tracked, so nothing can be lazy-loaded later. For read-only screens and reports, project. Load entity graphs when you are going to change them (Tier C).

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
