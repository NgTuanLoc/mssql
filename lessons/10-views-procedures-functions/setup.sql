USE AdventureWorks2022;
GO

-- Drop objects in dependency order before dropping the schema
IF SCHEMA_ID('lesson10') IS NOT NULL
BEGIN
    -- Drop functions
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP FUNCTION lesson10.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.objects WHERE schema_id = SCHEMA_ID('lesson10') AND type IN ('FN','IF','TF');
    EXEC sp_executesql @sql;

    -- Drop procedures
    SET @sql = N'';
    SELECT @sql += 'DROP PROCEDURE lesson10.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.procedures WHERE schema_id = SCHEMA_ID('lesson10');
    EXEC sp_executesql @sql;

    -- Drop views
    SET @sql = N'';
    SELECT @sql += 'DROP VIEW lesson10.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.views WHERE schema_id = SCHEMA_ID('lesson10');
    EXEC sp_executesql @sql;

    -- Drop tables
    SET @sql = N'';
    SELECT @sql += 'DROP TABLE lesson10.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson10');
    EXEC sp_executesql @sql;

    DROP SCHEMA lesson10;
END
GO
CREATE SCHEMA lesson10;
GO

-- Single-table data for the updatable-view challenge (Challenge 4): a
-- Status column with both Active and Inactive rows so a WITH CHECK OPTION
-- demo has a genuine violation to catch.
CREATE TABLE lesson10.Account (
    AccountID   INT          IDENTITY(1,1) PRIMARY KEY,
    AccountName NVARCHAR(100) NOT NULL,
    Status      VARCHAR(10)   NOT NULL DEFAULT 'Active'
);
INSERT lesson10.Account (AccountName, Status) VALUES
    (N'Contoso Retail',         'Active'),
    (N'Fabrikam Inc',           'Active'),
    (N'Northwind Traders',      'Inactive'),
    (N'Adventure Works Cycles', 'Active'),
    (N'Tailspin Toys',          'Inactive'),
    (N'Wingtip Corp',           'Active');
GO
PRINT 'Lesson 10 setup complete.';
