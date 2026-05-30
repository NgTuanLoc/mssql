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
