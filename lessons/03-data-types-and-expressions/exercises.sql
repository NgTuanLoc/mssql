USE AdventureWorks2022;
GO

-- Exercise 1: Format the ModifiedDate of every Person.Person row as 'DD/MM/YYYY'.
-- Expected columns: BusinessEntityID, ModifiedDateFormatted
-- Hint: CONVERT style 103 = DD/MM/YYYY
-- Your query here:


-- Exercise 2: Calculate each product's ListPrice rounded to the nearest dollar,
--             and the difference between the rounded and original price.
-- Expected columns: Name, ListPrice, Rounded, Difference
-- Your query here:


-- Exercise 3: Some rows in Production.Product have a NULL Weight.
--             Return all products showing Weight; replace NULL with 0.
-- Expected columns: Name, Weight, WeightOrZero
-- Your query here:


-- Exercise 4: Convert the SellStartDate of each product to a DATE (stripping time).
--             Show only products whose SellStartDate year is 2003.
-- Expected columns: Name, SellStartDate, SellStartDateOnly
-- Your query here:


-- Exercise 5: Using TRY_CAST, attempt to cast the ProductNumber column to INT.
--             Show ProductNumber, the attempted cast result, and a derived column
--             IsNumeric (1 if the cast succeeded, 0 if it returned NULL).
-- Expected columns: ProductNumber, CastResult, IsNumeric
-- Your query here:


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Demonstrate the MONEY rounding trap: split $100.00 into 3 equal
-- parts with MONEY and with DECIMAL(19,4), sum the parts back, and show the
-- difference in cents in one result row.
-- Expected columns: MoneyTotal, DecimalTotal, MoneyLostCents
-- Your query here:


-- Challenge 2: Group all 2013 orders into fiscal quarters where the fiscal
-- year starts July 1 (Q1 = Jul–Sep). Compute the fiscal quarter from
-- OrderDate with date functions — no CASE ladder over month numbers.
-- Expected columns: FiscalYear, FiscalQuarter, OrderCount, TotalDue
-- Hint: DATEADD the date back 6 months, then DATEPART quarter/year of the
-- shifted date.
-- Your query here:


-- Challenge 3: Using Person.Person, produce a padded display name exactly
-- 40 chars wide: "LastName, FirstName" left-aligned, padded with dots. Show
-- LEN vs DATALENGTH for 5 rows and explain the factor of 2 in a comment.
-- Expected columns: DisplayName, LenChars, DataLengthBytes
-- Hint: NVARCHAR: DATALENGTH = 2× LEN.
-- Your query here:


-- Challenge 4: lesson03.MixedDates (seeded in setup.sql) holds date strings
-- in mixed formats ('2014-03-01', '03/15/2014', '20140401', 'not a date').
-- Produce a clean report: original string, parsed DATE where possible
-- (trying styles 120, 101, 112 in that order), and a flag for unparseable
-- rows.
-- Expected columns: RawValue, ParsedDate, IsValid
-- Hint: COALESCE over TRY_CONVERT calls with explicit styles.
-- Your query here:
