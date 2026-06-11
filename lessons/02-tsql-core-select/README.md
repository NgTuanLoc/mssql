# Lesson 02 — T-SQL Core: SELECT, WHERE, ORDER BY, TOP

## What you'll learn
- Filter rows with `WHERE` using `LIKE`, `IN`, `BETWEEN`, and `NULL` semantics
- Combine predicates with `AND`, `OR`, `NOT`
- Control row order with `ORDER BY`
- Limit results with `TOP n` and page through results with `OFFSET/FETCH`

## Setup
Run `setup.sql` once (creates empty `lesson02` schema).

## Concepts

### LIKE

```sql
WHERE Name LIKE 'Road%'       -- starts with Road
WHERE Name LIKE '%Bike%'      -- contains Bike
WHERE Name LIKE '_oad%'       -- any single char then 'oad'
WHERE Name NOT LIKE '%Road%'  -- does not contain Road
```

Wildcards: `%` = any string, `_` = any single character.

### IN and BETWEEN

```sql
WHERE CustomerID IN (100, 200, 300)
WHERE ListPrice BETWEEN 500 AND 1000   -- inclusive
```

`NOT IN` with a subquery that can return NULL causes the entire result to be empty — use `NOT EXISTS` instead.

### NULL Semantics

NULL is unknown — it is not equal to anything, including itself.

```sql
WHERE Color = NULL      -- never matches any row
WHERE Color IS NULL     -- correct
WHERE Color IS NOT NULL -- correct
```

### OFFSET / FETCH (pagination)

```sql
SELECT Name FROM Production.Product
ORDER BY Name
OFFSET 20 ROWS          -- skip first 20
FETCH NEXT 10 ROWS ONLY;-- return next 10
```

Requires `ORDER BY`. Use for result-set pagination.

## Worked Examples (AdventureWorks)
1. `LIKE 'Road%'` — products starting with 'Road'.
2. `IN (11000, 11001, 11002)` — orders for specific customers.
3. `BETWEEN 500 AND 1000` — products in a price band.
4. `Color IS NULL` — products with no colour (vs `= NULL` which always fails).
5. Multiple `AND` conditions on shipped orders.
6. `OFFSET 10 ROWS FETCH NEXT 10 ROWS ONLY` — page 2.
7. `NOT LIKE` + `IS NOT NULL` — exclude road products in subcategories.

## Pitfalls
- `WHERE col = NULL` silently returns no rows — always use `IS NULL`.
- `NOT IN` with a subquery returning NULLs returns 0 rows — a silent bug.
- `BETWEEN` is inclusive on both ends — `BETWEEN '2024-01-01' AND '2024-12-31'` includes Dec 31 at midnight only for `DATE`; for `DATETIME` add a day and use `<`.
- `LIKE` is case-insensitive under most collations — confirm with `SELECT SERVERPROPERTY('Collation')`.

## Cheatsheet link
See `cheatsheets/01-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
