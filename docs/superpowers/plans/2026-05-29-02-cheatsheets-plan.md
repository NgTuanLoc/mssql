# Cheatsheets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write all six reference cheatsheets so learners have scannable, code-heavy docs they can consult at any point in the curriculum without reading lesson prose.

**Architecture:** Each cheatsheet is a standalone Markdown file: short intro → tables/code blocks → "common mistakes" footer. No cross-file dependencies. Files live in `cheatsheets/` and are numbered to match their first relevant lesson tier, but are usable in any order.

**Tech Stack:** Markdown (GitHub-flavored), T-SQL (MSSQL 2022).

---

## File Map

| Action | Path | Covers |
|--------|------|--------|
| Create | `cheatsheets/00-tsql-syntax.md` | Statement skeletons, built-ins, variables, control flow, `GO` |
| Create | `cheatsheets/01-data-types.md` | Every common type, ranges, conversion gotchas |
| Create | `cheatsheets/02-joins-and-sets.md` | Join types, set ops, diagrams, canonical examples |
| Create | `cheatsheets/03-window-functions.md` | Every window function, framing clauses |
| Create | `cheatsheets/04-indexes.md` | Index types, syntax, DMVs, rules of thumb |
| Create | `cheatsheets/05-execution-plans.md` | Plan operators, SARGability, `SET` commands, warning signs |

---

## Task 1: `cheatsheets/00-tsql-syntax.md`

**Files:**
- Create: `cheatsheets/00-tsql-syntax.md`

- [ ] **Step 1: Create the file**

````markdown
# T-SQL Syntax Cheatsheet

Quick reference for T-SQL statement structure, built-in functions, variables, and control flow.

---

## Statement Skeletons

```sql
-- SELECT
SELECT [TOP n] col1, col2, expr AS alias
FROM   schema.table  [WITH (NOLOCK)]
[JOIN  other ON condition]
[WHERE predicate]
[GROUP BY col1]
[HAVING aggregate_condition]
[ORDER BY col1 [ASC|DESC]]
[OFFSET n ROWS FETCH NEXT m ROWS ONLY];

-- INSERT
INSERT INTO schema.table (col1, col2)
VALUES (v1, v2), (v3, v4);

INSERT INTO schema.table (col1, col2)
SELECT col1, col2 FROM other_table WHERE condition;

-- UPDATE
UPDATE t
SET    t.col1 = expr
FROM   schema.table AS t
[JOIN  other AS o ON condition]
WHERE  predicate;

-- DELETE
DELETE t
FROM   schema.table AS t
[JOIN  other AS o ON condition]
WHERE  predicate;

-- MERGE
MERGE  target AS t
USING  source AS s ON (t.id = s.id)
WHEN MATCHED                     THEN UPDATE SET t.col = s.col
WHEN NOT MATCHED BY TARGET       THEN INSERT (col) VALUES (s.col)
WHEN NOT MATCHED BY SOURCE       THEN DELETE;
```

---

## Variables

```sql
DECLARE @name      NVARCHAR(100) = N'Alice';
DECLARE @count     INT;
SET     @count     = 0;

SELECT @count = COUNT(*) FROM dbo.Orders WHERE CustomerID = 1;
PRINT  CAST(@count AS NVARCHAR) + ' orders found';
```

---

## Control Flow

```sql
-- IF / ELSE
IF @count > 10
    PRINT 'High volume';
ELSE IF @count > 0
    PRINT 'Some orders';
ELSE
    PRINT 'No orders';

-- WHILE
WHILE @count < 5
BEGIN
    SET @count += 1;
    PRINT @count;
END;

-- CASE (expression)
SELECT
    CASE Status
        WHEN 1 THEN 'Active'
        WHEN 2 THEN 'Inactive'
        ELSE        'Unknown'
    END AS StatusLabel
FROM dbo.Orders;

-- CASE (searched)
SELECT
    CASE
        WHEN TotalDue > 1000 THEN 'High'
        WHEN TotalDue > 100  THEN 'Medium'
        ELSE                      'Low'
    END AS Tier
FROM Sales.SalesOrderHeader;
```

---

## Batches and GO

`GO` is **not** a T-SQL keyword — it is an SSMS/sqlcmd batch separator. Statements before each `GO` are sent as one batch.

```sql
CREATE TABLE dbo.Foo (id INT);
GO                                -- must separate DDL from DML in same script

INSERT dbo.Foo VALUES (1);
GO
```

