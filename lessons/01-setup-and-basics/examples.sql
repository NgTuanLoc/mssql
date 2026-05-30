USE AdventureWorks2022;
GO

-- Example 1: Your first SELECT — retrieve all columns from a small table
SELECT * FROM Person.CountryRegion;

-- Example 2: Select specific columns with aliases
SELECT
    CountryRegionCode AS Code,
    Name              AS CountryName
FROM Person.CountryRegion;

-- Example 3: Limit rows with TOP
SELECT TOP 10
    FirstName,
    LastName,
    EmailPromotion
FROM Person.Person
ORDER BY LastName;

-- Example 4: Filter rows with WHERE
SELECT
    ProductID,
    Name,
    ListPrice
FROM Production.Product
WHERE ListPrice > 1000
ORDER BY ListPrice DESC;

-- Example 5: Explore a table's structure
EXEC sp_help 'Production.Product';
-- Or in Object Explorer: expand Tables → Production.Product → Columns
