USE AdventureWorks2022;
GO

-- Example 1: CAST — convert ListPrice to string for concatenation
SELECT Name + ' costs $' + CAST(ListPrice AS VARCHAR(20)) AS Description
FROM Production.Product
WHERE ListPrice > 0;

-- Example 2: CONVERT with style — format an order date as 'YYYY-MM-DD'
SELECT
    SalesOrderID,
    CONVERT(VARCHAR(10), OrderDate, 120) AS OrderDateFormatted
FROM Sales.SalesOrderHeader
WHERE YEAR(OrderDate) = 2014;

-- Example 3: TRY_CAST — safe conversion (returns NULL on failure)
SELECT
    VarcharCode,
    TRY_CAST(VarcharCode AS INT) AS AsInt   -- 'A001' → NULL, not an error
FROM lesson03.TypeDemo;

-- Example 4: Implicit conversion gotcha — VARCHAR column vs INT literal
-- This forces a scan because SQL Server must convert every VarcharCode row to INT
SELECT VarcharCode FROM lesson03.TypeDemo WHERE VarcharCode = 1;      -- implicit scan
SELECT VarcharCode FROM lesson03.TypeDemo WHERE VarcharCode = '1';    -- correct

-- Example 5: DECIMAL vs MONEY arithmetic precision
SELECT
    PriceDecimal / 3          AS DecimalDiv,   -- preserves precision
    PriceMoney   / 3          AS MoneyDiv       -- rounds to 4 decimal places mid-calc
FROM lesson03.TypeDemo;

-- Example 6: Date arithmetic
SELECT
    BirthDate,
    DATEDIFF(YEAR, BirthDate, GETDATE())       AS AgeApprox,
    DATEADD(YEAR, 18, BirthDate)               AS Turns18,
    EOMONTH(BirthDate)                          AS LastDayOfBirthMonth
FROM lesson03.TypeDemo;

-- Example 7: Unicode vs VARCHAR — the café problem
SELECT
    NVarcharName,                              -- 'Café' stored correctly
    CAST(NVarcharName AS VARCHAR(100))          AS LostAccent   -- 'Caf?' on many collations
FROM lesson03.TypeDemo
WHERE NVarcharName = N'Café';                  -- N prefix required for correct match
