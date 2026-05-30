USE AdventureWorks2022;
GO

-- Exercise 1: Use DBCC SHOW_STATISTICS on IX_lesson15_SOH_OrderDate. From the histogram,
--             pick a RANGE_HI_KEY date and report its EQ_ROWS (estimated rows equal to that key).
-- Your statements + the value you read here:


-- Exercise 2: Run the query below with the ACTUAL plan on (Ctrl+M in SSMS). Report the
--             Estimated vs Actual number of rows on the seek/scan operator.
SELECT SalesOrderID, TotalDue FROM lesson15.SalesOrderHeader WHERE OrderDate >= '2014-01-01';
-- Your observed estimated/actual here:


-- Exercise 3: Find the cached plan(s) for any query touching lesson15.SalesOrderHeader and
--             report the highest usecounts (reuse count).
-- Your query here:


-- Exercise 4: Deliberately make statistics stale: reassign a large batch of rows to
--             CustomerID = 11001 via UPDATE (do NOT update stats yet). Then run a query
--             filtering CustomerID = 11001 with the actual plan on and observe the estimate is
--             now too low. Finally fix it with UPDATE STATISTICS and re-run.
-- Your statements here:


-- Exercise 5: In a comment, explain the four phases of the query lifecycle and name the phase
--             where statistics are used.
-- Your comment here:
