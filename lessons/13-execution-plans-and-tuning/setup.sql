USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson13') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    -- Drop procs
    SELECT @sql += 'DROP PROCEDURE lesson13.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.procedures WHERE schema_id = SCHEMA_ID('lesson13');
    EXEC sp_executesql @sql;

    SET @sql = N'';
    SELECT @sql += 'DROP TABLE lesson13.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson13');
    EXEC sp_executesql @sql;

    DROP SCHEMA lesson13;
END
GO
CREATE SCHEMA lesson13;
GO

-- Indexed copy for tuning exercises
SELECT * INTO lesson13.SalesOrderHeader
FROM Sales.SalesOrderHeader;

ALTER TABLE lesson13.SalesOrderHeader
    ADD CONSTRAINT PK_lesson13_SOH PRIMARY KEY CLUSTERED (SalesOrderID);

CREATE INDEX IX_lesson13_SOH_CustomerID
    ON lesson13.SalesOrderHeader (CustomerID)
    INCLUDE (OrderDate, TotalDue, Status);

CREATE INDEX IX_lesson13_SOH_TerritoryDate
    ON lesson13.SalesOrderHeader (TerritoryID, OrderDate)
    INCLUDE (TotalDue);

-- Update statistics for accurate plan estimates
UPDATE STATISTICS lesson13.SalesOrderHeader WITH FULLSCAN;

PRINT 'Lesson 13 setup complete.';
