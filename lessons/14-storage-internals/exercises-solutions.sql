USE AdventureWorks2022;
GO

-- Exercise 1: Physical location of a row + list of data pages.
-- Approach: %%physloc%% gives the row's RID; page allocations DMV lists every page.
SELECT SalesOrderID, sys.fn_PhysLocFormatter(%%physloc%%) AS PhysicalLocation
FROM lesson14.SalesOrderCI
WHERE SalesOrderID = 43659;

SELECT allocated_page_file_id AS FileId, allocated_page_page_id AS PageId, page_type_desc
FROM sys.dm_db_database_page_allocations(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderCI'), NULL, NULL, 'DETAILED')
WHERE page_type_desc = 'DATA_PAGE'
ORDER BY PageId;

-- Exercise 2: Crack the page holding SalesOrderID 43659.
-- Approach: read file/page from the physloc formatter output (file:page:slot), then DBCC PAGE.
-- Replace 1 and 0 below with the FileId/PageId from Exercise 1's PhysicalLocation.
DBCC TRACEON(3604);
-- DBCC PAGE ('AdventureWorks2022', 1, 0, 3);   -- <-- substitute real FileId/PageId
DBCC TRACEOFF(3604);
-- The output shows the page header (m_type=1 data page) and each slot's row contents.

-- Exercise 3: Fragmentation before and after a REBUILD.
-- Approach: REBUILD recreates the index contiguously, dropping fragmentation toward 0%.
SELECT 'before' AS Phase, avg_fragmentation_in_percent, page_count
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderCI'), 1, NULL, 'DETAILED');

ALTER INDEX PK_lesson14_SalesOrderCI ON lesson14.SalesOrderCI REBUILD;

SELECT 'after' AS Phase, avg_fragmentation_in_percent, page_count
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderCI'), 1, NULL, 'DETAILED');

-- Exercise 4: Heap forwarded records.
-- Approach: index_id 0 = the heap. Forwarded records appear after in-place updates grow rows.
SELECT forwarded_record_count, page_count
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderHeap'), 0, NULL, 'DETAILED');
-- Why they hurt: a forwarded record leaves a pointer at the original slot to the new location.
-- A scan must follow the pointer (an extra random I/O) to read the row, so reads do more work.
-- Fix: give the table a clustered index (B-trees relocate via the key, not forwarding pointers),
-- or run ALTER TABLE ... REBUILD to remove existing forwarding pointers.

-- Exercise 5: More splits on SplitDemo.
-- Approach: middle-valued inserts split full pages; page_count rises.
SELECT 'before' AS Phase, page_count FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SplitDemo'), 1, NULL, 'DETAILED');

INSERT lesson14.SplitDemo (Id) VALUES (5), (12), (18);

SELECT 'after' AS Phase, page_count FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SplitDemo'), 1, NULL, 'DETAILED');
-- A page split happens when a row must go onto a full page: SQL Server allocates a new page,
-- moves ~half the rows to it, then inserts. It causes fragmentation and extra log.
-- Reduce splits with a lower FILLFACTOR (leave free space) or an ever-increasing key
-- (appends go to the end and rarely split mid-index).
