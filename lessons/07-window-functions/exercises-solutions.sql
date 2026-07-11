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


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Longest streak of consecutive rising months per salesperson.
-- Reasoning: Decompose into (a) collapse orders to salesperson-month totals,
--   (b) mark where a rising run STARTS, (c) group each run, (d) pick the longest.
--   A month continues a streak only if the previous row is the immediately prior
--   calendar month AND its total was lower — so LAG must check BOTH the month gap
--   and the value. The tempting-but-wrong approach is to LAG only on value: that
--   silently treats a gap month (no orders) as "consecutive" and over-counts. The
--   running SUM of the streak-start flag is a group id (the gaps-and-islands trick);
--   COUNT per group is the streak length, and MIN/MAX give the span.
WITH Monthly AS (
    SELECT SalesPersonID,
           DATEFROMPARTS(YEAR(OrderDate), MONTH(OrderDate), 1) AS MonthStart,
           SUM(TotalDue) AS MonthTotal
    FROM Sales.SalesOrderHeader
    WHERE SalesPersonID IS NOT NULL          -- online orders have no salesperson
    GROUP BY SalesPersonID, DATEFROMPARTS(YEAR(OrderDate), MONTH(OrderDate), 1)
),
Flagged AS (
    SELECT SalesPersonID, MonthStart, MonthTotal,
        CASE WHEN LAG(MonthStart) OVER (PARTITION BY SalesPersonID ORDER BY MonthStart)
                     = DATEADD(MONTH, -1, MonthStart)                    -- no gap month
              AND MonthTotal
                     > LAG(MonthTotal) OVER (PARTITION BY SalesPersonID ORDER BY MonthStart)
             THEN 0 ELSE 1 END AS IsStreakStart
    FROM Monthly
),
Grouped AS (
    SELECT SalesPersonID, MonthStart,
        SUM(IsStreakStart) OVER (PARTITION BY SalesPersonID ORDER BY MonthStart
                                 ROWS UNBOUNDED PRECEDING) AS StreakId
    FROM Flagged
),
Streaks AS (
    SELECT SalesPersonID, StreakId,
           COUNT(*)        AS StreakLengthMonths,
           MIN(MonthStart) AS StreakStart,
           MAX(MonthStart) AS StreakEnd
    FROM Grouped
    GROUP BY SalesPersonID, StreakId
)
SELECT SalesPersonID, StreakLengthMonths, StreakStart, StreakEnd
FROM (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY SalesPersonID
                 ORDER BY StreakLengthMonths DESC, StreakStart) AS rn
    FROM Streaks
) AS best
WHERE rn = 1                                  -- one longest streak per salesperson
ORDER BY StreakLengthMonths DESC, SalesPersonID;

-- Challenge 2: Top-3 orders per customer (5+ orders) with % of lifetime spend.
-- Reasoning: One pass computes three windows over the same PARTITION BY CustomerID:
--   COUNT(*) for the 5+ gate, SUM(TotalDue) for the lifetime denominator, and
--   ROW_NUMBER ordered by TotalDue DESC for the top-3. All three are windows so no
--   GROUP BY is needed and detail rows survive. Filtering happens in the outer query
--   because window functions are illegal in WHERE. The tempting-but-wrong approach is
--   a GROUP BY to get the count/sum, then a self-join back for the top 3 — more work
--   and it collapses the rows you still need.
WITH Ranked AS (
    SELECT CustomerID, SalesOrderID, TotalDue,
        COUNT(*)      OVER (PARTITION BY CustomerID) AS OrderCount,
        SUM(TotalDue) OVER (PARTITION BY CustomerID) AS LifetimeSpend,
        ROW_NUMBER()  OVER (PARTITION BY CustomerID ORDER BY TotalDue DESC) AS OrderRank
    FROM Sales.SalesOrderHeader
)
SELECT CustomerID, SalesOrderID, TotalDue,
       CAST(TotalDue * 100.0 / LifetimeSpend AS DECIMAL(5,2)) AS PctOfLifetimeSpend,
       OrderRank
FROM Ranked
WHERE OrderCount >= 5 AND OrderRank <= 3
ORDER BY CustomerID, OrderRank;

-- Challenge 3: 3-month moving average per region + a >20%-below-average slump flag.
-- Reasoning: Compute the moving average in a CTE (frame ROWS BETWEEN 2 PRECEDING AND
--   CURRENT ROW, per region), then compare Revenue against it in the outer query. The
--   flag can't live in the same SELECT as the window it depends on — a window function
--   can't be referenced by another expression in its own SELECT — so the CTE layer is
--   what makes the comparison legal. "20% below" means Revenue < 0.8 * MovAvg3. West's
--   month-4 dip (12000 vs a ~19333 average) is the row that trips the flag.
WITH M AS (
    SELECT Region, SaleYear, SaleMonth, Revenue,
        AVG(Revenue) OVER (PARTITION BY Region ORDER BY SaleYear, SaleMonth
                           ROWS BETWEEN 2 PRECEDING AND CURRENT ROW) AS MovAvg3
    FROM lesson07.MonthlySales
)
SELECT Region, SaleYear, SaleMonth, Revenue,
       CAST(MovAvg3 AS DECIMAL(14,2)) AS MovAvg3,
       CASE WHEN Revenue < 0.8 * MovAvg3 THEN 1 ELSE 0 END AS IsSlumpMonth
FROM M
ORDER BY Region, SaleYear, SaleMonth;

-- Challenge 4: NTILE(4) spending quartiles, then aggregate per quartile.
-- Reasoning: Two window layers separated by a grain change. First collapse orders to
--   one lifetime total per customer (GROUP BY), THEN apply NTILE(4) over that ordered
--   list to bucket customers. NTILE must run on the per-customer totals, not raw order
--   rows — bucketing raw orders would quartile transactions, not customers. Finally a
--   plain GROUP BY on the quartile label rolls each bucket up to count/min/avg/max.
WITH Lifetime AS (
    SELECT CustomerID, SUM(TotalDue) AS LifetimeSpend
    FROM Sales.SalesOrderHeader
    GROUP BY CustomerID
),
Quartiled AS (
    SELECT CustomerID, LifetimeSpend,
        NTILE(4) OVER (ORDER BY LifetimeSpend) AS SpendQuartile
    FROM Lifetime
)
SELECT SpendQuartile,
       COUNT(*)                                  AS CustomerCount,
       CAST(MIN(LifetimeSpend) AS DECIMAL(14,2)) AS MinSpend,
       CAST(AVG(LifetimeSpend) AS DECIMAL(14,2)) AS AvgSpend,
       CAST(MAX(LifetimeSpend) AS DECIMAL(14,2)) AS MaxSpend
FROM Quartiled
GROUP BY SpendQuartile
ORDER BY SpendQuartile;
