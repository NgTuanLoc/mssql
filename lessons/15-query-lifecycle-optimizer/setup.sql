USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson15') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson15.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson15');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson15;
END
GO
CREATE SCHEMA lesson15;
GO

-- Indexed copy so we can manipulate statistics independently of the real table
SELECT * INTO lesson15.SalesOrderHeader
FROM Sales.SalesOrderHeader;

ALTER TABLE lesson15.SalesOrderHeader
    ADD CONSTRAINT PK_lesson15_SOH PRIMARY KEY CLUSTERED (SalesOrderID);

CREATE INDEX IX_lesson15_SOH_CustomerID
    ON lesson15.SalesOrderHeader (CustomerID) INCLUDE (OrderDate, TotalDue);

CREATE INDEX IX_lesson15_SOH_OrderDate
    ON lesson15.SalesOrderHeader (OrderDate) INCLUDE (TotalDue);

UPDATE STATISTICS lesson15.SalesOrderHeader WITH FULLSCAN;

PRINT 'Lesson 15 setup complete.';
