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
