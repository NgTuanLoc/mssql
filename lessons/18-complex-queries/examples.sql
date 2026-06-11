USE AdventureWorks2022;
GO

---------------------------------------------------------------------------
-- CONCEPTS 1 & 2: The case-study pipeline, built step by step.
-- Business question: "For each territory: monthly revenue, month-over-month
-- growth, top 3 products, and each product's contribution %."
---------------------------------------------------------------------------

-- Example 1: Step A — flatten orders to the grain we need (one row per line item)
WITH OrderLines AS (
    SELECT soh.TerritoryID,
           DATEFROMPARTS(YEAR(soh.OrderDate), MONTH(soh.OrderDate), 1) AS OrderMonth,
           sod.ProductID,
           sod.LineTotal
    FROM Sales.SalesOrderHeader AS soh
    JOIN Sales.SalesOrderDetail AS sod ON sod.SalesOrderID = soh.SalesOrderID
)
SELECT TOP (10) * FROM OrderLines;   -- debug technique: eyeball the step's shape/rows (use explicit columns in real code)

-- Example 2: Step B — first aggregation layer (territory × month)
WITH OrderLines AS (
    SELECT soh.TerritoryID,
           DATEFROMPARTS(YEAR(soh.OrderDate), MONTH(soh.OrderDate), 1) AS OrderMonth,
           sod.ProductID,
           sod.LineTotal
    FROM Sales.SalesOrderHeader AS soh
    JOIN Sales.SalesOrderDetail AS sod ON sod.SalesOrderID = soh.SalesOrderID
),
MonthlyTerritory AS (
    SELECT TerritoryID, OrderMonth, SUM(LineTotal) AS MonthRevenue
    FROM OrderLines
    GROUP BY TerritoryID, OrderMonth
)
SELECT TOP (10) * FROM MonthlyTerritory ORDER BY TerritoryID, OrderMonth;

-- Example 3: Step C — window function OVER the grouped result (aggregate of an aggregate)
-- Note: LAG appears three times here for compactness. Example 14 shows the clean
-- form: compute LAG once in its own CTE, then reference the column.
WITH OrderLines AS (
    SELECT soh.TerritoryID,
           DATEFROMPARTS(YEAR(soh.OrderDate), MONTH(soh.OrderDate), 1) AS OrderMonth,
           sod.ProductID,
           sod.LineTotal
    FROM Sales.SalesOrderHeader AS soh
    JOIN Sales.SalesOrderDetail AS sod ON sod.SalesOrderID = soh.SalesOrderID
),
MonthlyTerritory AS (
    SELECT TerritoryID, OrderMonth, SUM(LineTotal) AS MonthRevenue
    FROM OrderLines
    GROUP BY TerritoryID, OrderMonth
)
SELECT TerritoryID, OrderMonth, MonthRevenue,
       LAG(MonthRevenue) OVER (PARTITION BY TerritoryID ORDER BY OrderMonth) AS PrevMonthRevenue,
       CAST(100.0 * (MonthRevenue - LAG(MonthRevenue) OVER (PARTITION BY TerritoryID ORDER BY OrderMonth))
            / NULLIF(LAG(MonthRevenue) OVER (PARTITION BY TerritoryID ORDER BY OrderMonth), 0)
            AS DECIMAL(10,1)) AS GrowthPct
FROM MonthlyTerritory
ORDER BY TerritoryID, OrderMonth;
GO

---------------------------------------------------------------------------
-- CONCEPT 3: CROSS / OUTER APPLY
---------------------------------------------------------------------------

-- Example 4: top-3-per-group with CROSS APPLY — each territory's 3 biggest orders
SELECT st.Name AS Territory, big.SalesOrderID, big.TotalDue
FROM Sales.SalesTerritory AS st
CROSS APPLY (
    SELECT TOP (3) soh.SalesOrderID, soh.TotalDue
    FROM Sales.SalesOrderHeader AS soh
    WHERE soh.TerritoryID = st.TerritoryID
    ORDER BY soh.TotalDue DESC
) AS big
ORDER BY st.Name, big.TotalDue DESC;

-- Example 5: CROSS vs OUTER APPLY — CROSS silently drops rows with no match.
-- Products that have never sold disappear under CROSS APPLY; OUTER keeps them with NULLs.
SELECT p.Name, lastSale.OrderDate
FROM Production.Product AS p
OUTER APPLY (
    SELECT TOP (1) soh.OrderDate
    FROM Sales.SalesOrderDetail AS sod
    JOIN Sales.SalesOrderHeader AS soh ON soh.SalesOrderID = sod.SalesOrderID
    WHERE sod.ProductID = p.ProductID
    ORDER BY soh.OrderDate DESC
) AS lastSale
WHERE lastSale.OrderDate IS NULL;   -- only possible with OUTER APPLY
-- (504 products in the catalog; 238 have never been sold)
GO

---------------------------------------------------------------------------
-- CONCEPT 4: PIVOT / UNPIVOT and conditional aggregation
---------------------------------------------------------------------------

-- Example 6: PIVOT — territory revenue by year, years as columns
SELECT Name AS Territory, [2011], [2012], [2013], [2014]
FROM (
    SELECT st.Name, YEAR(soh.OrderDate) AS OrderYear, soh.TotalDue
    FROM Sales.SalesOrderHeader AS soh
    JOIN Sales.SalesTerritory  AS st ON st.TerritoryID = soh.TerritoryID
) AS src
PIVOT (SUM(TotalDue) FOR OrderYear IN ([2011], [2012], [2013], [2014])) AS pvt
ORDER BY Territory;

-- Example 7: the same result with conditional aggregation — no PIVOT syntax,
-- and you can mix in other aggregates (like the TotalOrders column) freely.
SELECT st.Name AS Territory,
       SUM(CASE WHEN YEAR(soh.OrderDate) = 2011 THEN soh.TotalDue END) AS [2011],
       SUM(CASE WHEN YEAR(soh.OrderDate) = 2012 THEN soh.TotalDue END) AS [2012],
       SUM(CASE WHEN YEAR(soh.OrderDate) = 2013 THEN soh.TotalDue END) AS [2013],
       SUM(CASE WHEN YEAR(soh.OrderDate) = 2014 THEN soh.TotalDue END) AS [2014],
       COUNT(*) AS TotalOrders
FROM Sales.SalesOrderHeader AS soh
JOIN Sales.SalesTerritory  AS st ON st.TerritoryID = soh.TerritoryID
GROUP BY st.Name
ORDER BY Territory;

-- Example 8: UNPIVOT — wide salesperson measure columns back into rows
-- Note: UNPIVOT silently drops rows where the value is NULL (SalesQuota often is).
-- If NULL rows matter, use conditional aggregation instead.
SELECT BusinessEntityID, MeasureName, MeasureValue
FROM (
    SELECT BusinessEntityID,
           SalesQuota   AS CurrentQuota,
           SalesYTD     AS YearToDate,
           SalesLastYear AS LastYear
    FROM Sales.SalesPerson
) AS src
UNPIVOT (MeasureValue FOR MeasureName IN (CurrentQuota, YearToDate, LastYear)) AS unp
ORDER BY BusinessEntityID, MeasureName;
GO

---------------------------------------------------------------------------
-- CONCEPT 5: Classic patterns
---------------------------------------------------------------------------

-- Example 9: top-N per group with ROW_NUMBER (compare with Example 4's APPLY version)
-- APPLY reads naturally for small fixed N; ROW_NUMBER lets you also keep the rank,
-- filter ranges (rows 4-6), or switch to RANK/DENSE_RANK for ties.
WITH Ranked AS (
    SELECT st.Name AS Territory, soh.SalesOrderID, soh.TotalDue,
           ROW_NUMBER() OVER (PARTITION BY st.TerritoryID ORDER BY soh.TotalDue DESC) AS rn
    FROM Sales.SalesOrderHeader AS soh
    JOIN Sales.SalesTerritory  AS st ON st.TerritoryID = soh.TerritoryID
)
SELECT Territory, SalesOrderID, TotalDue
FROM Ranked
WHERE rn <= 3
ORDER BY Territory, TotalDue DESC;

-- Example 10: ties — RANK keeps all rows that tie for a place; TOP WITH TIES
-- does the same for a single (non-grouped) top-N
SELECT TOP (5) WITH TIES p.Name, p.ListPrice
FROM Production.Product AS p
ORDER BY p.ListPrice DESC;

-- Example 11: de-duplication with ROW_NUMBER (preview only — the DELETE version
-- is exercise 6). rn = 1 marks the keeper per email; rows with rn > 1 are the
-- copies a DELETE would remove.
WITH Ranked AS (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY Email
                                 ORDER BY LoadDate DESC, StagingID DESC) AS rn
    FROM lesson18.CustomerStaging
)
SELECT StagingID, Email, LoadDate, rn
FROM Ranked
WHERE rn > 1
ORDER BY Email, rn;

-- Example 12: gaps & islands — consecutive visit streaks per member.
-- Trick: VisitDate minus its row number is CONSTANT within a consecutive run.
WITH Numbered AS (
    SELECT MemberID, VisitDate,
           DATEADD(DAY,
                   -ROW_NUMBER() OVER (PARTITION BY MemberID ORDER BY VisitDate),
                   VisitDate) AS StreakKey
    FROM lesson18.GymVisit
)
SELECT MemberID,
       MIN(VisitDate) AS StreakStart,
       MAX(VisitDate) AS StreakEnd,
       COUNT(*)       AS StreakDays
FROM Numbered
GROUP BY MemberID, StreakKey
ORDER BY MemberID, StreakStart;

-- Example 13: running total + share of period (windowed SUM at two scopes)
WITH MonthlySales AS (
    SELECT DATEFROMPARTS(YEAR(OrderDate), MONTH(OrderDate), 1) AS OrderMonth,
           SUM(TotalDue) AS Revenue
    FROM Sales.SalesOrderHeader
    GROUP BY DATEFROMPARTS(YEAR(OrderDate), MONTH(OrderDate), 1)
)
SELECT OrderMonth, Revenue,
       SUM(Revenue) OVER (ORDER BY OrderMonth
                          ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningTotal,
       CAST(100.0 * Revenue / SUM(Revenue) OVER () AS DECIMAL(5,2)) AS PctOfAllTime
FROM MonthlySales
ORDER BY OrderMonth;
GO

---------------------------------------------------------------------------
-- THE ASSEMBLED CASE STUDY: pipeline + window functions + APPLY together
---------------------------------------------------------------------------

-- Example 14: the full answer to the business question
WITH OrderLines AS (
    SELECT soh.TerritoryID,
           DATEFROMPARTS(YEAR(soh.OrderDate), MONTH(soh.OrderDate), 1) AS OrderMonth,
           sod.ProductID,
           sod.LineTotal
    FROM Sales.SalesOrderHeader AS soh
    JOIN Sales.SalesOrderDetail AS sod ON sod.SalesOrderID = soh.SalesOrderID
),
MonthlyTerritory AS (
    SELECT TerritoryID, OrderMonth, SUM(LineTotal) AS MonthRevenue
    FROM OrderLines
    GROUP BY TerritoryID, OrderMonth
),
WithGrowth AS (
    SELECT TerritoryID, OrderMonth, MonthRevenue,
           LAG(MonthRevenue) OVER (PARTITION BY TerritoryID ORDER BY OrderMonth) AS PrevMonthRevenue
    FROM MonthlyTerritory
)
SELECT st.Name AS Territory,
       g.OrderMonth,
       g.MonthRevenue,
       CAST(100.0 * (g.MonthRevenue - g.PrevMonthRevenue)
            / NULLIF(g.PrevMonthRevenue, 0) AS DECIMAL(10,1)) AS GrowthPct,
       top3.ProductName,
       top3.ProductRevenue,
       CAST(100.0 * top3.ProductRevenue / g.MonthRevenue AS DECIMAL(5,1)) AS ContributionPct
FROM WithGrowth AS g
JOIN Sales.SalesTerritory AS st ON st.TerritoryID = g.TerritoryID
CROSS APPLY (
    SELECT TOP (3) p.Name AS ProductName, SUM(ol.LineTotal) AS ProductRevenue
    FROM OrderLines AS ol
    JOIN Production.Product AS p ON p.ProductID = ol.ProductID
    WHERE ol.TerritoryID = g.TerritoryID
      AND ol.OrderMonth  = g.OrderMonth
    GROUP BY p.Name
    ORDER BY SUM(ol.LineTotal) DESC
) AS top3
ORDER BY Territory, g.OrderMonth, top3.ProductRevenue DESC;
GO
