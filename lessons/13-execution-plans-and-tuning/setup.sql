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

-- SARGability challenge (Challenge 1) needs a leading OrderDate index and a
-- SalesOrderNumber index so the rewritten predicates can actually seek. In the
-- SELECT * INTO copy, OrderDate and SalesOrderNumber are plain (non-computed)
-- columns, so unlike the base table they are directly seekable once indexed.
CREATE INDEX IX_lesson13_SOH_OrderDate
    ON lesson13.SalesOrderHeader (OrderDate);

CREATE INDEX IX_lesson13_SOH_SalesOrderNumber
    ON lesson13.SalesOrderHeader (SalesOrderNumber);

-- Update statistics for accurate plan estimates
UPDATE STATISTICS lesson13.SalesOrderHeader WITH FULLSCAN;
GO

-- =========================================================================
-- Skewed table for the parameter-sniffing challenge (Challenge 2).
-- 100,000 rows, deterministically generated (no RAND): CustomerID = 1 owns
-- 90,000 rows (90%); the other 10,000 are spread ~10 each over customers
-- 2..1000. The nonclustered index is KEY-ONLY, so any SELECT of a non-key
-- column (TotalDue) pays a Key Lookup per row — that is what makes a plan
-- sniffed for a tiny customer disastrous when reused for customer 1.
-- =========================================================================
CREATE TABLE lesson13.SkewedOrders
(
    OrderID    INT     IDENTITY(1,1) NOT NULL,
    CustomerID INT     NOT NULL,
    OrderDate  DATE    NOT NULL,
    TotalDue   MONEY   NOT NULL,
    CONSTRAINT PK_lesson13_SkewedOrders PRIMARY KEY CLUSTERED (OrderID)
);

;WITH
 L0 AS (SELECT 1 c UNION ALL SELECT 1),
 L1 AS (SELECT 1 c FROM L0 a CROSS JOIN L0 b),
 L2 AS (SELECT 1 c FROM L1 a CROSS JOIN L1 b),
 L3 AS (SELECT 1 c FROM L2 a CROSS JOIN L2 b),
 L4 AS (SELECT 1 c FROM L3 a CROSS JOIN L3 b),
 L5 AS (SELECT 1 c FROM L4 a CROSS JOIN L4 b),
 Nums AS (SELECT TOP (100000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) rn FROM L5)
INSERT INTO lesson13.SkewedOrders (CustomerID, OrderDate, TotalDue)
SELECT
    CASE WHEN rn <= 90000 THEN 1 ELSE 2 + (rn % 999) END      AS CustomerID,
    DATEADD(DAY, rn % 365, '2013-01-01')                      AS OrderDate,
    CAST((rn % 1000) + 10 AS MONEY)                           AS TotalDue
FROM Nums
ORDER BY rn;

-- Key-only nonclustered index: seek on CustomerID, then Key Lookup for TotalDue.
CREATE INDEX IX_lesson13_SkewedOrders_CustomerID
    ON lesson13.SkewedOrders (CustomerID);

UPDATE STATISTICS lesson13.SkewedOrders WITH FULLSCAN;

PRINT 'Lesson 13 setup complete.';
