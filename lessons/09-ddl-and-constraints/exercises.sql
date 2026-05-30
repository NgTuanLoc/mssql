USE AdventureWorks2022;
GO

-- Exercise 1: Create a table lesson09.Department with:
--             - DepartmentID INT IDENTITY PRIMARY KEY
--             - Name NVARCHAR(100) NOT NULL, must be UNIQUE
--             - Budget DECIMAL(14,2) NOT NULL, must be >= 0
-- Your query here:


-- Exercise 2: Create a table lesson09.Staff referencing lesson09.Department with a FK.
--             Columns: StaffID INT IDENTITY PK, DepartmentID INT NOT NULL (FK),
--             FirstName NVARCHAR(100) NOT NULL, HireDate DATE NOT NULL DEFAULT today.
-- Your query here:


-- Exercise 3: Insert 2 departments and 3 staff members (at least 2 in one department).
-- Your query here:


-- Exercise 4: Try to insert a Staff row with a non-existent DepartmentID.
--             Observe the FK violation error.
--             Then try to insert a Department with a duplicate Name.
--             Observe the UNIQUE violation.
-- (These should both fail — that's the expected outcome.)
-- Your query here:


-- Exercise 5: Add a computed column FullName to lesson09.Staff that concatenates
--             FirstName with ' (hired: ' + CAST(HireDate AS VARCHAR) + ')'.
--             Mark it PERSISTED.
-- Your query here:
