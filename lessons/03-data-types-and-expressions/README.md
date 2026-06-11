# Lesson 03 — Data Types & Expressions

## What you'll learn
- Numeric types: when to use `INT`, `DECIMAL`, and why to avoid `MONEY` for arithmetic
- String types: `VARCHAR` vs `NVARCHAR`, the `N` prefix, and collation basics
- Date/time types: `DATE`, `DATETIME2`, `DATETIMEOFFSET` and their trade-offs
- `CAST`, `CONVERT` (with style), `TRY_CAST`, `TRY_CONVERT`
- Implicit conversion pitfalls that kill index seeks

## Setup
Run `setup.sql` once. It creates the `lesson03` schema and a `TypeDemo` table with sample rows demonstrating type differences.

## Concepts

### CAST vs CONVERT

```sql
CAST(expr AS type)               -- ANSI standard; use for simple type changes
CONVERT(type, expr, style)       -- SQL Server extension; style codes for date formatting
TRY_CAST(expr AS type)           -- returns NULL on failure; safe for untrusted input
```

Common CONVERT date styles: 120 = `YYYY-MM-DD HH:MM:SS`, 103 = `DD/MM/YYYY`, 101 = `MM/DD/YYYY`.

### VARCHAR vs NVARCHAR

`VARCHAR` stores ASCII (1 byte/char). `NVARCHAR` stores Unicode (2 bytes/char). Use `NVARCHAR` for user-facing text. Always prefix Unicode literals with `N`: `N'café'`.

Mixing types: comparing a `VARCHAR` column to an `NVARCHAR` literal works, but SQL Server must implicitly convert every row — this can prevent index use.

### DECIMAL arithmetic

`DECIMAL(p,s)` is exact. `MONEY` rounds intermediate results to 4 decimal places — use `DECIMAL(19,4)` for financial calculations that need accuracy.

### Date functions quick reference

```sql
DATEDIFF(year, BirthDate, GETDATE())   -- age in years (approximate)
DATEADD(month, 3, GETDATE())           -- 3 months from now
EOMONTH(GETDATE())                     -- last day of current month
CAST(SYSDATETIME() AS DATE)            -- today's date, no time component
```

## Worked Examples (AdventureWorks)
1. `CAST(ListPrice AS VARCHAR)` to build a description string.
2. `CONVERT(VARCHAR, OrderDate, 120)` — ISO date string from a DATETIME.
3. `TRY_CAST(VarcharCode AS INT)` — returns NULL instead of erroring.
4. Implicit conversion: `WHERE VarcharCode = 1` vs `WHERE VarcharCode = '1'`.
5. `DECIMAL / 3` vs `MONEY / 3` — precision difference.
6. `DATEDIFF` / `DATEADD` / `EOMONTH` on BirthDate.
7. `N'Café'` vs `'Café'` — Unicode vs VARCHAR.

## Pitfalls
- `FLOAT` / `REAL` are not exact — `0.1 + 0.2 ≠ 0.3` in binary floating point.
- `MONEY` intermediate rounding: `$10.00 / 3 * 3 = $9.9999`, not `$10.00`.
- Forgetting the `N` prefix silently mangles accented or non-Latin characters.
- `CAST(DATETIME AS DATE)` is the right way to strip time — not `CONVERT(VARCHAR, ...)` followed by re-parsing.
- `YEAR(col) = 2024` in a `WHERE` clause is non-SARGable; prefer a range: `col >= '2024-01-01' AND col < '2025-01-01'`.

## Cheatsheet link
See `cheatsheets/02-data-types.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