---

## String Functions

| Function | Example | Result |
|---|---|---|
| `LEN(s)` | `LEN('hello')` | `5` |
| `DATALENGTH(s)` | `DATALENGTH(N'hi')` | `4` (bytes, 2 per NCHAR) |
| `LEFT(s,n)` | `LEFT('hello',3)` | `'hel'` |
| `RIGHT(s,n)` | `RIGHT('hello',3)` | `'llo'` |
| `SUBSTRING(s,start,len)` | `SUBSTRING('hello',2,3)` | `'ell'` (1-based) |
| `CHARINDEX(find,s)` | `CHARINDEX('l','hello')` | `3` |
| `REPLACE(s,old,new)` | `REPLACE('hello','l','r')` | `'herro'` |
| `LTRIM/RTRIM/TRIM(s)` | `TRIM('  hi  ')` | `'hi'` |
| `UPPER/LOWER(s)` | `LOWER('ABC')` | `'abc'` |
| `CONCAT(a,b,...)` | `CONCAT('a','-','b')` | `'a-b'` |
| `STRING_AGG(col,sep)` | `STRING_AGG(Name,',')` | `'a,b,c'` |
| `FORMAT(val,fmt)` | `FORMAT(1234.5,'N2')` | `'1,234.50'` |

---

## Date Functions

| Function | Notes |
|---|---|
| `GETDATE()` | Current local datetime (DATETIME) |
| `SYSDATETIME()` | Current local datetime (DATETIME2, higher precision) |
| `GETUTCDATE()` | UTC datetime (DATETIME) |
| `DATEADD(part,n,date)` | Add `n` units of `part` to `date` |
| `DATEDIFF(part,start,end)` | Difference in `part` units |
| `DATEPART(part,date)` | Extract integer part (year, month, day, hour…) |
| `DATENAME(part,date)` | Extract named part ('January', 'Monday'…) |
| `EOMONTH(date)` | Last day of the month |
| `FORMAT(date,'yyyy-MM-dd')` | Format as string |
| `CAST(s AS DATE)` | Parse string to date |

Common `part` values: `year`, `quarter`, `month`, `day`, `hour`, `minute`, `second`, `millisecond`, `weekday`.

---

## Math Functions

| Function | Notes |
|---|---|
| `ROUND(n,d)` | Round to `d` decimal places |
| `FLOOR(n)` / `CEILING(n)` | Round down / up to integer |
| `ABS(n)` | Absolute value |
| `POWER(base,exp)` | Exponentiation |
| `SQRT(n)` | Square root |
| `RAND()` | Random FLOAT between 0 and 1 |

---

## Conversion Functions

```sql
CAST(expr AS type)           -- standard SQL, preferred for simple casts
CONVERT(type, expr [, style])-- SQL Server extension; style matters for dates
TRY_CAST(expr AS type)       -- returns NULL on failure instead of error
TRY_CONVERT(type, expr)      -- same, CONVERT variant

-- Examples
SELECT CAST(3.7 AS INT)                        -- 3  (truncates)
SELECT CONVERT(VARCHAR(10), GETDATE(), 120)    -- '2026-05-29' (style 120 = ISO)
SELECT TRY_CAST('abc' AS INT)                  -- NULL (no error)
```

---

## NULL Handling

```sql
ISNULL(expr, replacement)         -- replace NULL with replacement
COALESCE(a, b, c)                 -- first non-NULL; ANSI standard, preferred
NULLIF(a, b)                      -- returns NULL if a = b, else a

-- NULL comparisons: always use IS NULL / IS NOT NULL
WHERE col IS NULL
WHERE col IS NOT NULL
-- col = NULL never matches, even when col is NULL
```

---

## Common Mistakes

- `SELECT *` in production — always name columns explicitly.
- Implicit `NVARCHAR`/`VARCHAR` conversion: comparing `VARCHAR` column to `N'literal'` forces a scan.
- `PRINT` only outputs after the batch completes in SSMS — not mid-loop.
- `RAND()` inside a join is evaluated once per query, not per row — use `NEWID()` for per-row randomness.
- `DATEDIFF(day, start, end)` counts day boundaries crossed, not 24-hour periods.
````

- [ ] **Step 2: Commit**

```powershell
git add cheatsheets/00-tsql-syntax.md
git commit -m "docs: add T-SQL syntax cheatsheet"
```

---

## Task 2: `cheatsheets/01-data-types.md`

**Files:**
- Create: `cheatsheets/01-data-types.md`

- [ ] **Step 1: Create the file**

````markdown
# Data Types Cheatsheet

---

## Exact Numerics

| Type | Storage | Range / Notes |
|---|---|---|
| `BIT` | 1 bit (1 byte if standalone) | 0, 1, or NULL |
| `TINYINT` | 1 byte | 0–255 |
| `SMALLINT` | 2 bytes | –32,768–32,767 |
| `INT` | 4 bytes | –2.1B–2.1B (default integer choice) |
| `BIGINT` | 8 bytes | ±9.2 × 10¹⁸ |
| `DECIMAL(p,s)` / `NUMERIC(p,s)` | 5–17 bytes | Exact; `p` = total digits, `s` = decimal places |
| `MONEY` | 8 bytes | ±922 trillion, 4 decimal places — avoid; use `DECIMAL(19,4)` instead |
| `SMALLMONEY` | 4 bytes | ±214,748, 4 decimal places |

---

## Approximate Numerics

| Type | Storage | Notes |
|---|---|---|
| `FLOAT(n)` | 4 or 8 bytes | `n` 1–24 → 4 bytes; 25–53 → 8 bytes. Not exact — never use for money. |
| `REAL` | 4 bytes | Alias for `FLOAT(24)` |

---

## Strings

| Type | Max length | Notes |
|---|---|---|
| `CHAR(n)` | 8,000 bytes | Fixed-width ASCII. Pads with spaces. |
| `VARCHAR(n)` | 8,000 bytes | Variable-width ASCII. `VARCHAR(MAX)` → 2 GB. |
| `NCHAR(n)` | 4,000 chars | Fixed-width Unicode (UTF-16). |
| `NVARCHAR(n)` | 4,000 chars | Variable-width Unicode. `NVARCHAR(MAX)` → 2 GB. Use for user-facing text. |

**Rule of thumb:** use `NVARCHAR` for anything a user types; `VARCHAR` for codes and identifiers you control.

Literals: prefix Unicode strings with `N`: `N'café'`. Without `N`, non-ASCII characters are silently mangled.

---

## Date and Time

| Type | Accuracy | Range | Storage | Notes |
|---|---|---|---|---|
| `DATE` | 1 day | 0001–9999 | 3 bytes | Date only |
| `TIME(n)` | 100ns (n=7) | 00:00–23:59 | 3–5 bytes | Time only |
| `DATETIME` | ~3.33ms | 1753–9999 | 8 bytes | Legacy; rounding surprises |
| `DATETIME2(n)` | 100ns (n=7) | 0001–9999 | 6–8 bytes | **Preferred** for datetime |
| `SMALLDATETIME` | 1 min | 1900–2079 | 4 bytes | Legacy |
| `DATETIMEOFFSET(n)` | 100ns | 0001–9999 | 8–10 bytes | Includes UTC offset; use for timezone-aware data |

---

## Other Types

| Type | Notes |
|---|---|
| `UNIQUEIDENTIFIER` | 16-byte GUID. `NEWID()` generates a random GUID; `NEWSEQUENTIALID()` generates ordered GUIDs (better for clustered index). |
| `XML` | Up to 2 GB. Has its own query methods (`.value()`, `.nodes()`). |
| `VARBINARY(n)` / `VARBINARY(MAX)` | Binary data. `MAX` = 2 GB. |
| `ROWVERSION` / `TIMESTAMP` | Auto-incrementing binary, used for optimistic concurrency. |

---

## Implicit Conversion Gotchas

```sql
-- WRONG: implicit conversion from INT to VARCHAR forces a full scan on a VARCHAR index
WHERE VarcharColumn = 42          -- 42 is INT; SQL Server must convert every row

-- RIGHT
WHERE VarcharColumn = '42'

-- WRONG: comparing DATE to DATETIME loses index SARGability in some cases
WHERE DatetimeCol = '2024-01-01'  -- '2024-01-01' becomes midnight; misses rows at other times

-- RIGHT for open-ended date ranges
WHERE DatetimeCol >= '2024-01-01' AND DatetimeCol < '2024-01-02'
```

Implicit conversion chart: SQL Server will implicitly convert "lower" types to "higher" types in the data type precedence hierarchy. When in doubt, use explicit `CAST`/`CONVERT`.

---

## Common Mistakes

