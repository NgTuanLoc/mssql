USE AdventureWorks2022;
GO

-- Exercise 1: Products per category.
-- Approach: Two-hop join: Product → ProductSubcategory → ProductCategory, then GROUP BY.
SELECT
    pc.Name  AS CategoryName,
    COUNT(*) AS ProductCount
FROM Production.Product              AS p
JOIN Production.ProductSubcategory  AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
JOIN Production.ProductCategory     AS pc ON pc.ProductCategoryID    = ps.ProductCategoryID
GROUP BY pc.Name
ORDER BY ProductCount DESC;

-- Exercise 2: Top 3 territories by 2013 revenue.
-- Approach: Filter year in WHERE, GROUP BY territory, ORDER BY + TOP.
SELECT TOP 3
    TerritoryID,
    SUM(TotalDue) AS TotalRevenue
FROM Sales.SalesOrderHeader
WHERE YEAR(OrderDate) = 2013
GROUP BY TerritoryID
ORDER BY TotalRevenue DESC;

-- Exercise 3: Customers with > 10 orders AND avg order > $2,000.
-- Approach: Both conditions go in HAVING — they operate on aggregated data.
SELECT
    CustomerID,
    COUNT(*)        AS OrderCount,
    AVG(TotalDue)   AS AvgOrderValue
FROM Sales.SalesOrderHeader
GROUP BY CustomerID
HAVING COUNT(*) > 10
   AND AVG(TotalDue) > 2000
ORDER BY AvgOrderValue DESC;

-- Exercise 4: ROLLUP for 2012–2014 totals.
-- Approach: Filter in WHERE first; ROLLUP adds year subtotals and a grand total (NULL year).
SELECT
    YEAR(OrderDate) AS OrderYear,
    SUM(TotalDue)   AS TotalSales
FROM Sales.SalesOrderHeader
WHERE YEAR(OrderDate) BETWEEN 2012 AND 2014
GROUP BY ROLLUP (YEAR(OrderDate))
ORDER BY OrderYear;

-- Exercise 5: Most expensive and average price by subcategory, avg > $200.
-- Approach: JOIN subcategory, GROUP BY, HAVING on the average.
SELECT
    ps.Name        AS SubcategoryName,
    MAX(p.ListPrice) AS MaxPrice,
    AVG(p.ListPrice) AS AvgPrice
FROM Production.Product            AS p
JOIN Production.ProductSubcategory AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
WHERE p.ListPrice > 0
GROUP BY ps.Name
HAVING AVG(p.ListPrice) > 200
ORDER BY AvgPrice DESC;

------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Revenue at three levels via GROUPING SETS, labelled with GROUPING_ID.
-- Reasoning: GROUPING SETS ((Territory,Year),(Territory),()) yields exactly the three
--            levels needed in one pass, no UNION of three queries. GROUPING_ID packs
--            both columns' grouping bits into an int (0=detail, 1=territory subtotal,
--            3=grand total) that CASE turns into a readable label. Tempting-but-wrong:
--            ROLLUP(Territory,Year) looks similar but only rolls up in declared column
--            order, so it can't express arbitrary/independent grouping combinations.
SELECT
    st.Name AS TerritoryName,
    YEAR(soh.OrderDate) AS OrderYear,
    SUM(soh.TotalDue) AS Revenue,
    CASE GROUPING_ID(st.Name, YEAR(soh.OrderDate))
        WHEN 0 THEN 'Detail'
        WHEN 1 THEN 'Territory total'
        WHEN 3 THEN 'Grand total'
    END AS Level
FROM Sales.SalesOrderHeader soh
JOIN Sales.SalesTerritory st ON st.TerritoryID = soh.TerritoryID
GROUP BY GROUPING SETS (
    (st.Name, YEAR(soh.OrderDate)),
    (st.Name),
    ()
)
ORDER BY GROUPING(st.Name), st.Name, GROUPING(YEAR(soh.OrderDate)), OrderYear;

-- Challenge 2: Correct average order value vs the naive AVG-over-a-fanned-join.
-- Reasoning: The naive column joins header to detail (to reproduce the bug) and AVGs
--            TotalDue over the fanned rows, so multi-line orders pull the average up.
--            Correct = aggregate SalesOrderHeader alone (no detail join) in its own
--            subquery: SUM(TotalDue)/COUNT(DISTINCT SalesOrderID) = real revenue over
--            real order count. Tempting-but-wrong: applying that same formula to the
--            fanned join still inflates the numerator even with a DISTINCT denominator.
SELECT
    n.TerritoryName,
    n.NaiveAvg,
    c.CorrectAvg
FROM (
    SELECT st.Name AS TerritoryName, AVG(soh.TotalDue) AS NaiveAvg
    FROM Sales.SalesOrderHeader soh
    JOIN Sales.SalesOrderDetail sod ON sod.SalesOrderID = soh.SalesOrderID
    JOIN Sales.SalesTerritory st ON st.TerritoryID = soh.TerritoryID
    GROUP BY st.Name
) AS n
JOIN (
    SELECT st.Name AS TerritoryName,
           SUM(soh.TotalDue) / COUNT(DISTINCT soh.SalesOrderID) AS CorrectAvg
    FROM Sales.SalesOrderHeader soh
    JOIN Sales.SalesTerritory st ON st.TerritoryID = soh.TerritoryID
    GROUP BY st.Name
) AS c ON c.TerritoryName = n.TerritoryName
ORDER BY n.TerritoryName;

-- Challenge 3: Territory x year order-count matrix via conditional aggregation.
-- Reasoning: One row per territory with one column per year means the year has to move
--            from rows into columns — conditional aggregation does that directly: one
--            SUM(CASE WHEN YEAR(OrderDate)=<year> THEN 1 ELSE 0 END) per year, all four
--            computed in a single GROUP BY pass over the table. Tempting-but-wrong: four
--            separate per-year queries UNIONed together would need a second pivot step
--            afterwards and scan the table four times instead of once.
SELECT
    st.Name AS TerritoryName,
    SUM(CASE WHEN YEAR(soh.OrderDate) = 2011 THEN 1 ELSE 0 END) AS Y2011,
    SUM(CASE WHEN YEAR(soh.OrderDate) = 2012 THEN 1 ELSE 0 END) AS Y2012,
    SUM(CASE WHEN YEAR(soh.OrderDate) = 2013 THEN 1 ELSE 0 END) AS Y2013,
    SUM(CASE WHEN YEAR(soh.OrderDate) = 2014 THEN 1 ELSE 0 END) AS Y2014
FROM Sales.SalesOrderHeader soh
JOIN Sales.SalesTerritory st ON st.TerritoryID = soh.TerritoryID
GROUP BY st.Name
ORDER BY st.Name;

-- Challenge 4: Territories where 2013 revenue beat 2012 by more than 10%.
-- Reasoning: Conditional aggregation puts each territory's 2012 and 2013 revenue on one
--            row (a CTE). The growth comparison goes in an OUTER WHERE, not HAVING:
--            T-SQL's HAVING cannot reference SELECT-list aliases like Rev2012/Rev2013
--            (verified — "Invalid column name"), so a HAVING version would repeat the
--            full CASE expressions and division a second time. The CTE computes each
--            aggregate once and reuses it for both filtering and ORDER BY.
WITH TerritoryYear AS (
    SELECT
        st.Name AS TerritoryName,
        SUM(CASE WHEN YEAR(soh.OrderDate) = 2012 THEN soh.TotalDue ELSE 0 END) AS Rev2012,
        SUM(CASE WHEN YEAR(soh.OrderDate) = 2013 THEN soh.TotalDue ELSE 0 END) AS Rev2013
    FROM Sales.SalesOrderHeader soh
    JOIN Sales.SalesTerritory st ON st.TerritoryID = soh.TerritoryID
    GROUP BY st.Name
)
SELECT
    TerritoryName,
    Rev2012,
    Rev2013,
    ROUND((Rev2013 - Rev2012) * 100.0 / Rev2012, 2) AS GrowthPct
FROM TerritoryYear
WHERE Rev2012 > 0
  AND (Rev2013 - Rev2012) * 100.0 / Rev2012 > 10
ORDER BY GrowthPct DESC;
