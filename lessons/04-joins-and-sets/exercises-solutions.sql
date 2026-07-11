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
-- Left/Right are reserved keywords, so the table names need bracket-quoting.
SELECT
    l.ID    AS LeftID,
    l.Value AS LeftValue,
    r.ID    AS RightID,
    r.Value AS RightValue
FROM lesson04.[Left]  AS l
CROSS JOIN lesson04.[Right] AS r;

-- Exercise 5: UNION ALL of person names and vendor names with a source label.
-- Approach: UNION ALL preserves all rows; add a literal string column for the source.
SELECT FirstName + ' ' + LastName AS Name, 'Person' AS Source
FROM Person.Person
UNION ALL
SELECT Name, 'Vendor'
FROM Purchasing.Vendor
ORDER BY Name;


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Fan-out trap, fixed.
-- Reasoning: Naive version joins header to every detail row, so SUM(Freight) repeats
-- the whole order's freight once per line — inflated for multi-line orders. Fix:
-- pre-aggregate SalesOrderDetail into one row per order (OrderRevenue CTE) first, then
-- allocate each order's Freight to its lines proportionally by line revenue share
-- before joining to the header. Aggregating after the join (the tempting approach)
-- can't undo the duplication; you must aggregate the many-side before it meets Freight.
-- Naive (wrong) for comparison — inflates freight, e.g. ProductID 707: ~456603 vs correct ~2053:
--   SELECT sod.ProductID, SUM(soh.Freight) AS NaiveFreightGuess
--   FROM Sales.SalesOrderHeader soh
--   JOIN Sales.SalesOrderDetail sod ON sod.SalesOrderID = soh.SalesOrderID
--   WHERE YEAR(soh.OrderDate) = 2013
--   GROUP BY sod.ProductID;
WITH OrderRevenue AS (
    SELECT SalesOrderID, SUM(OrderQty * UnitPrice * (1 - UnitPriceDiscount)) AS OrderRevenue
    FROM Sales.SalesOrderDetail
    GROUP BY SalesOrderID
),
LineFreight AS (
    SELECT
        sod.ProductID,
        sod.OrderQty * sod.UnitPrice * (1 - sod.UnitPriceDiscount) AS LineRevenue,
        soh.Freight * (sod.OrderQty * sod.UnitPrice * (1 - sod.UnitPriceDiscount))
            / NULLIF(orr.OrderRevenue, 0) AS LineFreightShare
    FROM Sales.SalesOrderDetail AS sod
    JOIN Sales.SalesOrderHeader AS soh ON soh.SalesOrderID = sod.SalesOrderID
    JOIN OrderRevenue AS orr           ON orr.SalesOrderID = sod.SalesOrderID
    WHERE YEAR(soh.OrderDate) = 2013
)
SELECT ProductID, SUM(LineRevenue) AS Revenue, SUM(LineFreightShare) AS FreightShare
FROM LineFreight
GROUP BY ProductID
ORDER BY ProductID;

-- Challenge 2: 2013-not-2014 customers, two ways.
-- Reasoning: EXCEPT is the direct set-difference reading of the question. The anti-join
-- alternative (LEFT JOIN back to 2014 orders, keep rows where the right side is NULL)
-- proves the same result via row matching instead of set algebra. Both must return the
-- same row count — that's the point of solving it twice, not a stylistic choice.
SELECT CustomerID FROM Sales.SalesOrderHeader WHERE YEAR(OrderDate) = 2013
EXCEPT
SELECT CustomerID FROM Sales.SalesOrderHeader WHERE YEAR(OrderDate) = 2014;

SELECT DISTINCT soh13.CustomerID
FROM Sales.SalesOrderHeader AS soh13
LEFT JOIN Sales.SalesOrderHeader AS soh14
    ON soh14.CustomerID = soh13.CustomerID AND YEAR(soh14.OrderDate) = 2014
WHERE YEAR(soh13.OrderDate) = 2013 AND soh14.CustomerID IS NULL;

-- Confirmation: both approaches return the same row count (verified: 8312 and 8312).
SELECT COUNT(*) AS ExceptCount
FROM (
    SELECT CustomerID FROM Sales.SalesOrderHeader WHERE YEAR(OrderDate) = 2013
    EXCEPT
    SELECT CustomerID FROM Sales.SalesOrderHeader WHERE YEAR(OrderDate) = 2014
) AS x;

SELECT COUNT(*) AS AntiJoinCount
FROM (
    SELECT DISTINCT soh13.CustomerID
    FROM Sales.SalesOrderHeader AS soh13
    LEFT JOIN Sales.SalesOrderHeader AS soh14
        ON soh14.CustomerID = soh13.CustomerID AND YEAR(soh14.OrderDate) = 2014
    WHERE YEAR(soh13.OrderDate) = 2013 AND soh14.CustomerID IS NULL
) AS x;

-- Challenge 3: Online vs offline reconciliation.
-- Reasoning: Pre-aggregate quantity per product for each channel separately (two CTEs
-- filtered on OnlineOrderFlag), then LEFT JOIN both onto the full Product list so
-- products sold in only one channel — or never sold at all — still appear. A single
-- join with a CASE-based pivot would double-count lines whenever a product was sold
-- through both channels; keeping the aggregation separate per channel avoids that.
WITH OnlineQty AS (
    SELECT sod.ProductID, SUM(sod.OrderQty) AS Qty
    FROM Sales.SalesOrderDetail AS sod
    JOIN Sales.SalesOrderHeader AS soh ON soh.SalesOrderID = sod.SalesOrderID
    WHERE soh.OnlineOrderFlag = 1
    GROUP BY sod.ProductID
),
OfflineQty AS (
    SELECT sod.ProductID, SUM(sod.OrderQty) AS Qty
    FROM Sales.SalesOrderDetail AS sod
    JOIN Sales.SalesOrderHeader AS soh ON soh.SalesOrderID = sod.SalesOrderID
    WHERE soh.OnlineOrderFlag = 0
    GROUP BY sod.ProductID
)
SELECT
    p.ProductID,
    p.Name,
    COALESCE(o.Qty, 0) AS OnlineQty,
    COALESCE(f.Qty, 0) AS OfflineQty
FROM Production.Product AS p
LEFT JOIN OnlineQty  AS o ON o.ProductID = p.ProductID
LEFT JOIN OfflineQty AS f ON f.ProductID = p.ProductID
ORDER BY p.ProductID;

-- Challenge 4: Price-band non-equi join.
-- Reasoning: A VALUES constructor makes a tiny 3-row reference table; joining on
-- ListPrice >= low AND ListPrice < high matches each product to exactly one band by
-- range instead of equality, so no CASE expression is needed. GROUP BY the band name
-- to count. Bands are half-open [low, high) so a price landing exactly on a boundary
-- (e.g. 100.00) falls into the higher band, not both.
SELECT b.BandName, COUNT(p.ProductID) AS ProductCount
FROM (VALUES ('Budget', 0, 100), ('Mid', 100, 1000), ('Premium', 1000, 10000))
    AS b(BandName, LowPrice, HighPrice)
JOIN Production.Product AS p ON p.ListPrice >= b.LowPrice AND p.ListPrice < b.HighPrice
GROUP BY b.BandName
ORDER BY MIN(b.LowPrice);
