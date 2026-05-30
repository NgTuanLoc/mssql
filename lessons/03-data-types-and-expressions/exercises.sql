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
