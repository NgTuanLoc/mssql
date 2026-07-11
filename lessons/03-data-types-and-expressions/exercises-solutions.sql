USE AdventureWorks2022;
GO

-- Exercise 1: Format ModifiedDate as DD/MM/YYYY.
-- Approach: CONVERT style 103 produces DD/MM/YYYY directly.
SELECT
    BusinessEntityID,
    CONVERT(VARCHAR(10), ModifiedDate, 103) AS ModifiedDateFormatted
FROM Person.Person;

-- Exercise 2: Rounded price and difference.
-- Approach: ROUND(x, 0) rounds to nearest integer; subtraction gives the delta.
SELECT
    Name,
    ListPrice,
    ROUND(ListPrice, 0)            AS Rounded,
    ListPrice - ROUND(ListPrice, 0) AS Difference
FROM Production.Product
WHERE ListPrice > 0;

-- Exercise 3: Replace NULL Weight with 0.
-- Approach: ISNULL replaces NULL with the specified value; COALESCE is the ANSI equivalent.
SELECT
    Name,
    Weight,
    ISNULL(Weight, 0) AS WeightOrZero
FROM Production.Product;

-- Exercise 4: Products whose SellStartDate is in 2003, showing DATE-only value.
-- Approach: CAST to DATE strips the time component; YEAR() extracts the year for filtering.
SELECT
    Name,
    SellStartDate,
    CAST(SellStartDate AS DATE) AS SellStartDateOnly
FROM Production.Product
WHERE YEAR(SellStartDate) = 2003;

-- Exercise 5: TRY_CAST ProductNumber to INT.
-- Approach: TRY_CAST returns NULL on failure; CASE on the result gives IsNumeric.
SELECT
    ProductNumber,
    TRY_CAST(ProductNumber AS INT)                              AS CastResult,
    CASE WHEN TRY_CAST(ProductNumber AS INT) IS NOT NULL
         THEN 1 ELSE 0 END                                      AS IsNumeric
FROM Production.Product;


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: MONEY vs DECIMAL(19,4) rounding trap on a 3-way split of $100.
-- Reasoning: Divide-then-multiply as one bare expression per type, casting to the
--   final scale only at the end — T-SQL widens a division's intermediate scale, so
--   DECIMAL keeps the extra digits through the multiply and lands on $100.0000.
--   Tempting-but-wrong: declaring the DECIMAL third into a DECIMAL(19,4) variable
--   first — that assignment truncates to 4 places immediately, matching MONEY's
--   $99.9999 and hiding the very trap this challenge demonstrates.
SELECT
    (CAST(100.00 AS MONEY) / 3) * 3                                       AS MoneyTotal,
    CAST((CAST(100.00 AS DECIMAL(19,4)) / 3) * 3 AS DECIMAL(19,4))        AS DecimalTotal,
    (100.00 - CAST((CAST(100.00 AS MONEY) / 3) * 3 AS DECIMAL(19,4))) * 100 AS MoneyLostCents;

-- Challenge 2: 2013 orders grouped into July-start fiscal quarters.
-- Reasoning: Shift OrderDate back 6 months so July lands on "month 1" of the
--   shifted calendar; DATEPART(QUARTER,...) and YEAR(...) of that SAME shifted
--   date give FiscalQuarter and FiscalYear directly, no per-month CASE needed.
--   Tempting-but-wrong: CASE WHEN MONTH(OrderDate) IN (7,8,9) THEN 1... — exactly
--   the ladder the exercise forbids, and it doesn't generalize to other fiscal
--   shapes. 2013 calendar orders span two fiscal years (FY2012 Q3/Q4, FY2013 Q1/Q2).
SELECT
    YEAR(DATEADD(MONTH, -6, OrderDate))                AS FiscalYear,
    DATEPART(QUARTER, DATEADD(MONTH, -6, OrderDate))   AS FiscalQuarter,
    COUNT(*)                                            AS OrderCount,
    SUM(TotalDue)                                       AS TotalDue
FROM Sales.SalesOrderHeader
WHERE YEAR(OrderDate) = 2013
GROUP BY YEAR(DATEADD(MONTH, -6, OrderDate)), DATEPART(QUARTER, DATEADD(MONTH, -6, OrderDate))
ORDER BY FiscalYear, FiscalQuarter;

-- Challenge 3: 40-char padded "LastName, FirstName" display name, LEN vs DATALENGTH.
-- Reasoning: Append 40 dots then LEFT-truncate to 40 — guarantees a fixed width
--   regardless of name length. Dots (not spaces) matter: LEN() strips trailing
--   spaces but not dots, so a space-padded string would silently misreport
--   LenChars. NVARCHAR stores 2 bytes/char, so once every row is padded to
--   exactly 40 chars, DataLengthBytes is exactly 2x LenChars (80) on every row.
SELECT TOP (5)
    LEFT(LastName + ', ' + FirstName + REPLICATE('.', 40), 40)            AS DisplayName,
    LEN(LEFT(LastName + ', ' + FirstName + REPLICATE('.', 40), 40))        AS LenChars,
    DATALENGTH(LEFT(LastName + ', ' + FirstName + REPLICATE('.', 40), 40)) AS DataLengthBytes
FROM Person.Person
ORDER BY BusinessEntityID;

-- Challenge 4: Clean report over lesson03.MixedDates mixed-format date strings.
-- Reasoning: Try each candidate style with TRY_CONVERT, which returns NULL on a
--   non-matching format instead of erroring; COALESCE picks the first non-NULL
--   hit in priority order (120 ISO, 101 US slash, 112 compact). IsValid flags
--   rows where every style failed. Tempting-but-wrong: a single CAST/CONVERT
--   with one fixed style — it errors on the first mismatched format and kills
--   the whole batch, the exact pitfall this lesson's date-string demo shows.
SELECT
    RawValue,
    COALESCE(TRY_CONVERT(DATE, RawValue, 120),
             TRY_CONVERT(DATE, RawValue, 101),
             TRY_CONVERT(DATE, RawValue, 112))              AS ParsedDate,
    CASE WHEN COALESCE(TRY_CONVERT(DATE, RawValue, 120),
                        TRY_CONVERT(DATE, RawValue, 101),
                        TRY_CONVERT(DATE, RawValue, 112)) IS NULL
         THEN 0 ELSE 1 END                                  AS IsValid
FROM lesson03.MixedDates
ORDER BY ID;
