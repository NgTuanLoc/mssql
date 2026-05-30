# Lesson 01 — Setup & First Queries

## What you'll learn
- Start MSSQL 2022 in Docker and connect with SSMS or Azure Data Studio
- Write your first `SELECT` statement
- Use `TOP`, `WHERE`, and `ORDER BY`
- Explore table structure with Object Explorer and `sp_help`

## Setup
Run `setup.sql` once. It creates the `lesson01` schema (no extra data needed — AdventureWorks is sufficient for this lesson).

```powershell
# Container must already be running (see root README)
.\scripts\connect.ps1
# Then in sqlcmd:
:r lessons\01-setup-and-basics\setup.sql
```

Or open `setup.sql` in SSMS and press F5.

## Concepts

### Connecting
- SSMS / Azure Data Studio: server `localhost,1433`, login `sa`, password from `docker/.env`.
- Object Explorer (SSMS) → Databases → AdventureWorks2022 → Tables to browse schema.

### Basic SELECT

```sql
SELECT col1, col2        -- which columns (or * for all)
FROM   schema.table      -- which table
WHERE  predicate         -- filter rows (optional)
ORDER BY col1 DESC;      -- sort (optional; without it, order is undefined)
```

### TOP n

```sql
SELECT TOP 10 Name, ListPrice
FROM Production.Product
ORDER BY ListPrice DESC;
```

`TOP` without `ORDER BY` returns an arbitrary set of rows.

### Counting and DISTINCT

```sql
SELECT COUNT(*)        FROM Sales.SalesOrderHeader;   -- total rows
SELECT COUNT(col)      FROM table;                    -- non-NULL values only
SELECT DISTINCT Color  FROM Production.Product;        -- unique values
```

## Worked Examples (AdventureWorks)

1. `SELECT * FROM Person.CountryRegion` — see all countries (small table, safe to SELECT *).
2. Select specific columns with aliases to make output readable.
3. `TOP 10 ... ORDER BY LastName` — first ten people alphabetically.
4. Filter products by `ListPrice > 1000` — WHERE clause basics.
5. `EXEC sp_help 'Production.Product'` — inspect column names, types, and constraints.

## Pitfalls
- `SELECT *` is fine for exploration; avoid in production queries (schema changes break callers).
- `ORDER BY` without `TOP` or `OFFSET` is technically undefined in SQL Server — results may appear ordered in practice but are not guaranteed.
- `COUNT(col)` skips NULLs; `COUNT(*)` counts every row.
- Without `WHERE`, `UPDATE` and `DELETE` affect every row in the table — always confirm the predicate first.

## Cheatsheet link
See `cheatsheets/00-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql` — peek only after attempting.
