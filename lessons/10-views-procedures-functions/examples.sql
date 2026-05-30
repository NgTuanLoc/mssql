USE AdventureWorks2022;
GO

-- =========================================================================
-- VIEWS
-- =========================================================================

-- Example 1: Simple view — customer order summary
CREATE OR ALTER VIEW lesson10.vw_CustomerOrderSummary AS
SELECT
    c.CustomerID,
    p.FirstName + ' ' + p.LastName AS FullName,
    COUNT(soh.SalesOrderID)        AS OrderCount,
    SUM(soh.TotalDue)              AS TotalSpend,
    MAX(soh.OrderDate)             AS LastOrderDate
FROM Sales.Customer          AS c
JOIN Person.Person           AS p   ON p.BusinessEntityID = c.PersonID
JOIN Sales.SalesOrderHeader  AS soh ON soh.CustomerID     = c.CustomerID
GROUP BY c.CustomerID, p.FirstName, p.LastName;
GO

-- Query the view like a table
SELECT TOP 10 * FROM lesson10.vw_CustomerOrderSummary ORDER BY TotalSpend DESC;

-- =========================================================================
-- STORED PROCEDURES
-- =========================================================================

-- Example 2: Parameterised stored procedure
CREATE OR ALTER PROCEDURE lesson10.usp_GetOrdersByCustomer
    @CustomerID INT,
    @MinAmount  MONEY = 0   -- optional parameter with default
AS
BEGIN
    SET NOCOUNT ON;   -- suppresses "X rows affected" messages

    SELECT
        SalesOrderID,
        OrderDate,
        TotalDue
    FROM Sales.SalesOrderHeader
    WHERE CustomerID = @CustomerID
      AND TotalDue  >= @MinAmount
    ORDER BY OrderDate DESC;
END;
GO

-- Call the procedure
EXEC lesson10.usp_GetOrdersByCustomer @CustomerID = 11000;
EXEC lesson10.usp_GetOrdersByCustomer @CustomerID = 11000, @MinAmount = 500;

-- Example 3: Procedure with OUTPUT parameter
CREATE OR ALTER PROCEDURE lesson10.usp_GetCustomerOrderCount
    @CustomerID INT,
    @OrderCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @OrderCount = COUNT(*)
    FROM Sales.SalesOrderHeader
    WHERE CustomerID = @CustomerID;
END;
GO

DECLARE @Count INT;
EXEC lesson10.usp_GetCustomerOrderCount @CustomerID = 11000, @OrderCount = @Count OUTPUT;
PRINT 'Orders: ' + CAST(@Count AS VARCHAR);

-- =========================================================================
-- FUNCTIONS
-- =========================================================================

-- Example 4: Scalar function — compute age in years
CREATE OR ALTER FUNCTION lesson10.fn_AgeInYears (@BirthDate DATE)
RETURNS INT
AS
BEGIN
    RETURN DATEDIFF(YEAR, @BirthDate, GETDATE())
         - CASE WHEN MONTH(GETDATE()) < MONTH(@BirthDate)
                  OR (MONTH(GETDATE()) = MONTH(@BirthDate)
                      AND DAY(GETDATE()) < DAY(@BirthDate))
                THEN 1 ELSE 0 END;
END;
GO

-- Scalar UDF called in a SELECT (note: scalar UDFs can slow queries — see pitfalls)
SELECT
    BusinessEntityID,
    BirthDate,
    lesson10.fn_AgeInYears(BirthDate) AS Age
FROM HumanResources.Employee;

-- Example 5: Inline Table-Valued Function (iTVF) — preferred over scalar UDFs for sets
CREATE OR ALTER FUNCTION lesson10.fn_GetProductsByCategory (@CategoryID INT)
RETURNS TABLE
AS
RETURN (
    SELECT
        p.ProductID,
        p.Name,
        p.ListPrice,
        ps.Name AS SubcategoryName
    FROM Production.Product            AS p
    JOIN Production.ProductSubcategory AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
    JOIN Production.ProductCategory    AS pc ON pc.ProductCategoryID    = ps.ProductCategoryID
    WHERE pc.ProductCategoryID = @CategoryID
);
GO

-- Use the iTVF like a table
SELECT * FROM lesson10.fn_GetProductsByCategory(1) ORDER BY ListPrice DESC;

-- CROSS APPLY — call the iTVF for each row of another table
SELECT
    pc.ProductCategoryID,
    pc.Name AS Category,
    p.Name  AS TopProduct,
    p.ListPrice
FROM Production.ProductCategory AS pc
CROSS APPLY (
    SELECT TOP 1 *
    FROM lesson10.fn_GetProductsByCategory(pc.ProductCategoryID)
    ORDER BY ListPrice DESC
) AS p;
