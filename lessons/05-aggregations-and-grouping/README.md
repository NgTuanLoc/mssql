# Lesson 05 — Aggregations & Grouping

**Prerequisites:** Lessons 01–04 (SELECT basics, filtering/sorting, data types &
expressions, joins — especially join fan-out, which resurfaces here).

A monthly report asks for the company's average order value. Someone already has
`AVG(TotalDue)` per sales territory sitting in a dashboard, so they average those ten
numbers together and ship it — a figure that looks entirely reasonable and is entirely
wrong. `GROUP BY` doesn't average averages; each bucket carries its own weight, and
pretending otherwise is one of the most common aggregation bugs there is (Challenge 2 is
this exact trap, wearing a join).

## What you'll learn

- `GROUP BY` and the aggregate functions `COUNT`, `SUM`, `AVG`, `MIN`, `MAX` — and what
  "bucketing" actually does to your rows
- `COUNT(*)` vs `COUNT(col)`: how `NULL` is (and isn't) counted
- `HAVING` vs `WHERE` — filtering rows before grouping vs filtering completed groups
- `GROUPING SETS`, `ROLLUP`, and `CUBE` for multi-level subtotals in one result set, plus
  `GROUPING_ID` to label which level a row belongs to
- Conditional aggregation (`SUM(CASE WHEN ...)`) as a poor-man's pivot table

## The mental model

`GROUP BY` sorts rows into buckets by key, then **collapses each bucket to one row** —
the aggregate survives, the detail rows don't. Four real orders placed on 2011-06-01,
bucketed by `TerritoryID`:

```
 rows (Sales.SalesOrderHeader, OrderDate = 2011-06-01)
 ────────────────────────────────────────────────────
 SalesOrderID  TerritoryID  TotalDue
 43702         4            3953.9884   ┐
 43703         9            3953.9884   |
 43704         9            3729.3640   |── fall into buckets by TerritoryID
 43705         9            3756.9890   ┘

 bucket TerritoryID=4          bucket TerritoryID=9
 [3953.9884]                   [3953.9884, 3729.3640, 3756.9890]
 COUNT=1  SUM=3953.9884        COUNT=3  SUM=11440.3414
```

`GROUP BY TerritoryID` turns those 4 detail rows into 2 output rows — `SalesOrderID` and
the individual `TotalDue` values are gone, replaced by the per-bucket `COUNT`/`SUM`.
Contrast with lesson 07: a **window function** would annotate all 4 rows with their
bucket's total instead of collapsing them — same math, but every detail row survives.

## Setup

Run `setup.sql` once — it creates an empty `lesson05` schema. This lesson runs entirely
against AdventureWorks; no seed tables are needed, since `Production.Product.Color` and
`.Weight` already contain real `NULL`s to demonstrate aggregate NULL-handling.

## Concepts

### 1. `COUNT(*)` vs `COUNT(col)` — how NULL is (and isn't) counted

```sql
SELECT
    COUNT(*)              AS AllProducts,
    COUNT(Color)           AS WithColor,          -- NULLs excluded
    COUNT(DISTINCT Color)  AS DistinctColors       -- NULLs excluded, then de-duped
FROM Production.Product;
```

```
AllProducts WithColor   DistinctColors
----------- ----------- --------------
        504         256              9
```

`COUNT(*)` counts rows regardless of content — 504. `COUNT(Color)` counts only rows where
`Color IS NOT NULL` — 248 products (fasteners, adjustable races, and the like) have no
color at all, leaving 256. `COUNT(DISTINCT Color)` further collapses those 256 down to 9
distinct color values. Three different questions, three different numbers, same column.

**Reach for this when** you need to know "how many rows" vs "how many rows actually have
this value" vs "how many distinct values" — they are rarely the same number.

### 2. `HAVING` vs `WHERE` — filtering rows vs filtering groups

```sql
SELECT TerritoryID, COUNT(*) AS OrderCount, SUM(TotalDue) AS Revenue
FROM Sales.SalesOrderHeader
WHERE OrderDate >= '2013-01-01' AND OrderDate < '2014-01-01'   -- row filter, before grouping
GROUP BY TerritoryID
HAVING COUNT(*) >= 2000                                        -- group filter, after grouping
ORDER BY Revenue DESC;
```

```
TerritoryID OrderCount  Revenue
----------- ----------- ---------------------
          4        2725         10239209.3403
          1        2053          6759500.6713
          9        3015          4702404.0504
```

`WHERE` throws out individual 2011/2012/2014 orders before any bucket exists. `GROUP BY`
then buckets what's left by territory — all 10 territories had 2013 orders — and `HAVING`
discards the 7 buckets whose order count fell under 2000, leaving 3. Swap the order and it
won't compile: `WHERE` can't see `COUNT(*)` because groups don't exist yet when `WHERE`
runs.

**Reach for this when** the condition is about a raw column (`WHERE`) vs about something
only the aggregate knows, like a count or a sum (`HAVING`).

### 3. `GROUPING SETS` / `ROLLUP` + `GROUPING_ID` — multi-level subtotals, labelled

```sql
SELECT
    YEAR(OrderDate)  AS OrderYear,
    MONTH(OrderDate) AS OrderMonth,
    SUM(TotalDue)    AS Revenue,
    CASE GROUPING_ID(YEAR(OrderDate), MONTH(OrderDate))
        WHEN 0 THEN 'Detail'
        WHEN 1 THEN 'Year total'
        WHEN 3 THEN 'Grand total'
    END AS Level
FROM Sales.SalesOrderHeader
WHERE YEAR(OrderDate) = 2013
GROUP BY ROLLUP (YEAR(OrderDate), MONTH(OrderDate));
```

```
OrderYear   OrderMonth  Revenue               Level
----------- ----------- --------------------- -----------
       2013           1          2340061.5521 Detail
       2013           2          2600218.8667 Detail
       2013           3          3831605.9389 Detail
       2013        NULL         48965887.9632 Year total
       NULL        NULL         48965887.9632 Grand total
```

`ROLLUP (A, B)` is shorthand for `GROUPING SETS ((A,B), (A), ())` — detail rows, then an
`A`-only subtotal, then a grand total. `GROUPING_ID` packs each row's grouping bits into
one integer (0 = both columns real, 1 = `OrderMonth` rolled up, 3 = both rolled up), which
is what makes a readable `Level` label possible instead of squinting at which columns are
`NULL`.

**Reach for this when** a report needs detail rows and their subtotals in one result set
instead of running several queries and stitching them together.

### 4. Conditional aggregation — the poor-man's pivot

```sql
SELECT
    TerritoryID,
    SUM(CASE WHEN OnlineOrderFlag = 1 THEN 1 ELSE 0 END) AS OnlineOrders,
    SUM(CASE WHEN OnlineOrderFlag = 0 THEN 1 ELSE 0 END) AS StoreOrders
FROM Sales.SalesOrderHeader
GROUP BY TerritoryID
ORDER BY TerritoryID;
```

```
TerritoryID OnlineOrders StoreOrders
----------- ------------ -----------
          1         4058         536
          2           10         342
          3            9         376
          4         5473         751
          5           17         469
```

Each `CASE` turns "does this row belong to this column" into a 1-or-0, and `SUM` inside
the same `GROUP BY` folds those flags into per-bucket counts — one output column per
category, all from a single pass over the table. It's a manual pivot: no `PIVOT` keyword,
just `CASE` nested in an aggregate.

**Reach for this when** you want a matrix (one row per group, one column per category)
without a dedicated pivot operator.

## Worked examples

`examples.sql` runs these against AdventureWorks:

1. Revenue, order count, avg/min/max per year — notice `AvgOrderValue` keeps decimals
   because `TotalDue` is `money`, unlike the truncating `INT` average in Pitfalls.
2. `HAVING`: customers with 5+ orders — notice the WHERE-then-HAVING pipeline from
   Concept 2, sorted by spend rather than order count.
3. `GROUP BY` two columns: year + month revenue grid — notice one row per (year, month)
   combination actually present, not every possible pair.
4. `COUNT(*)` vs `COUNT(Color)` vs `COUNT(DISTINCT Color)` — the exact query from
   Concept 1.
5. `GROUPING SETS`: monthly + yearly + grand total in one query — notice which columns
   go `NULL` at each level, without a `Level` label (that's what Concept 3 adds).
6. `ROLLUP`: hierarchical year/month subtotals — the shorthand equivalent of example 5.
7. `CUBE`: all combinations of territory and year — notice `CUBE` adds a year-only
   subtotal row (all territories combined) that `ROLLUP` never produces, since `ROLLUP`
   only rolls up in the declared column order (territory subtotal, then grand total).

## Pitfalls

- **A non-aggregated column not in `GROUP BY` errors — unlike MySQL, which silently picks
  an arbitrary row.** SQL Server refuses:

  ```sql
  SELECT CustomerID, SalesOrderID, SUM(TotalDue) FROM Sales.SalesOrderHeader GROUP BY CustomerID;
  ```
  ```
  Msg 8120, Level 16, State 1
  Column 'Sales.SalesOrderHeader.SalesOrderID' is invalid in the select list because it
  is not contained in either an aggregate function or the GROUP BY clause.
  ```
  Fix: add `SalesOrderID` to `GROUP BY`, or wrap it in an aggregate (`MIN`/`MAX`) if any
  one value per group is acceptable.

- **An aggregate can't appear in `WHERE`**, even syntactically valid-looking ones:

  ```sql
  SELECT CustomerID, COUNT(*) FROM Sales.SalesOrderHeader WHERE COUNT(*) > 5 GROUP BY CustomerID;
  ```
  ```
  Msg 147, Level 15, State 1
  An aggregate may not appear in the WHERE clause unless it is in a subquery contained
  in a HAVING clause or a select list, and the column being aggregated is an outer
  reference.
  ```
  Fix: move the condition to `HAVING COUNT(*) > 5` (Concept 2) — `WHERE` runs before
  groups exist.

- **`AVG` ignores `NULL`s; a naive `SUM / COUNT(*)` does not — and they disagree.**
  205 of 504 products have a `Weight`; 299 don't:

  ```sql
  SELECT SUM(Weight) / COUNT(*) AS NaiveAvg_WrongDenominator, AVG(Weight) AS AvgWeight_IgnoresNulls
  FROM Production.Product;
  ```
  ```
  NaiveAvg_WrongDenominator                AvgWeight_IgnoresNulls
  ---------------------------------------- ----------------------------------------
                                 30.127361                                74.069219
  ```
  `AVG` divides by 205 (rows with a real weight); dividing the same `SUM` by `COUNT(*)`
  (504) silently treats every weightless product as if it weighed zero, cutting the
  average by more than half.

- **`AVG` on an `INT`/`SMALLINT` column truncates to a whole number.** `OrderQty` is
  `SMALLINT`:

  ```sql
  SELECT AVG(OrderQty) AS AvgQty_Int, AVG(CAST(OrderQty AS DECIMAL(10,2))) AS AvgQty_Dec
  FROM Sales.SalesOrderDetail;
  ```
  ```
  AvgQty_Int  AvgQty_Dec
  ----------- ----------------------------------------
            2                                 2.266079
  ```
  Fix: cast to `DECIMAL`/`FLOAT` before averaging, as shown.

- **`ROLLUP`-generated `NULL` and a genuine data `NULL` look identical unless you check
  `GROUPING()`.** 248 products have a real `NULL` `Color`; `ROLLUP`'s grand-total row is
  *also* `NULL`:

  ```sql
  SELECT Color, COUNT(*) AS ProductCount, GROUPING(Color) AS IsRollupNull
  FROM Production.Product GROUP BY ROLLUP (Color);
  ```
  ```
  Color           ProductCount IsRollupNull
  --------------- ------------ ------------
  NULL                     248            0
  Black                     93            0
  Red                       38            0
  NULL                     504            1
  ```
  Both rows print `Color = NULL`, but `GROUPING(Color) = 0` on the real data row and `= 1`
  on the rollup-manufactured row — that's the only reliable way to tell them apart.

## Exercises

Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`. The
CHALLENGES section at the bottom is harder: those problems combine this lesson's topic
with earlier lessons and usually need 2+ concepts in one query.

## Cheatsheet link

See `cheatsheets/01-tsql-syntax.md`.
