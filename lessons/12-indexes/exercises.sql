USE AdventureWorks2022;
GO
SET STATISTICS IO ON;
GO

-- Exercise 1: Run the query below and note the logical reads.
--             Then create the most effective nonclustered index for it.
--             Run again and compare logical reads.
--             Expected: significantly fewer logical reads after indexing.
SELECT SalesOrderID, TotalDue
FROM lesson12.SalesOrderNC
WHERE TerritoryID = 1
  AND YEAR(OrderDate) = 2013;
-- Your index CREATE statement here:


-- Exercise 2: The query below suffers from a key lookup.
--             Add the minimum INCLUDE columns to eliminate it.
--             Verify with SET STATISTICS IO ON that logical reads drop.
SELECT OrderDate, SubTotal, Freight
FROM lesson12.SalesOrderNC
WHERE CustomerID = 29825;
-- Your index CREATE (or ALTER) statement here:


-- Exercise 3: Create a filtered index on lesson12.SalesOrderNC
--             that covers only rows where OnlineOrderFlag = 1 (online orders).
--             Index on OrderDate, INCLUDE CustomerID and TotalDue.
--             Write a query that uses this filtered index.
-- Your CREATE INDEX and SELECT here:


-- Exercise 4: List all indexes on lesson12.SalesOrderNC
--             showing name, type, key columns, and included columns.
-- Hint: join sys.indexes, sys.index_columns, sys.columns
-- Your query here:


-- Exercise 5: Check fragmentation of all indexes in the lesson12 schema.
--             For any index with fragmentation > 30%, write the REBUILD statement.
-- Your query here:

SET STATISTICS IO OFF;

------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Workload-driven design. Three queries run against
--   lesson12.OrderCopy (a clean copy with only a clustered PK on
--   SalesOrderID):
--     (a) WHERE CustomerID = ? ORDER BY OrderDate DESC
--     (b) WHERE OrderDate range + SalesPersonID equality, returning TotalDue
--     (c) WHERE Status = 5 AND OnlineOrderFlag = 1 (99% of rows are Status 5)
--   Design a MINIMAL index set covering these three queries. Justify each
--   key column's position and each INCLUDE in comments; prove each query
--   seeks (or explain why you chose not to index it) via STATISTICS IO
--   before/after counts recorded in comments.
-- Hint: for (b), put the equality column (SalesPersonID) before the range
--   column (OrderDate) in the key. For (c), it's a candidate for a filtered
--   index — or for no dedicated index at all; justify either way with reads.
-- Your index DDL + evidence comments here:


-- Challenge 2: Key-lookup elimination. Write a query against
--   lesson12.OrderCopy that seeks on an indexed column but still shows a
--   Key Lookup (predicate on the indexed column, SELECT includes columns
--   not in that index). Record the logical reads. Then add the minimum
--   INCLUDE columns to eliminate the lookup and record logical reads again.
--   Numbers go in the solution comments.
-- Your query + index changes + evidence comments here:


-- Challenge 3: Index health report. Write one query joining
--   sys.dm_db_index_usage_stats and sys.indexes over AdventureWorks2022
--   that lists every index's total reads (seeks + scans + lookups) vs
--   writes, flagging candidates WHERE writes > 0 AND reads = 0.
-- Expected columns: SchemaName, TableName, IndexName, Reads, Writes, Flag
-- Hint: usage stats reset on every SQL Server restart — note that caveat
--   in your comment.
-- Your query here:


-- Challenge 4: Filtered index for the soft-delete pattern. Add an
--   IsDeleted column to lesson12.OrderCopy (99% = 0, 1% = 1), create a
--   filtered index WHERE IsDeleted = 0, then show one query that uses it
--   and one (a parameterised predicate) that cannot, explaining why in
--   the comment.
-- Hint: parameterised predicates can't be proven to match a filtered
--   index's filter at compile time.
-- Your ALTER TABLE + index + two queries + evidence comments here:

