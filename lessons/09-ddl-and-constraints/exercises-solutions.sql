USE AdventureWorks2022;
GO

-- Exercise 1: Department table with UNIQUE name and CHECK budget.
-- Approach: inline constraints for simple ones; named constraints for discoverability.
CREATE TABLE lesson09.Department (
    DepartmentID INT           IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(100) NOT NULL,
    Budget       DECIMAL(14,2) NOT NULL,
    CONSTRAINT UQ_Department_Name   UNIQUE (Name),
    CONSTRAINT CK_Department_Budget CHECK (Budget >= 0)
);

-- Exercise 2: Staff with FK to Department.
-- Approach: explicit FK name makes it easy to drop or modify later.
CREATE TABLE lesson09.Staff (
    StaffID      INT          IDENTITY(1,1) PRIMARY KEY,
    DepartmentID INT          NOT NULL,
    FirstName    NVARCHAR(100) NOT NULL,
    HireDate     DATE         NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    CONSTRAINT FK_Staff_Department FOREIGN KEY (DepartmentID)
        REFERENCES lesson09.Department (DepartmentID)
);

-- Exercise 3: Seed data.
INSERT lesson09.Department (Name, Budget) VALUES (N'Engineering', 500000), (N'Marketing', 200000);
INSERT lesson09.Staff (DepartmentID, FirstName, HireDate)
VALUES (1, N'Alice', '2022-01-15'),
       (1, N'Bob',   '2023-06-01'),
       (2, N'Carol', '2021-09-10');

-- Exercise 4: Constraint violations (both should fail — that is correct behaviour).
-- FK violation:
INSERT lesson09.Staff (DepartmentID, FirstName) VALUES (999, N'Ghost');
-- Expected error: The INSERT statement conflicted with the FOREIGN KEY constraint.

-- UNIQUE violation:
INSERT lesson09.Department (Name, Budget) VALUES (N'Engineering', 100);
-- Expected error: Violation of UNIQUE KEY constraint.

-- Exercise 5: Computed column FullName on Staff.
-- Approach: ALTER TABLE ADD ... AS expression PERSISTED stores the result on disk.
ALTER TABLE lesson09.Staff
    ADD FullName AS (FirstName + ' (hired: ' + CONVERT(VARCHAR(10), HireDate, 120) + ')') PERSISTED;

SELECT StaffID, FirstName, HireDate, FullName FROM lesson09.Staff;
