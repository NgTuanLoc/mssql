USE AdventureWorks2022;
GO

-- Exercise 1: Rank all products by ListPrice descending within each subcategory.
--             Use DENSE_RANK so there are no gaps in the ranking.
-- Expected columns: SubcategoryName, ProductName, ListPrice, PriceRank
-- Tables: Production.Product, Production.ProductSubcategory
-- Your query here:


-- Exercise 2: For each sales order, show TotalDue and the running total of TotalDue
--             ordered by OrderDate for ALL orders (no partition — one window for everything).
-- Expected columns: SalesOrderID, OrderDate, TotalDue, RunningTotal
-- Your query here:


-- Exercise 3: Using LAG, show each order's TotalDue and the TotalDue of the
--             PREVIOUS order for the SAME customer (ordered by OrderDate).
--             Show NULL when there is no previous order.
-- Expected columns: CustomerID, SalesOrderID, OrderDate, TotalDue, PrevOrderAmount
-- Your query here:


-- Exercise 4: Return only the MOST RECENT order for each customer
--             (use ROW_NUMBER to pick the latest order).
-- Expected columns: CustomerID, SalesOrderID, OrderDate, TotalDue
-- Your query here:


-- Exercise 5: Using lesson07.MonthlySales, calculate each month's Revenue
--             as a percentage of that region's TOTAL revenue across all months.
-- Expected columns: Region, SaleYear, SaleMonth, Revenue, PctOfRegionTotal
-- Your query here:


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Using Sales.SalesOrderHeader, find each salesperson's longest streak
--              of consecutive months with month-over-month increasing total sales.
--              A streak breaks on a down-or-flat month OR a gap month (a calendar
--              month with no orders). Report each salesperson's best streak length
--              and the months it spanned.
-- Expected columns: SalesPersonID, StreakLengthMonths, StreakStart, StreakEnd
-- Hint: Aggregate to salesperson-month first; LAG for the comparison; the difference
--       of two ROW_NUMBERs (or a running flag-sum) groups consecutive rising months
--       into islands — gaps-and-islands. Remember SalesPersonID is NULL for online orders.
-- Your query here:


-- Challenge 2: For each customer with 5 or more orders: their top-3 orders by TotalDue,
--              and each of those orders' percentage of that customer's lifetime spend.
-- Expected columns: CustomerID, SalesOrderID, TotalDue, PctOfLifetimeSpend, OrderRank
-- Your query here:


-- Challenge 3: Using lesson07.MonthlySales, compute each region's 3-month moving
--              average of revenue, plus a flag column marking months where revenue
--              fell more than 20% below that moving average.
-- Expected columns: Region, SaleYear, SaleMonth, Revenue, MovAvg3, IsSlumpMonth
-- Your query here:


-- Challenge 4: Split all customers into spending quartiles with NTILE(4) over lifetime
--              TotalDue, then report one row per quartile: customer count and the
--              min / avg / max lifetime spend in that quartile.
-- Expected columns: SpendQuartile, CustomerCount, MinSpend, AvgSpend, MaxSpend
-- Your query here:
