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
