USE AdventureWorks2022;
GO

-- Exercise 1: List all departments in the company.
-- Expected columns: DepartmentID, Name, GroupName
-- Hint: HumanResources.Department
-- Your query here:


-- Exercise 2: Find all products with a ListPrice of exactly $0.00.
-- Expected columns: ProductID, Name, ProductNumber
-- Your query here:


-- Exercise 3: Show the top 5 most expensive products (by ListPrice).
-- Expected columns: Name, ListPrice
-- Your query here:


-- Exercise 4: How many rows are in the Sales.SalesOrderHeader table?
-- Expected columns: TotalOrders (one row, one column)
-- Your query here:


-- Exercise 5: List all unique colors used by products.
-- Expected columns: Color
-- Hint: some products have NULL color — include them.
-- Your query here:


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Using INFORMATION_SCHEMA.COLUMNS, list every table in AdventureWorks2022
-- that has a column named ModifiedDate, with the table's schema and total column count.
-- Expected columns: TableSchema, TableName, ColumnCount
-- Hint: Filter for the column, then aggregate per table.
-- Your query here:


-- Challenge 2: Show the 10 most expensive products, but include ties: if products 10
-- and 11 share a price, show both. Compare row counts with and without WITH TIES.
-- Expected columns: Name, ListPrice
-- Hint: TOP (10) WITH TIES requires ORDER BY.
-- Your query here:


-- Challenge 3: Find the 5 largest tables in the database by row count using
-- sys.tables, sys.schemas and sys.partitions (index_id 0 or 1).
-- Expected columns: SchemaName, TableName, RowCnt
-- Your query here:
