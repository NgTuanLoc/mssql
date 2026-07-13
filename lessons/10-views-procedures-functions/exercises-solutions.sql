USE AdventureWorks2022;
GO

-- Exercise 1: View for top 20 products by price.
-- Approach: CREATE OR ALTER VIEW; TOP inside a view requires ORDER BY to be meaningful.
CREATE OR ALTER VIEW lesson10.vw_TopProducts AS
SELECT TOP 20
    p.ProductID,
    p.Name       AS ProductName,
    pc.Name      AS CategoryName,
    p.ListPrice
FROM Production.Product             AS p
JOIN Production.ProductSubcategory  AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
JOIN Production.ProductCategory     AS pc ON pc.ProductCategoryID    = ps.ProductCategoryID
WHERE p.ListPrice > 0
ORDER BY p.ListPrice DESC;
GO

SELECT * FROM lesson10.vw_TopProducts;

-- Exercise 2: Procedure to search products by keyword.
-- Approach: LIKE with wildcards around the keyword; default '%' matches everything.
CREATE OR ALTER PROCEDURE lesson10.usp_SearchProducts
    @Keyword NVARCHAR(100) = N'%'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ProductID, Name, ListPrice
    FROM Production.Product
    WHERE Name LIKE N'%' + @Keyword + N'%'
    ORDER BY Name;
END;
GO

EXEC lesson10.usp_SearchProducts @Keyword = N'Road';

-- Exercise 3: Inline TVF for orders in a date range.
-- Approach: RETURNS TABLE AS RETURN — single SELECT, no BEGIN/END, fastest type of UDF.
CREATE OR ALTER FUNCTION lesson10.fn_OrdersInDateRange
    (@StartDate DATE, @EndDate DATE)
RETURNS TABLE
AS
RETURN (
    SELECT SalesOrderID, OrderDate, CustomerID, TotalDue
    FROM Sales.SalesOrderHeader
    WHERE OrderDate >= @StartDate
      AND OrderDate <  DATEADD(DAY, 1, @EndDate)  -- inclusive end
);
GO

SELECT * FROM lesson10.fn_OrdersInDateRange('2013-01-01','2013-03-31') ORDER BY OrderDate;

-- Exercise 4: Scalar function for currency formatting.
-- Approach: FORMAT with culture 'en-US' produces US currency string.
CREATE OR ALTER FUNCTION lesson10.fn_FormatMoney (@Amount MONEY)
RETURNS NVARCHAR(50)
AS
BEGIN
    RETURN FORMAT(@Amount, 'C', 'en-US');
END;
GO

-- Exercise 5: Apply fn_FormatMoney to top 5 orders.
-- Approach: scalar UDF called in SELECT; acceptable here for 5 rows.
SELECT TOP 5
    SalesOrderID,
    TotalDue,
    lesson10.fn_FormatMoney(TotalDue) AS FormattedTotal
FROM Sales.SalesOrderHeader
ORDER BY TotalDue DESC;


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Top-3 orders per customer in territory 1, via iTVF + CROSS APPLY.
-- Reasoning: An iTVF parameterises TOP (@N) ... ORDER BY, giving one per-customer ranked
--   result the optimizer can inline; CROSS APPLY re-invokes it once per customer row, which
--   a JOIN can't do since its ON clause can't reference @N/@CustomerID per outer row.
--   Tempting-but-wrong: ROW_NUMBER() PARTITION BY CustomerID in a single query works too,
--   but doesn't produce a reusable, independently testable, parameterised object — the
--   point of this challenge is the iTVF contract itself, not just the ranking technique.
CREATE OR ALTER FUNCTION lesson10.ufn_TopOrdersForCustomer (@CustomerID INT, @N INT)
RETURNS TABLE
AS
RETURN (
    SELECT TOP (@N)
        soh.SalesOrderID,
        soh.TotalDue,
        ROW_NUMBER() OVER (ORDER BY soh.TotalDue DESC) AS OrderRank
    FROM Sales.SalesOrderHeader AS soh
    WHERE soh.CustomerID = @CustomerID
    ORDER BY soh.TotalDue DESC
);
GO

SELECT c.CustomerID, t.SalesOrderID, t.TotalDue, t.OrderRank
FROM Sales.Customer AS c
CROSS APPLY lesson10.ufn_TopOrdersForCustomer(c.CustomerID, 3) AS t
WHERE c.TerritoryID = 1
ORDER BY c.CustomerID, t.OrderRank;
GO

