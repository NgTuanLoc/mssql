# Tier 1 Lessons Implementation Plan (Lessons 01–04)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write the four Tier 1 Foundation lessons — Setup & Basics, T-SQL Core SELECT, Data Types & Expressions, and Joins & Set Operations — each containing README.md, setup.sql, examples.sql, exercises.sql, and exercises-solutions.sql.

**Architecture:** Each lesson directory is self-contained. `setup.sql` is idempotent and uses a dedicated `lessonNN` schema inside `AdventureWorks2022`. Exercises never depend on the output of a prior exercise. AdventureWorks is the default dataset; `setup.sql` adds only what AdventureWorks doesn't provide for the lesson topic.

**Tech Stack:** T-SQL (MSSQL 2022 Developer), AdventureWorks2022, Markdown.

**Prerequisite:** Infrastructure plan (Plan 01) must be complete — container running, AdventureWorks restored.

---

## File Map

| Action | Path |
|--------|------|
| Create | `lessons/01-setup-and-basics/README.md` |
| Create | `lessons/01-setup-and-basics/setup.sql` |
| Create | `lessons/01-setup-and-basics/examples.sql` |
| Create | `lessons/01-setup-and-basics/exercises.sql` |
| Create | `lessons/01-setup-and-basics/exercises-solutions.sql` |
| Create | `lessons/02-tsql-core-select/README.md` |
| Create | `lessons/02-tsql-core-select/setup.sql` |
| Create | `lessons/02-tsql-core-select/examples.sql` |
| Create | `lessons/02-tsql-core-select/exercises.sql` |
| Create | `lessons/02-tsql-core-select/exercises-solutions.sql` |
| Create | `lessons/03-data-types-and-expressions/README.md` |
| Create | `lessons/03-data-types-and-expressions/setup.sql` |
| Create | `lessons/03-data-types-and-expressions/examples.sql` |
| Create | `lessons/03-data-types-and-expressions/exercises.sql` |
| Create | `lessons/03-data-types-and-expressions/exercises-solutions.sql` |
| Create | `lessons/04-joins-and-sets/README.md` |
| Create | `lessons/04-joins-and-sets/setup.sql` |
| Create | `lessons/04-joins-and-sets/examples.sql` |
| Create | `lessons/04-joins-and-sets/exercises.sql` |
| Create | `lessons/04-joins-and-sets/exercises-solutions.sql` |

---

## Task 1: Lesson 01 — Setup & First Queries

**Files:** `lessons/01-setup-and-basics/` (5 files)

- [ ] **Step 1: Create `lessons/01-setup-and-basics/setup.sql`**

```sql
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
```

- [ ] **Step 2: Create `lessons/01-setup-and-basics/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- Example 1: Your first SELECT — retrieve all columns from a small table
SELECT * FROM Person.CountryRegion;

-- Example 2: Select specific columns with aliases
SELECT
    CountryRegionCode AS Code,
    Name              AS CountryName
FROM Person.CountryRegion;

-- Example 3: Limit rows with TOP
SELECT TOP 10
    FirstName,
    LastName,
    EmailPromotion
FROM Person.Person
ORDER BY LastName;

-- Example 4: Filter rows with WHERE
SELECT
    ProductID,
    Name,
    ListPrice
FROM Production.Product
WHERE ListPrice > 1000
ORDER BY ListPrice DESC;

-- Example 5: Explore a table's structure
EXEC sp_help 'Production.Product';
-- Or in Object Explorer: expand Tables → Production.Product → Columns
```

- [ ] **Step 3: Create `lessons/01-setup-and-basics/exercises.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: List all departments in the company.
-- Expected columns: DepartmentID, Name, GroupName
-- Hint: HumanResources.Department
-- Your query here:


-- Exercise 2: Find all products with a ListPrice of exactly $0.00.
-- Expected columns: ProductID, Name, ProductNumber
-- Your query here:


-- Exercise 3: Show the top 5 most expensive products (by ListPrice).
-- Expected columns: Name, ListPrice
-- Your query here:


-- Exercise 4: How many rows are in the Sales.SalesOrderHeader table?
-- Expected columns: TotalOrders (one row, one column)
-- Your query here:


-- Exercise 5: List all unique colors used by products.
-- Expected columns: Color
-- Hint: some products have NULL color — include them.
-- Your query here:
```

- [ ] **Step 4: Create `lessons/01-setup-and-basics/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: List all departments in the company.
-- Approach: straightforward SELECT from the department table.
SELECT DepartmentID, Name, GroupName
FROM HumanResources.Department
ORDER BY GroupName, Name;

-- Exercise 2: Find all products with a ListPrice of exactly $0.00.
-- Approach: = 0 works for MONEY type; 0.00 is equivalent.
SELECT ProductID, Name, ProductNumber
FROM Production.Product
WHERE ListPrice = 0;

-- Exercise 3: Show the top 5 most expensive products (by ListPrice).
-- Approach: ORDER BY DESC + TOP n; ties are broken arbitrarily without a tiebreaker.
SELECT TOP 5 Name, ListPrice
FROM Production.Product
ORDER BY ListPrice DESC;

-- Exercise 4: How many rows are in the Sales.SalesOrderHeader table?
-- Approach: COUNT(*) counts all rows including those with NULLs in any column.
SELECT COUNT(*) AS TotalOrders
FROM Sales.SalesOrderHeader;

-- Exercise 5: List all unique colors used by products (including NULL).
-- Approach: DISTINCT removes duplicates; NULL appears once in the result.
SELECT DISTINCT Color
FROM Production.Product
ORDER BY Color;
```

- [ ] **Step 5: Create `lessons/01-setup-and-basics/README.md`**

```markdown
# Lesson 01 — Setup & First Queries

## What you'll learn
- Start MSSQL 2022 in Docker and connect with SSMS or Azure Data Studio
- Write your first `SELECT` statement
- Use `TOP`, `WHERE`, and `ORDER BY`
- Explore table structure with Object Explorer and `sp_help`

## Setup
Run `setup.sql` once. It creates the `lesson01` schema (no extra data needed — AdventureWorks is sufficient for this lesson).

```powershell
# Container must already be running (see root README)
.\scripts\connect.ps1
# Then in sqlcmd:
:r lessons\01-setup-and-basics\setup.sql
```

Or open `setup.sql` in SSMS and press F5.

## Concepts

### Connecting
- SSMS / Azure Data Studio: server `localhost,1433`, login `sa`, password from `docker/.env`.
- Object Explorer (SSMS) → Databases → AdventureWorks2022 → Tables to browse schema.

### Basic SELECT

```sql
SELECT col1, col2        -- which columns (or * for all)
FROM   schema.table      -- which table
WHERE  predicate         -- filter rows (optional)
ORDER BY col1 DESC;      -- sort (optional; without it, order is undefined)
```

### TOP n

```sql
SELECT TOP 10 Name, ListPrice
FROM Production.Product
ORDER BY ListPrice DESC;
```

`TOP` without `ORDER BY` returns an arbitrary set of rows.

### Counting and DISTINCT

```sql
SELECT COUNT(*)        FROM Sales.SalesOrderHeader;   -- total rows
SELECT COUNT(col)      FROM table;                    -- non-NULL values only
SELECT DISTINCT Color  FROM Production.Product;        -- unique values
```

## Worked Examples (AdventureWorks)

1. `SELECT * FROM Person.CountryRegion` — see all countries (small table, safe to SELECT *).
2. Select specific columns with aliases to make output readable.
3. `TOP 10 ... ORDER BY LastName` — first ten people alphabetically.
4. Filter products by `ListPrice > 1000` — WHERE clause basics.
5. `EXEC sp_help 'Production.Product'` — inspect column names, types, and constraints.

## Pitfalls
- `SELECT *` is fine for exploration; avoid in production queries (schema changes break callers).
- `ORDER BY` without `TOP` or `OFFSET` is technically undefined in SQL Server — results may appear ordered in practice but are not guaranteed.
- `COUNT(col)` skips NULLs; `COUNT(*)` counts every row.
- Without `WHERE`, `UPDATE` and `DELETE` affect every row in the table — always confirm the predicate first.

## Cheatsheet link
See `cheatsheets/00-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql` — peek only after attempting.
```

- [ ] **Step 6: Run setup.sql to verify it executes without errors**

```powershell
docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U sa -P "YourStr0ngP@ssword!" -No `
  -i /path/in/container  # or paste contents directly
```

Simpler: open `setup.sql` in SSMS connected to `AdventureWorks2022` and press F5. Expected: `Lesson 01 setup complete.` with no errors.

- [ ] **Step 7: Commit**

```powershell
git add lessons/01-setup-and-basics/
git commit -m "feat: add lesson 01 - setup and first queries"
```

---

## Task 2: Lesson 02 — T-SQL Core: SELECT, WHERE, ORDER BY, TOP

**Files:** `lessons/02-tsql-core-select/` (5 files)

- [ ] **Step 1: Create `lessons/02-tsql-core-select/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson02') IS NOT NULL
    DROP SCHEMA lesson02;
GO
CREATE SCHEMA lesson02;
GO
PRINT 'Lesson 02 setup complete.';
```

- [ ] **Step 2: Create `lessons/02-tsql-core-select/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- Example 1: LIKE — products whose name starts with 'Road'
SELECT ProductID, Name, ListPrice
FROM Production.Product
WHERE Name LIKE 'Road%';

-- Example 2: IN — orders placed by specific customers
SELECT SalesOrderID, CustomerID, TotalDue
FROM Sales.SalesOrderHeader
WHERE CustomerID IN (11000, 11001, 11002);

-- Example 3: BETWEEN — products priced $500–$1000
SELECT Name, ListPrice
FROM Production.Product
WHERE ListPrice BETWEEN 500 AND 1000   -- inclusive on both ends
ORDER BY ListPrice;

-- Example 4: NULL semantics — products with no color specified
SELECT ProductID, Name, Color
FROM Production.Product
WHERE Color IS NULL;   -- WHERE Color = NULL never matches

-- Example 5: Multiple conditions with AND / OR
SELECT SalesOrderID, Status, TotalDue
FROM Sales.SalesOrderHeader
WHERE Status = 5          -- shipped
  AND TotalDue > 5000
ORDER BY TotalDue DESC;

-- Example 6: TOP with OFFSET/FETCH (page 2 of 10 rows per page)
SELECT ProductID, Name, ListPrice
FROM Production.Product
WHERE ListPrice > 0
ORDER BY ListPrice DESC
OFFSET 10 ROWS
FETCH NEXT 10 ROWS ONLY;

-- Example 7: Negation with NOT LIKE and NOT IN
SELECT Name
FROM Production.Product
WHERE Name NOT LIKE '%Road%'
  AND ProductSubcategoryID IS NOT NULL;
```

- [ ] **Step 3: Create `lessons/02-tsql-core-select/exercises.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Find all employees whose job title contains the word 'Manager'.
-- Expected columns: BusinessEntityID, JobTitle
-- Table: HumanResources.Employee
-- Your query here:


-- Exercise 2: List products in subcategory 1, 2, or 3 with a ListPrice above $100.
-- Expected columns: Name, ProductSubcategoryID, ListPrice
-- Your query here:


-- Exercise 3: Find orders placed between 2013-01-01 and 2013-03-31 (inclusive).
-- Expected columns: SalesOrderID, OrderDate, TotalDue
-- Table: Sales.SalesOrderHeader
-- Your query here:


-- Exercise 4: Find products that have NO size specified (Size IS NULL) AND no weight (Weight IS NULL).
-- Expected columns: ProductID, Name
-- Your query here:


-- Exercise 5: Using OFFSET/FETCH, return rows 21–30 of products ordered by Name ascending.
-- Expected columns: ProductID, Name
-- Your query here:
```

- [ ] **Step 4: Create `lessons/02-tsql-core-select/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Employees whose job title contains 'Manager'.
-- Approach: LIKE with % on both sides matches anywhere in the string.
SELECT BusinessEntityID, JobTitle
FROM HumanResources.Employee
WHERE JobTitle LIKE '%Manager%'
ORDER BY JobTitle;

-- Exercise 2: Products in subcategories 1–3 with ListPrice > $100.
-- Approach: IN for the subcategory set, AND for the price filter.
SELECT Name, ProductSubcategoryID, ListPrice
FROM Production.Product
WHERE ProductSubcategoryID IN (1, 2, 3)
  AND ListPrice > 100
ORDER BY ListPrice DESC;

-- Exercise 3: Orders in Q1 2013.
-- Approach: BETWEEN is inclusive; use DATE literals for clarity.
SELECT SalesOrderID, OrderDate, TotalDue
FROM Sales.SalesOrderHeader
WHERE OrderDate BETWEEN '2013-01-01' AND '2013-03-31'
ORDER BY OrderDate;

-- Exercise 4: Products with neither size nor weight.
-- Approach: two IS NULL conditions joined by AND.
SELECT ProductID, Name
FROM Production.Product
WHERE Size IS NULL
  AND Weight IS NULL;

-- Exercise 5: Products 21–30 by name (zero-based offset = 20).
-- Approach: OFFSET n ROWS skips n rows; FETCH NEXT m ROWS ONLY takes m.
SELECT ProductID, Name
FROM Production.Product
ORDER BY Name
OFFSET 20 ROWS
FETCH NEXT 10 ROWS ONLY;
```

- [ ] **Step 5: Create `lessons/02-tsql-core-select/README.md`**

```markdown
# Lesson 02 — T-SQL Core: SELECT, WHERE, ORDER BY, TOP

## What you'll learn
- Filter rows with `WHERE` using `LIKE`, `IN`, `BETWEEN`, and `NULL` semantics
- Combine predicates with `AND`, `OR`, `NOT`
- Control row order with `ORDER BY`
- Limit results with `TOP n` and page through results with `OFFSET/FETCH`

## Setup
Run `setup.sql` once (creates empty `lesson02` schema).

## Concepts

### LIKE

```sql
WHERE Name LIKE 'Road%'       -- starts with Road
WHERE Name LIKE '%Bike%'      -- contains Bike
WHERE Name LIKE '_oad%'       -- second char is 'o', 'a', 'd' ...? No — any single char then 'oad'
WHERE Name NOT LIKE '%Road%'  -- does not contain Road
```

Wildcards: `%` = any string, `_` = any single character.

### IN and BETWEEN

```sql
WHERE CustomerID IN (100, 200, 300)
WHERE ListPrice BETWEEN 500 AND 1000   -- inclusive
```

`NOT IN` with a subquery that can return NULL causes the entire result to be empty — use `NOT EXISTS` instead.

### NULL Semantics

NULL is unknown — it is not equal to anything, including itself.

```sql
WHERE Color = NULL      -- never matches any row
WHERE Color IS NULL     -- correct
WHERE Color IS NOT NULL -- correct
```

### OFFSET / FETCH (pagination)

```sql
SELECT Name FROM Production.Product
ORDER BY Name
OFFSET 20 ROWS          -- skip first 20
FETCH NEXT 10 ROWS ONLY;-- return next 10
```

Requires `ORDER BY`. Use for result-set pagination.

## Worked Examples (AdventureWorks)
1. `LIKE 'Road%'` — products starting with 'Road'.
2. `IN (11000, 11001, 11002)` — orders for specific customers.
3. `BETWEEN 500 AND 1000` — products in a price band.
4. `Color IS NULL` — products with no colour (vs `= NULL` which always fails).
5. Multiple `AND` conditions on shipped orders.
6. `OFFSET 10 ROWS FETCH NEXT 10 ROWS ONLY` — page 2.
7. `NOT LIKE` + `IS NOT NULL` — exclude road products in subcategories.

## Pitfalls
- `WHERE col = NULL` silently returns no rows — always use `IS NULL`.
- `NOT IN` with a subquery returning NULLs returns 0 rows — a silent bug.
- `BETWEEN` is inclusive on both ends — `BETWEEN '2024-01-01' AND '2024-12-31'` includes Dec 31 at midnight only for `DATE`; for `DATETIME` add a day and use `<`.
- `LIKE` is case-insensitive under most collations — confirm with `SELECT SERVERPROPERTY('Collation')`.

## Cheatsheet link
See `cheatsheets/00-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/02-tsql-core-select/
git commit -m "feat: add lesson 02 - T-SQL core SELECT"
```

---

## Task 3: Lesson 03 — Data Types & Expressions

**Files:** `lessons/03-data-types-and-expressions/` (5 files)

- [ ] **Step 1: Create `lessons/03-data-types-and-expressions/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson03') IS NOT NULL
BEGIN
    -- Drop all tables in the schema before dropping the schema
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson03.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables
    WHERE schema_id = SCHEMA_ID('lesson03');
    EXEC sp_executesql @sql;

    DROP SCHEMA lesson03;
END
GO
CREATE SCHEMA lesson03;
GO

-- Demo table for implicit conversion and type behaviour examples
CREATE TABLE lesson03.TypeDemo (
    ID              INT             IDENTITY(1,1) PRIMARY KEY,
    VarcharCode     VARCHAR(20)     NOT NULL,
    NVarcharName    NVARCHAR(100)   NOT NULL,
    PriceDecimal    DECIMAL(10,4)   NOT NULL,
    PriceMoney      MONEY           NOT NULL,
    BirthDate       DATE            NOT NULL,
    CreatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSDATETIME(),
    IsActive        BIT             NOT NULL DEFAULT 1
);

INSERT lesson03.TypeDemo (VarcharCode, NVarcharName, PriceDecimal, PriceMoney, BirthDate)
VALUES
    ('A001', N'Alice',   1234.5678, 1234.5678, '1990-06-15'),
    ('B002', N'Bob',     999.9999,  999.9999,  '1985-11-30'),
    ('C003', N'Café',    0.0100,    0.0100,    '2000-01-01'),
    ('D004', N'正確',    50000.00,  50000.00,  '1975-03-22');

PRINT 'Lesson 03 setup complete.';
```

- [ ] **Step 2: Create `lessons/03-data-types-and-expressions/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- Example 1: CAST — convert ListPrice to string for concatenation
SELECT Name + ' costs $' + CAST(ListPrice AS VARCHAR(20)) AS Description
FROM Production.Product
WHERE ListPrice > 0;

-- Example 2: CONVERT with style — format an order date as 'YYYY-MM-DD'
SELECT
    SalesOrderID,
    CONVERT(VARCHAR(10), OrderDate, 120) AS OrderDateFormatted
FROM Sales.SalesOrderHeader
WHERE YEAR(OrderDate) = 2014;

-- Example 3: TRY_CAST — safe conversion (returns NULL on failure)
SELECT
    VarcharCode,
    TRY_CAST(VarcharCode AS INT) AS AsInt   -- 'A001' → NULL, not an error
FROM lesson03.TypeDemo;

-- Example 4: Implicit conversion gotcha — VARCHAR column vs INT literal
-- This forces a scan because SQL Server must convert every VarcharCode row to INT
SELECT VarcharCode FROM lesson03.TypeDemo WHERE VarcharCode = 1;      -- implicit scan
SELECT VarcharCode FROM lesson03.TypeDemo WHERE VarcharCode = '1';    -- correct

-- Example 5: DECIMAL vs MONEY arithmetic precision
SELECT
    PriceDecimal / 3          AS DecimalDiv,   -- preserves precision
    PriceMoney   / 3          AS MoneyDiv       -- rounds to 4 decimal places mid-calc
FROM lesson03.TypeDemo;

-- Example 6: Date arithmetic
SELECT
    BirthDate,
    DATEDIFF(YEAR, BirthDate, GETDATE())       AS AgeApprox,
    DATEADD(YEAR, 18, BirthDate)               AS Turns18,
    EOMONTH(BirthDate)                          AS LastDayOfBirthMonth
FROM lesson03.TypeDemo;

-- Example 7: Unicode vs VARCHAR — the café problem
SELECT
    NVarcharName,                              -- 'Café' stored correctly
    CAST(NVarcharName AS VARCHAR(100))          AS LostAccent   -- 'Caf?' on many collations
FROM lesson03.TypeDemo
WHERE NVarcharName = N'Café';                  -- N prefix required for correct match
```

- [ ] **Step 3: Create `lessons/03-data-types-and-expressions/exercises.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Format the ModifiedDate of every Person.Person row as 'DD/MM/YYYY'.
-- Expected columns: BusinessEntityID, ModifiedDateFormatted
-- Hint: CONVERT style 103 = DD/MM/YYYY
-- Your query here:


-- Exercise 2: Calculate each product's ListPrice rounded to the nearest dollar,
--             and the difference between the rounded and original price.
-- Expected columns: Name, ListPrice, Rounded, Difference
-- Your query here:


-- Exercise 3: Some rows in Production.Product have a NULL Weight.
--             Return all products showing Weight; replace NULL with 0.
-- Expected columns: Name, Weight, WeightOrZero
-- Your query here:


-- Exercise 4: Convert the SellStartDate of each product to a DATE (stripping time).
--             Show only products whose SellStartDate year is 2003.
-- Expected columns: Name, SellStartDate, SellStartDateOnly
-- Your query here:


-- Exercise 5: Using TRY_CAST, attempt to cast the ProductNumber column to INT.
--             Show ProductNumber, the attempted cast result, and a derived column
--             IsNumeric (1 if the cast succeeded, 0 if it returned NULL).
-- Expected columns: ProductNumber, CastResult, IsNumeric
-- Your query here:
```

- [ ] **Step 4: Create `lessons/03-data-types-and-expressions/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Format ModifiedDate as DD/MM/YYYY.
-- Approach: CONVERT style 103 produces DD/MM/YYYY directly.
SELECT
    BusinessEntityID,
    CONVERT(VARCHAR(10), ModifiedDate, 103) AS ModifiedDateFormatted
FROM Person.Person;

-- Exercise 2: Rounded price and difference.
-- Approach: ROUND(x, 0) rounds to nearest integer; subtraction gives the delta.
SELECT
    Name,
    ListPrice,
    ROUND(ListPrice, 0)            AS Rounded,
    ListPrice - ROUND(ListPrice, 0) AS Difference
FROM Production.Product
WHERE ListPrice > 0;

-- Exercise 3: Replace NULL Weight with 0.
-- Approach: ISNULL replaces NULL with the specified value; COALESCE is the ANSI equivalent.
SELECT
    Name,
    Weight,
    ISNULL(Weight, 0) AS WeightOrZero
FROM Production.Product;

-- Exercise 4: Products whose SellStartDate is in 2003, showing DATE-only value.
-- Approach: CAST to DATE strips the time component; YEAR() extracts the year for filtering.
SELECT
    Name,
    SellStartDate,
    CAST(SellStartDate AS DATE) AS SellStartDateOnly
FROM Production.Product
WHERE YEAR(SellStartDate) = 2003;

-- Exercise 5: TRY_CAST ProductNumber to INT.
-- Approach: TRY_CAST returns NULL on failure; CASE on the result gives IsNumeric.
SELECT
    ProductNumber,
    TRY_CAST(ProductNumber AS INT)                              AS CastResult,
    CASE WHEN TRY_CAST(ProductNumber AS INT) IS NOT NULL
         THEN 1 ELSE 0 END                                      AS IsNumeric
FROM Production.Product;
```

- [ ] **Step 5: Create `lessons/03-data-types-and-expressions/README.md`**

```markdown
# Lesson 03 — Data Types & Expressions

## What you'll learn
- Numeric types: when to use `INT`, `DECIMAL`, and why to avoid `MONEY` for arithmetic
- String types: `VARCHAR` vs `NVARCHAR`, the `N` prefix, and collation basics
- Date/time types: `DATE`, `DATETIME2`, `DATETIMEOFFSET` and their trade-offs
- `CAST`, `CONVERT` (with style), `TRY_CAST`, `TRY_CONVERT`
- Implicit conversion pitfalls that kill index seeks

## Setup
Run `setup.sql` once. It creates the `lesson03` schema and a `TypeDemo` table with sample rows demonstrating type differences.

## Concepts

### CAST vs CONVERT

```sql
CAST(expr AS type)               -- ANSI standard; use for simple type changes
CONVERT(type, expr, style)       -- SQL Server extension; style codes for date formatting
TRY_CAST(expr AS type)           -- returns NULL on failure; safe for untrusted input
```

Common CONVERT date styles: 120 = `YYYY-MM-DD HH:MM:SS`, 103 = `DD/MM/YYYY`, 101 = `MM/DD/YYYY`.

### VARCHAR vs NVARCHAR

`VARCHAR` stores ASCII (1 byte/char). `NVARCHAR` stores Unicode (2 bytes/char). Use `NVARCHAR` for user-facing text. Always prefix Unicode literals with `N`: `N'café'`.

Mixing types: comparing a `VARCHAR` column to an `NVARCHAR` literal works, but SQL Server must implicitly convert every row — this can prevent index use.

### DECIMAL arithmetic

`DECIMAL(p,s)` is exact. `MONEY` rounds intermediate results to 4 decimal places — use `DECIMAL(19,4)` for financial calculations that need accuracy.

### Date functions quick reference

```sql
DATEDIFF(year, BirthDate, GETDATE())   -- age in years (approximate)
DATEADD(month, 3, GETDATE())           -- 3 months from now
EOMONTH(GETDATE())                     -- last day of current month
CAST(SYSDATETIME() AS DATE)            -- today's date, no time component
```

## Worked Examples (AdventureWorks)
1. `CAST(ListPrice AS VARCHAR)` to build a description string.
2. `CONVERT(VARCHAR, OrderDate, 120)` — ISO date string from a DATETIME.
3. `TRY_CAST(VarcharCode AS INT)` — returns NULL instead of erroring.
4. Implicit conversion: `WHERE VarcharCode = 1` vs `WHERE VarcharCode = '1'`.
5. `DECIMAL / 3` vs `MONEY / 3` — precision difference.
6. `DATEDIFF` / `DATEADD` / `EOMONTH` on BirthDate.
7. `N'Café'` vs `'Café'` — Unicode vs VARCHAR.

## Pitfalls
- `FLOAT` / `REAL` are not exact — `0.1 + 0.2 ≠ 0.3` in binary floating point.
- `MONEY` intermediate rounding: `$10.00 / 3 * 3 = $9.9999`, not `$10.00`.
- Forgetting the `N` prefix silently mangles accented or non-Latin characters.
- `CAST(DATETIME AS DATE)` is the right way to strip time — not `CONVERT(VARCHAR, ...)` followed by re-parsing.
- `YEAR(col) = 2024` in a `WHERE` clause is non-SARGable; prefer a range: `col >= '2024-01-01' AND col < '2025-01-01'`.

## Cheatsheet link
See `cheatsheets/01-data-types.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/03-data-types-and-expressions/
git commit -m "feat: add lesson 03 - data types and expressions"
```

---

## Task 4: Lesson 04 — Joins & Set Operations

**Files:** `lessons/04-joins-and-sets/` (5 files)

- [ ] **Step 1: Create `lessons/04-joins-and-sets/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson04') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson04.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson04');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson04;
END
GO
CREATE SCHEMA lesson04;
GO

-- Small tables to demo join types clearly without AdventureWorks noise
CREATE TABLE lesson04.Left  (ID INT, Value NVARCHAR(20));
CREATE TABLE lesson04.Right (ID INT, Value NVARCHAR(20));

INSERT lesson04.Left  VALUES (1,'L-only'), (2,'Both-A'), (3,'Both-B');
INSERT lesson04.Right VALUES (2,'Both-A'), (3,'Both-B'), (4,'R-only');

PRINT 'Lesson 04 setup complete.';
```

- [ ] **Step 2: Create `lessons/04-joins-and-sets/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- Example 1: INNER JOIN — only matching rows
SELECT l.ID, l.Value AS LeftVal, r.Value AS RightVal
FROM lesson04.Left  AS l
INNER JOIN lesson04.Right AS r ON r.ID = l.ID;
-- Result: rows 2 and 3 only

-- Example 2: LEFT JOIN — all left rows; NULL for unmatched right
SELECT l.ID, l.Value AS LeftVal, r.Value AS RightVal
FROM lesson04.Left  AS l
LEFT JOIN lesson04.Right AS r ON r.ID = l.ID;
-- Result: rows 1 (NULL right), 2, 3

-- Example 3: RIGHT JOIN — all right rows
SELECT l.Value AS LeftVal, r.ID, r.Value AS RightVal
FROM lesson04.Left  AS l
RIGHT JOIN lesson04.Right AS r ON r.ID = l.ID;
-- Result: rows 2, 3, 4 (NULL left)

-- Example 4: FULL JOIN — all rows from both sides
SELECT l.ID AS LeftID, l.Value AS LeftVal, r.ID AS RightID, r.Value AS RightVal
FROM lesson04.Left  AS l
FULL JOIN lesson04.Right AS r ON r.ID = l.ID;
-- Result: rows 1 (NULL right), 2, 3, 4 (NULL left)

-- Example 5: Real-world multi-table join
SELECT
    soh.SalesOrderID,
    soh.OrderDate,
    p.FirstName + ' ' + p.LastName AS CustomerName,
    pr.Name                         AS ProductName,
    sod.OrderQty,
    sod.UnitPrice
FROM Sales.SalesOrderHeader  AS soh
JOIN Sales.Customer          AS c   ON c.CustomerID        = soh.CustomerID
JOIN Person.Person           AS p   ON p.BusinessEntityID  = c.PersonID
JOIN Sales.SalesOrderDetail  AS sod ON sod.SalesOrderID    = soh.SalesOrderID
JOIN Production.Product      AS pr  ON pr.ProductID        = sod.ProductID
WHERE soh.SalesOrderID = 43659;

-- Example 6: UNION ALL vs UNION
-- UNION ALL — keeps duplicates, faster
SELECT ProductID FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43659
UNION ALL
SELECT ProductID FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43660;

-- UNION — removes duplicates
SELECT ProductID FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43659
UNION
SELECT ProductID FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43660;

-- Example 7: EXCEPT — products sold but never purchased
SELECT ProductID FROM Sales.SalesOrderDetail
EXCEPT
SELECT ProductID FROM Purchasing.PurchaseOrderDetail;

-- Example 8: INTERSECT — products both sold and purchased
SELECT ProductID FROM Sales.SalesOrderDetail
INTERSECT
SELECT ProductID FROM Purchasing.PurchaseOrderDetail;
```

- [ ] **Step 3: Create `lessons/04-joins-and-sets/exercises.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: List every product with its subcategory name.
--             Include products that have no subcategory (NULL subcategory).
-- Expected columns: ProductID, ProductName, SubcategoryName
-- Tables: Production.Product, Production.ProductSubcategory
-- Your query here:


-- Exercise 2: List every sales order with the customer's full name.
--             Only include orders where a matching person record exists.
-- Expected columns: SalesOrderID, OrderDate, CustomerFullName
-- Tables: Sales.SalesOrderHeader, Sales.Customer, Person.Person
-- Your query here:


-- Exercise 3: Find all employees who have NEVER placed a purchase order.
-- Expected columns: BusinessEntityID, JobTitle
-- Tables: HumanResources.Employee, Purchasing.PurchaseOrderHeader
-- Hint: use a LEFT JOIN checking for NULL, or NOT EXISTS / EXCEPT.
-- Your query here:


-- Exercise 4: Show the CROSS JOIN of lesson04.Left and lesson04.Right.
--             How many rows do you expect?
-- Expected columns: LeftID, LeftValue, RightID, RightValue
-- Your query here:


-- Exercise 5: Using UNION ALL, combine the first names of all people
--             (Person.Person) with all contact names in the Vendor table
--             (Purchasing.Vendor, column: Name). Label which table each row
--             comes from.
-- Expected columns: Name, Source
-- Your query here:
```

- [ ] **Step 4: Create `lessons/04-joins-and-sets/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Products with their subcategory (include products with no subcategory).
-- Approach: LEFT JOIN keeps products even when ProductSubcategoryID is NULL.
SELECT
    p.ProductID,
    p.Name          AS ProductName,
    ps.Name         AS SubcategoryName
FROM Production.Product            AS p
LEFT JOIN Production.ProductSubcategory AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
ORDER BY SubcategoryName, ProductName;

-- Exercise 2: Sales orders with customer full name (only where person record exists).
-- Approach: INNER JOINs drop rows with no match; three-table chain through Customer → Person.
SELECT
    soh.SalesOrderID,
    soh.OrderDate,
    p.FirstName + ' ' + p.LastName AS CustomerFullName
FROM Sales.SalesOrderHeader AS soh
JOIN Sales.Customer          AS c ON c.CustomerID       = soh.CustomerID
JOIN Person.Person           AS p ON p.BusinessEntityID = c.PersonID
ORDER BY soh.OrderDate;

-- Exercise 3: Employees who have never placed a purchase order.
-- Approach: LEFT JOIN to PurchaseOrderHeader; rows with NULL VendorID have no PO.
SELECT e.BusinessEntityID, e.JobTitle
FROM HumanResources.Employee      AS e
LEFT JOIN Purchasing.PurchaseOrderHeader AS poh ON poh.EmployeeID = e.BusinessEntityID
WHERE poh.EmployeeID IS NULL
ORDER BY e.BusinessEntityID;

