USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson08') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson08.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson08');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson08;
END
GO
CREATE SCHEMA lesson08;
GO

-- Staging table for INSERT/UPDATE/DELETE exercises (re-seeded on each setup run)
CREATE TABLE lesson08.ProductStaging (
    StagingID   INT          IDENTITY(1,1) PRIMARY KEY,
    ProductID   INT          NOT NULL,
    Name        NVARCHAR(50) NOT NULL,
    ListPrice   MONEY        NOT NULL,
    IsActive    BIT          NOT NULL DEFAULT 1,
    LoadedAt    DATETIME2    NOT NULL DEFAULT SYSDATETIME()
);

-- Target table for MERGE demo
CREATE TABLE lesson08.InventoryTarget (
    ProductID  INT          NOT NULL PRIMARY KEY,
    StockQty   INT          NOT NULL DEFAULT 0,
    LastSyncAt DATETIME2    NOT NULL DEFAULT SYSDATETIME()
);

-- Source data — simulates an incoming feed
CREATE TABLE lesson08.InventorySource (
    ProductID  INT NOT NULL PRIMARY KEY,
    StockQty   INT NOT NULL
);

INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
SELECT TOP 10 ProductID, Name, ListPrice
FROM Production.Product
WHERE ListPrice > 0
ORDER BY ProductID;

INSERT lesson08.InventoryTarget (ProductID, StockQty)
VALUES (1, 100), (2, 200), (3, 300);

INSERT lesson08.InventorySource (ProductID, StockQty)
VALUES (1, 95),    -- update
       (3, 310),   -- update
       (4, 50),    -- insert (new)
       (5, 0);     -- insert (new, zero stock)

PRINT 'Lesson 08 setup complete.';
