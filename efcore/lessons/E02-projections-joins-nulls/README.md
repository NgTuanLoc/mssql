# E02 — Projections, Joins & Nulls

## What you'll learn

- How a projection decides which columns EF fetches, and why a projection of plain columns is not tracked
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

`Select` decides what crosses the wire. Query an entity and EF selects every mapped column and tracks the result. Project into a DTO, record or anonymous type and EF selects only the columns the projection uses, and tracks nothing (an entity inside the projection would still be tracked).

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
