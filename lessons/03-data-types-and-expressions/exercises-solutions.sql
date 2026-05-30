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
