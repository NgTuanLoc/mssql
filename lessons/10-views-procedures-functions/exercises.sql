USE AdventureWorks2022;
GO

-- Exercise 1: Create a view lesson10.vw_TopProducts that returns the top 20
--             products by ListPrice with their category name.
-- Expected columns: ProductID, ProductName, CategoryName, ListPrice
-- Your query here:


-- Exercise 2: Create a stored procedure lesson10.usp_SearchProducts that accepts
--             @Keyword NVARCHAR(100) and returns all products whose Name contains
--             the keyword (case-insensitive). Default keyword = '%' (returns all).
-- Test it with EXEC lesson10.usp_SearchProducts @Keyword = 'Road'
-- Your query here:


-- Exercise 3: Create an inline table-valued function lesson10.fn_OrdersInDateRange
--             that accepts @StartDate DATE and @EndDate DATE and returns all orders
--             with their OrderDate, CustomerID, and TotalDue within that range.
-- Test it with SELECT * FROM lesson10.fn_OrdersInDateRange('2013-01-01','2013-03-31')
-- Your query here:


-- Exercise 4: Create a scalar function lesson10.fn_FormatMoney that takes a MONEY
--             value and returns it as NVARCHAR formatted with a $ sign and 2 decimal places.
--             e.g. 1234.5 → '$1,234.50'
-- Hint: FORMAT(value, 'C', 'en-US') returns currency format.
-- Your query here:


-- Exercise 5: Call lesson10.fn_FormatMoney from a SELECT to format the TotalDue
--             of the 5 largest orders in Sales.SalesOrderHeader.
-- Expected columns: SalesOrderID, TotalDue, FormattedTotal
-- Your query here:
