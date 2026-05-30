# Lesson 05 — Aggregations & Grouping

## What you'll learn
- `GROUP BY` and aggregate functions: `COUNT`, `SUM`, `AVG`, `MIN`, `MAX`
- `HAVING` to filter after aggregation
- `COUNT(*)` vs `COUNT(col)` and NULL behaviour
- `GROUPING SETS`, `ROLLUP`, and `CUBE` for multi-level subtotals

## Setup
Run `setup.sql` once (creates empty `lesson05` schema).

## Concepts

### GROUP BY and aggregates

```sql
SELECT col, COUNT(*), SUM(amount), AVG(amount)
FROM table
GROUP BY col;
```

Every column in `SELECT` that is not an aggregate must appear in `GROUP BY`.

### HAVING vs WHERE

- `WHERE` filters *before* grouping (on individual rows).
- `HAVING` filters *after* grouping (on aggregated values).

```sql
-- Wrong: can't reference an aggregate in WHERE
WHERE COUNT(*) > 5         -- error

-- Right
HAVING COUNT(*) > 5
```

### NULL in aggregates

`COUNT(col)` ignores NULL; `COUNT(*)` counts every row. `SUM`, `AVG`, `MIN`, `MAX` all ignore NULL.

### ROLLUP / CUBE / GROUPING SETS

```sql
GROUP BY ROLLUP (A, B)
-- = GROUPING SETS ((A,B), (A), ())
-- Produces: per-A-B rows, per-A subtotals, grand total

GROUP BY CUBE (A, B)
-- = GROUPING SETS ((A,B), (A), (B), ())
-- Produces all combinations

GROUP BY GROUPING SETS ((A,B), (A), ())
-- Explicit control over which groupings appear
```

NULL in a grouping column inside a ROLLUP/CUBE result means "this is a subtotal row". Use `GROUPING(col)` to distinguish subtotal NULLs from data NULLs.

## Worked Examples (AdventureWorks)
1. Revenue, order count, avg/min/max per year.
2. HAVING: customers with 5+ orders.
3. GROUP BY two columns: year + month revenue grid.
4. COUNT(*) vs COUNT(Color) vs COUNT(DISTINCT Color).
5. GROUPING SETS: monthly + yearly + grand total in one query.
6. ROLLUP: hierarchical year/month subtotals.
7. CUBE: all combinations of territory and year.

## Pitfalls
- Selecting a non-aggregated column not in GROUP BY — SQL Server errors (unlike MySQL).
- Filtering on aggregated values in WHERE instead of HAVING.
- `AVG(INT column)` truncates to integer — cast first: `AVG(CAST(col AS DECIMAL(10,2)))`.
- ROLLUP NULL vs real NULL — use `GROUPING(col)` to tell them apart.

## Cheatsheet link
See `cheatsheets/00-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
