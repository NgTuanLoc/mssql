USE AdventureWorks2022;
GO

-- Exercise 1: Using a subquery, find all products whose ListPrice is above
--             the average ListPrice of ALL products.
-- Expected columns: Name, ListPrice
-- Your query here:


-- Exercise 2: Using EXISTS, find all customers who have ordered at least one
--             product from ProductCategoryID = 1 (Bikes).
-- Expected columns: CustomerID
-- Tables: Sales.Customer, Sales.SalesOrderHeader, Sales.SalesOrderDetail,
--         Production.Product, Production.ProductSubcategory
-- Your query here:


-- Exercise 3: Using a CTE named TopCustomers, identify the top 10 customers
--             by total spend, then join back to Person.Person to get their names.
-- Expected columns: CustomerID, FullName, TotalSpend
-- Your query here:


-- Exercise 4: Without using a JOIN, use a correlated subquery to find
--             the most recent OrderDate for each CustomerID in Sales.SalesOrderHeader.
-- Expected columns: CustomerID, LatestOrderDate
-- Your query here:


-- Exercise 5: Using a recursive CTE, generate a sequence of integers from 1 to 10.
-- Expected columns: n
-- Your query here:
