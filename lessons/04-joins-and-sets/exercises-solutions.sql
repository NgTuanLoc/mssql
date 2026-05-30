USE AdventureWorks2022;
GO

-- Exercise 1: Products with their subcategory (include products with no subcategory).
-- Approach: LEFT JOIN keeps products even when ProductSubcategoryID is NULL.
SELECT
    p.ProductID,
    p.Name          AS ProductName,
    ps.Name         AS SubcategoryName
FROM Production.Product            AS p
LEFT JOIN Production.ProductSubcategory AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
ORDER BY SubcategoryName, ProductName;

-- Exercise 2: Sales orders with customer full name (only where person record exists).
-- Approach: INNER JOINs drop rows with no match; three-table chain through Customer → Person.
SELECT
    soh.SalesOrderID,
    soh.OrderDate,
    p.FirstName + ' ' + p.LastName AS CustomerFullName
FROM Sales.SalesOrderHeader AS soh
JOIN Sales.Customer          AS c ON c.CustomerID       = soh.CustomerID
JOIN Person.Person           AS p ON p.BusinessEntityID = c.PersonID
ORDER BY soh.OrderDate;

-- Exercise 3: Employees who have never placed a purchase order.
-- Approach: LEFT JOIN to PurchaseOrderHeader; rows with NULL EmployeeID have no PO.
SELECT e.BusinessEntityID, e.JobTitle
FROM HumanResources.Employee      AS e
LEFT JOIN Purchasing.PurchaseOrderHeader AS poh ON poh.EmployeeID = e.BusinessEntityID
WHERE poh.EmployeeID IS NULL
ORDER BY e.BusinessEntityID;

-- Exercise 4: CROSS JOIN of Left and Right (3 × 3 = 9 rows).
SELECT
    l.ID    AS LeftID,
    l.Value AS LeftValue,
    r.ID    AS RightID,
    r.Value AS RightValue
FROM lesson04.Left  AS l
CROSS JOIN lesson04.Right AS r;

-- Exercise 5: UNION ALL of person names and vendor names with a source label.
-- Approach: UNION ALL preserves all rows; add a literal string column for the source.
SELECT FirstName + ' ' + LastName AS Name, 'Person' AS Source
FROM Person.Person
UNION ALL
SELECT Name, 'Vendor'
FROM Purchasing.Vendor
ORDER BY Name;
