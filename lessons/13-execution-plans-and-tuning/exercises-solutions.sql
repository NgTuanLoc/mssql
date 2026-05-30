USE AdventureWorks2022;
GO
SET STATISTICS IO   ON;
SET STATISTICS TIME ON;
GO

-- Exercise 1: SARGable rewrite for the date filter.
-- Non-SARGable: CONVERT() on the column forces a scan.
-- SARGable rewrite: use a date range so the index on OrderDate can be used.
SELECT soh.SalesOrderID, SUM(sod.LineTotal) AS OrderTotal
FROM lesson13.SalesOrderHeader AS soh
JOIN Sales.SalesOrderDetail    AS sod ON sod.SalesOrderID = soh.SalesOrderID
WHERE soh.OrderDate >= '2013-07-01' AND soh.OrderDate < '2013-08-01'  -- SARGable
GROUP BY soh.SalesOrderID;
-- Expected: fewer logical reads and an Index Seek instead of a Scan on SalesOrderHeader.

-- Exercise 2: Add index to eliminate key lookup for TerritoryID + SalesPersonID + SubTotal.
CREATE INDEX IX_lesson13_SOH_TerritoryID_Inc
    ON lesson13.SalesOrderHeader (TerritoryID)
    INCLUDE (SalesPersonID, SubTotal);

SELECT SalesOrderID, SalesPersonID, SubTotal
FROM lesson13.SalesOrderHeader
WHERE TerritoryID = 4;
-- Expected: the Key Lookup operator disappears from the plan.

-- Exercise 3: Plan difference for Status = 5 vs Status = 1.
-- Status = 5 (shipped) covers most rows — optimizer may choose a Clustered Index Scan
-- because seeking + looking up each row would be more expensive than a full scan.
-- Status = 1 (in process) covers very few rows — optimizer chooses an Index Seek.
-- This illustrates how selectivity affects the optimizer's access-path choice.
SELECT SalesOrderID, TotalDue FROM lesson13.SalesOrderHeader WHERE Status = 5;
SELECT SalesOrderID, TotalDue FROM lesson13.SalesOrderHeader WHERE Status = 1;

-- Exercise 4: Revenue by territory procedure with OPTIMIZE FOR UNKNOWN.
CREATE OR ALTER PROCEDURE lesson13.usp_RevenueByTerritory
    @TerritoryID INT
AS
    SELECT
        YEAR(OrderDate)  AS OrderYear,
        SUM(TotalDue)    AS Revenue
    FROM lesson13.SalesOrderHeader
    WHERE TerritoryID = @TerritoryID
    GROUP BY YEAR(OrderDate)
    ORDER BY OrderYear
    OPTION (OPTIMIZE FOR (@TerritoryID UNKNOWN));
GO

EXEC lesson13.usp_RevenueByTerritory @TerritoryID = 1;
EXEC lesson13.usp_RevenueByTerritory @TerritoryID = 10;

-- Exercise 5: JOIN vs correlated subquery.
-- Query A (JOIN): one pass over SalesOrderDetail with a Hash Match aggregate.
SELECT p.Name, SUM(sod.OrderQty) AS TotalSold
FROM Sales.SalesOrderDetail AS sod
JOIN Production.Product     AS p  ON p.ProductID = sod.ProductID
GROUP BY p.Name
ORDER BY TotalSold DESC;

-- Query B (correlated subquery): executes once PER product — N executions for N products.
-- For ~500 products, that is ~500 individual scans of SalesOrderDetail.
-- Query A will have far fewer logical reads.
SELECT p.Name,
       (SELECT SUM(OrderQty) FROM Sales.SalesOrderDetail sod WHERE sod.ProductID = p.ProductID) AS TotalSold
FROM Production.Product AS p
ORDER BY TotalSold DESC;
-- Lesson: correlated subqueries that aggregate a large table execute once per outer row.
-- A JOIN with GROUP BY is almost always more efficient.

GO
SET STATISTICS IO   OFF;
SET STATISTICS TIME OFF;
