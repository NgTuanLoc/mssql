USE AdventureWorks2022;
GO

-- Exercise 1: Employees whose job title contains 'Manager'.
-- Approach: LIKE with % on both sides matches anywhere in the string.
SELECT BusinessEntityID, JobTitle
FROM HumanResources.Employee
WHERE JobTitle LIKE '%Manager%'
ORDER BY JobTitle;

-- Exercise 2: Products in subcategories 1–3 with ListPrice > $100.
-- Approach: IN for the subcategory set, AND for the price filter.
SELECT Name, ProductSubcategoryID, ListPrice
FROM Production.Product
WHERE ProductSubcategoryID IN (1, 2, 3)
  AND ListPrice > 100
ORDER BY ListPrice DESC;

-- Exercise 3: Orders in Q1 2013.
-- Approach: BETWEEN is inclusive; use DATE literals for clarity.
SELECT SalesOrderID, OrderDate, TotalDue
FROM Sales.SalesOrderHeader
WHERE OrderDate BETWEEN '2013-01-01' AND '2013-03-31'
ORDER BY OrderDate;

-- Exercise 4: Products with neither size nor weight.
-- Approach: two IS NULL conditions joined by AND.
SELECT ProductID, Name
FROM Production.Product
WHERE Size IS NULL
  AND Weight IS NULL;

-- Exercise 5: Products 21–30 by name (zero-based offset = 20).
-- Approach: OFFSET n ROWS skips n rows; FETCH NEXT m ROWS ONLY takes m.
SELECT ProductID, Name
FROM Production.Product
ORDER BY Name
OFFSET 20 ROWS
FETCH NEXT 10 ROWS ONLY;
