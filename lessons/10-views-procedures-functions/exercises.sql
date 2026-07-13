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


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Write lesson10.ufn_TopOrdersForCustomer(@CustomerID, @N) as an iTVF
--              returning the customer's top-N orders by TotalDue; use CROSS APPLY to
--              run it over all customers in territory 1 with @N = 3.
-- Expected columns: CustomerID, SalesOrderID, TotalDue, OrderRank
-- Your query here:


-- Challenge 2: Proc lesson10.usp_TerritoryRevenue (@TerritoryID, @Year, @Revenue OUTPUT,
--              returns 0 ok / 1 not found): TRY/CATCH, SET NOCOUNT ON, validated params
--              (THROW 50001 on NULL inputs). Demo the success call, the not-found return
--              code, and the thrown validation error.
-- Expected columns: (three demo calls)
-- Your query here:


-- Challenge 3: Rewrite a scalar UDF as inline logic: create ufn_CustomerLifetimeSpend
--              (@CustomerID) scalar UDF, use it in a query over all customers with
--              STATISTICS TIME on; then produce the same result via one windowed/grouped
--              query. Record both CPU times in the solution comment.
-- Expected columns: CustomerID, LifetimeSpend
-- Your query here:


-- Challenge 4: Updatable-view boundary: create a single-table view over lesson10 data
--              WITH CHECK OPTION filtering Status = 'Active'; show an UPDATE through the
--              view that succeeds, one that violates the filter (error 550), and explain
--              in a comment what would make the view non-updatable (joins, aggregates,
--              DISTINCT).
-- Expected columns: (demo sequence)
-- Your query here:
