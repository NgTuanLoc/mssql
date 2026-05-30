USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson16') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson16.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson16');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson16;
END
GO
CREATE SCHEMA lesson16;
GO

-- A table large enough to make buffer-pool and spill behaviour visible.
-- One-shot SELECT INTO with a CROSS JOIN multiplier (~121k rows x 4 = ~485k rows).
-- Because the SELECT contains a JOIN, the IDENTITY property of SalesOrderDetailID is NOT
-- carried to BigDetail, so there is no IDENTITY_INSERT conflict; the computed LineTotal
-- column is materialised as a plain column. SELECT INTO copies no constraints/indexes.
-- (First run may take a minute or two — this is expected.)
SELECT sod.*
INTO lesson16.BigDetail
FROM Sales.SalesOrderDetail AS sod
CROSS JOIN (SELECT TOP 4 object_id FROM sys.all_objects) AS m;

UPDATE STATISTICS lesson16.BigDetail;

PRINT 'Lesson 16 setup complete.';
