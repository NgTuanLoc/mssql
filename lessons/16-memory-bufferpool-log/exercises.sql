USE AdventureWorks2022;
GO

-- Exercise 1: Report how many MB of the buffer pool AdventureWorks2022 currently occupies.
-- Hint: sys.dm_os_buffer_descriptors grouped by database_id; 8KB per page.
-- Your query here:


-- Exercise 2: Clear the cache (CHECKPOINT then DBCC DROPCLEANBUFFERS), show that few pages of
--             lesson16.BigDetail are cached, scan the table, then show many more are cached.
-- Your statements here:


-- Exercise 3: Report the current Page Life Expectancy in seconds. In a comment, say whether a
--             value of 50 on a busy server would worry you and why.
-- Your query + comment here:


-- Exercise 4: Open a transaction that updates some rows of lesson16.BigDetail and DO NOT commit
--             yet. In another batch (or just below), show that
--             sys.databases.log_reuse_wait_desc for AdventureWorks2022 is ACTIVE_TRANSACTION.
--             Then commit and show it returns to NOTHING.
-- Your statements here:


-- Exercise 5: Force a tempdb spill on a large sort of lesson16.BigDetail using
--             OPTION (MAX_GRANT_PERCENT = 0.0). In a comment, explain what a spill is and one
--             non-hint way to avoid it in real workloads.
-- Your statements + comment here:
