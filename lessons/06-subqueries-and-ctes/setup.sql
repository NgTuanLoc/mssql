USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson06') IS NOT NULL
    DROP SCHEMA lesson06;
GO
CREATE SCHEMA lesson06;
GO
PRINT 'Lesson 06 setup complete.';
