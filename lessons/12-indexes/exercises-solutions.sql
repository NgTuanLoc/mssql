USE AdventureWorks2022;
GO
-- Required for the filtered index in Exercise 3 (and every plain CREATE INDEX
-- against SalesOrderNC once that filtered index exists) -- sqlcmd's default
-- session has QUOTED_IDENTIFIER OFF, which SQL Server rejects for filtered
-- indexes both at CREATE time and at query time. Set once, persists for the
-- rest of this session. This also makes CREATE INDEX re-runnable below.
SET QUOTED_IDENTIFIER ON;
GO
SET STATISTICS IO ON;
GO

-- Exercise 1: Index for TerritoryID + year filter.
-- Note: YEAR(OrderDate) is non-SARGable. The index is on OrderDate; the optimizer
-- may still scan. For true SARGability, use a date range in the query (shown in lesson 13).
-- Best index given the query as written: TerritoryID leading (most selective in this context),
-- OrderDate for range, TotalDue included to avoid lookup.
DROP INDEX IF EXISTS IX_lesson12_SalesOrderNC_TerritoryDate ON lesson12.SalesOrderNC;
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
DROP INDEX IF EXISTS IX_lesson12_SalesOrderNC_CustomerID_Full ON lesson12.SalesOrderNC;

CREATE INDEX IX_lesson12_SalesOrderNC_CustomerID_Full
    ON lesson12.SalesOrderNC (CustomerID)
    INCLUDE (OrderDate, SubTotal, Freight);

SELECT OrderDate, SubTotal, Freight
FROM lesson12.SalesOrderNC
WHERE CustomerID = 29825;

-- Exercise 3: Filtered index for online orders.
DROP INDEX IF EXISTS IX_lesson12_SalesOrderNC_Online ON lesson12.SalesOrderNC;
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

------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Workload-driven index design
-- Reasoning: One equality-then-range covering index per distinct leading column: (a)
--   CustomerID, (b) SalesPersonID. (c) Status=5 is 99% of rows, OnlineOrderFlag=1 is 88%
--   -- neither is selective, so no dedicated index; verified below a narrow 2-column
--   index only cuts 805->43 reads, not worth another write-path index for a predicate
--   that already returns 87% of the table. Tempting-but-wrong: index Status first for
--   (c) -- it's almost constant, so leading with it buys nothing.
DROP INDEX IF EXISTS IX_lesson12_OrderCopy_CustomerOrderDate ON lesson12.OrderCopy;
CREATE INDEX IX_lesson12_OrderCopy_CustomerOrderDate
    ON lesson12.OrderCopy (CustomerID, OrderDate DESC)   -- (a): equality column leads
    INCLUDE (TotalDue);

DROP INDEX IF EXISTS IX_lesson12_OrderCopy_SalesPersonOrderDate ON lesson12.OrderCopy;
CREATE INDEX IX_lesson12_OrderCopy_SalesPersonOrderDate
    ON lesson12.OrderCopy (SalesPersonID, OrderDate)     -- (b): equality before range
    INCLUDE (TotalDue);

SET STATISTICS IO ON;
-- (a) CustomerID = ? ORDER BY OrderDate DESC
--   Before (no index): 805 logical reads, Clustered Index Scan + Sort
--   After  (index above): 2 logical reads, Index Seek, ORDERED (DESC key matches ORDER BY)
SELECT SalesOrderID, OrderDate, TotalDue FROM lesson12.OrderCopy
WHERE CustomerID = 11176 ORDER BY OrderDate DESC;

-- (b) SalesPersonID equality + OrderDate range, returning TotalDue
--   Before (no index): 805 logical reads, Clustered Index Scan
--   After  (index above): 2 logical reads, Index Seek
SELECT SalesOrderID, TotalDue FROM lesson12.OrderCopy
WHERE SalesPersonID = 277 AND OrderDate >= '2013-01-01' AND OrderDate < '2013-07-01';

-- (c) Status = 5 AND OnlineOrderFlag = 1 -- no dedicated index; see reasoning above
--   Without any index: 805 logical reads (Clustered Index Scan) for 27,379 matching rows
SELECT SalesOrderID FROM lesson12.OrderCopy WHERE Status = 5 AND OnlineOrderFlag = 1;
SET STATISTICS IO OFF;

-- Challenge 2: Key-lookup elimination
-- Reasoning: CustomerID = 11019 already seeks via Challenge 1's index, but ShipDate and
--   Freight aren't in it, so SQL Server does a seek plus one Key Lookup per matching row
--   (17 rows here). Extending the SAME index with INCLUDE removes the lookup entirely,
--   instead of adding a second, overlapping index that duplicates the seek path.
--   Tempting-but-wrong: create a brand-new CustomerID index for this -- doubles the
--   write cost of every INSERT/UPDATE/DELETE for no extra read benefit.
SET STATISTICS IO ON;
-- Before (index from Challenge 1, no ShipDate/Freight): 53 logical reads --
--   Index Seek on IX_lesson12_OrderCopy_CustomerOrderDate + 17 Key Lookups
SELECT SalesOrderID, ShipDate, Freight FROM lesson12.OrderCopy WHERE CustomerID = 11019;
SET STATISTICS IO OFF;

DROP INDEX IF EXISTS IX_lesson12_OrderCopy_CustomerOrderDate ON lesson12.OrderCopy;
CREATE INDEX IX_lesson12_OrderCopy_CustomerOrderDate
    ON lesson12.OrderCopy (CustomerID, OrderDate DESC)
    INCLUDE (TotalDue, ShipDate, Freight);

SET STATISTICS IO ON;
-- After (ShipDate/Freight now covered): 2 logical reads -- pure Index Seek, no lookup
SELECT SalesOrderID, ShipDate, Freight FROM lesson12.OrderCopy WHERE CustomerID = 11019;
SET STATISTICS IO OFF;

-- Challenge 3: Index health report
-- Reasoning: LEFT JOIN sys.dm_db_index_usage_stats onto sys.indexes so untouched
--   indexes stay visible (ISNULL) instead of disappearing from an inner join. Reads =
--   seeks+scans+lookups; Flag fires only when writes > 0 AND reads = 0 -- a never-
--   touched-at-all index (writes = 0 too) is unused but not yet "costly," so it's left
--   unflagged. Usage stats reset on every SQL Server restart, so run this after real
--   traffic has accumulated, not right after a restart, or everything looks unused.
-- Demo: an index nobody queries, written to once, so the flag has a real positive case.
DROP INDEX IF EXISTS IX_lesson12_SalesOrderCI_Freight ON lesson12.SalesOrderCI;
CREATE INDEX IX_lesson12_SalesOrderCI_Freight ON lesson12.SalesOrderCI (Freight);
UPDATE lesson12.SalesOrderCI SET Freight = Freight WHERE SalesOrderID = 43659;

SELECT
    OBJECT_SCHEMA_NAME(i.object_id)                       AS SchemaName,
    OBJECT_NAME(i.object_id)                              AS TableName,
    i.name                                                 AS IndexName,
    ISNULL(s.user_seeks, 0) + ISNULL(s.user_scans, 0)
        + ISNULL(s.user_lookups, 0)                        AS Reads,
    ISNULL(s.user_updates, 0)                              AS Writes,
    CASE WHEN ISNULL(s.user_updates, 0) > 0
          AND ISNULL(s.user_seeks, 0) + ISNULL(s.user_scans, 0)
              + ISNULL(s.user_lookups, 0) = 0
         THEN 'UNUSED - candidate to drop' ELSE '' END    AS Flag
FROM sys.indexes AS i
JOIN sys.tables  AS t ON t.object_id = i.object_id
LEFT JOIN sys.dm_db_index_usage_stats AS s
       ON s.object_id = i.object_id AND s.index_id = i.index_id AND s.database_id = DB_ID()
WHERE i.name IS NOT NULL
ORDER BY Flag DESC, Writes DESC;

-- Challenge 4: Filtered index for the soft-delete pattern
-- Reasoning: A literal predicate (IsDeleted = 0) matches the filter definition at
--   compile time, so the optimizer can prove the filtered index is safe -- Index Seek,
--   5 reads. A parameterised predicate (@IsDeleted) could hold any value at runtime as
--   far as the compiler's concerned, so it can't prove the filter always holds and
--   falls back to the base table -- Clustered Index Scan, 805 reads. Tempting-but-wrong:
--   assuming "the value is 0 at runtime" is enough -- matching happens at compile time.
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('lesson12.OrderCopy') AND name = 'IsDeleted')
    ALTER TABLE lesson12.OrderCopy ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO
UPDATE lesson12.OrderCopy SET IsDeleted = 1 WHERE SalesOrderID % 100 = 0;  -- ~1% deleted

DROP INDEX IF EXISTS IX_lesson12_OrderCopy_ActiveOrders ON lesson12.OrderCopy;
CREATE INDEX IX_lesson12_OrderCopy_ActiveOrders
    ON lesson12.OrderCopy (TerritoryID)
    INCLUDE (OrderDate, TotalDue)
    WHERE IsDeleted = 0;

SET STATISTICS IO ON;
-- Query A: literal predicate -- matches the filter, uses the filtered index
--   5 logical reads, Index Seek on IX_lesson12_OrderCopy_ActiveOrders
SELECT SalesOrderID, OrderDate, TotalDue FROM lesson12.OrderCopy
WHERE IsDeleted = 0 AND TerritoryID = 2;

-- Query B: parameterised predicate -- can't be proven to match the filter, falls back
--   805 logical reads, Clustered Index Scan
DECLARE @IsDeleted BIT = 0;
SELECT SalesOrderID, OrderDate, TotalDue FROM lesson12.OrderCopy
WHERE IsDeleted = @IsDeleted AND TerritoryID = 2;
SET STATISTICS IO OFF;
