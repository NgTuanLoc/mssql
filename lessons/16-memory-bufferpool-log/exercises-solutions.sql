USE AdventureWorks2022;
GO

-- Exercise 1: AdventureWorks cache footprint.
-- Approach: count cached pages for this database_id and convert to MB.
SELECT DB_NAME(database_id) AS DatabaseName,
       COUNT(*)             AS CachedPages,
       COUNT(*) * 8 / 1024.0  AS CachedMB
FROM sys.dm_os_buffer_descriptors
WHERE database_id = DB_ID('AdventureWorks2022')
GROUP BY database_id;

-- Exercise 2: Watch a table load into cache.
-- Approach: drop clean buffers, measure, scan, measure again.
CHECKPOINT;
DBCC DROPCLEANBUFFERS;

SELECT COUNT(*) AS CachedBefore
FROM sys.dm_os_buffer_descriptors AS bd
WHERE bd.database_id = DB_ID()
  AND bd.allocation_unit_id IN (
        SELECT au.allocation_unit_id FROM sys.allocation_units au
        JOIN sys.partitions p ON p.partition_id = au.container_id
        WHERE p.object_id = OBJECT_ID('lesson16.BigDetail'));

SELECT COUNT(*) FROM lesson16.BigDetail;   -- forces a scan into cache

SELECT COUNT(*) AS CachedAfter
FROM sys.dm_os_buffer_descriptors AS bd
WHERE bd.database_id = DB_ID()
  AND bd.allocation_unit_id IN (
        SELECT au.allocation_unit_id FROM sys.allocation_units au
        JOIN sys.partitions p ON p.partition_id = au.container_id
        WHERE p.object_id = OBJECT_ID('lesson16.BigDetail'));

-- Exercise 3: Page Life Expectancy.
-- Approach: read the Buffer Manager PLE counter.
SELECT cntr_value AS PLE_Seconds
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Page life expectancy'
  AND object_name LIKE '%Buffer Manager%';
-- A PLE of 50 on a busy server is alarming: pages survive only ~50s in cache, meaning SQL Server
-- is constantly re-reading from disk (memory pressure). On a quiet dev container PLE can be low
-- simply because nothing is keeping it warm — context matters; watch the trend, not one number.

-- Exercise 4: Open transaction blocks log truncation.
-- Approach: an uncommitted transaction sets log_reuse_wait_desc = ACTIVE_TRANSACTION.
BEGIN TRANSACTION;
UPDATE lesson16.BigDetail SET OrderQty = OrderQty WHERE SalesOrderDetailID % 1000 = 0;

SELECT log_reuse_wait_desc FROM sys.databases WHERE name = 'AdventureWorks2022';
-- Expect: ACTIVE_TRANSACTION

COMMIT TRANSACTION;
SELECT log_reuse_wait_desc FROM sys.databases WHERE name = 'AdventureWorks2022';
-- Expect: NOTHING

-- Exercise 5: Force and explain a tempdb spill.
-- Approach: starve the memory grant so the sort cannot complete in memory.
SELECT *
FROM lesson16.BigDetail
ORDER BY ProductID, ModifiedDate, OrderQty, UnitPrice
OPTION (MAX_GRANT_PERCENT = 0.0);
-- A spill happens when an operator (sort/hash) is granted too little memory and must write
-- intermediate data to tempdb, doing extra slow I/O. The actual plan flags the Sort with a
-- warning. Non-hint fixes: refresh statistics so the optimizer requests the right grant, reduce
-- the rows/columns being sorted, or add an index that delivers rows already sorted (no sort).
