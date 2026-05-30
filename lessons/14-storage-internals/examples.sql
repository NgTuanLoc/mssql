USE AdventureWorks2022;
GO

-- Example 1: Find the physical location (file:page:slot) of specific rows
-- %%physloc%% is an undocumented-but-stable virtual column; fn_PhysLocFormatter decodes it.
SELECT TOP 5
    SalesOrderID,
    sys.fn_PhysLocFormatter(%%physloc%%) AS PhysicalLocation   -- format: (file:page:slot)
FROM lesson14.SalesOrderCI
ORDER BY SalesOrderID;

-- Example 2: List the pages allocated to a table and their types
-- page_type_desc shows DATA_PAGE, INDEX_PAGE, IAM_PAGE, etc.
SELECT
    allocated_page_file_id  AS FileId,
    allocated_page_page_id  AS PageId,
    page_type_desc,
    index_id
FROM sys.dm_db_database_page_allocations(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderCI'), NULL, NULL, 'DETAILED')
WHERE page_type_desc IS NOT NULL
ORDER BY page_type_desc, PageId;

-- Example 3: Crack open a real data page with DBCC PAGE
-- Trace flag 3604 redirects DBCC output to the client instead of the error log.
-- Pick any DATA_PAGE PageId from Example 2 and substitute it below.
DBCC TRACEON(3604);
-- DBCC PAGE ('AdventureWorks2022', <FileId>, <PageId>, 3);  -- style 3 = page header + each row
-- Example with a placeholder page number — replace 0 with a real PageId from Example 2:
-- DBCC PAGE ('AdventureWorks2022', 1, 0, 3);
PRINT 'Replace the FileId/PageId above with a real DATA_PAGE from Example 2, then run DBCC PAGE.';
DBCC TRACEOFF(3604);

-- Example 4: Fragmentation and page density of the clustered index
SELECT
    i.name                              AS IndexName,
    ips.index_type_desc,
    ips.page_count,
    ips.avg_fragmentation_in_percent,
    ips.avg_page_space_used_in_percent  AS AvgPageFullnessPct,
    ips.forwarded_record_count          -- only meaningful for heaps
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderCI'), NULL, NULL, 'DETAILED') AS ips
JOIN sys.indexes AS i
  ON i.object_id = ips.object_id AND i.index_id = ips.index_id;

-- Example 5: Heaps create FORWARDED RECORDS when an updated row no longer fits in place
-- Inspect the heap before the update
SELECT 'before' AS Phase, forwarded_record_count, page_count
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderHeap'), 0, NULL, 'DETAILED');

-- Widen rows in place by filling a variable-length column that starts out NULL.
-- Comment is nvarchar(128 chars); seeding it grows the row so it no longer fits in place.
UPDATE lesson14.SalesOrderHeap
SET Comment = REPLICATE(N'Z', 120)
WHERE SalesOrderID % 5 = 0;

-- Inspect again — forwarded_record_count rises because moved rows leave a forwarding pointer
SELECT 'after' AS Phase, forwarded_record_count, page_count
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderHeap'), 0, NULL, 'DETAILED');

-- Example 6: Trigger and observe a PAGE SPLIT on a clustered B-tree
-- Page count BEFORE inserting a middle key
SELECT 'before split' AS Phase, page_count, avg_page_space_used_in_percent
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SplitDemo'), 1, NULL, 'DETAILED');

-- Insert a key that lands in the MIDDLE of a full page (rows are 2000 bytes; ~4 fit per 8KB page)
INSERT lesson14.SplitDemo (Id) VALUES (15), (25), (35);

-- Page count AFTER — the engine splits full pages to make room, raising page_count
SELECT 'after split' AS Phase, page_count, avg_page_space_used_in_percent
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SplitDemo'), 1, NULL, 'DETAILED');
