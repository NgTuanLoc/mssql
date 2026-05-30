USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson17') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson17.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson17');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson17;
END
GO
CREATE SCHEMA lesson17;
GO

-- Two small tables for deterministic blocking and deadlock demos
CREATE TABLE lesson17.Account (
    AccountID INT NOT NULL PRIMARY KEY,
    Owner     NVARCHAR(50) NOT NULL,
    Balance   DECIMAL(14,2) NOT NULL
);
INSERT lesson17.Account VALUES (1, N'Alice', 1000), (2, N'Bob', 500), (3, N'Carol', 250);

-- A larger table so a big UPDATE triggers lock escalation (row/page locks escalate to a table lock).
-- Single SELECT INTO copy (no re-insert), then add the clustered PK.
SELECT * INTO lesson17.BigOrders FROM Sales.SalesOrderHeader;
ALTER TABLE lesson17.BigOrders ADD CONSTRAINT PK_lesson17_BigOrders PRIMARY KEY CLUSTERED (SalesOrderID);

PRINT 'Lesson 17 setup complete.';