- Using `FLOAT`/`REAL` for financial data — use `DECIMAL(p,s)`.
- `MONEY` rounding: intermediate arithmetic uses only 4 decimal places, causing errors. Use `DECIMAL(19,4)`.
- `DATETIME` rounds to nearest 3.33ms — never use for precise time storage.
- Forgetting `N` prefix on Unicode literals: `'café'` silently drops the accent on non-Unicode columns.
- `VARCHAR(MAX)` columns cannot be indexed (only first 900 bytes usable in an index key).
````

- [ ] **Step 2: Commit**

```powershell
git add cheatsheets/01-data-types.md
git commit -m "docs: add data types cheatsheet"
```

---

## Task 3: `cheatsheets/02-joins-and-sets.md`

**Files:**
- Create: `cheatsheets/02-joins-and-sets.md`

- [ ] **Step 1: Create the file**

````markdown
# Joins & Set Operations Cheatsheet

---

## Join Types

```
A      B         INNER JOIN        LEFT JOIN         RIGHT JOIN        FULL JOIN
●●●    ●●●       ●●● ∩ ●●●        ●●● + overlap     overlap + ●●●     ●●● ∪ ●●●
```

### INNER JOIN — rows with matches on both sides

```sql
SELECT o.SalesOrderID, c.AccountNumber
FROM   Sales.SalesOrderHeader AS o
INNER JOIN Sales.Customer      AS c ON c.CustomerID = o.CustomerID;
```

### LEFT (OUTER) JOIN — all left rows; NULLs for unmatched right

```sql
SELECT c.CustomerID, o.SalesOrderID   -- o columns are NULL when no order exists
FROM   Sales.Customer          AS c
LEFT JOIN Sales.SalesOrderHeader AS o ON o.CustomerID = c.CustomerID;
```

### RIGHT (OUTER) JOIN — all right rows; NULLs for unmatched left

Equivalent to flipping LEFT JOIN. Rarely needed — prefer LEFT JOIN for readability.

### FULL (OUTER) JOIN — all rows from both sides; NULLs where no match

```sql
SELECT a.ID AS aID, b.ID AS bID
FROM   dbo.TableA AS a
FULL JOIN dbo.TableB AS b ON b.ID = a.ID;
```

### CROSS JOIN — every combination (Cartesian product)

```sql
SELECT c.Color, s.Size
FROM   dbo.Colors AS c
CROSS JOIN dbo.Sizes AS s;
-- n × m rows
```

### Self Join — join a table to itself

```sql
SELECT e.FirstName AS Employee, m.FirstName AS Manager
FROM   HumanResources.Employee AS e
LEFT JOIN HumanResources.Employee AS m ON m.BusinessEntityID = e.ManagerID;
```

---

## Multi-Table Joins

```sql
SELECT soh.SalesOrderID,
       p.FirstName + ' ' + p.LastName AS CustomerName,
       pr.Name AS ProductName,
       sod.OrderQty
FROM   Sales.SalesOrderHeader  AS soh
JOIN   Sales.Customer          AS c   ON c.CustomerID   = soh.CustomerID
JOIN   Person.Person           AS p   ON p.BusinessEntityID = c.PersonID
JOIN   Sales.SalesOrderDetail  AS sod ON sod.SalesOrderID   = soh.SalesOrderID
JOIN   Production.Product      AS pr  ON pr.ProductID        = sod.ProductID;
```

---

## Set Operations

All set operations require matching column count and compatible types. Column names come from the first query.

### UNION vs UNION ALL

```sql
-- UNION: removes duplicates (sorts internally — slower)
SELECT City FROM Person.Address WHERE StateProvinceID = 1
UNION
SELECT City FROM Person.Address WHERE StateProvinceID = 2;

-- UNION ALL: keeps duplicates (faster, use when duplicates are acceptable or impossible)
SELECT ProductID FROM Sales.SalesOrderDetail
UNION ALL
SELECT ProductID FROM Purchasing.PurchaseOrderDetail;
```

### INTERSECT — rows present in both result sets

```sql
SELECT ProductID FROM Sales.SalesOrderDetail
INTERSECT
SELECT ProductID FROM Purchasing.PurchaseOrderDetail;
```

### EXCEPT — rows in first set but not in second

```sql
-- Products that have been sold but never purchased
SELECT ProductID FROM Sales.SalesOrderDetail
EXCEPT
SELECT ProductID FROM Purchasing.PurchaseOrderDetail;
```