-- Exercise 4: CROSS JOIN of Left and Right (3 × 3 = 9 rows).
SELECT
    l.ID    AS LeftID,
    l.Value AS LeftValue,
    r.ID    AS RightID,
    r.Value AS RightValue
FROM lesson04.Left  AS l
CROSS JOIN lesson04.Right AS r;

-- Exercise 5: UNION ALL of person names and vendor names with a source label.
-- Approach: UNION ALL preserves all rows; add a literal string column for the source.
SELECT FirstName + ' ' + LastName AS Name, 'Person' AS Source
FROM Person.Person
UNION ALL
SELECT Name, 'Vendor'
FROM Purchasing.Vendor
ORDER BY Name;
```

- [ ] **Step 5: Create `lessons/04-joins-and-sets/README.md`**

```markdown
# Lesson 04 — Joins & Set Operations

## What you'll learn
- INNER, LEFT, RIGHT, FULL OUTER, and CROSS joins
- Multi-table join chains
- `UNION` vs `UNION ALL`, `INTERSECT`, `EXCEPT`
- Common join mistakes: fan-out, NULL filtering, non-SARGable conditions

## Setup
Run `setup.sql` once. It creates the `lesson04` schema with small `Left` and `Right` tables to demonstrate join types clearly.

## Concepts

### Join types — what they keep

| Join | Rows kept |
|------|-----------|
| INNER | Only rows with a match on both sides |
| LEFT OUTER | All left rows + matched right (NULL if no match) |
| RIGHT OUTER | Matched left (NULL if no match) + all right rows |
| FULL OUTER | All rows from both sides; NULLs where no match |
| CROSS | Every combination (m × n rows) |

### ON vs WHERE in outer joins

```sql
-- LEFT JOIN with filter in WHERE — silently becomes an INNER JOIN
SELECT * FROM A LEFT JOIN B ON A.id = B.id WHERE B.col = 'x';

-- LEFT JOIN with filter in ON — keeps all A rows; B columns are NULL when no match
SELECT * FROM A LEFT JOIN B ON A.id = B.id AND B.col = 'x';
```

Move filters on the *outer* (right) table to the `ON` clause if you want to preserve left rows.

### Set operations

All require the same number of columns with compatible types. Column names come from the first query.

- `UNION ALL` — all rows, including duplicates. Use when duplicates are acceptable or impossible — faster than `UNION`.
- `UNION` — deduplicates the combined result (sorts internally — slower).
- `INTERSECT` — rows present in both results.
- `EXCEPT` — rows in the first result but not the second.

## Worked Examples (AdventureWorks)
1. INNER JOIN on `lesson04.Left`/`Right` — rows 2 and 3 only.
2. LEFT JOIN — row 1 appears with NULL right-side columns.
3. RIGHT JOIN — row 4 appears with NULL left-side columns.
4. FULL JOIN — all four rows, NULLs on the unmatched sides.
5. Five-table join: order → customer → person → order detail → product.
6. `UNION ALL` vs `UNION` on order detail products.
7. `EXCEPT` — products sold but never purchased.
8. `INTERSECT` — products both sold and purchased.

## Pitfalls
- **Accidental fan-out:** joining one-to-many without being aware multiplies rows (e.g., one order → many order lines → aggregates are wrong).
- **WHERE on outer table kills LEFT JOIN:** `WHERE right.col = value` removes the NULL rows, effectively making it an INNER JOIN.
- **UNION vs UNION ALL:** if duplicates are impossible, `UNION ALL` is always faster.
- **Self-join without aliasing:** you must alias both copies of the table or SQL Server errors.

## Cheatsheet link
See `cheatsheets/02-joins-and-sets.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/04-joins-and-sets/
git commit -m "feat: add lesson 04 - joins and set operations"
```

---

## Self-Review

**Spec coverage check:**

| Lesson | Spec topics | Covered |
|---|---|---|
| 01 | Docker up, restore, connect with SSMS/ADS, first SELECT, Object Explorer | ✓ |
| 02 | Predicates, LIKE, IN, BETWEEN, NULL semantics, TOP n vs OFFSET/FETCH | ✓ |
| 03 | int/decimal/money, VARCHAR vs NVARCHAR, DATE/DATETIME2/DATETIMEOFFSET, implicit conversion, CAST/CONVERT/TRY_CAST | ✓ |
| 04 | INNER/LEFT/RIGHT/FULL/CROSS, multi-table joins, UNION/UNION ALL/INTERSECT/EXCEPT, common join mistakes | ✓ |

All five lesson files created for each lesson. `setup.sql` is idempotent. No exercise depends on prior exercise mutations. All exercises have solutions with approach comments. No placeholders.
