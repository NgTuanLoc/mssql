USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson11') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson11.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson11');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson11;
END
GO
CREATE SCHEMA lesson11;
GO

-- Accounts table for transaction demos
CREATE TABLE lesson11.Account (
    AccountID   INT           NOT NULL PRIMARY KEY,
    Owner       NVARCHAR(100) NOT NULL,
    Balance     DECIMAL(14,2) NOT NULL,
    CONSTRAINT CK_Account_Balance CHECK (Balance >= 0)
);

INSERT lesson11.Account (AccountID, Owner, Balance)
VALUES (1, N'Alice', 1000.00),
       (2, N'Bob',     500.00),
       (3, N'Carol',   250.00);

PRINT 'Lesson 11 setup complete.';
