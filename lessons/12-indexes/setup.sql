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

-- Copyable table for the CHALLENGES: a clean SalesOrderHeader clone the
-- learner designs an index set for from scratch (no indexes pre-added).
SELECT * INTO lesson12.OrderCopy
FROM Sales.SalesOrderHeader;

ALTER TABLE lesson12.OrderCopy
    ADD CONSTRAINT PK_lesson12_OrderCopy PRIMARY KEY CLUSTERED (SalesOrderID);

-- Enrichment (see workflow amendment on seed-data enrichment): every order in
-- AdventureWorks ships as Status = 5, so Challenge 1's "Status = 5 AND
-- OnlineOrderFlag = 1 (99% of rows are Status 5)" premise has no negative case
-- to filter against. Flip ~1% of rows (every 100th SalesOrderID) to Status = 3
-- ("Shipped" in progress) so the 99/1 split is real and Status is minimally
-- selective, matching the challenge's design question.
UPDATE lesson12.OrderCopy
SET Status = 3
WHERE SalesOrderID % 100 = 0;

-- Update statistics on all copies
UPDATE STATISTICS lesson12.SalesOrderHeap;
UPDATE STATISTICS lesson12.SalesOrderCI;
UPDATE STATISTICS lesson12.SalesOrderNC;
UPDATE STATISTICS lesson12.OrderCopy;

PRINT 'Lesson 12 setup complete.';
