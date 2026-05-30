USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson07') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson07.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson07');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson07;
END
GO
CREATE SCHEMA lesson07;
GO

-- Monthly sales data for window function demos
CREATE TABLE lesson07.MonthlySales (
    SaleYear  INT  NOT NULL,
    SaleMonth INT  NOT NULL,
    Region    NVARCHAR(50) NOT NULL,
    Revenue   DECIMAL(14,2) NOT NULL,
    PRIMARY KEY (SaleYear, SaleMonth, Region)
);

INSERT lesson07.MonthlySales (SaleYear, SaleMonth, Region, Revenue) VALUES
    (2023, 1,  'North', 12000), (2023, 2,  'North', 15000), (2023, 3,  'North', 11000),
    (2023, 1,  'South', 8000),  (2023, 2,  'South', 9500),  (2023, 3,  'South', 10200),
    (2023, 4,  'North', 18000), (2023, 5,  'North', 16500), (2023, 6,  'North', 19000),
    (2023, 4,  'South', 11000), (2023, 5,  'South', 12400), (2023, 6,  'South', 13100);

PRINT 'Lesson 07 setup complete.';
