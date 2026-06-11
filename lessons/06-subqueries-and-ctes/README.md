# Lesson 06 — Subqueries & CTEs

## What you'll learn
- Scalar subqueries in `SELECT` and `WHERE`
- `IN` with a subquery vs `EXISTS` / `NOT EXISTS`
- Correlated subqueries (reference the outer query)
- Non-recursive CTEs to organise complex queries
- Recursive CTEs for hierarchies and sequences

## Setup
Run `setup.sql` once (creates empty `lesson06` schema).

## Concepts

### Subquery placement

```sql
-- In SELECT (must return exactly one row/column)
SELECT col, (SELECT MAX(x) FROM other) AS Max FROM table;

-- In WHERE with IN
WHERE col IN (SELECT id FROM lookup);

-- In WHERE with EXISTS
WHERE EXISTS (SELECT 1 FROM other WHERE other.fk = outer.id);
```

### EXISTS vs IN

- `EXISTS` is short-circuit — it stops at the first match. Often faster on large sets.
- `IN` materialises the full subquery result. Use when the subquery is small and straightforward.
- `NOT IN` breaks silently when the subquery returns any NULL. Always prefer `NOT EXISTS`.

```sql
-- Dangerous: if subquery returns NULL, entire NOT IN result is empty
WHERE id NOT IN (SELECT foreign_id FROM other)  -- avoid

-- Safe:
WHERE NOT EXISTS (SELECT 1 FROM other WHERE other.foreign_id = outer.id)
```

### Common Table Expressions (CTEs)

```sql
WITH CTE_Name AS (
    SELECT ...
),
CTE2 AS (
    SELECT ... FROM CTE_Name
)
SELECT * FROM CTE2;
```

CTEs are not materialised by default — the optimizer may inline them. Use `OPTION (MAXRECURSION n)` on recursive CTEs if you expect deep hierarchies (default limit = 100).

### Recursive CTE pattern

```sql
WITH R AS (
    SELECT ...        -- anchor (base case)
    UNION ALL
    SELECT ...        -- recursive member (references R)
    FROM R
    WHERE <stop condition>
)
SELECT * FROM R;
```

## Worked Examples (AdventureWorks)
1. Scalar subquery: each order vs overall average.
2. `IN` subquery: products in the Bikes category.
3. Correlated subquery: order count per customer.
4. `EXISTS`: customers with at least one order.
5. `NOT EXISTS`: customers who have never ordered.
6. Non-recursive CTE: top spenders joined to customer table.
7. Recursive CTE: employee hierarchy.

## Pitfalls
- `NOT IN` with subqueries returning NULL — produces empty results silently. Use `NOT EXISTS`.
- Correlated subqueries execute once per outer row — can be slow on large tables. Consider a JOIN or CTE instead.
- CTEs are not guaranteed to be materialised — if you need repeated access, use a temp table.
- Recursive CTEs default to 100 recursion levels; set `OPTION (MAXRECURSION 0)` for unlimited (be careful).

## Cheatsheet link
See `cheatsheets/01-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
