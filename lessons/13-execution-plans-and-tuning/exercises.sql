USE AdventureWorks2022;
GO
-- Use SSMS with Ctrl+M (Include Actual Execution Plan) for all exercises.
SET STATISTICS IO   ON;
SET STATISTICS TIME ON;
GO

-- Exercise 1: Run the query below and look at its execution plan.
--             Identify: (a) the plan operator on Sales.SalesOrderDetail,
--             (b) estimated vs actual rows, (c) logical reads.
--             Then rewrite the WHERE clause to be SARGable and compare.
SELECT soh.SalesOrderID, SUM(sod.LineTotal) AS OrderTotal
FROM lesson13.SalesOrderHeader AS soh
JOIN Sales.SalesOrderDetail    AS sod ON sod.SalesOrderID = soh.SalesOrderID
WHERE CONVERT(VARCHAR(7), soh.OrderDate, 120) = '2013-07'   -- non-SARGable
GROUP BY soh.SalesOrderID;
-- Your SARGable rewrite here:


-- Exercise 2: The query below triggers a key lookup.
--             Add an index to lesson13.SalesOrderHeader to eliminate it.
--             Verify with the execution plan that the key lookup is gone.
SELECT SalesOrderID, SalesPersonID, SubTotal
FROM lesson13.SalesOrderHeader
WHERE TerritoryID = 4;
-- Your CREATE INDEX here:


-- Exercise 3: Run both queries and compare their plans and logical reads.
--             Explain in a comment why the plans differ.
-- Query A:
SELECT SalesOrderID, TotalDue FROM lesson13.SalesOrderHeader WHERE Status = 5;
-- Query B:
SELECT SalesOrderID, TotalDue FROM lesson13.SalesOrderHeader WHERE Status = 1;
-- Your explanation comment here:


-- Exercise 4: Create a stored procedure lesson13.usp_RevenueByTerritory that accepts
--             @TerritoryID INT and returns SUM(TotalDue) grouped by YEAR(OrderDate).
--             Call it with territory 1 (large) and territory 10 (small) and
--             check whether the plan is appropriate for both.
--             Add OPTION (OPTIMIZE FOR (@TerritoryID UNKNOWN)) if needed.
-- Your CREATE PROCEDURE and test EXECs here:


-- Exercise 5: Use SET STATISTICS IO ON to compare logical reads for these two queries.
--             Which one is more efficient and why?
-- Query A — explicit JOIN
SELECT p.Name, SUM(sod.OrderQty) AS TotalSold
FROM Sales.SalesOrderDetail     AS sod
JOIN Production.Product         AS p  ON p.ProductID = sod.ProductID
GROUP BY p.Name
ORDER BY TotalSold DESC;

-- Query B — correlated subquery
SELECT p.Name,
       (SELECT SUM(OrderQty) FROM Sales.SalesOrderDetail sod WHERE sod.ProductID = p.ProductID) AS TotalSold
FROM Production.Product AS p
ORDER BY TotalSold DESC;
-- Your analysis comment here:

SET STATISTICS IO   OFF;
SET STATISTICS TIME OFF;
