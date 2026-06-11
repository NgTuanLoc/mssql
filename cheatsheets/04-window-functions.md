# Window Functions Cheatsheet

Window functions compute a value across a set of rows related to the current row — without collapsing rows like `GROUP BY` does.

```sql
function_name(...) OVER (
    [PARTITION BY col, ...]
    [ORDER BY col [ASC|DESC], ...]
    [ROWS|RANGE BETWEEN frame_start AND frame_end]
)
```

---

## Ranking Functions

| Function | Notes |
|---|---|
| `ROW_NUMBER()` | Unique sequential integer per partition; ties get arbitrary but distinct numbers |
| `RANK()` | Same rank for ties; gaps after ties (1,1,3) |
| `DENSE_RANK()` | Same rank for ties; no gaps (1,1,2) |
| `NTILE(n)` | Divides rows into `n` buckets as evenly as possible |

```sql
SELECT
    SalesOrderID,
    TotalDue,
    ROW_NUMBER()  OVER (ORDER BY TotalDue DESC) AS RowNum,
    RANK()        OVER (ORDER BY TotalDue DESC) AS Rnk,
    DENSE_RANK()  OVER (ORDER BY TotalDue DESC) AS DenseRnk,
    NTILE(4)      OVER (ORDER BY TotalDue DESC) AS Quartile
FROM Sales.SalesOrderHeader;
```

---

## Offset Functions

| Function | Notes |
|---|---|
| `LAG(col, offset, default)` | Value from `offset` rows before current row |
| `LEAD(col, offset, default)` | Value from `offset` rows after current row |
| `FIRST_VALUE(col)` | First value in the window frame |
| `LAST_VALUE(col)` | Last value in the window frame (frame matters — see below) |

```sql
SELECT
    OrderDate,
    TotalDue,
    LAG(TotalDue,  1, 0) OVER (ORDER BY OrderDate) AS PrevOrderAmount,
    LEAD(TotalDue, 1, 0) OVER (ORDER BY OrderDate) AS NextOrderAmount
FROM Sales.SalesOrderHeader
WHERE CustomerID = 11000;
```

---

## Aggregate Window Functions

Any aggregate can be used as a window function with `OVER()`.

```sql
SELECT
    SalesOrderID,
    TotalDue,
    SUM(TotalDue)   OVER (PARTITION BY YEAR(OrderDate)) AS YearTotal,
    AVG(TotalDue)   OVER (PARTITION BY YEAR(OrderDate)) AS YearAvg,
    COUNT(*)        OVER (PARTITION BY CustomerID)       AS CustomerOrderCount,
    TotalDue * 1.0
      / SUM(TotalDue) OVER (PARTITION BY YEAR(OrderDate)) AS PctOfYear
FROM Sales.SalesOrderHeader;
```

---

## Framing Clauses

The frame defines which rows within the partition are included in the aggregate.

```
ROWS|RANGE BETWEEN frame_start AND frame_end

frame_start / frame_end values:
  UNBOUNDED PRECEDING   -- first row of the partition
  n PRECEDING           -- n rows before current row
  CURRENT ROW           -- current row
  n FOLLOWING           -- n rows after current row
  UNBOUNDED FOLLOWING   -- last row of the partition
```

```sql
-- Running total (cumulative sum)
SUM(TotalDue) OVER (ORDER BY OrderDate ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)

-- 3-row moving average (current row + 2 before)
AVG(TotalDue) OVER (ORDER BY OrderDate ROWS BETWEEN 2 PRECEDING AND CURRENT ROW)

-- LAST_VALUE needs explicit frame to reach the end of the partition
LAST_VALUE(TotalDue) OVER (
    ORDER BY OrderDate
    ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING
)
```

**ROWS vs RANGE:**
- `ROWS` is physical — counts actual rows. Fast and unambiguous.
- `RANGE` is logical — includes all rows with the same ORDER BY value as the current row. Default when you specify `ORDER BY` without a frame clause. Can produce unexpected results with ties.

**Always specify the frame explicitly when using `FIRST_VALUE`/`LAST_VALUE` or aggregates with `ORDER BY`.**

---

## Common Mistakes

- `LAST_VALUE` without an explicit frame returns the current row, not the last in the partition — the default frame is `RANGE BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW`.
- Forgetting `PARTITION BY` when you want per-group calculations — omitting it makes the entire result set one window.
- Using `GROUP BY` and window functions together: window functions are computed after `GROUP BY`, so they operate on the grouped rows.
- `RANK()` gaps confuse reporting — use `DENSE_RANK()` when the report must show consecutive integers.
