USE AdventureWorks2022;
GO

-- Example 1: INNER JOIN — only matching rows
SELECT l.ID, l.Value AS LeftVal, r.Value AS RightVal
FROM lesson04.Left  AS l
INNER JOIN lesson04.Right AS r ON r.ID = l.ID;
-- Result: rows 2 and 3 only

-- Example 2: LEFT JOIN — all left rows; NULL for unmatched right
SELECT l.ID, l.Value AS LeftVal, r.Value AS RightVal
FROM lesson04.Left  AS l
LEFT JOIN lesson04.Right AS r ON r.ID = l.ID;
-- Result: rows 1 (NULL right), 2, 3

-- Example 3: RIGHT JOIN — all right rows
SELECT l.Value AS LeftVal, r.ID, r.Value AS RightVal
FROM lesson04.Left  AS l
RIGHT JOIN lesson04.Right AS r ON r.ID = l.ID;
-- Result: rows 2, 3, 4 (NULL left)

-- Example 4: FULL JOIN — all rows from both sides
SELECT l.ID AS LeftID, l.Value AS LeftVal, r.ID AS RightID, r.Value AS RightVal
FROM lesson04.Left  AS l
FULL JOIN lesson04.Right AS r ON r.ID = l.ID;
-- Result: rows 1 (NULL right), 2, 3, 4 (NULL left)

-- Example 5: Real-world multi-table join
SELECT
    soh.SalesOrderID,
    soh.OrderDate,
    p.FirstName + ' ' + p.LastName AS CustomerName,
    pr.Name                         AS ProductName,
    sod.OrderQty,
    sod.UnitPrice
FROM Sales.SalesOrderHeader  AS soh
JOIN Sales.Customer          AS c   ON c.CustomerID        = soh.CustomerID
JOIN Person.Person           AS p   ON p.BusinessEntityID  = c.PersonID
JOIN Sales.SalesOrderDetail  AS sod ON sod.SalesOrderID    = soh.SalesOrderID
JOIN Production.Product      AS pr  ON pr.ProductID        = sod.ProductID
WHERE soh.SalesOrderID = 43659;

-- Example 6: UNION ALL vs UNION
-- UNION ALL — keeps duplicates, faster
SELECT ProductID FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43659
UNION ALL
SELECT ProductID FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43660;

-- UNION — removes duplicates
SELECT ProductID FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43659
UNION
SELECT ProductID FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43660;

-- Example 7: EXCEPT — products sold but never purchased
SELECT ProductID FROM Sales.SalesOrderDetail
EXCEPT
SELECT ProductID FROM Purchasing.PurchaseOrderDetail;

-- Example 8: INTERSECT — products both sold and purchased
SELECT ProductID FROM Sales.SalesOrderDetail
INTERSECT
SELECT ProductID FROM Purchasing.PurchaseOrderDetail;