---

## Common Join Mistakes

- **Forgetting NULL in outer joins:** `WHERE right.col = value` on a LEFT JOIN turns it into an INNER JOIN — move the filter to the `ON` clause.
- **Accidental fan-out:** joining a one-to-many relationship without being aware multiplies rows.
- **Non-SARGable join condition:** `ON YEAR(o.OrderDate) = YEAR(s.ShipDate)` prevents index seeks.
- **UNION instead of UNION ALL:** when duplicates are impossible (different PKs), the dedup work is wasted.
````

- [ ] **Step 2: Commit**

```powershell
git add cheatsheets/02-joins-and-sets.md
git commit -m "docs: add joins and set operations cheatsheet"
```

---

## Task 4: `cheatsheets/03-window-functions.md`

**Files:**
- Create: `cheatsheets/03-window-functions.md`

- [ ] **Step 1: Create the file**

````markdown
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
````

- [ ] **Step 2: Commit**

```powershell
git add cheatsheets/03-window-functions.md
git commit -m "docs: add window functions cheatsheet"
```

---

## Task 5: `cheatsheets/04-indexes.md`

**Files:**
- Create: `cheatsheets/04-indexes.md`

- [ ] **Step 1: Create the file**

````markdown
# Indexes Cheatsheet

---

## Index Types

| Type | Description |
|---|---|
| **Clustered** | Physically orders the table's data pages by key. One per table (usually the PK). |
| **Nonclustered** | Separate B-tree structure; leaf nodes hold key + row locator (clustered key or RID). Up to 999 per table. |
| **Unique** | Enforces uniqueness. Can be clustered or nonclustered. |
| **Filtered** | Nonclustered with a `WHERE` clause — smaller, more efficient for sparse columns. |
| **Columnstore** | Column-oriented storage; optimal for analytics/aggregations over large tables. |
| **Full-text** | For `CONTAINS` / `FREETEXT` linguistic searches on character columns. |

---

## Syntax

```sql
-- Basic nonclustered index
CREATE INDEX IX_SalesOrderHeader_OrderDate
    ON Sales.SalesOrderHeader (OrderDate);

-- Composite index (order matters — most selective / most filtered first)
CREATE INDEX IX_SalesOrderHeader_CustomerDate
    ON Sales.SalesOrderHeader (CustomerID, OrderDate);

-- With included columns (avoid key lookups for common SELECT columns)
CREATE INDEX IX_SalesOrderHeader_CustomerDate_Inc
    ON Sales.SalesOrderHeader (CustomerID, OrderDate)
    INCLUDE (TotalDue, Status);

-- Filtered index (only active orders)
CREATE INDEX IX_SalesOrderHeader_Active
    ON Sales.SalesOrderHeader (OrderDate)
    INCLUDE (TotalDue)
    WHERE Status = 5;

-- Unique index
CREATE UNIQUE INDEX UX_Person_Email
    ON Person.EmailAddress (EmailAddress);

-- Drop
DROP INDEX IX_SalesOrderHeader_OrderDate ON Sales.SalesOrderHeader;
```

---

## What to Index

1. **Foreign keys** — always index FK columns unless the table is tiny.
2. **WHERE / JOIN predicates** — columns that appear in `WHERE` or `ON` clauses.
3. **ORDER BY / GROUP BY columns** — can eliminate a sort operator.
4. **INCLUDE columns** — add frequently SELECTed columns to avoid key lookups without bloating the key.
5. **Filtered indexes** — when queries always filter on a subset (e.g., `Status = 'Active'`, `DeletedAt IS NULL`).

---

## Useful DMVs

```sql
-- Index usage stats (resets on service restart)
SELECT
    OBJECT_NAME(i.object_id)          AS TableName,
    i.name                            AS IndexName,
    i.type_desc,
    s.user_seeks,
    s.user_scans,
    s.user_lookups,
    s.user_updates,
    s.last_user_seek
FROM sys.indexes                   AS i
LEFT JOIN sys.dm_db_index_usage_stats AS s
       ON s.object_id = i.object_id
      AND s.index_id  = i.index_id
      AND s.database_id = DB_ID()
WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
ORDER BY s.user_seeks DESC;

-- Missing index suggestions (hints only — validate before creating)
SELECT
    mid.statement,
    mig.avg_total_user_cost * mig.avg_user_impact * (mig.user_seeks + mig.user_scans) AS score,
    mid.equality_columns,
    mid.inequality_columns,
    mid.included_columns
FROM sys.dm_db_missing_index_details   AS mid
JOIN sys.dm_db_missing_index_groups    AS mig ON mig.index_handle    = mid.index_handle
JOIN sys.dm_db_missing_index_group_stats AS migs ON migs.group_handle = mig.index_group_handle
ORDER BY score DESC;

-- Fragmentation (run in context of target database)
SELECT
    OBJECT_NAME(ips.object_id)   AS TableName,
    i.name                       AS IndexName,
    ips.avg_fragmentation_in_percent,
    ips.page_count
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') AS ips
JOIN sys.indexes AS i
  ON i.object_id = ips.object_id AND i.index_id = ips.index_id
WHERE ips.avg_fragmentation_in_percent > 10
ORDER BY ips.avg_fragmentation_in_percent DESC;
```

**Fragmentation remediation:**
- < 30% fragmentation → `ALTER INDEX ... REORGANIZE` (online, low impact)
- ≥ 30% fragmentation → `ALTER INDEX ... REBUILD` (faster, briefly locks by default; use `WITH (ONLINE = ON)` on Enterprise to avoid blocking)

---

## Common Mistakes

- **Too many indexes** — every index slows `INSERT`/`UPDATE`/`DELETE`. Index for reads, but measure the write cost.
- **Leading column mismatch** — `IX_Table (A, B)` helps `WHERE A = ?` but not `WHERE B = ?` alone.
- **Key lookups** — if the query needs columns not in the index key, SQL Server does a key lookup per matching row. Add them as `INCLUDE` columns.
- **Over-indexing FKs** — index FKs on the *many* side (child table), not the *one* side (parent).
- **Ignoring fragmentation** — heavily fragmented indexes cause excessive I/O. Check monthly on active tables.
````

- [ ] **Step 2: Commit**

```powershell
git add cheatsheets/04-indexes.md
git commit -m "docs: add indexes cheatsheet"
```

---

## Task 6: `cheatsheets/05-execution-plans.md`

**Files:**
- Create: `cheatsheets/05-execution-plans.md`

- [ ] **Step 1: Create the file**

````markdown
# Execution Plans & Query Tuning Cheatsheet

---

## Getting Plans

```sql
-- Estimated plan (no execution): press Ctrl+L in SSMS, or:
SET SHOWPLAN_ALL ON;
GO
SELECT ...;
GO
SET SHOWPLAN_ALL OFF;

-- Actual plan (requires execution): press Ctrl+M in SSMS before running, or:
SET STATISTICS PROFILE ON;
GO
SELECT ...;
GO
SET STATISTICS PROFILE OFF;

-- I/O and time stats
SET STATISTICS IO   ON;
SET STATISTICS TIME ON;
GO
SELECT ...;
GO
SET STATISTICS IO   OFF;
SET STATISTICS TIME OFF;
```

In SSMS: Query → Include Actual Execution Plan (Ctrl+M) is the most common workflow.

---

## Reading Plans

Plans are read **right to left, top to bottom**. Data flows from leaf operators (scans/seeks) up through intermediate operators to the root (SELECT/INSERT/etc.).

**Key numbers on each operator:**
- **Cost %** — estimated share of total query cost.
- **Estimated vs Actual rows** — large discrepancy → stale statistics.
- **Estimated vs Actual executions** — nested-loops inner side shows how many times an operator ran.

---

## Common Operators

| Operator | What it does | When it's bad |
|---|---|---|
| **Table Scan** | Reads every row of a heap (no clustered index) | Almost always — add a clustered index |
| **Clustered Index Scan** | Reads entire clustered index | Query needs most rows (OK) or missing a nonclustered index (bad) |
| **Index Seek** | Navigates B-tree to matching rows | Generally good — what you want |
| **Key Lookup** | Goes back to clustered index to fetch non-index columns | Appears with nonclustered seeks; add INCLUDE columns to eliminate |
| **Nested Loops** | For each outer row, probe inner side | Good for small outer sets; bad with large row counts |
| **Hash Match** | Builds hash table of one input, probes with other | Good for large unsorted sets; watch for memory spills |
| **Merge Join** | Both inputs sorted on join key; merge in one pass | Efficient but requires sorted inputs |
| **Sort** | Explicit sort | Often a sign of missing index; check for memory spills |
| **Parallelism (Repartition Streams / Gather Streams)** | Distributes/collects parallel threads | Fine for large queries; unexpected parallelism on small queries suggests bad estimates |

---

## Warning Signs

- **Yellow warning triangle** on any operator → hover to see: missing statistics, implicit conversion, memory grant issue, etc.
- **Thick arrows** (many estimated rows) into a cheap operator → may be a fan-out from a bad join.
- **Thin arrows** (few rows estimated) but many actual rows → outdated statistics; run `UPDATE STATISTICS tablename`.
- **Key Lookup** present → add `INCLUDE` columns to the nonclustered index.
- **Spill to TempDB** (on Sort or Hash Match) → increase `max server memory`, rewrite to avoid the sort, or add an index that delivers sorted rows.

---

## SARGability Checklist

A predicate is **SARGable** (Search ARGument able) if SQL Server can use an index seek to satisfy it.

| Non-SARGable (full scan) | SARGable rewrite |
|---|---|
| `WHERE YEAR(OrderDate) = 2024` | `WHERE OrderDate >= '2024-01-01' AND OrderDate < '2025-01-01'` |
| `WHERE CONVERT(VARCHAR, col) = '42'` | `WHERE col = 42` (fix type mismatch at source) |
| `WHERE col LIKE '%suffix'` | Cannot rewrite — leading wildcard always scans |
| `WHERE col + 1 = 5` | `WHERE col = 4` |
| `WHERE ISNULL(col, 0) = 0` | `WHERE col = 0 OR col IS NULL` |
| `WHERE LEN(col) > 5` | Cannot rewrite simply — consider a computed column + index |

---

## Parameter Sniffing

SQL Server compiles a plan for the first set of parameter values it sees. If that plan is poor for other values:

```sql
-- Force recompile on each execution (diagnoses, but expensive)
EXEC dbo.MyProc @param = 1 WITH RECOMPILE;

-- Optimize for a specific value
CREATE OR ALTER PROCEDURE dbo.MyProc @param INT
AS
    SELECT * FROM dbo.Orders WHERE CustomerID = @param
    OPTION (OPTIMIZE FOR (@param = 1));

-- Optimize for unknown (uses average statistics)
    OPTION (OPTIMIZE FOR (@param UNKNOWN));

-- Local variable trick (breaks sniffing — use as last resort)
CREATE OR ALTER PROCEDURE dbo.MyProc @param INT
AS
    DECLARE @local INT = @param;
    SELECT * FROM dbo.Orders WHERE CustomerID = @local;
```

---

## `SET STATISTICS IO` Output

```
Table 'SalesOrderHeader'. Scan count 1, logical reads 689, physical reads 0, ...
```

- **logical reads** — pages read from buffer cache. Lower is better. This is the primary I/O metric to optimize.
- **physical reads** — pages read from disk (only on cold cache). Typically 0 in dev.
- **scan count** — number of times the index was scanned. > 1 often means nested-loops inner side.

---

## Common Mistakes

- Reading plans left to right — always right to left.
- Treating estimated cost % as absolute — it's relative to the query, not to other queries.
- Adding indexes based on missing-index hints alone — always check whether the suggested index already exists or overlaps an existing one.
- Ignoring implicit conversion warnings — they kill index seeks silently.
- Comparing estimated plans across different servers — statistics differ, so plans differ.
````

- [ ] **Step 2: Commit**

```powershell
git add cheatsheets/05-execution-plans.md
git commit -m "docs: add execution plans and query tuning cheatsheet"
```

---

## Self-Review

**Spec coverage check:**

| Cheatsheet | Spec requirement | Covered |
|---|---|---|
| `00-tsql-syntax.md` | Statement skeletons, built-in functions (string/date/math/conv), variables, control flow, `GO` | ✓ |
| `01-data-types.md` | Every common type with size, range, precision; conversion-matrix gotchas | ✓ |
| `02-joins-and-sets.md` | Text-based diagrams, join types, set ops, one canonical example each | ✓ |
| `03-window-functions.md` | Every window function, framing-clause cheat (`ROWS` vs `RANGE`, `UNBOUNDED PRECEDING`) | ✓ |
| `04-indexes.md` | Index types, syntax, "what to index" rules of thumb, useful DMVs | ✓ |
| `05-execution-plans.md` | How to read a plan, common operators, warning signs, SARGability checklist, `SET` commands | ✓ |

Format: all files follow short intro → tables/code blocks → common mistakes. No placeholders.
