USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson09') IS NOT NULL
BEGIN
    -- Remove FK constraints before dropping tables
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql +=
        'ALTER TABLE lesson09.' + QUOTENAME(t.name)
        + ' DROP CONSTRAINT ' + QUOTENAME(fk.name) + ';' + CHAR(10)
    FROM sys.foreign_keys AS fk
    JOIN sys.tables AS t ON t.object_id = fk.parent_object_id
    WHERE t.schema_id = SCHEMA_ID('lesson09');
    EXEC sp_executesql @sql;

    SET @sql = N'';
    SELECT @sql += 'DROP TABLE lesson09.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson09');
    EXEC sp_executesql @sql;

    DROP SCHEMA lesson09;
END
GO
CREATE SCHEMA lesson09;
GO

-- Demo tables created during the lesson exercises
-- (Tables are created in examples.sql and exercises.sql; setup just establishes the schema)
PRINT 'Lesson 09 setup complete.';
