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
