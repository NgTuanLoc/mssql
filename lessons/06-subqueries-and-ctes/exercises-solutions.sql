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


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Full management chain (path + depth) from the CEO down.
-- Reasoning: The CEO's OrganizationNode is NULL in this restore (not
-- hierarchyid::GetRoot() as documented), so GetAncestor() can never reach it
-- from below — anchoring on GetRoot() alone returns zero rows. Fix: seed the
-- CEO as its own literal anchor row (Depth 0), then let level-1 reports join
-- to it via a special-cased ON condition. CAST both path expressions to
-- NVARCHAR(4000) so the anchor and recursive member types match.
WITH ChainCTE AS (
    SELECT BusinessEntityID, JobTitle, OrganizationNode, 0 AS Depth,
           CAST(JobTitle AS NVARCHAR(4000)) AS ChainPath
    FROM HumanResources.Employee
    WHERE BusinessEntityID = 1

    UNION ALL

    SELECT e.BusinessEntityID, e.JobTitle, e.OrganizationNode, c.Depth + 1,
           CAST(c.ChainPath + ' > ' + e.JobTitle AS NVARCHAR(4000))
    FROM HumanResources.Employee AS e
    JOIN ChainCTE AS c
      ON (e.OrganizationNode.GetAncestor(1) = c.OrganizationNode)
      OR (c.BusinessEntityID = 1 AND e.OrganizationNode.GetLevel() = 1)
)
SELECT BusinessEntityID, JobTitle, Depth, ChainPath
FROM ChainCTE
ORDER BY Depth, BusinessEntityID
OPTION (MAXRECURSION 100);

-- Challenge 2: Products never ordered by a store customer.
-- Reasoning: NOT EXISTS correlates on ProductID directly and is unaffected by
-- NULLs anywhere in the checked columns. NOT IN would be risky here: if the
-- subquery's ProductID column could ever be NULL (e.g. a future outer-join
-- upstream, or a nullable FK), every row's NOT IN comparison collapses to
-- UNKNOWN and the whole result silently becomes empty (see Pitfalls) —
-- NOT EXISTS has no such failure mode regardless of NULLs in the subquery.
SELECT p.ProductID, p.Name
FROM Production.Product AS p
WHERE NOT EXISTS (
    SELECT 1
    FROM Sales.SalesOrderDetail AS sod
    JOIN Sales.SalesOrderHeader AS soh ON soh.SalesOrderID = sod.SalesOrderID
    JOIN Sales.Customer AS c ON c.CustomerID = soh.CustomerID
    WHERE sod.ProductID = p.ProductID
      AND c.StoreID IS NOT NULL
)
ORDER BY p.ProductID;

-- Challenge 3: Complete 2013 month calendar with order totals.
-- Reasoning: Build the 12 calendar rows first (recursive CTE, anchor
-- '2013-01-01', +1 month per pass, stop at '2013-12-01') so the month list
-- exists independently of whether any orders happened that month. Then
-- LEFT JOIN (not INNER) SalesOrderHeader on a half-open date range — INNER
-- JOIN would silently drop any month with zero orders, defeating the point.
WITH MonthCalendar AS (
    SELECT CAST('2013-01-01' AS DATE) AS MonthStart
    UNION ALL
    SELECT DATEADD(MONTH, 1, MonthStart)
    FROM MonthCalendar
    WHERE MonthStart < '2013-12-01'
)
SELECT mc.MonthStart,
       COUNT(soh.SalesOrderID) AS OrderCount,
       SUM(soh.TotalDue)       AS Revenue
FROM MonthCalendar AS mc
LEFT JOIN Sales.SalesOrderHeader AS soh
  ON soh.OrderDate >= mc.MonthStart
 AND soh.OrderDate <  DATEADD(MONTH, 1, mc.MonthStart)
GROUP BY mc.MonthStart
ORDER BY mc.MonthStart
OPTION (MAXRECURSION 100);

-- Challenge 4: Highest-lifetime-spend customer per territory.
-- Reasoning: A CTE first collapses orders to one row per (TerritoryID,
-- CustomerID) with SUM(TotalDue); a correlated subquery in WHERE then keeps
-- only the row(s) matching that territory's MAX. This lesson-07 pattern
-- replaces it more efficiently: ROW_NUMBER() OVER (PARTITION BY TerritoryID
-- ORDER BY LifetimeSpend DESC) = 1 in a CTE — one windowed pass instead of
-- re-scanning per territory for every candidate row's correlated MAX.
WITH CustomerSpend AS (
    SELECT TerritoryID, CustomerID, SUM(TotalDue) AS LifetimeSpend
    FROM Sales.SalesOrderHeader
    WHERE TerritoryID IS NOT NULL
    GROUP BY TerritoryID, CustomerID
)
SELECT cs.TerritoryID, cs.CustomerID, cs.LifetimeSpend
FROM CustomerSpend AS cs
WHERE cs.LifetimeSpend = (
    SELECT MAX(cs2.LifetimeSpend)
    FROM CustomerSpend AS cs2
    WHERE cs2.TerritoryID = cs.TerritoryID
)
ORDER BY cs.TerritoryID;
