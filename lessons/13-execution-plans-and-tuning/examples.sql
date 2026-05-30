USE AdventureWorks2022;
GO

SET STATISTICS IO   ON;
SET STATISTICS TIME ON;
GO

-- =========================================================================
-- READING PLANS — Run each query with Ctrl+M (Include Actual Plan) in SSMS
-- =========================================================================

-- Example 1: Index Seek — SalesOrderID is the clustered key
SELECT SalesOrderID, OrderDate, TotalDue
FROM lesson13.SalesOrderHeader
WHERE SalesOrderID = 43659;
-- Plan: Clustered Index Seek — very few logical reads

-- Example 2: Index Scan — no useful predicate, reads all rows
SELECT COUNT(*) FROM lesson13.SalesOrderHeader;
-- Plan: Clustered Index Scan — reads every page

-- Example 3: Nonclustered seek + key lookup eliminated by INCLUDE
SELECT OrderDate, TotalDue
FROM lesson13.SalesOrderHeader
WHERE CustomerID = 11000;
-- Plan: nonclustered seek (IX_lesson13_SOH_CustomerID) — no key lookup (INCLUDE covers it)

-- Example 4: SARGable vs non-SARGable predicate
-- Non-SARGable: function on indexed column → full scan
SELECT SalesOrderID, TotalDue
FROM lesson13.SalesOrderHeader
WHERE YEAR(OrderDate) = 2014;

-- SARGable rewrite: range on the indexed column → seek
SELECT SalesOrderID, TotalDue
FROM lesson13.SalesOrderHeader
WHERE OrderDate >= '2014-01-01' AND OrderDate < '2015-01-01';
-- Compare logical reads and plan operator between the two

-- Example 5: Hash Match Join vs Nested Loops — join two large result sets
SELECT soh.SalesOrderID, soh.TotalDue, sod.ProductID, sod.OrderQty
FROM lesson13.SalesOrderHeader AS soh
JOIN Sales.SalesOrderDetail    AS sod ON sod.SalesOrderID = soh.SalesOrderID
WHERE soh.TerritoryID = 1;
-- Likely: Hash Match or Merge Join for the large row set

-- Example 6: Parameter sniffing demo — see README for two-step walkthrough
-- Step 1: prime the plan with a common, selective value
CREATE OR ALTER PROCEDURE lesson13.usp_OrdersByTerritory @TerritoryID INT
AS
    SELECT SalesOrderID, OrderDate, TotalDue
    FROM lesson13.SalesOrderHeader
    WHERE TerritoryID = @TerritoryID;
GO

EXEC lesson13.usp_OrdersByTerritory @TerritoryID = 9;   -- small territory
EXEC lesson13.usp_OrdersByTerritory @TerritoryID = 1;   -- large territory (reuses sniffed plan)
-- May see a poor plan (e.g. nested loops) for territory 1 that was optimised for territory 9

-- Flush plan cache for the procedure (demo only — never in production)
-- DBCC FREEPROCCACHE;
-- EXEC lesson13.usp_OrdersByTerritory @TerritoryID = 1;   -- now gets its own plan

-- Example 7: OPTIMIZE FOR UNKNOWN — break sniffing without RECOMPILE
CREATE OR ALTER PROCEDURE lesson13.usp_OrdersByTerritory_Fixed @TerritoryID INT
AS
    SELECT SalesOrderID, OrderDate, TotalDue
    FROM lesson13.SalesOrderHeader
    WHERE TerritoryID = @TerritoryID
    OPTION (OPTIMIZE FOR (@TerritoryID UNKNOWN));
GO

EXEC lesson13.usp_OrdersByTerritory_Fixed @TerritoryID = 1;

GO
SET STATISTICS IO   OFF;
SET STATISTICS TIME OFF;
