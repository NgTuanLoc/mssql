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


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Full management chain: for every employee, a path string from
--              the CEO down to them ('CEO > VP > Manager > Employee') and
--              their depth, via recursive CTE over HumanResources.Employee's
--              OrganizationNode/JobTitle (parent = direct manager).
-- Expected columns: BusinessEntityID, JobTitle, Depth, ChainPath
-- Hint: Build the path in the recursive member: parent.ChainPath + ' > ' +
--       child title; CAST path as NVARCHAR(4000) in both members to match types.
-- Your query here:


-- Challenge 2: Products that have NEVER been ordered by a store
--              (Sales.Customer.StoreID IS NOT NULL) — with NOT EXISTS. In the
--              comment, explain why NOT IN would be risky here.
-- Expected columns: ProductID, Name
-- Your query here:


-- Challenge 3: Generate a complete month calendar for 2013 (12 rows) with a
--              recursive CTE, then LEFT JOIN monthly order totals so months
--              with zero orders still show — verify no month is missing.
-- Expected columns: MonthStart, OrderCount, Revenue
-- Hint: Anchor '2013-01-01', DATEADD month +1 until '2013-12-01'.
-- Your query here:


-- Challenge 4: For each territory, the single customer with the highest
--              lifetime TotalDue — solved with a correlated subquery in
--              WHERE, and a comment on which lesson-07 pattern replaces this
--              more efficiently.
-- Expected columns: TerritoryID, CustomerID, LifetimeSpend
-- Hint: WHERE spend = (SELECT MAX ... same territory).
-- Your query here:
