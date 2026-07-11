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

------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Design lesson09.Enrollment (StudentID, CourseID, EnrolledOn, Grade):
--              composite PK, FKs to seeded Student/Course tables, CHECK on Grade IN
--              ('A','B','C','D','F') allowing NULL for in-progress, DEFAULT SYSDATETIME()
--              for EnrolledOn. Then write one INSERT per constraint that violates it,
--              each in a comment noting the expected error number.
-- Expected columns: (DDL + violation demos)
-- Your query here:


-- Challenge 2: One SEQUENCE feeding TicketID defaults in two tables (OnlineTicket,
--              CounterTicket); insert into both alternately and show the interleaved
--              global ordering. In the comment: why IDENTITY cannot do this.
-- Expected columns: TicketID, Source
-- Your query here:


-- Challenge 3: Add computed column TotalPrice AS (Quantity * UnitPrice) PERSISTED to a
--              seeded order-lines table, plus a CHECK that it's <= 10000. Demonstrate
--              the CHECK firing via an UPDATE to Quantity.
-- Expected columns: (DDL + demo)
-- Hint: Constraint on the computed column is enforced through its inputs.
-- Your query here:


-- Challenge 4: Safely add a NOT NULL Status column (default 'Active') to a populated
--              table three ways attempted: (a) plain ADD Status NVARCHAR(10) NOT NULL —
--              show the failure, (b) ADD with DEFAULT constraint — works, (c)
--              nullable-add + backfill + ALTER to NOT NULL. Comment on when (c) beats
--              (b) for huge tables.
-- Expected columns: (DDL sequence)
-- Your query here:
