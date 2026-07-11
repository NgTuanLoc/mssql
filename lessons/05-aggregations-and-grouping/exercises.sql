USE AdventureWorks2022;
GO

-- Exercise 1: How many products exist in each product category?
--             Join through ProductSubcategory to get to ProductCategory.
-- Expected columns: CategoryName, ProductCount
-- Tables: Production.Product, Production.ProductSubcategory, Production.ProductCategory
-- Your query here:


-- Exercise 2: Find the top 3 sales territories by total revenue in 2013.
-- Expected columns: TerritoryID, TotalRevenue
-- Table: Sales.SalesOrderHeader
-- Your query here:


-- Exercise 3: Find customers who placed more than 10 orders AND whose average
--             order value exceeded $2,000.
-- Expected columns: CustomerID, OrderCount, AvgOrderValue
-- Your query here:


-- Exercise 4: Using ROLLUP, show total sales by year and by overall grand total.
--             Only include years 2012–2014.
-- Expected columns: OrderYear, TotalSales
-- (Grand total row will have NULL OrderYear)
-- Your query here:


-- Exercise 5: For each product subcategory, show the most expensive product's ListPrice
--             and the average ListPrice. Include only subcategories with avg ListPrice > $200.
-- Expected columns: SubcategoryName, MaxPrice, AvgPrice
-- Your query here:


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: One result set with revenue at three levels — per (territory, year),
--              per territory subtotal, and grand total — using GROUPING SETS, with a
--              readable Level column ('Detail'/'Territory total'/'Grand total') derived
--              from GROUPING_ID.
-- Expected columns: TerritoryName, OrderYear, Revenue, Level
-- Your query here:


-- Challenge 2: The correct "average order value" per territory: SUM(TotalDue)/COUNT(DISTINCT
--              SalesOrderID), shown next to the naive AVG(TotalDue) over a header+detail
--              join, with a comment explaining why they differ.
-- Expected columns: TerritoryName, NaiveAvg, CorrectAvg
-- Hint: The join fans out headers; AVG then weights big orders by line count.
-- Your query here:


-- Challenge 3: A matrix of order counts: one row per territory, one column per year
--              2011–2014, built with conditional aggregation (SUM(CASE...)).
-- Expected columns: TerritoryName, Y2011, Y2012, Y2013, Y2014
-- Your query here:


-- Challenge 4: Territories whose 2013 revenue exceeded their 2012 revenue by more than
--              10% — aggregate per territory-year first, then compare years. Decide
--              (and justify in the comment) whether the comparison belongs in HAVING or
--              in an outer query.
-- Expected columns: TerritoryName, Rev2012, Rev2013, GrowthPct
-- Hint: Conditional aggregation puts both years on one row.
-- Your query here:
