USE AdventureWorks2022;
GO

-- Exercise 1: List all departments in the company.
-- Approach: straightforward SELECT from the department table.
SELECT DepartmentID, Name, GroupName
FROM HumanResources.Department
ORDER BY GroupName, Name;

-- Exercise 2: Find all products with a ListPrice of exactly $0.00.
-- Approach: = 0 works for MONEY type; 0.00 is equivalent.
SELECT ProductID, Name, ProductNumber
FROM Production.Product
WHERE ListPrice = 0;

-- Exercise 3: Show the top 5 most expensive products (by ListPrice).
-- Approach: ORDER BY DESC + TOP n; ties are broken arbitrarily without a tiebreaker.
SELECT TOP 5 Name, ListPrice
FROM Production.Product
ORDER BY ListPrice DESC;

-- Exercise 4: How many rows are in the Sales.SalesOrderHeader table?
-- Approach: COUNT(*) counts all rows including those with NULLs in any column.
SELECT COUNT(*) AS TotalOrders
FROM Sales.SalesOrderHeader;

-- Exercise 5: List all unique colors used by products (including NULL).
-- Approach: DISTINCT removes duplicates; NULL appears once in the result.
SELECT DISTINCT Color
FROM Production.Product
ORDER BY Color;


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Tables with a ModifiedDate column, with schema and total column count.
-- Reasoning: Decompose into "which tables have this column" and "how many columns does
--   each have" — two different grains of the same INFORMATION_SCHEMA.COLUMNS rowset.
--   An EXISTS correlated subquery marks the qualifying (schema, table) pairs without
--   collapsing them, so the outer GROUP BY can still count every column on the table.
--   Tempting-but-wrong: WHERE COLUMN_NAME = 'ModifiedDate' in the outer query — that
--   filters before GROUP BY, so COUNT(*) would return 1 for every table, always.
SELECT
    c.TABLE_SCHEMA AS TableSchema,
    c.TABLE_NAME   AS TableName,
    COUNT(*)       AS ColumnCount
FROM INFORMATION_SCHEMA.COLUMNS AS c
WHERE EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS AS c2
    WHERE c2.TABLE_SCHEMA = c.TABLE_SCHEMA
      AND c2.TABLE_NAME   = c.TABLE_NAME
      AND c2.COLUMN_NAME  = 'ModifiedDate'
)
GROUP BY c.TABLE_SCHEMA, c.TABLE_NAME
ORDER BY TableSchema, TableName;

-- Challenge 2: Top 10 most expensive products, ties included, vs. without ties.
-- Reasoning: TOP (10) alone cuts off arbitrarily inside a tie group — SQL Server picks
--   an arbitrary 10 of the equally-priced rows with no guarantee which. WITH TIES keeps
--   every row that shares the ORDER BY value of the last row admitted, which only means
--   something because ORDER BY ListPrice DESC is present (TOP...WITH TIES requires it).
--   Wrapping each version in COUNT(*) over a subquery is the simplest way to compare row
--   counts side by side without eyeballing two separate result sets.
SELECT TOP (10) Name, ListPrice
FROM Production.Product
ORDER BY ListPrice DESC;

SELECT TOP (10) WITH TIES Name, ListPrice
FROM Production.Product
ORDER BY ListPrice DESC;

SELECT
    (SELECT COUNT(*) FROM (SELECT TOP (10) ListPrice FROM Production.Product ORDER BY ListPrice DESC) AS NoTies)            AS NoTiesCount,
    (SELECT COUNT(*) FROM (SELECT TOP (10) WITH TIES ListPrice FROM Production.Product ORDER BY ListPrice DESC) AS WithTies) AS WithTiesCount;

-- Challenge 3: 5 largest tables in the database by row count.
-- Reasoning: sys.partitions has one row per (object, index, partition); index_id 0 is
--   a heap, 1 is the clustered index — exactly one of the two exists per table, so
--   filtering to IN (0,1) and summing p.rows (partitions can be split) gives the true
--   row count without double-counting nonclustered indexes, which also have rows in
--   sys.partitions at higher index_ids. Tempting-but-wrong: skip the index_id filter —
--   that sums in every nonclustered index too and wildly overstates row counts.
SELECT TOP (5)
    s.name      AS SchemaName,
    t.name      AS TableName,
    SUM(p.rows) AS RowCnt
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.partitions AS p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
GROUP BY s.name, t.name
ORDER BY RowCnt DESC;
