USE AdventureWorks2022;
GO

-- =========================================================================
-- THE BUFFER POOL (data cache)
-- =========================================================================

-- Example 1: How many pages of each database are currently cached?
SELECT
    CASE database_id WHEN 32767 THEN 'ResourceDB'
         ELSE DB_NAME(database_id) END AS DatabaseName,
    COUNT(*)                            AS CachedPages,
    COUNT(*) * 8 / 1024.0               AS CachedMB
FROM sys.dm_os_buffer_descriptors
GROUP BY database_id
ORDER BY CachedPages DESC;

-- Example 2: Show a table being pulled into cache
-- Clear THIS database's cached pages first (dev only — never in production)
-- DBCC DROPCLEANBUFFERS clears clean pages server-wide; CHECKPOINT flushes dirty ones first.
CHECKPOINT;
DBCC DROPCLEANBUFFERS;

-- How much of BigDetail is cached now? (expect ~0)
SELECT COUNT(*) AS BigDetailPagesCached
FROM sys.dm_os_buffer_descriptors AS bd
WHERE bd.database_id = DB_ID()
  AND bd.allocation_unit_id IN (
        SELECT au.allocation_unit_id
        FROM sys.allocation_units AS au
        JOIN sys.partitions AS p ON p.partition_id = au.container_id
        WHERE p.object_id = OBJECT_ID('lesson16.BigDetail'));

-- Scan the table to load it into the buffer pool
SELECT COUNT(*) FROM lesson16.BigDetail;

-- Now many more pages are cached
SELECT COUNT(*) AS BigDetailPagesCached
FROM sys.dm_os_buffer_descriptors AS bd
WHERE bd.database_id = DB_ID()
  AND bd.allocation_unit_id IN (
        SELECT au.allocation_unit_id
        FROM sys.allocation_units AS au
        JOIN sys.partitions AS p ON p.partition_id = au.container_id
        WHERE p.object_id = OBJECT_ID('lesson16.BigDetail'));

-- Example 3: Page Life Expectancy (seconds a page is expected to stay in cache)
-- Higher is better; a sharply falling PLE signals memory pressure.
SELECT object_name, counter_name, cntr_value AS PLE_Seconds
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Page life expectancy'
  AND object_name LIKE '%Buffer Manager%';

-- =========================================================================
-- THE TRANSACTION LOG
-- =========================================================================

-- Example 4: Inspect the Virtual Log Files (VLFs) of the current database
SELECT COUNT(*) AS VLF_Count
FROM sys.dm_db_log_info(DB_ID());

-- Example 5: Why can't the log be truncated right now?
-- log_reuse_wait_desc tells you what the log is waiting on (NOTHING, ACTIVE_TRANSACTION, etc.)
SELECT name, log_reuse_wait_desc, recovery_model_desc
FROM sys.databases
WHERE name = 'AdventureWorks2022';

-- Example 6: An open transaction holds the log hostage
BEGIN TRANSACTION;
UPDATE lesson16.BigDetail SET OrderQty = OrderQty WHERE SalesOrderDetailID % 1000 = 0;

-- While the transaction is OPEN, check log reuse wait again — expect ACTIVE_TRANSACTION
SELECT name, log_reuse_wait_desc FROM sys.databases WHERE name = 'AdventureWorks2022';

-- See the open transaction
SELECT
    DB_NAME(dt.database_id)   AS DatabaseName,
    st.session_id,
    at.name                   AS TranName,
    at.transaction_begin_time
FROM sys.dm_tran_active_transactions  AS at
JOIN sys.dm_tran_session_transactions AS st ON st.transaction_id = at.transaction_id
JOIN sys.dm_tran_database_transactions AS dt ON dt.transaction_id = at.transaction_id
WHERE dt.database_id = DB_ID();

COMMIT TRANSACTION;   -- release it; log_reuse_wait_desc returns to NOTHING

-- =========================================================================
-- MEMORY GRANTS & TEMPDB SPILLS
-- =========================================================================

-- Example 7: Force a tempdb spill by starving the sort of memory
-- A big sort needs a memory grant. MAX_GRANT_PERCENT caps it tiny, forcing a spill to tempdb.
-- Run with the ACTUAL plan (Ctrl+M): the Sort operator shows a warning triangle "spilled to tempdb".
-- (Returns ~485k rows — that is expected.)
SELECT *
FROM lesson16.BigDetail
ORDER BY ProductID, ModifiedDate, OrderQty, UnitPrice
OPTION (MAX_GRANT_PERCENT = 0.0);   -- starve the grant to guarantee a spill

-- Inspect active/last memory grants
SELECT TOP 5
    mg.session_id,
    mg.requested_memory_kb,
    mg.granted_memory_kb,
    mg.required_memory_kb,
    mg.dop
FROM sys.dm_exec_query_memory_grants AS mg;
