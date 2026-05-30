USE AdventureWorks2022;
GO

-- Example 1: ROW_NUMBER — unique row per customer ordered by spend
SELECT
    CustomerID,
    TotalDue,
    ROW_NUMBER() OVER (
        PARTITION BY CustomerID
        ORDER BY TotalDue DESC
    ) AS OrderRankForCustomer
FROM Sales.SalesOrderHeader;

-- Example 2: RANK vs DENSE_RANK — ties handled differently
SELECT
    ProductID,
    ListPrice,
    RANK()       OVER (ORDER BY ListPrice DESC) AS Rnk,       -- gaps after ties
    DENSE_RANK() OVER (ORDER BY ListPrice DESC) AS DenseRnk   -- no gaps
FROM Production.Product
WHERE ListPrice > 0;

-- Example 3: Top-N per group using ROW_NUMBER
SELECT *
FROM (
    SELECT
        CustomerID,
        SalesOrderID,
        TotalDue,
        ROW_NUMBER() OVER (
            PARTITION BY CustomerID
            ORDER BY TotalDue DESC
        ) AS rn
    FROM Sales.SalesOrderHeader
) AS ranked
WHERE rn <= 2;  -- top 2 orders per customer

-- Example 4: LAG and LEAD — month-over-month change
SELECT
    SaleYear,
    SaleMonth,
    Region,
    Revenue,
    LAG(Revenue,  1, 0) OVER (PARTITION BY Region ORDER BY SaleYear, SaleMonth) AS PrevMonthRevenue,
    LEAD(Revenue, 1, 0) OVER (PARTITION BY Region ORDER BY SaleYear, SaleMonth) AS NextMonthRevenue,
    Revenue - LAG(Revenue, 1, 0) OVER (PARTITION BY Region ORDER BY SaleYear, SaleMonth) AS MoMChange
FROM lesson07.MonthlySales
ORDER BY Region, SaleYear, SaleMonth;

-- Example 5: Running total (cumulative sum)
SELECT
    SaleYear,
    SaleMonth,
    Region,
    Revenue,
    SUM(Revenue) OVER (
        PARTITION BY Region
        ORDER BY SaleYear, SaleMonth
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS CumulativeRevenue
FROM lesson07.MonthlySales
ORDER BY Region, SaleYear, SaleMonth;

-- Example 6: 3-month moving average
SELECT
    SaleYear,
    SaleMonth,
    Region,
    Revenue,
    AVG(Revenue) OVER (
        PARTITION BY Region
        ORDER BY SaleYear, SaleMonth
        ROWS BETWEEN 2 PRECEDING AND CURRENT ROW
    ) AS Moving3MonthAvg
FROM lesson07.MonthlySales
ORDER BY Region, SaleYear, SaleMonth;

-- Example 7: FIRST_VALUE and LAST_VALUE
SELECT
    SaleYear,
    SaleMonth,
    Region,
    Revenue,
    FIRST_VALUE(Revenue) OVER (
        PARTITION BY Region, SaleYear
        ORDER BY SaleMonth
        ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING
    ) AS JanRevenue,
    LAST_VALUE(Revenue) OVER (
        PARTITION BY Region, SaleYear
        ORDER BY SaleMonth
        ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING
    ) AS LastMonthRevenue
FROM lesson07.MonthlySales
ORDER BY Region, SaleYear, SaleMonth;
