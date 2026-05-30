USE AdventureWorks2022;
GO

-- Exercise 1: DENSE_RANK by ListPrice within subcategory.
-- Approach: PARTITION BY subcategory; ORDER BY ListPrice DESC; join for name.
SELECT
    ps.Name  AS SubcategoryName,
    p.Name   AS ProductName,
    p.ListPrice,
    DENSE_RANK() OVER (
        PARTITION BY p.ProductSubcategoryID
        ORDER BY p.ListPrice DESC
    ) AS PriceRank
FROM Production.Product             AS p
JOIN Production.ProductSubcategory  AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
WHERE p.ListPrice > 0
ORDER BY SubcategoryName, PriceRank;

-- Exercise 2: Running total of TotalDue across all orders by OrderDate.
-- Approach: No PARTITION; ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW for cumsum.
SELECT
    SalesOrderID,
    OrderDate,
    TotalDue,
    SUM(TotalDue) OVER (
        ORDER BY OrderDate
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS RunningTotal
FROM Sales.SalesOrderHeader
ORDER BY OrderDate;

-- Exercise 3: Previous order amount per customer using LAG.
-- Approach: PARTITION BY CustomerID; LAG with no default returns NULL for first order.
SELECT
    CustomerID,
    SalesOrderID,
    OrderDate,
    TotalDue,
    LAG(TotalDue) OVER (
        PARTITION BY CustomerID
        ORDER BY OrderDate
    ) AS PrevOrderAmount
FROM Sales.SalesOrderHeader
ORDER BY CustomerID, OrderDate;

-- Exercise 4: Most recent order per customer with ROW_NUMBER.
-- Approach: ROW_NUMBER descending by date; filter rn = 1 in outer query.
SELECT CustomerID, SalesOrderID, OrderDate, TotalDue
FROM (
    SELECT
        CustomerID,
        SalesOrderID,
        OrderDate,
        TotalDue,
        ROW_NUMBER() OVER (
            PARTITION BY CustomerID
            ORDER BY OrderDate DESC
        ) AS rn
    FROM Sales.SalesOrderHeader
) AS ranked
WHERE rn = 1
ORDER BY CustomerID;

-- Exercise 5: Revenue as % of region total.
-- Approach: SUM() OVER (PARTITION BY Region) with no ORDER BY = full partition sum.
SELECT
    Region,
    SaleYear,
    SaleMonth,
    Revenue,
    CAST(Revenue * 100.0
         / SUM(Revenue) OVER (PARTITION BY Region)
         AS DECIMAL(5,2)) AS PctOfRegionTotal
FROM lesson07.MonthlySales
ORDER BY Region, SaleYear, SaleMonth;
