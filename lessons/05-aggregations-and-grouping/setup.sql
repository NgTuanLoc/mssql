USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson05') IS NOT NULL
    DROP SCHEMA lesson05;
GO
CREATE SCHEMA lesson05;
GO
PRINT 'Lesson 05 setup complete.';
