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
