USE AdventureWorks2022;
GO

-- Exercise 1: For SalesOrderID 43659 in lesson14.SalesOrderCI, report its physical
--             location (file:page:slot). Then list all DATA_PAGE pages of the table.
-- Expected: one location string, then a list of data page ids.
-- Hint: %%physloc%% + sys.fn_PhysLocFormatter; sys.dm_db_database_page_allocations.
-- Your query here:


-- Exercise 2: Using DBCC PAGE, crack open the data page that holds SalesOrderID 43659
--             (use the PageId you found in Exercise 1). Turn on trace flag 3604 first.
-- Your statements here:


-- Exercise 3: Measure fragmentation of lesson14.SalesOrderCI's clustered index.
--             Then REBUILD it and measure again. Report avg_fragmentation_in_percent
--             before and after.
-- Your statements here:


-- Exercise 4: lesson14.SalesOrderHeap is a heap. Report its forwarded_record_count.
--             Explain in a comment why forwarded records hurt read performance.
-- Your query + comment here:


-- Exercise 5: Add 3 more middle-valued keys to lesson14.SplitDemo (e.g. 5, 12, 18)
--             and show that page_count increased compared to before the insert.
--             In a comment, explain what a page split is and one way to reduce them.
-- Your statements + comment here:
