USE AdventureWorks2022;
GO

-- Example 1: LIKE — products whose name starts with 'Road'
SELECT ProductID, Name, ListPrice
FROM Production.Product
WHERE Name LIKE 'Road%';

-- Example 2: IN — orders placed by specific customers
SELECT SalesOrderID, CustomerID, TotalDue
FROM Sales.SalesOrderHeader
WHERE CustomerID IN (11000, 11001, 11002);

-- Example 3: BETWEEN — products priced $500–$1000
SELECT Name, ListPrice
FROM Production.Product
WHERE ListPrice BETWEEN 500 AND 1000   -- inclusive on both ends
ORDER BY ListPrice;

-- Example 4: NULL semantics — products with no color specified
SELECT ProductID, Name, Color
FROM Production.Product
WHERE Color IS NULL;   -- WHERE Color = NULL never matches

-- Example 5: Multiple conditions with AND / OR
SELECT SalesOrderID, Status, TotalDue
FROM Sales.SalesOrderHeader
WHERE Status = 5          -- shipped
  AND TotalDue > 5000
ORDER BY TotalDue DESC;

-- Example 6: TOP with OFFSET/FETCH (page 2 of 10 rows per page)
SELECT ProductID, Name, ListPrice
FROM Production.Product
WHERE ListPrice > 0
ORDER BY ListPrice DESC
OFFSET 10 ROWS
FETCH NEXT 10 ROWS ONLY;

-- Example 7: Negation with NOT LIKE and NOT IN
SELECT Name
FROM Production.Product
WHERE Name NOT LIKE '%Road%'
  AND ProductSubcategoryID IS NOT NULL;
