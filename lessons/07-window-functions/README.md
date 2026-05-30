# Lesson 07 — Window Functions

## What you'll learn
- `ROW_NUMBER`, `RANK`, `DENSE_RANK`, `NTILE`
- `LAG`, `LEAD`, `FIRST_VALUE`, `LAST_VALUE`
- Aggregate window functions: `SUM() OVER`, `AVG() OVER`
- `PARTITION BY` and `ORDER BY` inside `OVER()`
- Framing clauses: `ROWS BETWEEN`

## Setup
Run `setup.sql` once. It creates the `lesson07` schema and a `MonthlySales` table used in several examples.

## Concepts

### OVER() clause

```sql
function() OVER (
    [PARTITION BY col, ...]   -- divides rows into groups
    [ORDER BY col ASC|DESC]   -- ordering within each partition
    [ROWS BETWEEN start AND end]  -- which rows to include in the aggregate
)
```

Without `PARTITION BY`, all rows form one window.

### Ranking functions

| Function | Ties | Gaps |
|---|---|---|
| `ROW_NUMBER()` | unique (arbitrary for ties) | n/a |
| `RANK()` | same rank | yes — next rank skips |
| `DENSE_RANK()` | same rank | no |
| `NTILE(n)` | distributed across buckets | n/a |

### Framing

```sql
ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW  -- running total
ROWS BETWEEN 2 PRECEDING AND CURRENT ROW          -- 3-row moving window
ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING  -- full partition (for LAST_VALUE)
```

Use `ROWS` (physical), not `RANGE` (logical), unless you specifically want ties included.

## Worked Examples (AdventureWorks + lesson07.MonthlySales)
1. `ROW_NUMBER()` per customer ordered by spend.
2. `RANK` vs `DENSE_RANK` — tie handling.
3. Top-2 orders per customer with `ROW_NUMBER` in a subquery.
4. `LAG`/`LEAD` — month-over-month change.
5. Running total with `ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW`.
6. 3-month moving average with `ROWS BETWEEN 2 PRECEDING AND CURRENT ROW`.
7. `FIRST_VALUE`/`LAST_VALUE` with explicit unbounded frame.

## Pitfalls
- `LAST_VALUE` default frame is `RANGE UNBOUNDED PRECEDING TO CURRENT ROW` — you'll get the current row's value, not the partition's last. Always specify the frame explicitly.
- Omitting `PARTITION BY` makes all rows one window — intentional for running totals, wrong for per-group rankings.
- `ROW_NUMBER()` requires `ORDER BY` in `OVER()` — it doesn't have a meaningful default order.
- Window functions cannot be used in `WHERE` — wrap in a subquery or CTE.

## Cheatsheet link
See `cheatsheets/03-window-functions.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
