USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson14') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson14.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson14');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson14;
END
GO
CREATE SCHEMA lesson14;
GO

-- A heap copy (no clustered index) to show heap structure + forwarded records
SELECT * INTO lesson14.SalesOrderHeap
FROM Sales.SalesOrderHeader;

-- A clustered copy to inspect B-tree pages
SELECT * INTO lesson14.SalesOrderCI
FROM Sales.SalesOrderHeader;

ALTER TABLE lesson14.SalesOrderCI
    ADD CONSTRAINT PK_lesson14_SalesOrderCI PRIMARY KEY CLUSTERED (SalesOrderID);

-- A small table with a non-sequential clustered key to demonstrate page splits.
-- Low fill factor + wide rows make a split easy to trigger and observe.
CREATE TABLE lesson14.SplitDemo (
    Id        INT          NOT NULL,
    Filler    CHAR(2000)   NOT NULL DEFAULT REPLICATE('x', 2000),
    CONSTRAINT PK_lesson14_SplitDemo PRIMARY KEY CLUSTERED (Id)
        WITH (FILLFACTOR = 100)
);

-- Seed gaps (10, 20, 30, ...) so we can later insert a middle value (e.g. 15) and split a page
INSERT lesson14.SplitDemo (Id)
SELECT TOP 12 ROW_NUMBER() OVER (ORDER BY object_id) * 10
FROM sys.all_objects;

UPDATE STATISTICS lesson14.SalesOrderHeap;
UPDATE STATISTICS lesson14.SalesOrderCI;

PRINT 'Lesson 14 setup complete.';
