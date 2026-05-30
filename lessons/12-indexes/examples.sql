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
