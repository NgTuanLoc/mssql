USE AdventureWorks2022;
GO

-- Example 1: Basic GROUP BY with aggregate functions
SELECT
    YEAR(OrderDate)    AS OrderYear,
    COUNT(*)           AS TotalOrders,
    SUM(TotalDue)      AS Revenue,
    AVG(TotalDue)      AS AvgOrderValue,
    MIN(TotalDue)      AS SmallestOrder,
    MAX(TotalDue)      AS LargestOrder
FROM Sales.SalesOrderHeader
GROUP BY YEAR(OrderDate)
ORDER BY OrderYear;

-- Example 2: HAVING — filter after aggregation
SELECT
    CustomerID,
    COUNT(*)    AS OrderCount,
    SUM(TotalDue) AS TotalSpend
FROM Sales.SalesOrderHeader
GROUP BY CustomerID
HAVING COUNT(*) >= 5              -- only customers with 5+ orders
ORDER BY TotalSpend DESC;

-- Example 3: Aggregate with multiple GROUP BY columns
SELECT
    YEAR(OrderDate)   AS OrderYear,
    MONTH(OrderDate)  AS OrderMonth,
    SUM(TotalDue)     AS MonthlyRevenue
FROM Sales.SalesOrderHeader
GROUP BY YEAR(OrderDate), MONTH(OrderDate)
ORDER BY OrderYear, OrderMonth;

-- Example 4: COUNT(col) vs COUNT(*) — NULLs
SELECT
    COUNT(*)              AS AllProducts,
    COUNT(Color)          AS ProductsWithColor,    -- NULLs excluded
    COUNT(DISTINCT Color) AS DistinctColors
FROM Production.Product;

-- Example 5: GROUPING SETS — multiple groupings in one query
SELECT
    YEAR(OrderDate) AS OrderYear,
    MONTH(OrderDate) AS OrderMonth,
    SUM(TotalDue)  AS Revenue
FROM Sales.SalesOrderHeader
GROUP BY GROUPING SETS (
    (YEAR(OrderDate), MONTH(OrderDate)),   -- monthly subtotals
    (YEAR(OrderDate)),                     -- yearly subtotals
    ()                                     -- grand total (NULL, NULL row)
)
ORDER BY OrderYear, OrderMonth;

-- Example 6: ROLLUP — hierarchical subtotals (equivalent shorthand)
SELECT
    YEAR(OrderDate)   AS OrderYear,
    MONTH(OrderDate)  AS OrderMonth,
    SUM(TotalDue)     AS Revenue
FROM Sales.SalesOrderHeader
GROUP BY ROLLUP (YEAR(OrderDate), MONTH(OrderDate))
ORDER BY OrderYear, OrderMonth;
-- NULL in OrderMonth = year subtotal; NULL in both = grand total

-- Example 7: CUBE — all combinations of groupings
SELECT
    TerritoryID,
    YEAR(OrderDate) AS OrderYear,
    SUM(TotalDue)   AS Revenue
FROM Sales.SalesOrderHeader
GROUP BY CUBE (TerritoryID, YEAR(OrderDate))
ORDER BY TerritoryID, OrderYear;
