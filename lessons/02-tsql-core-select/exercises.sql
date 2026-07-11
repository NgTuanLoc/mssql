USE AdventureWorks2022;
GO

-- Exercise 1: Find all employees whose job title contains the word 'Manager'.
-- Expected columns: BusinessEntityID, JobTitle
-- Table: HumanResources.Employee
-- Your query here:


-- Exercise 2: List products in subcategory 1, 2, or 3 with a ListPrice above $100.
-- Expected columns: Name, ProductSubcategoryID, ListPrice
-- Your query here:


-- Exercise 3: Find orders placed between 2013-01-01 and 2013-03-31 (inclusive).
-- Expected columns: SalesOrderID, OrderDate, TotalDue
-- Table: Sales.SalesOrderHeader
-- Your query here:


-- Exercise 4: Find products that have NO size specified (Size IS NULL) AND no weight (Weight IS NULL).
-- Expected columns: ProductID, Name
-- Your query here:


-- Exercise 5: Using OFFSET/FETCH, return rows 21–30 of products ordered by Name ascending.
-- Expected columns: ProductID, Name
-- Your query here:


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Build page 3 of a product listing: 10 rows per page, ordered by
-- ListPrice DESC. The ordering must be fully deterministic even when prices tie.
-- Expected columns: ProductID, Name, ListPrice
-- Hint: Add a unique tie-breaker to ORDER BY; OFFSET 20 ROWS FETCH NEXT 10.
-- Your query here:


-- Challenge 2: Return all products that are NOT red — where "not red" must include
-- products whose Color is unknown (NULL). Then explain (comment) why the naive
-- `<> 'Red'` version returns fewer rows.
-- Expected columns: ProductID, Name, Color
-- Your query here:


-- Challenge 3: Find products whose Name contains a literal percent sign or underscore
-- anywhere, using an ESCAPE clause. Seed check: if none exist in AdventureWorks, search
-- for names containing a hyphen followed by a digit instead (e.g. 'Mountain-100').
-- Expected columns: ProductID, Name
-- Hint: LIKE '%!%%' ESCAPE '!'
-- Your query here:


-- Challenge 4: List the orders placed in the last 30 days of available data — "last 30
-- days" computed from MAX(OrderDate), not hardcoded — using a half-open range
-- (>= start AND < end), never BETWEEN.
-- Expected columns: SalesOrderID, OrderDate, TotalDue
-- Hint: Scalar subquery or variable for the max date.
-- Your query here:
