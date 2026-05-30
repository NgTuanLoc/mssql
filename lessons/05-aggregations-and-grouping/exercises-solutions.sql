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
