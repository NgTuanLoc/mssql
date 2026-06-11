USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson18') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson18.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson18');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson18;
END
GO
CREATE SCHEMA lesson18;
GO

-- A staging table with DELIBERATE duplicates (AdventureWorks is too clean).
-- Used by the de-duplication pattern (concept 5) and exercise 6.
CREATE TABLE lesson18.CustomerStaging (
    StagingID INT IDENTITY(1,1) PRIMARY KEY,
    FirstName NVARCHAR(50)  NOT NULL,
    LastName  NVARCHAR(50)  NOT NULL,
    Email     NVARCHAR(100) NOT NULL,
    LoadDate  DATE          NOT NULL
);

-- First load: 50 real people from AdventureWorks
INSERT lesson18.CustomerStaging (FirstName, LastName, Email, LoadDate)
SELECT TOP (50) p.FirstName, p.LastName, ea.EmailAddress, '2026-01-15'
FROM Person.Person AS p
JOIN Person.EmailAddress AS ea ON ea.BusinessEntityID = p.BusinessEntityID
ORDER BY p.BusinessEntityID;

-- Second load arrives later and re-sends every 3rd record (classic ETL duplicate)
INSERT lesson18.CustomerStaging (FirstName, LastName, Email, LoadDate)
SELECT FirstName, LastName, Email, '2026-02-20'
FROM lesson18.CustomerStaging
WHERE StagingID % 3 = 0;

-- A visit log with GAPS in the dates (for the gaps & islands pattern, exercise 7).
CREATE TABLE lesson18.GymVisit (
    MemberID  INT  NOT NULL,
    VisitDate DATE NOT NULL,
    CONSTRAINT PK_lesson18_GymVisit PRIMARY KEY (MemberID, VisitDate)
);

INSERT lesson18.GymVisit (MemberID, VisitDate) VALUES
(1,'2026-03-01'),(1,'2026-03-02'),(1,'2026-03-03'),(1,'2026-03-06'),(1,'2026-03-07'),
(2,'2026-03-01'),(2,'2026-03-03'),(2,'2026-03-04'),(2,'2026-03-05'),(2,'2026-03-10'),
(3,'2026-03-02'),(3,'2026-03-03'),(3,'2026-03-04'),(3,'2026-03-05'),(3,'2026-03-06'),(3,'2026-03-07');

PRINT 'Lesson 18 setup complete.';
