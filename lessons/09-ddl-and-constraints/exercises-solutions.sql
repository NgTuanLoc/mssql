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

------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- sqlcmd sessions default to QUOTED_IDENTIFIER OFF, which breaks any DDL/DML touching a
-- PERSISTED computed column (Challenge 3). Turn it on once for the rest of this file.
SET QUOTED_IDENTIFIER ON;
GO

-- Challenge 1: lesson09.Enrollment with composite PK, two FKs, and a CHECK allowing NULL
-- Reasoning: Student/Course must exist before Enrollment's FKs can reference them; the
-- composite PK (StudentID, CourseID) IS the business rule ("one grade per student per
-- course") -- a surrogate EnrollmentID would let the same pair enroll twice. The CHECK
-- lists valid grades but must let NULL through (UNKNOWN, not FALSE) so in-progress rows
-- stay representable -- a NOT NULL-guarded version would wrongly block them. One INSERT
-- per constraint, since SQL Server reports only the first violation it hits per statement.
DROP TABLE IF EXISTS lesson09.Enrollment;
DROP TABLE IF EXISTS lesson09.Student;
DROP TABLE IF EXISTS lesson09.Course;
GO
CREATE TABLE lesson09.Student (
    StudentID INT           IDENTITY(1,1) PRIMARY KEY,
    FullName  NVARCHAR(100) NOT NULL
);
CREATE TABLE lesson09.Course (
    CourseID   INT           IDENTITY(1,1) PRIMARY KEY,
    CourseName NVARCHAR(100) NOT NULL
);
CREATE TABLE lesson09.Enrollment (
    StudentID  INT          NOT NULL,
    CourseID   INT          NOT NULL,
    EnrolledOn DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
    Grade      CHAR(1)      NULL,
    CONSTRAINT PK_Enrollment PRIMARY KEY (StudentID, CourseID),
    CONSTRAINT FK_Enrollment_Student FOREIGN KEY (StudentID)
        REFERENCES lesson09.Student (StudentID),
    CONSTRAINT FK_Enrollment_Course FOREIGN KEY (CourseID)
        REFERENCES lesson09.Course (CourseID),
    CONSTRAINT CK_Enrollment_Grade CHECK (Grade IN ('A','B','C','D','F'))
);

INSERT lesson09.Student (FullName) VALUES (N'Priya Sharma'), (N'Diego Ramirez');
INSERT lesson09.Course (CourseName) VALUES (N'Databases 101'), (N'Algorithms');

INSERT lesson09.Enrollment (StudentID, CourseID, Grade)
VALUES (1, 1, 'B'), (2, 2, NULL);   -- Diego's Algorithms grade is in progress

SELECT StudentID, CourseID, Grade FROM lesson09.Enrollment ORDER BY StudentID, CourseID;

-- Violation demos (each fails on its own -- that is correct behaviour; run this file
-- without sqlcmd's -b flag, or the first one aborts the whole batch):
-- Composite PK duplicate (same StudentID+CourseID already exists)
INSERT lesson09.Enrollment (StudentID, CourseID, Grade) VALUES (1, 1, 'A');
-- expected: Msg 2627 (PK_Enrollment)

-- FK orphan: no such StudentID
INSERT lesson09.Enrollment (StudentID, CourseID) VALUES (999, 1);
-- expected: Msg 547 (FK_Enrollment_Student)

-- FK orphan: no such CourseID
INSERT lesson09.Enrollment (StudentID, CourseID) VALUES (1, 999);
-- expected: Msg 547 (FK_Enrollment_Course)

-- CHECK reject: 'Z' is not a valid grade
INSERT lesson09.Enrollment (StudentID, CourseID, Grade) VALUES (2, 1, 'Z');
-- expected: Msg 547 (CK_Enrollment_Grade)

-- Challenge 2: one SEQUENCE feeding TicketID in two tables
-- Reasoning: IDENTITY is a per-table property -- each table's counter starts at its own
-- seed and advances independently, so two IDENTITY tables can never share one number
-- line (both would issue 1, 2, 3, ... and collide). SEQUENCE is a schema-scoped object
-- with no table ownership; pointing both tables' DEFAULT at NEXT VALUE FOR the same
-- sequence gives one shared, monotonically increasing counter no matter which table's
-- INSERT calls it -- the interleaved order below proves it.
DROP TABLE IF EXISTS lesson09.OnlineTicket;
DROP TABLE IF EXISTS lesson09.CounterTicket;
DROP SEQUENCE IF EXISTS lesson09.TicketSeq;
GO
CREATE SEQUENCE lesson09.TicketSeq
    AS INT
    START WITH 1
    INCREMENT BY 1
    NO CYCLE;
GO
CREATE TABLE lesson09.OnlineTicket (
    TicketID INT           PRIMARY KEY DEFAULT (NEXT VALUE FOR lesson09.TicketSeq),
    Subject  NVARCHAR(100) NOT NULL
);
CREATE TABLE lesson09.CounterTicket (
    TicketID INT           PRIMARY KEY DEFAULT (NEXT VALUE FOR lesson09.TicketSeq),
    Subject  NVARCHAR(100) NOT NULL
);

INSERT lesson09.OnlineTicket  (Subject) VALUES (N'Password reset');
INSERT lesson09.CounterTicket (Subject) VALUES (N'Refund request');
INSERT lesson09.OnlineTicket  (Subject) VALUES (N'Login error');
INSERT lesson09.CounterTicket (Subject) VALUES (N'Damaged item');

SELECT TicketID, Subject, N'Online' AS Source FROM lesson09.OnlineTicket
UNION ALL
SELECT TicketID, Subject, N'Counter' AS Source FROM lesson09.CounterTicket
ORDER BY TicketID;

-- Challenge 3: computed TotalPrice, CHECK enforced through its inputs
-- Reasoning: TotalPrice is generated purely from Quantity * UnitPrice, so putting the
-- CHECK on the computed column itself means the engine re-evaluates it on every write
-- that changes either input -- an UPDATE to Quantity alone re-triggers it even though
-- Quantity has no CHECK of its own. The tempting-but-wrong move is capping Quantity or
-- UnitPrice separately, which can't express "the product must stay under 10000" without
-- duplicating the multiplication and risking the two staying out of sync.
DROP TABLE IF EXISTS lesson09.OrderLine;
GO
CREATE TABLE lesson09.OrderLine (
    OrderLineID INT           IDENTITY(1,1) PRIMARY KEY,
    Quantity    INT           NOT NULL,
    UnitPrice   DECIMAL(10,2) NOT NULL,
    TotalPrice  AS (Quantity * UnitPrice) PERSISTED,
    CONSTRAINT CK_OrderLine_TotalPrice CHECK (TotalPrice <= 10000)
);

INSERT lesson09.OrderLine (Quantity, UnitPrice)
VALUES (5, 100.00), (2, 50.00), (10, 20.00);

SELECT OrderLineID, Quantity, UnitPrice, TotalPrice FROM lesson09.OrderLine ORDER BY OrderLineID;

-- Demonstrate the CHECK firing via an UPDATE to Quantity (200 * 100.00 = 20000 > 10000):
UPDATE lesson09.OrderLine SET Quantity = 200 WHERE OrderLineID = 1;
-- expected: Msg 547 (CK_OrderLine_TotalPrice)

-- Challenge 4: add a NOT NULL Status column three ways
-- Reasoning: three demo tables isolate each attempt so (a)'s failure can't block (b)/(c).
-- (a) fails (Msg 4901): no DEFAULT, nothing to backfill existing rows with. (b)'s literal
-- DEFAULT is a fast metadata-only op even on huge tables, but still holds one schema lock
-- for the whole write. (c) trades that lock for many small ones -- instant nullable ADD,
-- batched background backfill, then ALTER COLUMN NOT NULL just re-scans for NULLs --
-- worth it when the default isn't a trivial constant or any blocking window is too costly.
DROP TABLE IF EXISTS lesson09.EmployeeA;
DROP TABLE IF EXISTS lesson09.EmployeeB;
DROP TABLE IF EXISTS lesson09.EmployeeC;
GO
CREATE TABLE lesson09.EmployeeA (EmployeeID INT IDENTITY(1,1) PRIMARY KEY, FullName NVARCHAR(100) NOT NULL);
CREATE TABLE lesson09.EmployeeB (EmployeeID INT IDENTITY(1,1) PRIMARY KEY, FullName NVARCHAR(100) NOT NULL);
CREATE TABLE lesson09.EmployeeC (EmployeeID INT IDENTITY(1,1) PRIMARY KEY, FullName NVARCHAR(100) NOT NULL);

INSERT lesson09.EmployeeA (FullName) VALUES (N'Alice'), (N'Bob'), (N'Carol');
INSERT lesson09.EmployeeB (FullName) VALUES (N'Alice'), (N'Bob'), (N'Carol');
INSERT lesson09.EmployeeC (FullName) VALUES (N'Alice'), (N'Bob'), (N'Carol');

-- (a) plain ADD NOT NULL, no DEFAULT, on a populated table -- fails
-- (own batch: unlike a PK/FK/CHECK violation, this DDL failure aborts the rest of its
-- batch, not just the one statement, so it must not share a batch with (b)/(c) below)
ALTER TABLE lesson09.EmployeeA ADD Status NVARCHAR(10) NOT NULL;
-- expected: Msg 4901
GO

-- (b) ADD with a DEFAULT constraint -- works, backfills existing rows immediately
ALTER TABLE lesson09.EmployeeB
    ADD Status NVARCHAR(10) NOT NULL CONSTRAINT DF_EmployeeB_Status DEFAULT (N'Active');
GO
SELECT EmployeeID, FullName, Status FROM lesson09.EmployeeB ORDER BY EmployeeID;
GO

-- (c) nullable add + backfill + ALTER to NOT NULL
ALTER TABLE lesson09.EmployeeC ADD Status NVARCHAR(10) NULL;
GO
UPDATE lesson09.EmployeeC SET Status = N'Active' WHERE Status IS NULL;
GO
ALTER TABLE lesson09.EmployeeC ALTER COLUMN Status NVARCHAR(10) NOT NULL;
GO
SELECT EmployeeID, FullName, Status FROM lesson09.EmployeeC ORDER BY EmployeeID;
