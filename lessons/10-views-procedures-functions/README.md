# Lesson 10 — Views, Stored Procedures, Functions

## What you'll learn
- When to use a view vs a stored procedure vs a function
- `CREATE OR ALTER VIEW` — updatable and non-updatable
- Parameterised stored procedures with `SET NOCOUNT ON`, `OUTPUT` parameters
- Scalar UDFs and why to avoid them for large row sets
- Inline Table-Valued Functions (iTVFs) and `CROSS APPLY`

## Setup
Run `setup.sql` once. It drops and re-creates the `lesson10` schema (including any leftover views, procs, functions from a previous run).

## Concepts

### Views

```sql
CREATE OR ALTER VIEW schema.vw_Name AS
SELECT ...;
```

Views are stored queries — not stored data (unless indexed). Use them to:
- Simplify complex joins for callers
- Restrict column access (security)
- Provide stable interfaces when underlying tables change

### Stored Procedures

```sql
CREATE OR ALTER PROCEDURE schema.usp_Name
    @Param1 INT,
    @Param2 NVARCHAR(100) = N'default'  -- optional with default
AS
BEGIN
    SET NOCOUNT ON;  -- always include; suppresses row-count messages
    ...
END;
```

Use procs for:
- Business logic that involves multiple statements
- DML operations called by applications
- Long-running batch operations

### Functions — when to use which

| Type | Returns | Use for | Avoid when |
|---|---|---|---|
| Scalar UDF | single value | Simple calculations on a single input | Called per-row in a large SELECT — they block parallelism |
| Inline TVF | TABLE (single SELECT) | Parameterised views, reusable filter logic | — |
| Multi-statement TVF | TABLE (multiple stmts) | Complex logic that builds a result set | Usually; prefer iTVF or proc instead |

**Scalar UDF performance trap:** calling a scalar UDF in a `WHERE` or `SELECT` against a large table executes the function once per row and prevents the optimizer from parallelising the query. For large data volumes, inline the logic or use an iTVF.

### CROSS APPLY vs OUTER APPLY

```sql
-- CROSS APPLY: like INNER JOIN — only rows where the TVF returns results
FROM table CROSS APPLY fn(table.col) AS t

-- OUTER APPLY: like LEFT JOIN — all rows; NULLs when TVF returns nothing
FROM table OUTER APPLY fn(table.col) AS t
```

## Worked Examples (lesson10 schema + AdventureWorks)
1. View: `vw_CustomerOrderSummary` — per-customer totals.
2. Procedure: `usp_GetOrdersByCustomer` with optional `@MinAmount`.
3. Procedure with `OUTPUT` parameter: `usp_GetCustomerOrderCount`.
4. Scalar UDF: `fn_AgeInYears` — correct age calculation.
5. Inline TVF: `fn_GetProductsByCategory` used with `CROSS APPLY`.

## Pitfalls
- Scalar UDFs prevent parallelism — rewrite as iTVF or inline expression for large queries.
- Views are not materialised — a complex view called in a query can still be slow.
- `TOP` inside a view without `ORDER BY` produces arbitrary results — views with `TOP` need `WITH TIES` or an `ORDER BY` applied by the calling query.
- `SET NOCOUNT ON` in procs prevents extra result-set messages that confuse some clients.
- Proc parameters cannot have table types passed from external clients without TVP (Table-Valued Parameters) — out of scope for this lesson.

## Cheatsheet link
See `cheatsheets/00-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
