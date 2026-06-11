USE AdventureWorks2022;
GO
-- Lesson 18 exercises. Run setup.sql first.
-- Write your answers below each exercise comment.

---------------------------------------------------------------------------
-- Exercise 1 (warm-up): Rewrite as a CTE pipeline.
-- The query below answers "products whose total sales beat the average
-- product's total sales" — but as an unreadable nest. Rewrite it as two
-- CTEs (ProductTotals, then the comparison) producing the same rows.
---------------------------------------------------------------------------
SELECT p.Name, t.TotalSold
FROM (SELECT ProductID, SUM(LineTotal) AS TotalSold
      FROM Sales.SalesOrderDetail GROUP BY ProductID) AS t
JOIN Production.Product AS p ON p.ProductID = t.ProductID
WHERE t.TotalSold > (SELECT AVG(x.TotalSold)
                     FROM (SELECT SUM(LineTotal) AS TotalSold
                           FROM Sales.SalesOrderDetail
                           GROUP BY ProductID) AS x);

---------------------------------------------------------------------------
-- Exercise 2 (layered aggregation): For each territory, compute the AVERAGE
-- MONTHLY revenue (average of the monthly SUMs of TotalDue), highest first.
-- Two pipeline steps: month sums, then the average of those.
---------------------------------------------------------------------------

---------------------------------------------------------------------------
-- Exercise 3 (top-N per group, ROW_NUMBER): For each territory, the top 3
-- customers by lifetime TotalDue. Output: territory name, CustomerID,
-- lifetime spend, rank.
---------------------------------------------------------------------------

---------------------------------------------------------------------------
-- Exercise 4 (top-N per group, APPLY): For each salesperson in
-- Sales.SalesPerson, their 3 most recent orders (SalesOrderID, OrderDate,
-- TotalDue). Salespeople with no orders must still appear.
-- Hint: which APPLY keeps left rows alive?
---------------------------------------------------------------------------

---------------------------------------------------------------------------
-- Exercise 5 (conditional aggregation): One row per territory, columns for
-- order COUNTS in 2012, 2013, 2014, plus a TotalOrders column.
-- No PIVOT allowed.
---------------------------------------------------------------------------

---------------------------------------------------------------------------
-- Exercise 6 (de-duplication): lesson18.CustomerStaging contains duplicate
-- emails from a re-sent ETL batch. DELETE the duplicates, keeping the row
-- with the newest LoadDate (break remaining ties by highest StagingID).
-- Verify: SELECT COUNT(*) afterwards should equal the number of distinct emails.
---------------------------------------------------------------------------

---------------------------------------------------------------------------
-- Exercise 7 (gaps & islands): Using lesson18.GymVisit, find each member's
-- LONGEST consecutive-day visit streak. Output: MemberID, StreakStart,
-- StreakEnd, StreakDays — one row per member.
---------------------------------------------------------------------------

---------------------------------------------------------------------------
-- Exercise 8 (final boss): The purchasing department wants the case-study
-- report for VENDORS: for each vendor — monthly purchase total (sum of
-- LineTotal from Purchasing.PurchaseOrderDetail), month-over-month growth %,
-- and the top 2 products by line total that month, with each product's
-- contribution % to that vendor's monthly total.
-- Structure it as a pipeline: lines → vendor-month totals → growth → APPLY top products.
---------------------------------------------------------------------------
