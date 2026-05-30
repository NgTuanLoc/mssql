USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson02') IS NOT NULL
    DROP SCHEMA lesson02;
GO
CREATE SCHEMA lesson02;
GO
PRINT 'Lesson 02 setup complete.';