-- Challenge 2: Territory revenue proc with validated params and a not-found return code.
-- Reasoning: THROW 50001 for NULL inputs must fire before the query touches
--   SalesOrderHeader, so it's the first check inside TRY; SUM() returning NULL (no
--   matching rows) is the not-found case, distinct from a validation failure, so it gets
--   its own RETURN 1 rather than also throwing. Tempting-but-wrong: checking @Revenue
--   IS NULL outside TRY/CATCH — that misses exceptions the SELECT itself could raise
--   (e.g. arithmetic overflow), which TRY/CATCH + THROW exists to rethrow honestly.
CREATE OR ALTER PROCEDURE lesson10.usp_TerritoryRevenue
    @TerritoryID INT,
    @Year        INT,
    @Revenue     MONEY OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        IF @TerritoryID IS NULL OR @Year IS NULL
            THROW 50001, 'TerritoryID and Year must not be NULL.', 1;

        SELECT @Revenue = SUM(TotalDue)
        FROM Sales.SalesOrderHeader
        WHERE TerritoryID = @TerritoryID
          AND OrderDate >= DATEFROMPARTS(@Year, 1, 1)
          AND OrderDate <  DATEFROMPARTS(@Year + 1, 1, 1);

        IF @Revenue IS NULL
        BEGIN
            SET @Revenue = 0;
            RETURN 1;      -- not found: no orders that territory/year
        END
        RETURN 0;          -- ok
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;
GO

-- Demo 1: success
DECLARE @Rev MONEY, @Rc INT;
EXEC @Rc = lesson10.usp_TerritoryRevenue @TerritoryID = 1, @Year = 2013, @Revenue = @Rev OUTPUT;
SELECT @Rc AS ReturnCode, @Rev AS Revenue;

-- Demo 2: not-found (territory 1 has no orders in 1999)
DECLARE @Rev2 MONEY, @Rc2 INT;
EXEC @Rc2 = lesson10.usp_TerritoryRevenue @TerritoryID = 1, @Year = 1999, @Revenue = @Rev2 OUTPUT;
SELECT @Rc2 AS ReturnCode, @Rev2 AS Revenue;

-- Demo 3: thrown validation error (run separately — errors abort the batch)
-- DECLARE @Rev3 MONEY, @Rc3 INT;
-- EXEC @Rc3 = lesson10.usp_TerritoryRevenue @TerritoryID = NULL, @Year = 2013, @Revenue = @Rev3 OUTPUT;
-- Msg 50001: TerritoryID and Year must not be NULL.
GO

-- Challenge 3: Scalar UDF vs. windowed/grouped rewrite for lifetime spend, with timings.
-- Reasoning: The UDF's early "RETURN NULL" on a NULL input is a second RETURN statement,
--   which disqualifies SQL 2019+ auto-inlining (sys.sql_modules.is_inlineable = 0) — so
--   this genuinely pays the per-row cost the lesson warns about. Tempting-but-wrong:
--   comparing against a scalar UDF WITHOUT that guard, which SQL Server silently inlines
--   and makes the "rewrite" look pointless. Measured: UDF CPU 140 ms vs GROUP BY CPU 12 ms
--   (times vary by run — see README Concept 3 for the plan-level reason).
CREATE OR ALTER FUNCTION lesson10.ufn_CustomerLifetimeSpend (@CustomerID INT)
RETURNS MONEY
AS
BEGIN
    DECLARE @Total MONEY;
    IF @CustomerID IS NULL
        RETURN NULL;
    SELECT @Total = SUM(TotalDue)
    FROM Sales.SalesOrderHeader
    WHERE CustomerID = @CustomerID;
    RETURN ISNULL(@Total, 0);
END;
GO

SET STATISTICS TIME ON;
SELECT CustomerID, lesson10.ufn_CustomerLifetimeSpend(CustomerID) AS LifetimeSpend
FROM Sales.Customer
ORDER BY CustomerID;

SELECT c.CustomerID, ISNULL(SUM(soh.TotalDue), 0) AS LifetimeSpend
FROM Sales.Customer AS c
LEFT JOIN Sales.SalesOrderHeader AS soh ON soh.CustomerID = c.CustomerID
GROUP BY c.CustomerID
ORDER BY c.CustomerID;
SET STATISTICS TIME OFF;
GO

-- Challenge 4: Updatable-view boundary — WITH CHECK OPTION on a single-table view.
-- Reasoning: A single-table view with no aggregate/DISTINCT is updatable by default; adding
--   WITH CHECK OPTION additionally forces every write through it to still satisfy the
--   WHERE. Tempting-but-wrong: assuming any view is updatable — a JOIN can't tell the
--   engine which base table an updated column belongs to, an aggregate/DISTINCT collapses
--   rows so there's no single row left to write back to, and both make a view read-only
--   regardless of WITH CHECK OPTION.
CREATE OR ALTER VIEW lesson10.vw_ActiveAccount AS
SELECT AccountID, AccountName, Status
FROM lesson10.Account
WHERE Status = 'Active'
WITH CHECK OPTION;
GO

-- Demo 1: update that keeps the row inside the filter — succeeds
UPDATE lesson10.vw_ActiveAccount SET AccountName = N'Contoso Retail Group' WHERE AccountID = 1;
SELECT * FROM lesson10.vw_ActiveAccount ORDER BY AccountID;

-- Demo 2: update that would remove the row from the filter — rejected (run separately)
-- UPDATE lesson10.vw_ActiveAccount SET Status = 'Inactive' WHERE AccountID = 1;
-- Msg 550: the target view specifies WITH CHECK OPTION and the row no longer qualifies.
