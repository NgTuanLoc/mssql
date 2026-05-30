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
