USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson12') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson12.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson12');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson12;
END
GO
CREATE SCHEMA lesson12;
GO

-- Heap copy of SalesOrderHeader — no clustered index, for comparison
SELECT * INTO lesson12.SalesOrderHeap
FROM Sales.SalesOrderHeader;

-- Clustered-index copy (clustered on SalesOrderID, no nonclustered indexes)
SELECT * INTO lesson12.SalesOrderCI
FROM Sales.SalesOrderHeader;

ALTER TABLE lesson12.SalesOrderCI
    ADD CONSTRAINT PK_lesson12_SalesOrderCI PRIMARY KEY CLUSTERED (SalesOrderID);

-- Working copy for nonclustered index demos
SELECT * INTO lesson12.SalesOrderNC
FROM Sales.SalesOrderHeader;

ALTER TABLE lesson12.SalesOrderNC
    ADD CONSTRAINT PK_lesson12_SalesOrderNC PRIMARY KEY CLUSTERED (SalesOrderID);

-- Update statistics on all copies
UPDATE STATISTICS lesson12.SalesOrderHeap;
UPDATE STATISTICS lesson12.SalesOrderCI;
UPDATE STATISTICS lesson12.SalesOrderNC;

PRINT 'Lesson 12 setup complete.';
