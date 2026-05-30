USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson04') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson04.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson04');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson04;
END
GO
CREATE SCHEMA lesson04;
GO

-- Small tables to demo join types clearly without AdventureWorks noise
CREATE TABLE lesson04.Left  (ID INT, Value NVARCHAR(20));
CREATE TABLE lesson04.Right (ID INT, Value NVARCHAR(20));

INSERT lesson04.Left  VALUES (1,'L-only'), (2,'Both-A'), (3,'Both-B');
INSERT lesson04.Right VALUES (2,'Both-A'), (3,'Both-B'), (4,'R-only');

PRINT 'Lesson 04 setup complete.';
