# Lesson 07 — Window Functions

**Prerequisites:** Lessons 01–05 (SELECT, joins, GROUP BY aggregation) and lesson 06
(subqueries & CTEs, which you'll use to filter window results).

You want each order **next to** its customer's running total — the detail row *and* the
cumulative figure. `GROUP BY` can't: aggregating collapses the order rows and the detail
is gone. Window functions answer that whole class of "give me the aggregate *without*
throwing away the rows" question — running totals, rankings, period comparisons, shares.

## What you'll learn

- The `OVER()` clause: `PARTITION BY`, `ORDER BY`, and the frame — and how they combine
- Ranking family: `ROW_NUMBER`, `RANK`, `DENSE_RANK`, `NTILE` (and how each treats ties)
- Offset functions: `LAG` / `LEAD` for row-to-row comparisons
- Aggregate windows with framing: running totals and moving averages
- `FIRST_VALUE` / `LAST_VALUE`, and the default-frame trap that bites everyone once

## The mental model

A window function keeps every input row and **annotates** it with a value computed from a
*window* of related rows. Where `GROUP BY` collapses each group to one row (6 orders → 1
total), a window annotates every row and keeps them all (6 orders → 6 rows + a total
column). Picture one partition (one customer's orders), sorted, with a frame that slides
down:

```
 ── one partition: CustomerID = 11091, ORDER BY OrderDate ──
   OrderDate    TotalDue   frame covers    RunningTotal
   2013-07-01      15.45   [■]                    15.45
   2013-07-11      59.66   [■ ■]                  75.11
   2013-08-09      64.61   [■ ■ ■]               139.72
   2013-08-14      14.30   [■ ■ ■ ■]             154.02
   the frame (UNBOUNDED PRECEDING..CURRENT ROW) grows a row per step; SUM covers it
```

`PARTITION BY` picks the box, `ORDER BY` sorts rows inside it, and the **frame** decides
which rows the function currently sees. Change the frame and the same `SUM` becomes a
running total, a moving average, or a whole-partition total.

## Setup

Run `setup.sql` once. It creates the `lesson07` schema and a `MonthlySales` table with
three regions. North and South trend smoothly; **West** rises then takes a deliberate
month-4 dip so the moving-average and anomaly demos have something to catch.

## Concepts

### 1. `OVER()` anatomy — partition, order, frame

Every window function is `f() OVER (PARTITION BY … ORDER BY … <frame>)`. Here is the hook
question answered — each order beside its customer's running total:

```sql
SELECT SalesOrderID, OrderDate, TotalDue,
       SUM(TotalDue) OVER (PARTITION BY CustomerID ORDER BY OrderDate
                           ROWS UNBOUNDED PRECEDING) AS RunningTotal
FROM Sales.SalesOrderHeader WHERE CustomerID = 11091 ORDER BY OrderDate;
```

```
SalesOrderID OrderDate               TotalDue    RunningTotal
------------ ----------------------- ----------- ------------
       51925 2013-07-01 00:00:00.000     15.4479      15.4479
       52420 2013-07-11 00:00:00.000     59.6590      75.1069
       54113 2013-08-09 00:00:00.000     64.6094     139.7163
       54391 2013-08-14 00:00:00.000     14.2987     154.0150
```

`PARTITION BY CustomerID` restarts the total for each customer, `ORDER BY OrderDate` fixes
the direction, and `ROWS UNBOUNDED PRECEDING` (short for `ROWS BETWEEN UNBOUNDED PRECEDING
AND CURRENT ROW`) is the frame — every row from the first up to this one. Drop `PARTITION
BY` and all rows form one window; change the frame and you change the math.

**Reach for this when** a per-row answer depends on the group but you must keep every row.

### 2. Ranking family — and how ties diverge

The three ranking functions differ *only* when the `ORDER BY` has ties. Five products
share the top `ListPrice`, so the difference is visible in one query:

```sql
SELECT Name, ListPrice,
    ROW_NUMBER() OVER (ORDER BY ListPrice DESC) AS RowNum,
    RANK()       OVER (ORDER BY ListPrice DESC) AS Rnk,
    DENSE_RANK() OVER (ORDER BY ListPrice DESC) AS DenseRnk
FROM Production.Product WHERE ListPrice > 0 ORDER BY ListPrice DESC, Name;
```

```
Name                     ListPrice   RowNum   Rnk   DenseRnk
------------------------ ----------- -------- ----- --------
Road-150 Red, 44          3578.2700        1     1        1
Road-150 Red, 48          3578.2700        2     1        1
Road-150 Red, 52          3578.2700        3     1        1
Road-150 Red, 56          3578.2700        4     1        1
Road-150 Red, 62          3578.2700        5     1        1
Mountain-100 Silver, 38   3399.9900        6     6        2
```

`ROW_NUMBER` breaks ties arbitrarily (1–5); `RANK` gives all ties the same number then
*skips* (next is 6); `DENSE_RANK` ties without skipping (next is 2). `NTILE(n)` is the
odd one out — it distributes rows into `n` roughly-equal buckets (see Challenge 4).

**Reach for this when** you want top-N-per-group (`ROW_NUMBER`), a tie-aware leaderboard
(`RANK`/`DENSE_RANK`), or quantile buckets (`NTILE`).

### 3. Offset functions — LAG and LEAD

`LAG` reaches back to a previous row, `LEAD` forward — perfect for "vs. last month":

```sql
SELECT SaleMonth, Revenue,
    LAG(Revenue) OVER (PARTITION BY Region ORDER BY SaleMonth) AS PrevMonth,
    Revenue - LAG(Revenue) OVER (PARTITION BY Region ORDER BY SaleMonth) AS MoMChange
FROM lesson07.MonthlySales WHERE Region = 'West' ORDER BY SaleMonth;
```

```
SaleMonth   Revenue     PrevMonth   MoMChange
----------- ----------- ----------- -----------
          1    20000.00        NULL        NULL
          2    22000.00    20000.00     2000.00
          3    24000.00    22000.00     2000.00
          4    12000.00    24000.00   -12000.00
          5    23000.00    12000.00    11000.00
```

The first row has no predecessor, so `LAG` returns `NULL` (pass a third argument for a
default, e.g. `LAG(Revenue, 1, 0)`). The month-4 dip pops out as a −12000 change.

**Reach for this when** you need period-over-period deltas without a self-join.

### 4. Aggregate windows + framing — the frame is the only difference

Same `OVER`, same `ORDER BY` — only the frame clause differs, and a running total becomes
a moving average. Both in one query so you can compare column by column:

```sql
SELECT SaleMonth, Revenue,
    SUM(Revenue) OVER (PARTITION BY Region ORDER BY SaleMonth
                       ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningTotal,
    AVG(Revenue) OVER (PARTITION BY Region ORDER BY SaleMonth
                       ROWS BETWEEN 2 PRECEDING AND CURRENT ROW) AS MovingAvg3
FROM lesson07.MonthlySales WHERE Region = 'North' ORDER BY SaleMonth;
```

```
SaleMonth   Revenue     RunningTotal   MovingAvg3
----------- ----------- ------------ ----------
          1    12000.00     12000.00   12000.00
          2    15000.00     27000.00   13500.00
          3    11000.00     38000.00   12666.67
          4    18000.00     56000.00   14666.67
```

`UNBOUNDED PRECEDING … CURRENT ROW` accumulates everything so far; `2 PRECEDING … CURRENT
ROW` is a fixed 3-row sliding window — nothing else changed.

**Reach for this when** you need cumulative sums, moving averages, or any rolling metric.

### 5. FIRST_VALUE / LAST_VALUE — mind the default frame

`FIRST_VALUE` and `LAST_VALUE` return the first/last row's value *within the frame* — and
the default frame is not the whole partition. You must state it explicitly:

```sql
SELECT SaleMonth, Revenue,
    FIRST_VALUE(Revenue) OVER (PARTITION BY Region ORDER BY SaleMonth) AS FirstV,
    LAST_VALUE(Revenue)  OVER (PARTITION BY Region ORDER BY SaleMonth
        ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) AS LastV
FROM lesson07.MonthlySales WHERE Region = 'West' ORDER BY SaleMonth;
```

```
SaleMonth   Revenue     FirstV      LastV
----------- ----------- ----------- -----------
          1    20000.00    20000.00    25000.00
          2    22000.00    20000.00    25000.00
          3    24000.00    20000.00    25000.00
```

With the explicit full frame, `LastV` is the partition's genuine last value (25000) on
every row. Leave the frame off `LAST_VALUE` and it breaks — see Pitfalls.

**Reach for this when** you want "compared to the first/last in the group" on every row.

## Worked examples

`examples.sql` runs these against AdventureWorks + `lesson07.MonthlySales`:

1. `ROW_NUMBER()` per customer by spend — notice each customer's numbering restarts at 1.
2. `RANK` vs `DENSE_RANK` — notice the gap after a tie in one but not the other.
3. Top-2 orders per customer — notice `ROW_NUMBER` must live in a subquery to be filtered.
4. `LAG`/`LEAD` month-over-month — notice the NULL neighbour at each partition edge.
5. Running total — notice the frame clause spelled out in full.
6. 3-month moving average — notice only the frame differs from example 5.
7. `FIRST_VALUE`/`LAST_VALUE` — notice the explicit `UNBOUNDED FOLLOWING` frame.

## Pitfalls

- **`LAST_VALUE` with the default frame gives the current row, not the last.** The default
  frame is `RANGE BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW`, so "last so far" is just
  the current row. Watch `LastV` track `Revenue` exactly:

  ```
  SaleMonth   Revenue     FirstV      LastV
  ----------- ----------- ----------- -----------
          1    20000.00    20000.00    20000.00
          2    22000.00    20000.00    22000.00
          3    24000.00    20000.00    24000.00
          4    12000.00    20000.00    12000.00
  ```
  Fix: add `ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING` (concept 5 above).

- **`RANGE` vs `ROWS` with duplicate `ORDER BY` values.** `RANGE` (the default when you
  write `ORDER BY` without a frame) treats all peer rows — rows with the same `ORDER BY`
  value — as one, inflating a running total; `ROWS` counts physical rows. Two rows share
  the date `2023-01-01` here:

  ```
  d          amt   Range_Total   Rows_Total
  ---------- ----- ----------- -----------
  2023-01-01   100         200         100
  2023-01-01   100         200         200
  2023-01-02    50         250         250
  ```
  Both tied rows get `Range_Total = 200` — `RANGE` hands every peer the group's full
  cumulative total (double-counting within the tie), while `Rows_Total` climbs one row at
  a time (100 then 200). Use `ROWS` for true running totals unless you want peers grouped.

- **Window functions can't go in `WHERE`.** They're computed after `WHERE`/`GROUP BY`, so
  `WHERE ROW_NUMBER() OVER (...) = 1` errors:

  ```
  Msg 4108, Level 15, State 1
  Windowed functions can only appear in the SELECT or ORDER BY clauses.
  ```
  Fix: compute the window in a subquery or CTE, then filter in the outer query. (Same
  reason `ROW_NUMBER()` always needs an `ORDER BY` inside `OVER()` — there is no default.)

## Exercises

Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`. The
CHALLENGES section at the bottom is harder: those problems combine this topic with earlier
lessons (aggregation, CTEs, gaps-and-islands) and usually need 2+ window concepts at once.

## Cheatsheet link

See `cheatsheets/04-window-functions.md`.
