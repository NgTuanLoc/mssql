# Tier 3 Lessons Implementation Plan (Lessons 09–11)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write the three Tier 3 "Programming MSSQL" lessons — DDL & Constraints, Views/Stored Procedures/Functions, and Transactions/Errors/Concurrency — each containing README.md, setup.sql, examples.sql, exercises.sql, and exercises-solutions.sql.

**Architecture:** Each lesson directory is self-contained. `setup.sql` is idempotent and uses a dedicated `lessonNN` schema. Lesson 11 (transactions/concurrency) requires two simultaneous connections to demonstrate isolation levels and deadlocks; the README guides the learner through the two-session walkthrough. AdventureWorks is the default dataset.

**Tech Stack:** T-SQL (MSSQL 2022 Developer), AdventureWorks2022, Markdown.

**Prerequisite:** Infrastructure plan (Plan 01) complete — container running, AdventureWorks restored.

---

## File Map

| Action | Path |
|--------|------|
| Create | `lessons/09-ddl-and-constraints/` (5 files) |
| Create | `lessons/10-views-procedures-functions/` (5 files) |
| Create | `lessons/11-transactions-errors-concurrency/` (5 files) |

---

## Task 1: Lesson 09 — DDL & Constraints

**Files:** `lessons/09-ddl-and-constraints/` (5 files)

- [ ] **Step 1: Create `lessons/09-ddl-and-constraints/setup.sql`**

```sql
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
```

- [ ] **Step 2: Create `lessons/09-ddl-and-constraints/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- Example 1: CREATE TABLE with common constraints
DROP TABLE IF EXISTS lesson09.Orders;
DROP TABLE IF EXISTS lesson09.Customers;
GO

CREATE TABLE lesson09.Customers (
    CustomerID   INT           IDENTITY(1,1) PRIMARY KEY,
    Email        NVARCHAR(200) NOT NULL,
    FullName     NVARCHAR(200) NOT NULL,
    CreatedAt    DATETIME2(7)  NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_Customers_Email UNIQUE (Email)
);

CREATE TABLE lesson09.Orders (
    OrderID      INT           IDENTITY(1,1) PRIMARY KEY,
    CustomerID   INT           NOT NULL,
    OrderDate    DATETIME2(7)  NOT NULL DEFAULT SYSDATETIME(),
    TotalAmount  DECIMAL(14,2) NOT NULL,
    Status       TINYINT       NOT NULL DEFAULT 1,
    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerID)
        REFERENCES lesson09.Customers (CustomerID),
    CONSTRAINT CK_Orders_TotalAmount CHECK (TotalAmount >= 0),
    CONSTRAINT CK_Orders_Status      CHECK (Status BETWEEN 1 AND 5)
);

-- Example 2: INSERT data
INSERT lesson09.Customers (Email, FullName)
VALUES (N'alice@example.com', N'Alice Smith'),
       (N'bob@example.com',   N'Bob Jones');

INSERT lesson09.Orders (CustomerID, TotalAmount)
VALUES (1, 250.00), (1, 89.50), (2, 1200.00);

-- Example 3: IDENTITY — see current seed and increment
DBCC CHECKIDENT('lesson09.Customers', NORESEED);
-- Reseed example (dangerous in production — demo only)
-- DBCC CHECKIDENT('lesson09.Customers', RESEED, 1000);

-- Example 4: Computed column
DROP TABLE IF EXISTS lesson09.Invoice;
GO
CREATE TABLE lesson09.Invoice (
    InvoiceID    INT           IDENTITY(1,1) PRIMARY KEY,
    Quantity     INT           NOT NULL,
    UnitPrice    DECIMAL(10,2) NOT NULL,
    LineTotal    AS (Quantity * UnitPrice) PERSISTED   -- stored on disk
);

INSERT lesson09.Invoice (Quantity, UnitPrice) VALUES (3, 25.00), (10, 4.99);
SELECT * FROM lesson09.Invoice;   -- LineTotal computed automatically

-- Example 5: ALTER TABLE — add a column and a constraint
ALTER TABLE lesson09.Customers
    ADD PhoneNumber NVARCHAR(20) NULL;

ALTER TABLE lesson09.Orders
    ADD CONSTRAINT DF_Orders_TotalAmount DEFAULT (0) FOR TotalAmount;

-- Example 6: Schemas — create and assign a table to a non-default schema
-- (lesson09 already exists; this shows the syntax)
SELECT SCHEMA_NAME(schema_id) AS SchemaName, name AS TableName
FROM sys.tables
WHERE schema_id = SCHEMA_ID('lesson09');

-- Example 7: Sequence object (alternative to IDENTITY for shared sequences)
DROP SEQUENCE IF EXISTS lesson09.InvoiceSeq;
CREATE SEQUENCE lesson09.InvoiceSeq
    AS INT
    START WITH 1000
    INCREMENT BY 1
    NO CYCLE;

SELECT NEXT VALUE FOR lesson09.InvoiceSeq AS NextID;
SELECT NEXT VALUE FOR lesson09.InvoiceSeq AS NextID;
```

- [ ] **Step 3: Create `lessons/09-ddl-and-constraints/exercises.sql`**

```sql
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
```

- [ ] **Step 4: Create `lessons/09-ddl-and-constraints/exercises-solutions.sql`**

```sql
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
```

- [ ] **Step 5: Create `lessons/09-ddl-and-constraints/README.md`**

```markdown
# Lesson 09 — DDL & Constraints

## What you'll learn
- `CREATE TABLE` with `PRIMARY KEY`, `FOREIGN KEY`, `UNIQUE`, `CHECK`, `DEFAULT`
- `IDENTITY` vs `SEQUENCE` for auto-increment values
- Computed columns (`AS expression PERSISTED`)
- `ALTER TABLE` to add columns and constraints
- Schema organisation

## Setup
Run `setup.sql` once (creates empty `lesson09` schema).

## Concepts

### CREATE TABLE skeleton

```sql
CREATE TABLE schema.TableName (
    ID       INT           IDENTITY(1,1) PRIMARY KEY,
    Code     VARCHAR(20)   NOT NULL,
    Amount   DECIMAL(10,2) NOT NULL DEFAULT 0,
    ParentID INT           NULL,
    CONSTRAINT UQ_TableName_Code   UNIQUE (Code),
    CONSTRAINT CK_TableName_Amount CHECK (Amount >= 0),
    CONSTRAINT FK_TableName_Parent FOREIGN KEY (ParentID)
        REFERENCES schema.Parent (ID)
);
```

### Constraint types

| Constraint | Enforces |
|---|---|
| `PRIMARY KEY` | Uniqueness + NOT NULL; one per table |
| `UNIQUE` | Uniqueness; NULLs allowed (one NULL per column by default) |
| `FOREIGN KEY` | Referential integrity to another table |
| `CHECK` | Arbitrary boolean condition |
| `DEFAULT` | Value inserted when column is omitted |

### IDENTITY vs SEQUENCE

- `IDENTITY(seed, increment)` — per-table auto-increment. Cannot be shared across tables.
- `SEQUENCE` — standalone object; shareable, gapless options available, can be queried with `NEXT VALUE FOR`.

### Computed columns

```sql
LineTotal AS (Quantity * UnitPrice) PERSISTED
```

`PERSISTED` stores the result on disk — faster reads, slower writes. Non-persisted columns are computed on every read.

## Worked Examples (lesson09 schema)
1. `CREATE TABLE` with PK, UNIQUE, FK, CHECK, DEFAULT.
2. `INSERT` data respecting constraints.
3. `DBCC CHECKIDENT` — inspect IDENTITY seed.
4. Computed column: `LineTotal AS (Quantity * UnitPrice) PERSISTED`.
5. `ALTER TABLE ADD` column and constraint.
6. Explore schema membership via `sys.tables`.
7. `SEQUENCE` object for shared auto-increment.

## Pitfalls
- Always name constraints (`CONSTRAINT CK_...`). Unnamed constraints get system-generated names like `CK__TableName__Amount__3C69FB99` — impossible to reference in `ALTER TABLE DROP CONSTRAINT`.
- `FOREIGN KEY` actions default to `NO ACTION` (error on violation). Consider `ON DELETE CASCADE` only for child records that have no meaning without the parent.
- `IDENTITY` gaps are normal — a rolled-back transaction still advances the identity counter.
- `UNIQUE` allows multiple NULL values (NULL ≠ NULL in SQL); only one per column if the column is `NOT NULL`.

## Cheatsheet link
See `cheatsheets/00-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/09-ddl-and-constraints/
git commit -m "feat: add lesson 09 - DDL and constraints"
```

---

## Task 2: Lesson 10 — Views, Stored Procedures, Functions

**Files:** `lessons/10-views-procedures-functions/` (5 files)

- [ ] **Step 1: Create `lessons/10-views-procedures-functions/setup.sql`**

```sql
USE AdventureWorks2022;
GO

-- Drop objects in dependency order before dropping the schema
IF SCHEMA_ID('lesson10') IS NOT NULL
BEGIN
    -- Drop functions
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP FUNCTION lesson10.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.objects WHERE schema_id = SCHEMA_ID('lesson10') AND type IN ('FN','IF','TF');
    EXEC sp_executesql @sql;

    -- Drop procedures
    SET @sql = N'';
    SELECT @sql += 'DROP PROCEDURE lesson10.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.procedures WHERE schema_id = SCHEMA_ID('lesson10');
    EXEC sp_executesql @sql;

    -- Drop views
    SET @sql = N'';
    SELECT @sql += 'DROP VIEW lesson10.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.views WHERE schema_id = SCHEMA_ID('lesson10');
    EXEC sp_executesql @sql;

    -- Drop tables
    SET @sql = N'';
    SELECT @sql += 'DROP TABLE lesson10.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson10');
    EXEC sp_executesql @sql;

    DROP SCHEMA lesson10;
END
GO
CREATE SCHEMA lesson10;
GO
PRINT 'Lesson 10 setup complete.';
```

- [ ] **Step 2: Create `lessons/10-views-procedures-functions/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- =========================================================================
-- VIEWS
-- =========================================================================

-- Example 1: Simple view — customer order summary
CREATE OR ALTER VIEW lesson10.vw_CustomerOrderSummary AS
SELECT
    c.CustomerID,
    p.FirstName + ' ' + p.LastName AS FullName,
    COUNT(soh.SalesOrderID)        AS OrderCount,
    SUM(soh.TotalDue)              AS TotalSpend,
    MAX(soh.OrderDate)             AS LastOrderDate
FROM Sales.Customer          AS c
JOIN Person.Person           AS p   ON p.BusinessEntityID = c.PersonID
JOIN Sales.SalesOrderHeader  AS soh ON soh.CustomerID     = c.CustomerID
GROUP BY c.CustomerID, p.FirstName, p.LastName;
GO

-- Query the view like a table
SELECT TOP 10 * FROM lesson10.vw_CustomerOrderSummary ORDER BY TotalSpend DESC;

-- =========================================================================
-- STORED PROCEDURES
-- =========================================================================

-- Example 2: Parameterised stored procedure
CREATE OR ALTER PROCEDURE lesson10.usp_GetOrdersByCustomer
    @CustomerID INT,
    @MinAmount  MONEY = 0   -- optional parameter with default
AS
BEGIN
    SET NOCOUNT ON;   -- suppresses "X rows affected" messages

    SELECT
        SalesOrderID,
        OrderDate,
        TotalDue
    FROM Sales.SalesOrderHeader
    WHERE CustomerID = @CustomerID
      AND TotalDue  >= @MinAmount
    ORDER BY OrderDate DESC;
END;
GO

-- Call the procedure
EXEC lesson10.usp_GetOrdersByCustomer @CustomerID = 11000;
EXEC lesson10.usp_GetOrdersByCustomer @CustomerID = 11000, @MinAmount = 500;

-- Example 3: Procedure with OUTPUT parameter
CREATE OR ALTER PROCEDURE lesson10.usp_GetCustomerOrderCount
    @CustomerID INT,
    @OrderCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @OrderCount = COUNT(*)
    FROM Sales.SalesOrderHeader
    WHERE CustomerID = @CustomerID;
END;
GO

DECLARE @Count INT;
EXEC lesson10.usp_GetCustomerOrderCount @CustomerID = 11000, @OrderCount = @Count OUTPUT;
PRINT 'Orders: ' + CAST(@Count AS VARCHAR);

-- =========================================================================
-- FUNCTIONS
-- =========================================================================

-- Example 4: Scalar function — compute age in years
CREATE OR ALTER FUNCTION lesson10.fn_AgeInYears (@BirthDate DATE)
RETURNS INT
AS
BEGIN
    RETURN DATEDIFF(YEAR, @BirthDate, GETDATE())
         - CASE WHEN MONTH(GETDATE()) < MONTH(@BirthDate)
                  OR (MONTH(GETDATE()) = MONTH(@BirthDate)
                      AND DAY(GETDATE()) < DAY(@BirthDate))
                THEN 1 ELSE 0 END;
END;
GO

-- Scalar UDF called in a SELECT (note: scalar UDFs can slow queries — see pitfalls)
SELECT
    BusinessEntityID,
    BirthDate,
    lesson10.fn_AgeInYears(BirthDate) AS Age
FROM HumanResources.Employee;

-- Example 5: Inline Table-Valued Function (iTVF) — preferred over scalar UDFs for sets
CREATE OR ALTER FUNCTION lesson10.fn_GetProductsByCategory (@CategoryID INT)
RETURNS TABLE
AS
RETURN (
    SELECT
        p.ProductID,
        p.Name,
        p.ListPrice,
        ps.Name AS SubcategoryName
    FROM Production.Product            AS p
    JOIN Production.ProductSubcategory AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
    JOIN Production.ProductCategory    AS pc ON pc.ProductCategoryID    = ps.ProductCategoryID
    WHERE pc.ProductCategoryID = @CategoryID
);
GO

-- Use the iTVF like a table
SELECT * FROM lesson10.fn_GetProductsByCategory(1) ORDER BY ListPrice DESC;

-- CROSS APPLY — call the iTVF for each row of another table
SELECT
    pc.ProductCategoryID,
    pc.Name AS Category,
    p.Name  AS TopProduct,
    p.ListPrice
FROM Production.ProductCategory AS pc
CROSS APPLY (
    SELECT TOP 1 *
    FROM lesson10.fn_GetProductsByCategory(pc.ProductCategoryID)
    ORDER BY ListPrice DESC
) AS p;
```

- [ ] **Step 3: Create `lessons/10-views-procedures-functions/exercises.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Create a view lesson10.vw_TopProducts that returns the top 20
--             products by ListPrice with their category name.
-- Expected columns: ProductID, ProductName, CategoryName, ListPrice
-- Your query here:


-- Exercise 2: Create a stored procedure lesson10.usp_SearchProducts that accepts
--             @Keyword NVARCHAR(100) and returns all products whose Name contains
--             the keyword (case-insensitive). Default keyword = '%' (returns all).
-- Test it with EXEC lesson10.usp_SearchProducts @Keyword = 'Road'
-- Your query here:


-- Exercise 3: Create an inline table-valued function lesson10.fn_OrdersInDateRange
--             that accepts @StartDate DATE and @EndDate DATE and returns all orders
--             with their OrderDate, CustomerID, and TotalDue within that range.
-- Test it with SELECT * FROM lesson10.fn_OrdersInDateRange('2013-01-01','2013-03-31')
-- Your query here:


-- Exercise 4: Create a scalar function lesson10.fn_FormatMoney that takes a MONEY
--             value and returns it as NVARCHAR formatted with a $ sign and 2 decimal places.
--             e.g. 1234.5 → '$1,234.50'
-- Hint: FORMAT(value, 'C', 'en-US') returns currency format.
-- Your query here:


-- Exercise 5: Call lesson10.fn_FormatMoney from a SELECT to format the TotalDue
--             of the 5 largest orders in Sales.SalesOrderHeader.
-- Expected columns: SalesOrderID, TotalDue, FormattedTotal
-- Your query here:
```

- [ ] **Step 4: Create `lessons/10-views-procedures-functions/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: View for top 20 products by price.
-- Approach: CREATE OR ALTER VIEW; TOP inside a view requires ORDER BY to be meaningful.
CREATE OR ALTER VIEW lesson10.vw_TopProducts AS
SELECT TOP 20
    p.ProductID,
    p.Name       AS ProductName,
    pc.Name      AS CategoryName,
    p.ListPrice
FROM Production.Product             AS p
JOIN Production.ProductSubcategory  AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
JOIN Production.ProductCategory     AS pc ON pc.ProductCategoryID    = ps.ProductCategoryID
WHERE p.ListPrice > 0
ORDER BY p.ListPrice DESC;
GO

SELECT * FROM lesson10.vw_TopProducts;

-- Exercise 2: Procedure to search products by keyword.
-- Approach: LIKE with wildcards around the keyword; default '%' matches everything.
CREATE OR ALTER PROCEDURE lesson10.usp_SearchProducts
    @Keyword NVARCHAR(100) = N'%'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ProductID, Name, ListPrice
    FROM Production.Product
    WHERE Name LIKE N'%' + @Keyword + N'%'
    ORDER BY Name;
END;
GO

EXEC lesson10.usp_SearchProducts @Keyword = N'Road';

-- Exercise 3: Inline TVF for orders in a date range.
-- Approach: RETURNS TABLE AS RETURN — single SELECT, no BEGIN/END, fastest type of UDF.
CREATE OR ALTER FUNCTION lesson10.fn_OrdersInDateRange
    (@StartDate DATE, @EndDate DATE)
RETURNS TABLE
AS
RETURN (
    SELECT SalesOrderID, OrderDate, CustomerID, TotalDue
    FROM Sales.SalesOrderHeader
    WHERE OrderDate >= @StartDate
      AND OrderDate <  DATEADD(DAY, 1, @EndDate)  -- inclusive end
);
GO

SELECT * FROM lesson10.fn_OrdersInDateRange('2013-01-01','2013-03-31') ORDER BY OrderDate;

-- Exercise 4: Scalar function for currency formatting.
-- Approach: FORMAT with culture 'en-US' produces US currency string.
CREATE OR ALTER FUNCTION lesson10.fn_FormatMoney (@Amount MONEY)
RETURNS NVARCHAR(50)
AS
BEGIN
    RETURN FORMAT(@Amount, 'C', 'en-US');
END;
GO

-- Exercise 5: Apply fn_FormatMoney to top 5 orders.
-- Approach: scalar UDF called in SELECT; acceptable here for 5 rows.
SELECT TOP 5
    SalesOrderID,
    TotalDue,
    lesson10.fn_FormatMoney(TotalDue) AS FormattedTotal
FROM Sales.SalesOrderHeader
ORDER BY TotalDue DESC;
```

- [ ] **Step 5: Create `lessons/10-views-procedures-functions/README.md`**

```markdown
# Lesson 10 — Views, Stored Procedures, Functions

## What you'll learn
- When to use a view vs a stored procedure vs a function
- `CREATE OR ALTER VIEW` — updatable and non-updatable
- Parameterised stored procedures with `SET NOCOUNT ON`, `OUTPUT` parameters
- Scalar UDFs and why to avoid them for large row sets
- Inline Table-Valued Functions (iTVFs) and `CROSS APPLY`

## Setup
Run `setup.sql` once. It drops and re-creates the `lesson10` schema (including any leftover views, procs, functions from a previous run).

## Concepts

### Views

```sql
CREATE OR ALTER VIEW schema.vw_Name AS
SELECT ...;
```

Views are stored queries — not stored data (unless indexed). Use them to:
- Simplify complex joins for callers
- Restrict column access (security)
- Provide stable interfaces when underlying tables change

### Stored Procedures

```sql
CREATE OR ALTER PROCEDURE schema.usp_Name
    @Param1 INT,
    @Param2 NVARCHAR(100) = N'default'  -- optional with default
AS
BEGIN
    SET NOCOUNT ON;  -- always include; suppresses row-count messages
    ...
END;
```

Use procs for:
- Business logic that involves multiple statements
- DML operations called by applications
- Long-running batch operations

### Functions — when to use which

| Type | Returns | Use for | Avoid when |
|---|---|---|---|
| Scalar UDF | single value | Simple calculations on a single input | Called per-row in a large SELECT — they block parallelism |
| Inline TVF | TABLE (single SELECT) | Parameterised views, reusable filter logic | — |
| Multi-statement TVF | TABLE (multiple stmts) | Complex logic that builds a result set | Usually; prefer iTVF or proc instead |

**Scalar UDF performance trap:** calling a scalar UDF in a `WHERE` or `SELECT` against a large table executes the function once per row and prevents the optimizer from parallelising the query. For large data volumes, inline the logic or use an iTVF.

### CROSS APPLY vs OUTER APPLY

```sql
-- CROSS APPLY: like INNER JOIN — only rows where the TVF returns results
FROM table CROSS APPLY fn(table.col) AS t

-- OUTER APPLY: like LEFT JOIN — all rows; NULLs when TVF returns nothing
FROM table OUTER APPLY fn(table.col) AS t
```

## Worked Examples (lesson10 schema + AdventureWorks)
1. View: `vw_CustomerOrderSummary` — per-customer totals.
2. Procedure: `usp_GetOrdersByCustomer` with optional `@MinAmount`.
3. Procedure with `OUTPUT` parameter: `usp_GetCustomerOrderCount`.
4. Scalar UDF: `fn_AgeInYears` — correct age calculation.
5. Inline TVF: `fn_GetProductsByCategory` used with `CROSS APPLY`.

## Pitfalls
- Scalar UDFs prevent parallelism — rewrite as iTVF or inline expression for large queries.
- Views are not materialised — a complex view called in a query can still be slow.
- `TOP` inside a view without `ORDER BY` produces arbitrary results — views with `TOP` need `WITH TIES` or an `ORDER BY` applied by the calling query.
- `SET NOCOUNT ON` in procs prevents extra result-set messages that confuse some clients.
- Proc parameters cannot have table types passed from external clients without TVP (Table-Valued Parameters) — out of scope for this lesson.

## Cheatsheet link
See `cheatsheets/00-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/10-views-procedures-functions/
git commit -m "feat: add lesson 10 - views procedures and functions"
```

---

## Task 3: Lesson 11 — Transactions, Errors & Concurrency

**Files:** `lessons/11-transactions-errors-concurrency/` (5 files)

- [ ] **Step 1: Create `lessons/11-transactions-errors-concurrency/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson11') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson11.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson11');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson11;
END
GO
CREATE SCHEMA lesson11;
GO

-- Accounts table for transaction demos
CREATE TABLE lesson11.Account (
    AccountID   INT           NOT NULL PRIMARY KEY,
    Owner       NVARCHAR(100) NOT NULL,
    Balance     DECIMAL(14,2) NOT NULL,
    CONSTRAINT CK_Account_Balance CHECK (Balance >= 0)
);

INSERT lesson11.Account (AccountID, Owner, Balance)
VALUES (1, N'Alice', 1000.00),
       (2, N'Bob',     500.00),
       (3, N'Carol',   250.00);

PRINT 'Lesson 11 setup complete.';
```

- [ ] **Step 2: Create `lessons/11-transactions-errors-concurrency/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- =========================================================================
-- TRANSACTIONS
-- =========================================================================

-- Example 1: Basic BEGIN / COMMIT / ROLLBACK
BEGIN TRANSACTION;

UPDATE lesson11.Account SET Balance = Balance - 100 WHERE AccountID = 1; -- Alice pays
UPDATE lesson11.Account SET Balance = Balance + 100 WHERE AccountID = 2; -- Bob receives

-- Verify before committing
SELECT * FROM lesson11.Account WHERE AccountID IN (1, 2);

COMMIT TRANSACTION;
-- Both updates are now permanent.

-- Example 2: ROLLBACK on error
BEGIN TRANSACTION;

UPDATE lesson11.Account SET Balance = Balance - 2000 WHERE AccountID = 1;
-- This would violate the CHECK constraint (Balance < 0)

IF @@ROWCOUNT = 0 OR (SELECT Balance FROM lesson11.Account WHERE AccountID = 1) < 0
BEGIN
    ROLLBACK TRANSACTION;
    PRINT 'Transfer cancelled — insufficient funds.';
END
ELSE
    COMMIT TRANSACTION;

-- =========================================================================
-- TRY / CATCH
-- =========================================================================

-- Example 3: TRY/CATCH with THROW
BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE lesson11.Account SET Balance = Balance - 300 WHERE AccountID = 1;
    UPDATE lesson11.Account SET Balance = Balance + 300 WHERE AccountID = 3;

    COMMIT TRANSACTION;
    PRINT 'Transfer complete.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'Error caught: ' + ERROR_MESSAGE();
    THROW;   -- re-raise to caller
END CATCH;

-- Example 4: TRY/CATCH — trigger a CHECK constraint violation deliberately
BEGIN TRY
    UPDATE lesson11.Account SET Balance = -1 WHERE AccountID = 1;
END TRY
BEGIN CATCH
    PRINT 'Severity:  ' + CAST(ERROR_SEVERITY() AS VARCHAR);
    PRINT 'State:     ' + CAST(ERROR_STATE()    AS VARCHAR);
    PRINT 'Message:   ' + ERROR_MESSAGE();
    PRINT 'Line:      ' + CAST(ERROR_LINE()     AS VARCHAR);
END CATCH;

-- =========================================================================
-- ISOLATION LEVELS  (run each block in a separate SSMS window for demos)
-- =========================================================================

-- Example 5: READ COMMITTED (default) — cannot read uncommitted data
-- Session 1:
BEGIN TRANSACTION;
UPDATE lesson11.Account SET Balance = 9999 WHERE AccountID = 1;
-- Do NOT commit yet. Switch to Session 2:
-- Session 2: SELECT * FROM lesson11.Account WHERE AccountID = 1;
-- Session 2 blocks until Session 1 commits or rolls back.
-- Session 1:
ROLLBACK;

-- Example 6: READ COMMITTED SNAPSHOT (RCSI) — non-blocking reads
-- Enable RCSI on the database (run once; requires no active connections):
-- ALTER DATABASE AdventureWorks2022 SET READ_COMMITTED_SNAPSHOT ON;
-- With RCSI, Session 2's SELECT above would return the committed value (1000)
-- instead of blocking.

-- Example 7: Check current isolation level
SELECT
    session_id,
    transaction_isolation_level,
    CASE transaction_isolation_level
        WHEN 0 THEN 'Unspecified'
        WHEN 1 THEN 'Read Uncommitted'
        WHEN 2 THEN 'Read Committed'
        WHEN 3 THEN 'Repeatable Read'
        WHEN 4 THEN 'Serializable'
        WHEN 5 THEN 'Snapshot'
    END AS IsolationLevelName
FROM sys.dm_exec_sessions
WHERE session_id = @@SPID;
```

- [ ] **Step 3: Create `lessons/11-transactions-errors-concurrency/exercises.sql`**

```sql
USE AdventureWorks2022;
GO
-- Re-run setup.sql to reset Account balances before retrying exercises.

-- Exercise 1: Write a transaction that transfers $200 from AccountID 1 to AccountID 2.
--             If the transfer would leave AccountID 1 with a negative balance, ROLLBACK
--             and print 'Insufficient funds'. Otherwise COMMIT.
-- Your query here:


-- Exercise 2: Wrap Exercise 1 in TRY/CATCH. After a successful transfer, also insert
--             a log row into a table variable (columns: TransferID INT, Amount MONEY,
--             TransferredAt DATETIME2). SELECT from the table variable at the end.
-- Your query here:


-- Exercise 3: Deliberately cause a divide-by-zero error inside a TRY block.
--             In the CATCH block, print the error number, message, and line number.
--             Do NOT re-throw — just report and continue.
-- Your query here:


-- Exercise 4: Write a stored procedure lesson11.usp_Transfer that:
--             - Accepts @FromID INT, @ToID INT, @Amount MONEY
--             - Uses TRY/CATCH and a transaction
--             - THROWs a custom error (number 50001, severity 16, state 1) if
--               the source account doesn't have enough balance
--             - Returns 0 on success
-- Test: EXEC lesson11.usp_Transfer @FromID=1, @ToID=2, @Amount=5000 (should fail)
--       EXEC lesson11.usp_Transfer @FromID=1, @ToID=2, @Amount=50   (should succeed)
-- Your query here:


-- Exercise 5: Using sys.dm_exec_sessions, write a query that shows your current
--             session ID, isolation level name, and login name.
-- Your query here:
```

- [ ] **Step 4: Create `lessons/11-transactions-errors-concurrency/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Transfer $200 with manual rollback on insufficient funds.
-- Approach: check balance after update; if negative, rollback.
BEGIN TRANSACTION;

UPDATE lesson11.Account SET Balance = Balance - 200 WHERE AccountID = 1;

IF (SELECT Balance FROM lesson11.Account WHERE AccountID = 1) < 0
BEGIN
    ROLLBACK TRANSACTION;
    PRINT 'Insufficient funds.';
END
ELSE
BEGIN
    UPDATE lesson11.Account SET Balance = Balance + 200 WHERE AccountID = 2;
    COMMIT TRANSACTION;
    PRINT 'Transfer complete.';
END;

-- Exercise 2: Transfer with TRY/CATCH and table-variable log.
-- Approach: capture the committed transfer in a table variable using OUTPUT.
DECLARE @Log TABLE (TransferID INT IDENTITY(1,1), Amount MONEY, TransferredAt DATETIME2);

BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE lesson11.Account SET Balance = Balance - 200 WHERE AccountID = 1;

    IF (SELECT Balance FROM lesson11.Account WHERE AccountID = 1) < 0
        THROW 50000, 'Insufficient funds.', 1;

    UPDATE lesson11.Account SET Balance = Balance + 200 WHERE AccountID = 2;
    COMMIT TRANSACTION;

    INSERT @Log (Amount, TransferredAt) VALUES (200, SYSDATETIME());
    PRINT 'Transfer logged.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'Error: ' + ERROR_MESSAGE();
END CATCH;

SELECT * FROM @Log;

-- Exercise 3: Deliberate divide-by-zero, caught and reported.
-- Approach: SELECT 1/0 triggers error 8134; CATCH reports without re-throwing.
BEGIN TRY
    SELECT 1 / 0 AS Oops;
END TRY
BEGIN CATCH
    PRINT 'Error number: ' + CAST(ERROR_NUMBER()   AS VARCHAR);
    PRINT 'Message:      ' + ERROR_MESSAGE();
    PRINT 'Line:         ' + CAST(ERROR_LINE()     AS VARCHAR);
END CATCH;

-- Exercise 4: usp_Transfer stored procedure.
-- Approach: THROW with a user-defined error number (50001–2147483647) and severity 16.
CREATE OR ALTER PROCEDURE lesson11.usp_Transfer
    @FromID INT,
    @ToID   INT,
    @Amount MONEY
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE lesson11.Account SET Balance = Balance - @Amount WHERE AccountID = @FromID;

        IF (SELECT Balance FROM lesson11.Account WHERE AccountID = @FromID) < 0
            THROW 50001, 'Insufficient balance for the requested transfer.', 1;

        UPDATE lesson11.Account SET Balance = Balance + @Amount WHERE AccountID = @ToID;

        COMMIT TRANSACTION;
        RETURN 0;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;  -- re-raise to caller
    END CATCH;
END;
GO

EXEC lesson11.usp_Transfer @FromID = 1, @ToID = 2, @Amount = 5000;  -- should fail
GO
EXEC lesson11.usp_Transfer @FromID = 1, @ToID = 2, @Amount = 50;    -- should succeed
GO
SELECT * FROM lesson11.Account;

-- Exercise 5: Current session isolation level.
-- Approach: sys.dm_exec_sessions; @@SPID = current session ID.
SELECT
    session_id,
    login_name,
    CASE transaction_isolation_level
        WHEN 0 THEN 'Unspecified'
        WHEN 1 THEN 'Read Uncommitted'
        WHEN 2 THEN 'Read Committed'
        WHEN 3 THEN 'Repeatable Read'
        WHEN 4 THEN 'Serializable'
        WHEN 5 THEN 'Snapshot'
    END AS IsolationLevel
FROM sys.dm_exec_sessions
WHERE session_id = @@SPID;
```

- [ ] **Step 5: Create `lessons/11-transactions-errors-concurrency/README.md`**

```markdown
# Lesson 11 — Transactions, Errors & Concurrency

## What you'll learn
- `BEGIN TRANSACTION` / `COMMIT` / `ROLLBACK`
- `TRY / CATCH` and `THROW` for error handling
- `ERROR_MESSAGE()`, `ERROR_NUMBER()`, `ERROR_LINE()`, `ERROR_SEVERITY()`
- Isolation levels: Read Committed, RCSI, Repeatable Read, Snapshot, Serializable
- Deadlock demo (two-session walkthrough)

## Setup
Run `setup.sql` once. It creates the `lesson11` schema with an `Account` table (three rows). Re-run `setup.sql` to reset balances between exercise attempts.

## Concepts

### Transaction boundaries

```sql
BEGIN TRANSACTION;
  -- one or more DML statements
COMMIT TRANSACTION;   -- makes changes permanent
-- or
ROLLBACK TRANSACTION; -- undoes all changes since BEGIN TRANSACTION
```

`@@TRANCOUNT` tracks nesting level. Always check `IF @@TRANCOUNT > 0` before rolling back inside a CATCH block.

### TRY / CATCH

```sql
BEGIN TRY
    BEGIN TRANSACTION;
    -- risky statements
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;   -- re-raise the original error to the caller
END CATCH;
```

`THROW` (SQL Server 2012+) re-raises the current error with its original number. `RAISERROR` is the older alternative.

### Custom errors with THROW

```sql
THROW 50001, 'Custom error message.', 1;
-- number must be >= 50000; severity 16 = non-fatal user error
```

### Isolation levels

| Level | Dirty reads | Non-repeatable reads | Phantom reads | Locking behaviour |
|---|---|---|---|---|
| Read Uncommitted | yes | yes | yes | No shared locks — fastest, least safe |
| **Read Committed** (default) | no | yes | yes | Shared lock released immediately after read |
| RCSI | no | yes | yes | Row versioning — readers don't block writers |
| Repeatable Read | no | no | yes | Shared locks held until end of transaction |
| Serializable | no | no | no | Range locks — safest, highest contention |
| Snapshot | no | no | no | Row versioning — fully non-blocking reads |

Set per-session: `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;`

**RCSI** (Read Committed Snapshot Isolation) is highly recommended for OLTP workloads. Enable with:
```sql
ALTER DATABASE AdventureWorks2022 SET READ_COMMITTED_SNAPSHOT ON;
```

### Deadlock demo (two SSMS sessions)

```sql
-- Session 1                          -- Session 2
BEGIN TRAN;                            BEGIN TRAN;
UPDATE lesson11.Account                UPDATE lesson11.Account
  SET Balance = 1 WHERE AccountID=1;    SET Balance = 2 WHERE AccountID=2;
-- wait...                             -- wait...
UPDATE lesson11.Account                UPDATE lesson11.Account
  SET Balance = 2 WHERE AccountID=2;    SET Balance = 1 WHERE AccountID=1;
-- One session becomes the deadlock victim (error 1205) and rolls back.
```

SQL Server detects the cycle and kills one session (the "deadlock victim"). The victim receives error 1205. The other session's transaction completes.

## Worked Examples (lesson11 schema)
1. Basic transfer: BEGIN/COMMIT.
2. Rollback on constraint violation.
3. TRY/CATCH with THROW — propagate error to caller.
4. CATCH error functions: `ERROR_MESSAGE()`, `ERROR_SEVERITY()`, `ERROR_LINE()`.
5. Isolation level demo (two-session walkthrough — see README).
6. Check RCSI setting on AdventureWorks.
7. `sys.dm_exec_sessions` — inspect current session isolation level.

## Pitfalls
- Forgetting `IF @@TRANCOUNT > 0` before ROLLBACK in CATCH — causes error if no active transaction.
- `XACT_ABORT ON` auto-rolls back on any error (useful in procs); off by default.
- Nested transactions: `COMMIT` inside a nested transaction does not commit — only the outermost `COMMIT` does. `ROLLBACK` always rolls back to the outermost begin.
- `THROW` without arguments can only be used inside a CATCH block (re-raises current error). Outside a CATCH, use `THROW number, msg, state`.
- RCSI adds TempDB write overhead for row versions — acceptable for most OLTP, but monitor TempDB growth.

## Cheatsheet link
See `cheatsheets/00-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Re-run `setup.sql` to reset Account balances. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/11-transactions-errors-concurrency/
git commit -m "feat: add lesson 11 - transactions errors and concurrency"
```

---

## Self-Review

**Spec coverage check:**

| Lesson | Spec topics | Covered |
|---|---|---|
| 09 | CREATE TABLE, PK/FK/UNIQUE/CHECK/DEFAULT, computed columns, schemas, IDENTITY vs sequences | ✓ |
| 10 | When to use views/procs/functions, parameters, table-valued vs scalar functions (scalar-UDF perf trap) | ✓ |
| 11 | BEGIN/COMMIT/ROLLBACK, TRY/CATCH, THROW, isolation levels (RC, RCSI, Snapshot, Serializable), deadlocks demo | ✓ |

All five lesson files per lesson. `setup.sql` idempotent. No exercise depends on prior exercise mutations (re-run `setup.sql` to reset). All exercises have solutions with approach comments. No placeholders.
