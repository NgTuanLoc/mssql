USE AdventureWorks2022;
GO

-- Example 1: Scalar subquery in SELECT — average as a reference value
SELECT
    SalesOrderID,
    TotalDue,
    (SELECT AVG(TotalDue) FROM Sales.SalesOrderHeader) AS AvgOrderValue,
    TotalDue - (SELECT AVG(TotalDue) FROM Sales.SalesOrderHeader) AS DiffFromAvg
FROM Sales.SalesOrderHeader
WHERE YEAR(OrderDate) = 2014;

-- Example 2: Subquery in WHERE with IN
SELECT ProductID, Name, ListPrice
FROM Production.Product
WHERE ProductSubcategoryID IN (
    SELECT ProductSubcategoryID
    FROM Production.ProductSubcategory
    WHERE ProductCategoryID = 1   -- Bikes
);

-- Example 3: Correlated subquery — for each customer, count their orders
SELECT
    c.CustomerID,
    (SELECT COUNT(*)
     FROM Sales.SalesOrderHeader AS soh
     WHERE soh.CustomerID = c.CustomerID) AS OrderCount
FROM Sales.Customer AS c
ORDER BY OrderCount DESC;

-- Example 4: EXISTS vs IN — customers who have placed at least one order
-- EXISTS: stops scanning as soon as one match is found — often faster
SELECT CustomerID
FROM Sales.Customer AS c
WHERE EXISTS (
    SELECT 1
    FROM Sales.SalesOrderHeader AS soh
    WHERE soh.CustomerID = c.CustomerID
);

-- Example 5: NOT EXISTS — customers who have NEVER ordered
SELECT CustomerID
FROM Sales.Customer AS c
WHERE NOT EXISTS (
    SELECT 1
    FROM Sales.SalesOrderHeader AS soh
    WHERE soh.CustomerID = c.CustomerID
);

-- Example 6: Non-recursive CTE
WITH OrderSummary AS (
    SELECT
        CustomerID,
        COUNT(*)      AS OrderCount,
        SUM(TotalDue) AS TotalSpend
    FROM Sales.SalesOrderHeader
    GROUP BY CustomerID
)
SELECT
    c.CustomerID,
    os.OrderCount,
    os.TotalSpend
FROM Sales.Customer AS c
JOIN OrderSummary   AS os ON os.CustomerID = c.CustomerID
WHERE os.TotalSpend > 10000
ORDER BY os.TotalSpend DESC;

-- Example 7: Recursive CTE — employee management chain
WITH EmpHierarchy AS (
    -- Anchor: top-level employees (no manager)
    SELECT
        BusinessEntityID,
        OrganizationNode,
        JobTitle,
        0 AS Level
    FROM HumanResources.Employee
    WHERE OrganizationNode = hierarchyid::GetRoot()
       OR OrganizationNode.GetLevel() = 1

    UNION ALL

    -- Recursive: employees reporting to a known employee
    SELECT
        e.BusinessEntityID,
        e.OrganizationNode,
        e.JobTitle,
        eh.Level + 1
    FROM HumanResources.Employee AS e
    JOIN EmpHierarchy AS eh
      ON e.OrganizationNode.GetAncestor(1) = eh.OrganizationNode
)
SELECT BusinessEntityID, JobTitle, Level
FROM EmpHierarchy
ORDER BY Level, BusinessEntityID;
