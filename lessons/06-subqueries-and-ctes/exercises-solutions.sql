USE AdventureWorks2022;
GO

-- Exercise 1: Products above average ListPrice.
-- Approach: scalar subquery in WHERE computes the average once.
SELECT Name, ListPrice
FROM Production.Product
WHERE ListPrice > (SELECT AVG(ListPrice) FROM Production.Product WHERE ListPrice > 0)
ORDER BY ListPrice DESC;

-- Exercise 2: Customers who ordered at least one Bike.
-- Approach: EXISTS terminates on the first match, which is efficient for large tables.
SELECT c.CustomerID
FROM Sales.Customer AS c
WHERE EXISTS (
    SELECT 1
    FROM Sales.SalesOrderHeader  AS soh
    JOIN Sales.SalesOrderDetail  AS sod ON sod.SalesOrderID        = soh.SalesOrderID
    JOIN Production.Product      AS p   ON p.ProductID             = sod.ProductID
    JOIN Production.ProductSubcategory AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
    WHERE soh.CustomerID = c.CustomerID
      AND ps.ProductCategoryID = 1
);

-- Exercise 3: CTE for top 10 customers, joined to Person.
-- Approach: CTE named TopCustomers isolates the aggregation; outer query adds the name.
WITH TopCustomers AS (
    SELECT TOP 10
        soh.CustomerID,
        SUM(soh.TotalDue) AS TotalSpend
    FROM Sales.SalesOrderHeader AS soh
    GROUP BY soh.CustomerID
    ORDER BY TotalSpend DESC
)
SELECT
    tc.CustomerID,
    p.FirstName + ' ' + p.LastName AS FullName,
    tc.TotalSpend
FROM TopCustomers AS tc
JOIN Sales.Customer AS c ON c.CustomerID       = tc.CustomerID
JOIN Person.Person  AS p ON p.BusinessEntityID = c.PersonID
ORDER BY tc.TotalSpend DESC;

-- Exercise 4: Most recent order date per customer via correlated subquery.
-- Approach: correlated subquery references the outer row's CustomerID each iteration.
SELECT
    CustomerID,
    (SELECT MAX(soh2.OrderDate)
     FROM Sales.SalesOrderHeader AS soh2
     WHERE soh2.CustomerID = soh.CustomerID) AS LatestOrderDate
FROM Sales.SalesOrderHeader AS soh
GROUP BY CustomerID   -- one row per customer
ORDER BY LatestOrderDate DESC;

-- Exercise 5: Recursive CTE to generate 1..10.
-- Approach: anchor returns 1; recursive member adds 1 each time until > 10.
WITH Numbers AS (
    SELECT 1 AS n
    UNION ALL
    SELECT n + 1 FROM Numbers WHERE n < 10
)
SELECT n FROM Numbers;
