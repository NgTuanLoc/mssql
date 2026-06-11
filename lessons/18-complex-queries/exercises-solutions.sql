USE AdventureWorks2022;
GO
-- Lesson 18 solutions. Each solution has a short note on the approach.

---------------------------------------------------------------------------
-- Solution 1: Name each step. ProductTotals is DEFINED once instead of
-- pasting the same derived table twice. (It's still referenced twice, so it
-- executes twice — see Pitfalls — but the logic now lives in one place.)
---------------------------------------------------------------------------
WITH ProductTotals AS (
    SELECT ProductID, SUM(LineTotal) AS TotalSold
    FROM Sales.SalesOrderDetail
    GROUP BY ProductID
)
SELECT p.Name, pt.TotalSold
FROM ProductTotals AS pt
JOIN Production.Product AS p ON p.ProductID = pt.ProductID
WHERE pt.TotalSold > (SELECT AVG(TotalSold) FROM ProductTotals);

---------------------------------------------------------------------------
-- Solution 2: Two grain changes, one per CTE: order → territory-month sums,
-- then territory-month → territory average.
---------------------------------------------------------------------------
WITH MonthlyRevenue AS (
    SELECT soh.TerritoryID,
           DATEFROMPARTS(YEAR(soh.OrderDate), MONTH(soh.OrderDate), 1) AS OrderMonth,
           SUM(soh.TotalDue) AS Revenue
    FROM Sales.SalesOrderHeader AS soh
    GROUP BY soh.TerritoryID, DATEFROMPARTS(YEAR(soh.OrderDate), MONTH(soh.OrderDate), 1)
)
SELECT st.Name AS Territory, AVG(mr.Revenue) AS AvgMonthlyRevenue
FROM MonthlyRevenue AS mr
JOIN Sales.SalesTerritory AS st ON st.TerritoryID = mr.TerritoryID
GROUP BY st.Name
ORDER BY AvgMonthlyRevenue DESC;

---------------------------------------------------------------------------
-- Solution 3: Rank inside a partition, filter outside — window functions
-- can't appear in WHERE, hence the wrapping CTE.
---------------------------------------------------------------------------
WITH CustomerSpend AS (
    SELECT soh.TerritoryID, soh.CustomerID, SUM(soh.TotalDue) AS LifetimeSpend
    FROM Sales.SalesOrderHeader AS soh
    GROUP BY soh.TerritoryID, soh.CustomerID
),
Ranked AS (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY TerritoryID
                                 ORDER BY LifetimeSpend DESC) AS rn
    FROM CustomerSpend
)
SELECT st.Name AS Territory, r.CustomerID, r.LifetimeSpend, r.rn
FROM Ranked AS r
JOIN Sales.SalesTerritory AS st ON st.TerritoryID = r.TerritoryID
WHERE r.rn <= 3
ORDER BY st.Name, r.rn;

---------------------------------------------------------------------------
-- Solution 4: OUTER APPLY keeps salespeople with no orders (NULL columns);
-- CROSS APPLY would silently drop them. (In this dataset every salesperson
-- happens to have orders, so both return the same rows — OUTER is still the
-- right tool for the stated requirement.)
---------------------------------------------------------------------------
SELECT sp.BusinessEntityID, recent.SalesOrderID, recent.OrderDate, recent.TotalDue
FROM Sales.SalesPerson AS sp
OUTER APPLY (
    SELECT TOP (3) soh.SalesOrderID, soh.OrderDate, soh.TotalDue
    FROM Sales.SalesOrderHeader AS soh
    WHERE soh.SalesPersonID = sp.BusinessEntityID
    ORDER BY soh.OrderDate DESC
) AS recent
ORDER BY sp.BusinessEntityID, recent.OrderDate DESC;

---------------------------------------------------------------------------
-- Solution 5: COUNT(CASE WHEN ...) counts only matching rows because CASE
-- yields NULL otherwise, and COUNT ignores NULLs.
---------------------------------------------------------------------------
SELECT st.Name AS Territory,
       COUNT(CASE WHEN YEAR(soh.OrderDate) = 2012 THEN 1 END) AS Orders2012,
       COUNT(CASE WHEN YEAR(soh.OrderDate) = 2013 THEN 1 END) AS Orders2013,
       COUNT(CASE WHEN YEAR(soh.OrderDate) = 2014 THEN 1 END) AS Orders2014,
       COUNT(*) AS TotalOrders
FROM Sales.SalesOrderHeader AS soh
JOIN Sales.SalesTerritory  AS st ON st.TerritoryID = soh.TerritoryID
GROUP BY st.Name
ORDER BY Territory;

---------------------------------------------------------------------------
-- Solution 6: You can DELETE through a CTE. rn = 1 marks the keeper per
-- email (newest load, then highest StagingID); everything else goes.
---------------------------------------------------------------------------
WITH Ranked AS (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY Email
                                 ORDER BY LoadDate DESC, StagingID DESC) AS rn
    FROM lesson18.CustomerStaging
)
DELETE FROM Ranked WHERE rn > 1;

-- Verification: both counts must match
SELECT COUNT(*) AS RemainingRows, COUNT(DISTINCT Email) AS DistinctEmails
FROM lesson18.CustomerStaging;

---------------------------------------------------------------------------
-- Solution 7: date - row_number is constant per consecutive run (the
-- "islands" trick); group on it, then keep each member's longest island.
---------------------------------------------------------------------------
WITH Numbered AS (
    SELECT MemberID, VisitDate,
           DATEADD(DAY,
                   -ROW_NUMBER() OVER (PARTITION BY MemberID ORDER BY VisitDate),
                   VisitDate) AS StreakKey
    FROM lesson18.GymVisit
),
Streaks AS (
    SELECT MemberID,
           MIN(VisitDate) AS StreakStart,
           MAX(VisitDate) AS StreakEnd,
           COUNT(*)       AS StreakDays,
           ROW_NUMBER() OVER (PARTITION BY MemberID
                              ORDER BY COUNT(*) DESC, MIN(VisitDate)) AS rn
    FROM Numbered
    GROUP BY MemberID, StreakKey
)
SELECT MemberID, StreakStart, StreakEnd, StreakDays
FROM Streaks
WHERE rn = 1
ORDER BY MemberID;

---------------------------------------------------------------------------
-- Solution 8: Same pipeline shape as the case study — flatten, group to
-- vendor-month, LAG for growth, APPLY for the per-month top products.
---------------------------------------------------------------------------
WITH PoLines AS (
    SELECT poh.VendorID,
           DATEFROMPARTS(YEAR(poh.OrderDate), MONTH(poh.OrderDate), 1) AS OrderMonth,
           pod.ProductID,
           pod.LineTotal
    FROM Purchasing.PurchaseOrderHeader AS poh
    JOIN Purchasing.PurchaseOrderDetail AS pod ON pod.PurchaseOrderID = poh.PurchaseOrderID
),
VendorMonthly AS (
    SELECT VendorID, OrderMonth, SUM(LineTotal) AS MonthTotal
    FROM PoLines
    GROUP BY VendorID, OrderMonth
),
WithGrowth AS (
    SELECT VendorID, OrderMonth, MonthTotal,
           LAG(MonthTotal) OVER (PARTITION BY VendorID ORDER BY OrderMonth) AS PrevMonthTotal
    FROM VendorMonthly
)
SELECT v.Name AS Vendor,
       g.OrderMonth,
       g.MonthTotal,
       CAST(100.0 * (g.MonthTotal - g.PrevMonthTotal)
            / NULLIF(g.PrevMonthTotal, 0) AS DECIMAL(10,1)) AS GrowthPct,
       top2.ProductName,
       top2.ProductTotal,
       CAST(100.0 * top2.ProductTotal / g.MonthTotal AS DECIMAL(5,1)) AS ContributionPct
FROM WithGrowth AS g
JOIN Purchasing.Vendor AS v ON v.BusinessEntityID = g.VendorID
CROSS APPLY (
    SELECT TOP (2) p.Name AS ProductName, SUM(pl.LineTotal) AS ProductTotal
    FROM PoLines AS pl
    JOIN Production.Product AS p ON p.ProductID = pl.ProductID
    WHERE pl.VendorID  = g.VendorID
      AND pl.OrderMonth = g.OrderMonth
    GROUP BY p.Name
    ORDER BY SUM(pl.LineTotal) DESC
) AS top2
ORDER BY Vendor, g.OrderMonth, top2.ProductTotal DESC;
