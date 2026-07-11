# Lesson 02 — T-SQL Core: SELECT, WHERE, ORDER BY, TOP

**Prerequisites:** Lesson 01 (`SELECT` anatomy, the logical order of execution, `TOP`
needing `ORDER BY`).

A "products that are not red" report built with `WHERE Color <> 'Red'` quietly comes back
short — every product whose color was never recorded just vanishes, no error, no warning.
That's `NULL`'s three-valued logic: `<>` means "known and different from red," and
"unknown" fails silently instead of loudly. This lesson is the toolkit to catch that.

## What you'll learn

- Filter rows with `WHERE` using the predicate toolkit: `LIKE`, `IN`, `BETWEEN`
- Combine predicates with `AND`, `OR`, `NOT`
- `NULL`'s three-valued logic — why `UNKNOWN` isn't `FALSE`, and where that bites
- Deterministic `ORDER BY` — why ties need a tie-breaker column
- Page through results with `OFFSET/FETCH`, and why it needs `ORDER BY` to mean anything

## The mental model

Lesson 01 named the logical order (`FROM → WHERE → SELECT → ORDER BY`) — here's the full
pipeline including paging, each stage seeing only what the previous stage handed it:

```
FROM (locate rows) ──▶ WHERE (predicate must be TRUE) ──▶ SELECT (compute cols/aliases)
  ──▶ ORDER BY (sort; aliases now exist) ──▶ OFFSET/FETCH or TOP (slice the sorted rows)
```

`WHERE` runs *before* `SELECT` builds its column list, so an alias doesn't exist yet when
`WHERE` runs — this fails with `Msg 207, Invalid column name 'PriceWithTax'`:

```sql
SELECT ListPrice * 1.1 AS PriceWithTax FROM Production.Product WHERE PriceWithTax > 1000;
```

The same alias works fine in `ORDER BY`, because by then `SELECT` has already run:

```sql
SELECT TOP (3) Name, ListPrice * 1.1 AS PriceWithTax
FROM Production.Product ORDER BY PriceWithTax DESC;
```
```
Name                     PriceWithTax
------------------------ ------------
Road-150 Red, 62           3936.09700
Road-150 Red, 44           3936.09700
Road-150 Red, 48           3936.09700
```

## Setup

Run `setup.sql` once (creates the empty `lesson02` schema — this lesson needs nothing
beyond AdventureWorks).

## Concepts

### 1. The predicate toolkit — `LIKE`, `IN`, `BETWEEN`

Real filters usually combine all three in one `WHERE`, not one at a time:

```sql
SELECT ProductID, Name, ListPrice, ProductSubcategoryID
FROM Production.Product
WHERE Name LIKE 'Mountain%'
  AND ProductSubcategoryID IN (1, 2, 3)
  AND ListPrice BETWEEN 500 AND 1500
ORDER BY ListPrice DESC;
```

```
ProductID   Name                       ListPrice   ProductSubcategoryID
----------- -------------------------- ----------- ---------------------
        785 Mountain-300 Black, 38       1079.9900                     1
        980 Mountain-400-W Silver, 38     769.4900                     1
        984 Mountain-500 Silver, 40       564.9900                     1
```

`LIKE` matches a string pattern: `%` = any string (`'Road%'` starts with "Road", `'%Bike%'`
contains "Bike" anywhere), `_` = any single character (`'_oad%'`), `[0-9]` a character
class, and `NOT LIKE '%Road%'` negates the match. `IN` is shorthand for chained `OR`s
against a fixed set; `BETWEEN lo AND hi` is inclusive on **both** ends — `BETWEEN 500 AND
1500` includes a product priced exactly `1500`.

**Reach for this when** a filter is "matches a pattern," "in this set," or "in this range"
— reach for `EXISTS`/joins once the set comes from another table.

### 2. `NULL`'s three-valued logic

SQL predicates evaluate `TRUE`, `FALSE`, or `UNKNOWN` — `WHERE` keeps a row only when its
predicate is `TRUE`. A comparison against `NULL` never reaches `TRUE`/`FALSE`:

```sql
SELECT a, b,
    CASE WHEN a = b  THEN 'TRUE'
         WHEN a <> b THEN 'FALSE'
         ELSE 'UNKNOWN' END AS EqualityResult
FROM (VALUES (1, 1), (1, 2), (1, NULL), (NULL, NULL)) AS t(a, b);
```

```
a           b           EqualityResult
----------- ----------- --------------
1           1           TRUE
1           2           FALSE
1           NULL        UNKNOWN
NULL        NULL        UNKNOWN
```

Note the last row: `NULL = NULL` is `UNKNOWN`, not `TRUE` — two unknowns aren't provably
equal. `WHERE` drops an `UNKNOWN` row just like a `FALSE` one, but it was never resolved
either way — exactly why `Color <> 'Red'` drops rows with no recorded color (see Pitfalls).

**Reach for this when** a query returns fewer rows than the data suggests it should —
check whether a filtered column can be `NULL` before suspecting the data itself.

### 3. `ORDER BY` determinism — ties need a tie-breaker

`ORDER BY` only guarantees the order of the column(s) you name. Rows tied on that column
have no guaranteed relative order unless you break the tie explicitly:

```sql
SELECT ListPrice, COUNT(*) AS Ties
FROM Production.Product
WHERE ListPrice > 0
GROUP BY ListPrice
HAVING COUNT(*) > 1
ORDER BY Ties DESC;
```

```
ListPrice    Ties
------------ -----------
782.9900     12
337.2200     12
1431.5000    11
742.3500     10
```

56 different prices have 2+ products sharing them — `ORDER BY ListPrice DESC` alone can
return tied rows in any order, and a new index can silently reorder them between runs.
Add a unique tie-breaker column and the order becomes reproducible:

```sql
SELECT ProductID, Name, ListPrice
FROM Production.Product
WHERE ListPrice = 3578.27
ORDER BY ListPrice DESC, ProductID;
```

```
ProductID   Name                ListPrice
----------- ------------------- -----------
749         Road-150 Red, 62    3578.2700
750         Road-150 Red, 44    3578.2700
751         Road-150 Red, 48    3578.2700
```

**Reach for this when** results feed a report, a diff, or pagination (concept 4) — anywhere
row order must be stable across runs, not just "usually looks right."

### 4. `OFFSET`/`FETCH` — paging results

`OFFSET n ROWS FETCH NEXT m ROWS ONLY` requires an `ORDER BY` and skips `n` rows before
returning the next `m` — page 2 at 10 rows/page skips the first 10:

```sql
SELECT ProductID, Name
FROM Production.Product
ORDER BY Name
OFFSET 10 ROWS FETCH NEXT 10 ROWS ONLY;
```

```
ProductID   Name
----------- -----------------
322         Chainring
320         Chainring Bolts
321         Chainring Nut
```

`Name` is unique here, so `ORDER BY Name` alone is deterministic — paging on a column
with ties (like `ListPrice`) needs the concept-3 tie-breaker too (Challenge 1).

**Reach for this when** you need result-set pagination — and always pair it with a
deterministic `ORDER BY`, or page boundaries can shift or duplicate rows between requests.

## Worked examples

`examples.sql` runs these against AdventureWorks:

1. `LIKE 'Road%'` — products starting with "Road"; notice the pattern anchors at the start.
2. `IN (11000, 11001, 11002)` — orders for three specific customers in one predicate.
3. `BETWEEN 500 AND 1000` — products in a price band; notice both endpoints are included.
4. `Color IS NULL` — products with no recorded color; notice `= NULL` would return nothing.
5. `AND` combining a status filter with a minimum total — notice both conditions must hold.
6. `OFFSET 10 ROWS FETCH NEXT 10 ROWS ONLY` — page 2 of 10-row pages, no tie-breaker yet.
7. `NOT LIKE` + `IS NOT NULL` — negate a pattern and require a non-null column together.

## Pitfalls

- **`<>` silently drops rows where the column is `NULL`, not just different values.** Of
  504 products, 82 have `Class = 'H'`, 257 have `Class IS NULL`, and the naive `<>` keeps
  only the 165 that are non-`NULL` and non-`'H'`:
  ```sql
  SELECT SUM(CASE WHEN Class = 'H' THEN 1 ELSE 0 END)   AS HighClass,
         SUM(CASE WHEN Class <> 'H' THEN 1 ELSE 0 END)  AS NotHighNaive,
         SUM(CASE WHEN Class IS NULL THEN 1 ELSE 0 END) AS NullClass
  FROM Production.Product;  -- HighClass 82, NotHighNaive 165, NullClass 257
  ```
  The correct "not high-class" puts the `NULL`s back: `WHERE Class <> 'H' OR Class IS
  NULL` → 422 rows (`504 − 82`). The hook's `Color <> 'Red'` report fails the same way.

- **`WHERE col = NULL` matches nothing, ever** — not even other `NULL`s (concept 2):
  `Color = NULL` returns 0 rows; `Color IS NULL` correctly returns 248. Always use
  `IS NULL` / `IS NOT NULL`, never `=`/`<>` against `NULL`.

- **`NOT IN` with a `NULL` anywhere in the list returns zero rows for everything.**
  `SalesPersonID` is `NULL` for online orders (27,659 of 31,465 rows):
  ```sql
  SELECT COUNT(*) FROM Person.Person
  WHERE BusinessEntityID NOT IN (SELECT SalesPersonID FROM Sales.SalesOrderHeader);
  -- 0 rows — one NULL in the list poisons every comparison to UNKNOWN
  ```
  Filtering the subquery (`WHERE SalesPersonID IS NOT NULL`) gives the real answer,
  19,955 rows. Prefer `NOT EXISTS` over `NOT IN` when the subquery column can be `NULL`.

- **`BETWEEN` on `DATETIME` silently excludes anything after midnight on the end date.**
  Every row in `Production.Product` was modified `2014-02-08 10:01:36.827` or later:
  ```sql
  SELECT COUNT(*) FROM Production.Product
  WHERE ModifiedDate BETWEEN '2014-02-01' AND '2014-02-08';                      -- 0 rows
  SELECT COUNT(*) FROM Production.Product
  WHERE ModifiedDate >= '2014-02-01' AND ModifiedDate < '2014-02-09';          -- 504 rows
  ```
  The literal `'2014-02-08'` parses as midnight, so `BETWEEN`'s inclusive upper bound still
  excludes every row modified later that day. Use a half-open range (`>= start AND < end`).

- **`LIKE` is case-insensitive under most collations.** AdventureWorks2022 uses
  `SQL_Latin1_General_CP1_CI_AS` (`CI`, same collation behind lesson 01's `=` pitfall), so
  `LIKE 'road%'` matches `Road-150 Red, 62` too. Confirm with `SERVERPROPERTY('Collation')`.

## Exercises

Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`. The
CHALLENGES section at the bottom is harder: those problems combine this lesson's topic with
lesson 01's ideas and usually need 2+ concepts in one query.

## Cheatsheet link

See `cheatsheets/01-tsql-syntax.md`.
