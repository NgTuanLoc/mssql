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
