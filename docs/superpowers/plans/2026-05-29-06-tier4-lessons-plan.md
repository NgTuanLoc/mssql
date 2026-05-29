# Tier 4 Lessons Implementation Plan (Lessons 12–13)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write the two Tier 4 "Performance" lessons — Indexes and Execution Plans & Query Tuning — each containing README.md, setup.sql, examples.sql, exercises.sql, and exercises-solutions.sql.

**Architecture:** Each lesson directory is self-contained. `setup.sql` is idempotent and uses a dedicated `lessonNN` schema. Lesson 12 creates copies of AdventureWorks tables with controlled indexing to make index behaviour observable. Lesson 13 builds on those tables and uses `SET STATISTICS IO/TIME` and actual execution plans to demonstrate tuning. SSMS is recommended for lesson 13 (richer plan viewer).

**Tech Stack:** T-SQL (MSSQL 2022 Developer), AdventureWorks2022, Markdown. SSMS for plan viewing in lesson 13.

**Prerequisite:** Infrastructure plan (Plan 01) complete — container running, AdventureWorks restored.

---

## File Map

| Action | Path |
|--------|------|
| Create | `lessons/12-indexes/` (5 files) |
| Create | `lessons/13-execution-plans-and-tuning/` (5 files) |

---

## Task 1: Lesson 12 — Indexes

**Files:** `lessons/12-indexes/` (5 files)

- [ ] **Step 1: Create `lessons/12-indexes/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson12') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson12.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson12');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson12;
END
GO
CREATE SCHEMA lesson12;
GO

-- Heap copy of SalesOrderHeader — no clustered index, for comparison
SELECT * INTO lesson12.SalesOrderHeap
FROM Sales.SalesOrderHeader;

-- Clustered-index copy (clustered on SalesOrderID, no nonclustered indexes)
SELECT * INTO lesson12.SalesOrderCI
FROM Sales.SalesOrderHeader;

ALTER TABLE lesson12.SalesOrderCI
    ADD CONSTRAINT PK_lesson12_SalesOrderCI PRIMARY KEY CLUSTERED (SalesOrderID);

-- Working copy for nonclustered index demos
SELECT * INTO lesson12.SalesOrderNC
FROM Sales.SalesOrderHeader;

ALTER TABLE lesson12.SalesOrderNC
    ADD CONSTRAINT PK_lesson12_SalesOrderNC PRIMARY KEY CLUSTERED (SalesOrderID);

-- Update statistics on all copies
UPDATE STATISTICS lesson12.SalesOrderHeap;
UPDATE STATISTICS lesson12.SalesOrderCI;
UPDATE STATISTICS lesson12.SalesOrderNC;

PRINT 'Lesson 12 setup complete.';
```

- [ ] **Step 2: Create `lessons/12-indexes/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- Enable I/O stats to observe index impact
SET STATISTICS IO ON;
GO

-- Example 1: Heap scan vs clustered index scan
-- Heap — no index at all
SELECT * FROM lesson12.SalesOrderHeap WHERE SalesOrderID = 43659;
-- Clustered index — seeks directly to the row
SELECT * FROM lesson12.SalesOrderCI   WHERE SalesOrderID = 43659;
-- Compare logical reads in the Messages tab

-- Example 2: Create a nonclustered index on CustomerID and observe query improvement
-- Before index — full scan
SELECT SalesOrderID, OrderDate, TotalDue
FROM lesson12.SalesOrderNC
WHERE CustomerID = 11000;

CREATE INDEX IX_lesson12_SalesOrderNC_CustomerID
    ON lesson12.SalesOrderNC (CustomerID);

-- After index — seek + key lookup
SELECT SalesOrderID, OrderDate, TotalDue
FROM lesson12.SalesOrderNC
WHERE CustomerID = 11000;

-- Example 3: Eliminate key lookup with INCLUDE columns
CREATE INDEX IX_lesson12_SalesOrderNC_CustomerID_Inc
    ON lesson12.SalesOrderNC (CustomerID)
    INCLUDE (OrderDate, TotalDue);

-- Now no key lookup needed
SELECT SalesOrderID, OrderDate, TotalDue
FROM lesson12.SalesOrderNC
WHERE CustomerID = 11000;

-- Example 4: Composite index — column order matters
CREATE INDEX IX_lesson12_SalesOrderNC_StatusDate
    ON lesson12.SalesOrderNC (Status, OrderDate);

-- Uses the index (leading column in predicate)
SELECT SalesOrderID, TotalDue
FROM lesson12.SalesOrderNC
WHERE Status = 5 AND OrderDate >= '2014-01-01';

-- Does NOT efficiently use the index (skips leading column)
SELECT SalesOrderID, TotalDue
FROM lesson12.SalesOrderNC
WHERE OrderDate >= '2014-01-01';  -- may still scan depending on selectivity

-- Example 5: Filtered index — only active/shipped orders
CREATE INDEX IX_lesson12_SalesOrderNC_Shipped
    ON lesson12.SalesOrderNC (OrderDate)
    INCLUDE (CustomerID, TotalDue)
    WHERE Status = 5;

SELECT CustomerID, OrderDate, TotalDue
FROM lesson12.SalesOrderNC
WHERE Status = 5 AND OrderDate >= '2014-01-01';

-- Example 6: Index fragmentation
SELECT
    OBJECT_NAME(ips.object_id)           AS TableName,
    i.name                               AS IndexName,
    ips.avg_fragmentation_in_percent,
    ips.page_count
FROM sys.dm_db_index_physical_stats(
         DB_ID(), OBJECT_ID('lesson12.SalesOrderNC'), NULL, NULL, 'LIMITED') AS ips
JOIN sys.indexes AS i
  ON i.object_id = ips.object_id AND i.index_id = ips.index_id;

-- Example 7: Index usage stats
SELECT
    i.name                AS IndexName,
    s.user_seeks,
    s.user_scans,
    s.user_lookups,
    s.user_updates,
    s.last_user_seek
FROM sys.indexes                      AS i
LEFT JOIN sys.dm_db_index_usage_stats AS s
       ON s.object_id   = i.object_id
      AND s.index_id    = i.index_id
      AND s.database_id = DB_ID()
WHERE i.object_id = OBJECT_ID('lesson12.SalesOrderNC')
  AND i.name IS NOT NULL;
GO

SET STATISTICS IO OFF;
```

- [ ] **Step 3: Create `lessons/12-indexes/exercises.sql`**

```sql
USE AdventureWorks2022;
GO
SET STATISTICS IO ON;
GO

-- Exercise 1: Run the query below and note the logical reads.
--             Then create the most effective nonclustered index for it.
--             Run again and compare logical reads.
--             Expected: significantly fewer logical reads after indexing.
SELECT SalesOrderID, TotalDue
FROM lesson12.SalesOrderNC
WHERE TerritoryID = 1
  AND YEAR(OrderDate) = 2013;
-- Your index CREATE statement here:


-- Exercise 2: The query below suffers from a key lookup.
--             Add the minimum INCLUDE columns to eliminate it.
--             Verify with SET STATISTICS IO ON that logical reads drop.
SELECT OrderDate, SubTotal, Freight
FROM lesson12.SalesOrderNC
WHERE CustomerID = 29825;
-- Your index CREATE (or ALTER) statement here:


-- Exercise 3: Create a filtered index on lesson12.SalesOrderNC
--             that covers only rows where OnlineOrderFlag = 1 (online orders).
--             Index on OrderDate, INCLUDE CustomerID and TotalDue.
--             Write a query that uses this filtered index.
-- Your CREATE INDEX and SELECT here:


-- Exercise 4: List all indexes on lesson12.SalesOrderNC
--             showing name, type, key columns, and included columns.
-- Hint: join sys.indexes, sys.index_columns, sys.columns
-- Your query here:


-- Exercise 5: Check fragmentation of all indexes in the lesson12 schema.
--             For any index with fragmentation > 30%, write the REBUILD statement.
-- Your query here:

SET STATISTICS IO OFF;
```

- [ ] **Step 4: Create `lessons/12-indexes/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO
SET STATISTICS IO ON;
GO

-- Exercise 1: Index for TerritoryID + year filter.
-- Note: YEAR(OrderDate) is non-SARGable. The index is on OrderDate; the optimizer
-- may still scan. For true SARGability, use a date range in the query (shown in lesson 13).
-- Best index given the query as written: TerritoryID leading (most selective in this context),
-- OrderDate for range, TotalDue included to avoid lookup.
CREATE INDEX IX_lesson12_SalesOrderNC_TerritoryDate
    ON lesson12.SalesOrderNC (TerritoryID, OrderDate)
    INCLUDE (TotalDue);

SELECT SalesOrderID, TotalDue
FROM lesson12.SalesOrderNC
WHERE TerritoryID = 1
  AND YEAR(OrderDate) = 2013;
-- Compare logical reads before vs after index creation.

-- Exercise 2: Eliminate key lookup for CustomerID query.
-- Approach: add OrderDate, SubTotal, Freight as INCLUDE columns on the CustomerID index.
-- Drop the existing CustomerID-only index first if it exists, then recreate.
DROP INDEX IF EXISTS IX_lesson12_SalesOrderNC_CustomerID ON lesson12.SalesOrderNC;

CREATE INDEX IX_lesson12_SalesOrderNC_CustomerID_Full
    ON lesson12.SalesOrderNC (CustomerID)
    INCLUDE (OrderDate, SubTotal, Freight);

SELECT OrderDate, SubTotal, Freight
FROM lesson12.SalesOrderNC
WHERE CustomerID = 29825;

-- Exercise 3: Filtered index for online orders.
CREATE INDEX IX_lesson12_SalesOrderNC_Online
    ON lesson12.SalesOrderNC (OrderDate)
    INCLUDE (CustomerID, TotalDue)
    WHERE OnlineOrderFlag = 1;

-- Query that uses the filtered index (must include the filter predicate)
SELECT CustomerID, OrderDate, TotalDue
FROM lesson12.SalesOrderNC
WHERE OnlineOrderFlag = 1
  AND OrderDate >= '2014-01-01';

-- Exercise 4: List all indexes and their columns.
SELECT
    i.name                                       AS IndexName,
    i.type_desc                                  AS IndexType,
    STRING_AGG(
        CASE WHEN ic.is_included_column = 0
             THEN c.name END, ', ')
        WITHIN GROUP (ORDER BY ic.key_ordinal)   AS KeyColumns,
    STRING_AGG(
        CASE WHEN ic.is_included_column = 1
             THEN c.name END, ', ')              AS IncludedColumns
FROM sys.indexes           AS i
JOIN sys.index_columns     AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns           AS c  ON c.object_id  = i.object_id AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID('lesson12.SalesOrderNC')
  AND i.name IS NOT NULL
GROUP BY i.name, i.type_desc
ORDER BY i.type_desc, i.name;

-- Exercise 5: Check fragmentation and generate REBUILD statements.
SELECT
    OBJECT_NAME(ips.object_id)           AS TableName,
    i.name                               AS IndexName,
    ips.avg_fragmentation_in_percent,
    'ALTER INDEX ' + QUOTENAME(i.name)
    + ' ON lesson12.' + QUOTENAME(OBJECT_NAME(ips.object_id))
    + ' REBUILD;'                        AS RebuildStatement
FROM sys.dm_db_index_physical_stats(
         DB_ID(), OBJECT_ID('lesson12.SalesOrderNC'), NULL, NULL, 'LIMITED') AS ips
JOIN sys.indexes AS i
  ON i.object_id = ips.object_id AND i.index_id = ips.index_id
WHERE ips.avg_fragmentation_in_percent > 30
  AND i.name IS NOT NULL;
-- Copy and run the generated REBUILD statements as needed.
GO

SET STATISTICS IO OFF;
```

- [ ] **Step 5: Create `lessons/12-indexes/README.md`**

```markdown
# Lesson 12 — Indexes

## What you'll learn
- Clustered vs nonclustered indexes — what they are and when to use each
- Creating indexes: basic, composite, with `INCLUDE`, filtered
- How key lookups happen and how to eliminate them
- Index fragmentation and how to fix it
- `sys.dm_db_index_usage_stats` and `sys.dm_db_index_physical_stats`

## Setup
Run `setup.sql` once. It creates the `lesson12` schema with three copies of `SalesOrderHeader`:
- `SalesOrderHeap` — no index (heap)
- `SalesOrderCI` — clustered index only
- `SalesOrderNC` — clustered index + nonclustered indexes added during examples

Re-run `setup.sql` to reset indexes between attempts.

## Concepts

### Clustered index

The table's data pages are physically sorted by the clustered key. One per table. Usually the primary key. An index seek on the clustered key goes directly to the row — no extra lookup.

### Nonclustered index

Separate B-tree with the key column(s) in sorted order; leaf nodes hold the clustered key (or heap RID) to fetch other columns. Up to 999 per table.

**Key lookup:** when the query needs columns not in the nonclustered index, SQL Server looks them up in the clustered index one row at a time. Expensive at scale — eliminate with `INCLUDE` columns.

### Index syntax

```sql
CREATE INDEX IX_Table_Col
    ON schema.Table (Col1 ASC, Col2 DESC)
    INCLUDE (Col3, Col4)
    WHERE ActiveFlag = 1;   -- filtered index
```

### Choosing what to index

1. FK columns on child tables.
2. Columns in `WHERE` and `JOIN` predicates.
3. Leading columns of composite indexes should be the most selective and/or most filtered.
4. Add `INCLUDE` columns for non-selective columns that appear only in `SELECT`.

### Fragmentation

```
< 10%  → ignore
10–30% → REORGANIZE (online, low-impact)
> 30%  → REBUILD (faster; ONLINE = ON avoids blocking on Enterprise)
```

## Worked Examples (lesson12 schema)
1. Heap scan vs clustered index seek — logical read comparison.
2. Nonclustered index on `CustomerID` — before and after logical reads.
3. Eliminate key lookup with `INCLUDE (OrderDate, TotalDue)`.
4. Composite index column order — leading column matters.
5. Filtered index — only shipped orders.
6. `sys.dm_db_index_physical_stats` — fragmentation report.
7. `sys.dm_db_index_usage_stats` — seek/scan/lookup counts since last restart.

## Pitfalls
- **Too many indexes** — every write must update every index. More than ~5–7 indexes on a hot OLTP table is usually a sign of over-indexing.
- **Leading column skipped** — `IX_Table(A, B)` helps `WHERE A = ?` but not `WHERE B = ?` alone.
- **Non-SARGable predicate** — `WHERE YEAR(col) = 2024` prevents a seek even with an index on `col`. Rewrite as `col >= '2024-01-01' AND col < '2025-01-01'`.
- **Forgetting `INCLUDE`** — a seek + key lookup per row can be worse than a scan for large result sets.
- **`sys.dm_db_index_usage_stats` resets on restart** — don't use it immediately after the container restarts.

## Cheatsheet link
See `cheatsheets/04-indexes.md`

## Exercises
Open `exercises.sql` and try them in order. Re-run `setup.sql` to reset indexes. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/12-indexes/
git commit -m "feat: add lesson 12 - indexes"
```

---

## Task 2: Lesson 13 — Execution Plans & Query Tuning

**Files:** `lessons/13-execution-plans-and-tuning/` (5 files)

- [ ] **Step 1: Create `lessons/13-execution-plans-and-tuning/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson13') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    -- Drop procs
    SELECT @sql += 'DROP PROCEDURE lesson13.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.procedures WHERE schema_id = SCHEMA_ID('lesson13');
    EXEC sp_executesql @sql;

    SET @sql = N'';
    SELECT @sql += 'DROP TABLE lesson13.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson13');
    EXEC sp_executesql @sql;

    DROP SCHEMA lesson13;
END
GO
CREATE SCHEMA lesson13;
GO

-- Indexed copy for tuning exercises
SELECT * INTO lesson13.SalesOrderHeader
FROM Sales.SalesOrderHeader;

ALTER TABLE lesson13.SalesOrderHeader
    ADD CONSTRAINT PK_lesson13_SOH PRIMARY KEY CLUSTERED (SalesOrderID);

CREATE INDEX IX_lesson13_SOH_CustomerID
    ON lesson13.SalesOrderHeader (CustomerID)
    INCLUDE (OrderDate, TotalDue, Status);

CREATE INDEX IX_lesson13_SOH_TerritoryDate
    ON lesson13.SalesOrderHeader (TerritoryID, OrderDate)
    INCLUDE (TotalDue);

-- Update statistics for accurate plan estimates
UPDATE STATISTICS lesson13.SalesOrderHeader WITH FULLSCAN;

PRINT 'Lesson 13 setup complete.';
```

- [ ] **Step 2: Create `lessons/13-execution-plans-and-tuning/examples.sql`**

```sql
USE AdventureWorks2022;
GO

SET STATISTICS IO   ON;
SET STATISTICS TIME ON;
GO

-- =========================================================================
-- READING PLANS — Run each query with Ctrl+M (Include Actual Plan) in SSMS
-- =========================================================================

-- Example 1: Index Seek — SalesOrderID is the clustered key
SELECT SalesOrderID, OrderDate, TotalDue
FROM lesson13.SalesOrderHeader
WHERE SalesOrderID = 43659;
-- Plan: Clustered Index Seek — very few logical reads

-- Example 2: Index Scan — no useful predicate, reads all rows
SELECT COUNT(*) FROM lesson13.SalesOrderHeader;
-- Plan: Clustered Index Scan — reads every page

-- Example 3: Nonclustered seek + key lookup eliminated by INCLUDE
SELECT OrderDate, TotalDue
FROM lesson13.SalesOrderHeader
WHERE CustomerID = 11000;
-- Plan: nonclustered seek (IX_lesson13_SOH_CustomerID) — no key lookup (INCLUDE covers it)

-- Example 4: SARGable vs non-SARGable predicate
-- Non-SARGable: function on indexed column → full scan
SELECT SalesOrderID, TotalDue
FROM lesson13.SalesOrderHeader
WHERE YEAR(OrderDate) = 2014;

-- SARGable rewrite: range on the indexed column → seek
SELECT SalesOrderID, TotalDue
FROM lesson13.SalesOrderHeader
WHERE OrderDate >= '2014-01-01' AND OrderDate < '2015-01-01';
-- Compare logical reads and plan operator between the two

-- Example 5: Hash Match Join vs Nested Loops — join two large result sets
SELECT soh.SalesOrderID, soh.TotalDue, sod.ProductID, sod.OrderQty
FROM lesson13.SalesOrderHeader AS soh
JOIN Sales.SalesOrderDetail    AS sod ON sod.SalesOrderID = soh.SalesOrderID
WHERE soh.TerritoryID = 1;
-- Likely: Hash Match or Merge Join for the large row set

-- Example 6: Parameter sniffing demo — see README for two-step walkthrough
-- Step 1: prime the plan with a common, selective value
CREATE OR ALTER PROCEDURE lesson13.usp_OrdersByTerritory @TerritoryID INT
AS
    SELECT SalesOrderID, OrderDate, TotalDue
    FROM lesson13.SalesOrderHeader
    WHERE TerritoryID = @TerritoryID;
GO

EXEC lesson13.usp_OrdersByTerritory @TerritoryID = 9;   -- small territory
EXEC lesson13.usp_OrdersByTerritory @TerritoryID = 1;   -- large territory (reuses sniffed plan)
-- May see a poor plan (e.g. nested loops) for territory 1 that was optimised for territory 9

-- Flush plan cache for the procedure (demo only — never in production)
-- DBCC FREEPROCCACHE;
-- EXEC lesson13.usp_OrdersByTerritory @TerritoryID = 1;   -- now gets its own plan

-- Example 7: OPTIMIZE FOR UNKNOWN — break sniffing without RECOMPILE
CREATE OR ALTER PROCEDURE lesson13.usp_OrdersByTerritory_Fixed @TerritoryID INT
AS
    SELECT SalesOrderID, OrderDate, TotalDue
    FROM lesson13.SalesOrderHeader
    WHERE TerritoryID = @TerritoryID
    OPTION (OPTIMIZE FOR (@TerritoryID UNKNOWN));
GO

EXEC lesson13.usp_OrdersByTerritory_Fixed @TerritoryID = 1;

GO
SET STATISTICS IO   OFF;
SET STATISTICS TIME OFF;
```

- [ ] **Step 3: Create `lessons/13-execution-plans-and-tuning/exercises.sql`**

```sql
USE AdventureWorks2022;
GO
-- Use SSMS with Ctrl+M (Include Actual Execution Plan) for all exercises.
SET STATISTICS IO   ON;
SET STATISTICS TIME ON;
GO

-- Exercise 1: Run the query below and look at its execution plan.
--             Identify: (a) the plan operator on Sales.SalesOrderDetail,
--             (b) estimated vs actual rows, (c) logical reads.
--             Then rewrite the WHERE clause to be SARGable and compare.
SELECT soh.SalesOrderID, SUM(sod.LineTotal) AS OrderTotal
FROM lesson13.SalesOrderHeader AS soh
JOIN Sales.SalesOrderDetail    AS sod ON sod.SalesOrderID = soh.SalesOrderID
WHERE CONVERT(VARCHAR(7), soh.OrderDate, 120) = '2013-07'   -- non-SARGable
GROUP BY soh.SalesOrderID;
-- Your SARGable rewrite here:


-- Exercise 2: The query below triggers a key lookup.
--             Add an index to lesson13.SalesOrderHeader to eliminate it.
--             Verify with the execution plan that the key lookup is gone.
SELECT SalesOrderID, SalesPersonID, SubTotal
FROM lesson13.SalesOrderHeader
WHERE TerritoryID = 4;
-- Your CREATE INDEX here:


-- Exercise 3: Run both queries and compare their plans and logical reads.
--             Explain in a comment why the plans differ.
-- Query A:
SELECT SalesOrderID, TotalDue FROM lesson13.SalesOrderHeader WHERE Status = 5;
-- Query B:
SELECT SalesOrderID, TotalDue FROM lesson13.SalesOrderHeader WHERE Status = 1;
-- Your explanation comment here:


-- Exercise 4: Create a stored procedure lesson13.usp_RevenueByTerritory that accepts
--             @TerritoryID INT and returns SUM(TotalDue) grouped by YEAR(OrderDate).
--             Call it with territory 1 (large) and territory 10 (small) and
--             check whether the plan is appropriate for both.
--             Add OPTION (OPTIMIZE FOR (@TerritoryID UNKNOWN)) if needed.
-- Your CREATE PROCEDURE and test EXECs here:


-- Exercise 5: Use SET STATISTICS IO ON to compare logical reads for these two queries.
--             Which one is more efficient and why?
-- Query A — explicit JOIN
SELECT p.Name, SUM(sod.OrderQty) AS TotalSold
FROM Sales.SalesOrderDetail     AS sod
JOIN Production.Product         AS p  ON p.ProductID = sod.ProductID
GROUP BY p.Name
ORDER BY TotalSold DESC;

-- Query B — correlated subquery
SELECT p.Name,
       (SELECT SUM(OrderQty) FROM Sales.SalesOrderDetail sod WHERE sod.ProductID = p.ProductID) AS TotalSold
FROM Production.Product AS p
ORDER BY TotalSold DESC;
-- Your analysis comment here:

SET STATISTICS IO   OFF;
SET STATISTICS TIME OFF;
```

- [ ] **Step 4: Create `lessons/13-execution-plans-and-tuning/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO
SET STATISTICS IO   ON;
SET STATISTICS TIME ON;
GO

-- Exercise 1: SARGable rewrite for the date filter.
-- Non-SARGable: CONVERT() on the column forces a scan.
-- SARGable rewrite: use a date range so the index on OrderDate can be used.
SELECT soh.SalesOrderID, SUM(sod.LineTotal) AS OrderTotal
FROM lesson13.SalesOrderHeader AS soh
JOIN Sales.SalesOrderDetail    AS sod ON sod.SalesOrderID = soh.SalesOrderID
WHERE soh.OrderDate >= '2013-07-01' AND soh.OrderDate < '2013-08-01'  -- SARGable
GROUP BY soh.SalesOrderID;
-- Expected: fewer logical reads and an Index Seek instead of a Scan on SalesOrderHeader.

-- Exercise 2: Add index to eliminate key lookup for TerritoryID + SalesPersonID + SubTotal.
CREATE INDEX IX_lesson13_SOH_TerritoryID_Inc
    ON lesson13.SalesOrderHeader (TerritoryID)
    INCLUDE (SalesPersonID, SubTotal);

SELECT SalesOrderID, SalesPersonID, SubTotal
FROM lesson13.SalesOrderHeader
WHERE TerritoryID = 4;
-- Expected: the Key Lookup operator disappears from the plan.

-- Exercise 3: Plan difference for Status = 5 vs Status = 1.
-- Status = 5 (shipped) covers most rows — optimizer may choose a Clustered Index Scan
-- because seeking + looking up each row would be more expensive than a full scan.
-- Status = 1 (in process) covers very few rows — optimizer chooses an Index Seek.
-- This illustrates how selectivity affects the optimizer's access-path choice.
SELECT SalesOrderID, TotalDue FROM lesson13.SalesOrderHeader WHERE Status = 5;
SELECT SalesOrderID, TotalDue FROM lesson13.SalesOrderHeader WHERE Status = 1;

-- Exercise 4: Revenue by territory procedure with OPTIMIZE FOR UNKNOWN.
CREATE OR ALTER PROCEDURE lesson13.usp_RevenueByTerritory
    @TerritoryID INT
AS
    SELECT
        YEAR(OrderDate)  AS OrderYear,
        SUM(TotalDue)    AS Revenue
    FROM lesson13.SalesOrderHeader
    WHERE TerritoryID = @TerritoryID
    GROUP BY YEAR(OrderDate)
    ORDER BY OrderYear
    OPTION (OPTIMIZE FOR (@TerritoryID UNKNOWN));
GO

EXEC lesson13.usp_RevenueByTerritory @TerritoryID = 1;
EXEC lesson13.usp_RevenueByTerritory @TerritoryID = 10;

-- Exercise 5: JOIN vs correlated subquery.
-- Query A (JOIN): one pass over SalesOrderDetail with a Hash Match aggregate.
SELECT p.Name, SUM(sod.OrderQty) AS TotalSold
FROM Sales.SalesOrderDetail AS sod
JOIN Production.Product     AS p  ON p.ProductID = sod.ProductID
GROUP BY p.Name
ORDER BY TotalSold DESC;

-- Query B (correlated subquery): executes once PER product — N executions for N products.
-- For ~500 products, that is ~500 individual scans of SalesOrderDetail.
-- Query A will have far fewer logical reads.
SELECT p.Name,
       (SELECT SUM(OrderQty) FROM Sales.SalesOrderDetail sod WHERE sod.ProductID = p.ProductID) AS TotalSold
FROM Production.Product AS p
ORDER BY TotalSold DESC;
-- Lesson: correlated subqueries that aggregate a large table execute once per outer row.
-- A JOIN with GROUP BY is almost always more efficient.

GO
SET STATISTICS IO   OFF;
SET STATISTICS TIME OFF;
```

- [ ] **Step 5: Create `lessons/13-execution-plans-and-tuning/README.md`**

```markdown
# Lesson 13 — Execution Plans & Query Tuning

## What you'll learn
- Getting estimated vs actual execution plans in SSMS (Ctrl+L / Ctrl+M)
- Reading plan operators: Seek, Scan, Key Lookup, Nested Loops, Hash Match, Merge Join, Sort
- `SET STATISTICS IO` and `SET STATISTICS TIME` output
- SARGability — what it means and how to fix non-SARGable predicates
- Parameter sniffing — what it is and three ways to address it
- Common rewrites: correlated subquery → JOIN, function-on-column → range predicate

## Setup
Run `setup.sql` once. It creates the `lesson13` schema with an indexed copy of `SalesOrderHeader` and pre-built indexes for the examples.

**SSMS is strongly recommended for this lesson** — its graphical plan viewer shows operator costs, tooltips, and memory grant warnings that Azure Data Studio does not expose as richly.

## Concepts

### Getting plans

| Method | What you get |
|---|---|
| Ctrl+L (SSMS) | Estimated plan — no execution |
| Ctrl+M then run | Actual plan — with real row counts |
| `SET STATISTICS IO ON` | Logical reads per table (Messages tab) |
| `SET STATISTICS TIME ON` | CPU and elapsed time |

### Reading plans — key rules

1. **Read right to left, top to bottom** — data flows from leaves (scans/seeks) to root (SELECT).
2. **Arrow width** = estimated row count. Thick arrow into a cheap operator = row count mismatch.
3. **Cost %** is relative to the query, not to other queries.
4. **Yellow triangle** = optimizer warning — hover to see it.

### Operator quick reference

| Operator | Good | Investigate when |
|---|---|---|
| Index Seek | Yes — finds rows via B-tree | Rarely bad |
| Key Lookup | Sometimes | Appears per row — add INCLUDE columns |
| Clustered Index Scan | OK for full reads | Bad when a seek was expected |
| Nested Loops | Small outer set | Large outer set × large inner = slow |
| Hash Match | Large unsorted inputs | Memory spill → TempDB |
| Sort | Unavoidable sometimes | Missing index that delivers sorted rows |

### SARGability

A predicate is SARGable if the optimizer can use an index seek to satisfy it.

```sql
-- Non-SARGable (scan)                        -- SARGable rewrite (seek)
WHERE YEAR(col) = 2024                  →  WHERE col >= '2024-01-01' AND col < '2025-01-01'
WHERE CONVERT(VARCHAR,col) = '42'       →  Fix the type mismatch at the source
WHERE col LIKE '%suffix'                →  No rewrite — leading wildcard always scans
WHERE col + 1 = 5                       →  WHERE col = 4
WHERE ISNULL(col,0) = 0                 →  WHERE col = 0 OR col IS NULL
```

### `SET STATISTICS IO` output

```
Table 'SalesOrderHeader'. Scan count 1, logical reads 4, ...
```
- **logical reads** — pages read from buffer cache. Lower = better. Primary I/O metric.
- **scan count** — > 1 on the inner side of a nested-loops join.

### Parameter sniffing

SQL Server compiles the plan for the first parameter value it sees. If that value is unrepresentative:

```sql
-- Option 1: OPTIMIZE FOR UNKNOWN — use average statistics
OPTION (OPTIMIZE FOR (@param UNKNOWN))

-- Option 2: OPTIMIZE FOR specific value — if one value is typical
OPTION (OPTIMIZE FOR (@param = 1))

-- Option 3: RECOMPILE — new plan every execution (most expensive)
OPTION (RECOMPILE)
-- or: WITH RECOMPILE on the procedure
```

## Worked Examples (lesson13 schema)
1. Clustered Index Seek on `SalesOrderID` — minimal logical reads.
2. Clustered Index Scan on `COUNT(*)` — all pages read.
3. Nonclustered seek with `INCLUDE` — no key lookup.
4. SARGable vs non-SARGable date filter — plan and I/O comparison.
5. Hash Match join on a large result set.
6. Parameter sniffing demo with `usp_OrdersByTerritory`.
7. `OPTIMIZE FOR UNKNOWN` fix in `usp_OrdersByTerritory_Fixed`.

## Pitfalls
- Trusting estimated plan cost % across different queries — it's not comparable.
- Adding indexes based on missing-index hints without checking overlap with existing indexes.
- `NOLOCK` (`READ UNCOMMITTED`) hint — reads dirty data and can even skip or duplicate rows due to page splits. Almost never the right fix.
- Comparing logical reads on a cold cache — use a warm cache (run the query twice, measure the second).
- Fixing parameter sniffing with `RECOMPILE` on every execution — expensive; use `OPTIMIZE FOR UNKNOWN` first.

## Cheatsheet link
See `cheatsheets/05-execution-plans.md`

## Exercises
Open `exercises.sql` with SSMS and enable the actual execution plan (Ctrl+M) before running. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/13-execution-plans-and-tuning/
git commit -m "feat: add lesson 13 - execution plans and query tuning"
```

---

## Self-Review

**Spec coverage check:**

| Lesson | Spec topics | Covered |
|---|---|---|
| 12 | Clustered vs nonclustered, included columns, filtered indexes, fragmentation, `sys.dm_db_index_usage_stats` | ✓ |
| 13 | Estimated vs actual plans, reading operators, statistics, parameter sniffing, SARGability, `SET STATISTICS IO/TIME`, common rewrites | ✓ |

All five lesson files per lesson. `setup.sql` idempotent. Exercises are self-contained and can be reset by re-running `setup.sql`. All exercises have solutions with approach/analysis comments. SSMS recommendation noted for lesson 13 as specified in the spec. No placeholders.
