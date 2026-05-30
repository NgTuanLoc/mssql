-- Lesson 01 setup: creates the lesson01 schema (nothing else needed beyond AdventureWorks)
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson01') IS NOT NULL
    DROP SCHEMA lesson01;
GO
CREATE SCHEMA lesson01;
GO
-- No extra tables needed for this lesson — AdventureWorks tables are sufficient.
PRINT 'Lesson 01 setup complete.';
