USE AdventureWorks2022;
GO

-- Exercise 1: List every product with its subcategory name.
--             Include products that have no subcategory (NULL subcategory).
-- Expected columns: ProductID, ProductName, SubcategoryName
-- Tables: Production.Product, Production.ProductSubcategory
-- Your query here:


-- Exercise 2: List every sales order with the customer's full name.
--             Only include orders where a matching person record exists.
-- Expected columns: SalesOrderID, OrderDate, CustomerFullName
-- Tables: Sales.SalesOrderHeader, Sales.Customer, Person.Person
-- Your query here:


-- Exercise 3: Find all employees who have NEVER placed a purchase order.
-- Expected columns: BusinessEntityID, JobTitle
-- Tables: HumanResources.Employee, Purchasing.PurchaseOrderHeader
-- Hint: use a LEFT JOIN checking for NULL, or NOT EXISTS / EXCEPT.
-- Your query here:


-- Exercise 4: Show the CROSS JOIN of lesson04.Left and lesson04.Right.
--             How many rows do you expect?
-- Expected columns: LeftID, LeftValue, RightID, RightValue
-- Your query here:


-- Exercise 5: Using UNION ALL, combine the first names of all people
--             (Person.Person) with all contact names in the Vendor table
--             (Purchasing.Vendor, column: Name). Label which table each row
--             comes from.
-- Expected columns: Name, Source
-- Your query here:


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: The fan-out trap, fixed: total 2013 revenue per product from
--              Sales.SalesOrderDetail joined with per-order freight share.
--              First write the naive join of header+detail that inflates
--              freight, show the wrong total in a comment, then fix it by
--              pre-aggregating detail per order in a derived table/CTE.
-- Expected columns: ProductID, Revenue, FreightShare
-- Hint: Aggregate before joining, not after.
-- Your query here:


-- Challenge 2: Customers who ordered in 2013 but not in 2014 — solve it
--              twice: once with EXCEPT, once with a LEFT JOIN ... IS NULL
--              anti-join, and confirm both return the same row count.
-- Expected columns: CustomerID
-- Your query here:


-- Challenge 3: Full reconciliation: every product with its total quantity
--              sold via online orders and via offline orders side by side,
--              including products sold through only one channel or neither.
-- Expected columns: ProductID, Name, OnlineQty, OfflineQty
-- Hint: Two pre-aggregated sets, FULL OUTER JOIN or LEFT JOINs from Product;
--       COALESCE the quantities to 0.
-- Your query here:


-- Challenge 4: Non-equi join: build a price-band reference with
--              VALUES (('Budget',0,100),('Mid',100,1000),('Premium',1000,10000))
--              and count products per band, joining on a range predicate —
--              no CASE expression.
-- Expected columns: BandName, ProductCount
-- Hint: JOIN ... ON ListPrice >= low AND ListPrice < high.
-- Your query here:
