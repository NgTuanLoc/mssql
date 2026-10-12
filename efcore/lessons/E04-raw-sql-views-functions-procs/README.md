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
- can't be the target of a navigation (the scaffolded views have none): join them explicitly (E02)

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
