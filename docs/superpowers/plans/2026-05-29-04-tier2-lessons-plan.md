# Tier 2 Lessons Implementation Plan (Lessons 05–08)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write the four Tier 2 "Working with Data" lessons — Aggregations & Grouping, Subqueries & CTEs, Window Functions, and DML (INSERT/UPDATE/DELETE/MERGE) — each containing README.md, setup.sql, examples.sql, exercises.sql, and exercises-solutions.sql.

**Architecture:** Each lesson directory is self-contained. `setup.sql` is idempotent and uses a dedicated `lessonNN` schema. DML exercises operate on `setup.sql`-seeded tables so they can be re-run safely. AdventureWorks is the default dataset.

**Tech Stack:** T-SQL (MSSQL 2022 Developer), AdventureWorks2022, Markdown.

**Prerequisite:** Infrastructure plan (Plan 01) complete — container running, AdventureWorks restored.

---

## File Map

| Action | Path |
|--------|------|
| Create | `lessons/05-aggregations-and-grouping/` (5 files) |
| Create | `lessons/06-subqueries-and-ctes/` (5 files) |
| Create | `lessons/07-window-functions/` (5 files) |
| Create | `lessons/08-dml-insert-update-delete-merge/` (5 files) |

---

## Task 1: Lesson 05 — Aggregations & Grouping

**Files:** `lessons/05-aggregations-and-grouping/` (5 files)

- [ ] **Step 1: Create `lessons/05-aggregations-and-grouping/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson05') IS NOT NULL
    DROP SCHEMA lesson05;
GO
CREATE SCHEMA lesson05;
GO
PRINT 'Lesson 05 setup complete.';
```

- [ ] **Step 2: Create `lessons/05-aggregations-and-grouping/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- Example 1: Basic GROUP BY with aggregate functions
SELECT
    YEAR(OrderDate)    AS OrderYear,
    COUNT(*)           AS TotalOrders,
    SUM(TotalDue)      AS Revenue,
    AVG(TotalDue)      AS AvgOrderValue,
    MIN(TotalDue)      AS SmallestOrder,
    MAX(TotalDue)      AS LargestOrder
FROM Sales.SalesOrderHeader
GROUP BY YEAR(OrderDate)
ORDER BY OrderYear;

-- Example 2: HAVING — filter after aggregation
SELECT
    CustomerID,
    COUNT(*)    AS OrderCount,
    SUM(TotalDue) AS TotalSpend
FROM Sales.SalesOrderHeader
GROUP BY CustomerID
HAVING COUNT(*) >= 5              -- only customers with 5+ orders
ORDER BY TotalSpend DESC;

-- Example 3: Aggregate with multiple GROUP BY columns
SELECT
    YEAR(OrderDate)   AS OrderYear,
    MONTH(OrderDate)  AS OrderMonth,
    SUM(TotalDue)     AS MonthlyRevenue
FROM Sales.SalesOrderHeader
GROUP BY YEAR(OrderDate), MONTH(OrderDate)
ORDER BY OrderYear, OrderMonth;

-- Example 4: COUNT(col) vs COUNT(*) — NULLs
SELECT
    COUNT(*)              AS AllProducts,
    COUNT(Color)          AS ProductsWithColor,    -- NULLs excluded
    COUNT(DISTINCT Color) AS DistinctColors
FROM Production.Product;

-- Example 5: GROUPING SETS — multiple groupings in one query
SELECT
    YEAR(OrderDate) AS OrderYear,
    MONTH(OrderDate) AS OrderMonth,
    SUM(TotalDue)  AS Revenue
FROM Sales.SalesOrderHeader
GROUP BY GROUPING SETS (
    (YEAR(OrderDate), MONTH(OrderDate)),   -- monthly subtotals
    (YEAR(OrderDate)),                     -- yearly subtotals
    ()                                     -- grand total (NULL, NULL row)
)
ORDER BY OrderYear, OrderMonth;

-- Example 6: ROLLUP — hierarchical subtotals (equivalent shorthand)
SELECT
    YEAR(OrderDate)   AS OrderYear,
    MONTH(OrderDate)  AS OrderMonth,
    SUM(TotalDue)     AS Revenue
FROM Sales.SalesOrderHeader
GROUP BY ROLLUP (YEAR(OrderDate), MONTH(OrderDate))
ORDER BY OrderYear, OrderMonth;
-- NULL in OrderMonth = year subtotal; NULL in both = grand total

-- Example 7: CUBE — all combinations of groupings
SELECT
    TerritoryID,
    YEAR(OrderDate) AS OrderYear,
    SUM(TotalDue)   AS Revenue
FROM Sales.SalesOrderHeader
GROUP BY CUBE (TerritoryID, YEAR(OrderDate))
ORDER BY TerritoryID, OrderYear;
```

- [ ] **Step 3: Create `lessons/05-aggregations-and-grouping/exercises.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: How many products exist in each product category?
--             Join through ProductSubcategory to get to ProductCategory.
-- Expected columns: CategoryName, ProductCount
-- Tables: Production.Product, Production.ProductSubcategory, Production.ProductCategory
-- Your query here:


-- Exercise 2: Find the top 3 sales territories by total revenue in 2013.
-- Expected columns: TerritoryID, TotalRevenue
-- Table: Sales.SalesOrderHeader
-- Your query here:


-- Exercise 3: Find customers who placed more than 10 orders AND whose average
--             order value exceeded $2,000.
-- Expected columns: CustomerID, OrderCount, AvgOrderValue
-- Your query here:


-- Exercise 4: Using ROLLUP, show total sales by year and by overall grand total.
--             Only include years 2012–2014.
-- Expected columns: OrderYear, TotalSales
-- (Grand total row will have NULL OrderYear)
-- Your query here:


-- Exercise 5: For each product subcategory, show the most expensive product's ListPrice
--             and the average ListPrice. Include only subcategories with avg ListPrice > $200.
-- Expected columns: SubcategoryName, MaxPrice, AvgPrice
-- Your query here:
```

- [ ] **Step 4: Create `lessons/05-aggregations-and-grouping/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Products per category.
-- Approach: Two-hop join: Product → ProductSubcategory → ProductCategory, then GROUP BY.
SELECT
    pc.Name  AS CategoryName,
    COUNT(*) AS ProductCount
FROM Production.Product              AS p
JOIN Production.ProductSubcategory  AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
JOIN Production.ProductCategory     AS pc ON pc.ProductCategoryID    = ps.ProductCategoryID
GROUP BY pc.Name
ORDER BY ProductCount DESC;

-- Exercise 2: Top 3 territories by 2013 revenue.
-- Approach: Filter year in WHERE, GROUP BY territory, ORDER BY + TOP.
SELECT TOP 3
    TerritoryID,
    SUM(TotalDue) AS TotalRevenue
FROM Sales.SalesOrderHeader
WHERE YEAR(OrderDate) = 2013
GROUP BY TerritoryID
ORDER BY TotalRevenue DESC;

-- Exercise 3: Customers with > 10 orders AND avg order > $2,000.
-- Approach: Both conditions go in HAVING — they operate on aggregated data.
SELECT
    CustomerID,
    COUNT(*)        AS OrderCount,
    AVG(TotalDue)   AS AvgOrderValue
FROM Sales.SalesOrderHeader
GROUP BY CustomerID
HAVING COUNT(*) > 10
   AND AVG(TotalDue) > 2000
ORDER BY AvgOrderValue DESC;

-- Exercise 4: ROLLUP for 2012–2014 totals.
-- Approach: Filter in WHERE first; ROLLUP adds year subtotals and a grand total (NULL year).
SELECT
    YEAR(OrderDate) AS OrderYear,
    SUM(TotalDue)   AS TotalSales
FROM Sales.SalesOrderHeader
WHERE YEAR(OrderDate) BETWEEN 2012 AND 2014
GROUP BY ROLLUP (YEAR(OrderDate))
ORDER BY OrderYear;

-- Exercise 5: Most expensive and average price by subcategory, avg > $200.
-- Approach: JOIN subcategory, GROUP BY, HAVING on the average.
SELECT
    ps.Name        AS SubcategoryName,
    MAX(p.ListPrice) AS MaxPrice,
    AVG(p.ListPrice) AS AvgPrice
FROM Production.Product            AS p
JOIN Production.ProductSubcategory AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
WHERE p.ListPrice > 0
GROUP BY ps.Name
HAVING AVG(p.ListPrice) > 200
ORDER BY AvgPrice DESC;
```

- [ ] **Step 5: Create `lessons/05-aggregations-and-grouping/README.md`**

```markdown
# Lesson 05 — Aggregations & Grouping

## What you'll learn
- `GROUP BY` and aggregate functions: `COUNT`, `SUM`, `AVG`, `MIN`, `MAX`
- `HAVING` to filter after aggregation
- `COUNT(*)` vs `COUNT(col)` and NULL behaviour
- `GROUPING SETS`, `ROLLUP`, and `CUBE` for multi-level subtotals

## Setup
Run `setup.sql` once (creates empty `lesson05` schema).

## Concepts

### GROUP BY and aggregates

```sql
SELECT col, COUNT(*), SUM(amount), AVG(amount)
FROM table
GROUP BY col;
```

Every column in `SELECT` that is not an aggregate must appear in `GROUP BY`.

### HAVING vs WHERE

- `WHERE` filters *before* grouping (on individual rows).
- `HAVING` filters *after* grouping (on aggregated values).

```sql
-- Wrong: can't reference an aggregate in WHERE
WHERE COUNT(*) > 5         -- error

-- Right
HAVING COUNT(*) > 5
```

### NULL in aggregates

`COUNT(col)` ignores NULL; `COUNT(*)` counts every row. `SUM`, `AVG`, `MIN`, `MAX` all ignore NULL.

### ROLLUP / CUBE / GROUPING SETS

```sql
GROUP BY ROLLUP (A, B)
-- = GROUPING SETS ((A,B), (A), ())
-- Produces: per-A-B rows, per-A subtotals, grand total

GROUP BY CUBE (A, B)
-- = GROUPING SETS ((A,B), (A), (B), ())
-- Produces all combinations

GROUP BY GROUPING SETS ((A,B), (A), ())
-- Explicit control over which groupings appear
```

NULL in a grouping column inside a ROLLUP/CUBE result means "this is a subtotal row". Use `GROUPING(col)` to distinguish subtotal NULLs from data NULLs.

## Worked Examples (AdventureWorks)
1. Revenue, order count, avg/min/max per year.
2. HAVING: customers with 5+ orders.
3. GROUP BY two columns: year + month revenue grid.
4. COUNT(*) vs COUNT(Color) vs COUNT(DISTINCT Color).
5. GROUPING SETS: monthly + yearly + grand total in one query.
6. ROLLUP: hierarchical year/month subtotals.
7. CUBE: all combinations of territory and year.

## Pitfalls
- Selecting a non-aggregated column not in GROUP BY — SQL Server errors (unlike MySQL).
- Filtering on aggregated values in WHERE instead of HAVING.
- `AVG(INT column)` truncates to integer — cast first: `AVG(CAST(col AS DECIMAL(10,2)))`.
- ROLLUP NULL vs real NULL — use `GROUPING(col)` to tell them apart.

## Cheatsheet link
See `cheatsheets/00-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/05-aggregations-and-grouping/
git commit -m "feat: add lesson 05 - aggregations and grouping"
```

---

## Task 2: Lesson 06 — Subqueries & CTEs

**Files:** `lessons/06-subqueries-and-ctes/` (5 files)

- [ ] **Step 1: Create `lessons/06-subqueries-and-ctes/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson06') IS NOT NULL
    DROP SCHEMA lesson06;
GO
CREATE SCHEMA lesson06;
GO
PRINT 'Lesson 06 setup complete.';
```

- [ ] **Step 2: Create `lessons/06-subqueries-and-ctes/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- Example 1: Scalar subquery in SELECT — average as a reference value
SELECT
    SalesOrderID,
    TotalDue,
    (SELECT AVG(TotalDue) FROM Sales.SalesOrderHeader) AS AvgOrderValue,
    TotalDue - (SELECT AVG(TotalDue) FROM Sales.SalesOrderHeader) AS DiffFromAvg
FROM Sales.SalesOrderHeader
WHERE YEAR(OrderDate) = 2014;

-- Example 2: Subquery in WHERE with IN
SELECT ProductID, Name, ListPrice
FROM Production.Product
WHERE ProductSubcategoryID IN (
    SELECT ProductSubcategoryID
    FROM Production.ProductSubcategory
    WHERE ProductCategoryID = 1   -- Bikes
);

-- Example 3: Correlated subquery — for each customer, count their orders
SELECT
    c.CustomerID,
    (SELECT COUNT(*)
     FROM Sales.SalesOrderHeader AS soh
     WHERE soh.CustomerID = c.CustomerID) AS OrderCount
FROM Sales.Customer AS c
ORDER BY OrderCount DESC;

-- Example 4: EXISTS vs IN — customers who have placed at least one order
-- EXISTS: stops scanning as soon as one match is found — often faster
SELECT CustomerID
FROM Sales.Customer AS c
WHERE EXISTS (
    SELECT 1
    FROM Sales.SalesOrderHeader AS soh
    WHERE soh.CustomerID = c.CustomerID
);

-- Example 5: NOT EXISTS — customers who have NEVER ordered
SELECT CustomerID
FROM Sales.Customer AS c
WHERE NOT EXISTS (
    SELECT 1
    FROM Sales.SalesOrderHeader AS soh
    WHERE soh.CustomerID = c.CustomerID
);

-- Example 6: Non-recursive CTE
WITH OrderSummary AS (
    SELECT
        CustomerID,
        COUNT(*)      AS OrderCount,
        SUM(TotalDue) AS TotalSpend
    FROM Sales.SalesOrderHeader
    GROUP BY CustomerID
)
SELECT
    c.CustomerID,
    os.OrderCount,
    os.TotalSpend
FROM Sales.Customer AS c
JOIN OrderSummary   AS os ON os.CustomerID = c.CustomerID
WHERE os.TotalSpend > 10000
ORDER BY os.TotalSpend DESC;

-- Example 7: Recursive CTE — employee management chain
WITH EmpHierarchy AS (
    -- Anchor: top-level employees (no manager)
    SELECT
        BusinessEntityID,
        OrganizationNode,
        JobTitle,
        0 AS Level
    FROM HumanResources.Employee
    WHERE OrganizationNode = hierarchyid::GetRoot()
       OR OrganizationNode.GetLevel() = 1

    UNION ALL

    -- Recursive: employees reporting to a known employee
    SELECT
        e.BusinessEntityID,
        e.OrganizationNode,
        e.JobTitle,
        eh.Level + 1
    FROM HumanResources.Employee AS e
    JOIN EmpHierarchy AS eh
      ON e.OrganizationNode.GetAncestor(1) = eh.OrganizationNode
)
SELECT BusinessEntityID, JobTitle, Level
FROM EmpHierarchy
ORDER BY Level, BusinessEntityID;
```

- [ ] **Step 3: Create `lessons/06-subqueries-and-ctes/exercises.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Using a subquery, find all products whose ListPrice is above
--             the average ListPrice of ALL products.
-- Expected columns: Name, ListPrice
-- Your query here:


-- Exercise 2: Using EXISTS, find all customers who have ordered at least one
--             product from ProductCategoryID = 1 (Bikes).
-- Expected columns: CustomerID
-- Tables: Sales.Customer, Sales.SalesOrderHeader, Sales.SalesOrderDetail,
--         Production.Product, Production.ProductSubcategory
-- Your query here:


-- Exercise 3: Using a CTE named TopCustomers, identify the top 10 customers
--             by total spend, then join back to Person.Person to get their names.
-- Expected columns: CustomerID, FullName, TotalSpend
-- Your query here:


-- Exercise 4: Without using a JOIN, use a correlated subquery to find
--             the most recent OrderDate for each CustomerID in Sales.SalesOrderHeader.
-- Expected columns: CustomerID, LatestOrderDate
-- Your query here:


-- Exercise 5: Using a recursive CTE, generate a sequence of integers from 1 to 10.
-- Expected columns: n
-- Your query here:
```

- [ ] **Step 4: Create `lessons/06-subqueries-and-ctes/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Products above average ListPrice.
-- Approach: scalar subquery in WHERE computes the average once.
SELECT Name, ListPrice
FROM Production.Product
WHERE ListPrice > (SELECT AVG(ListPrice) FROM Production.Product WHERE ListPrice > 0)
ORDER BY ListPrice DESC;

-- Exercise 2: Customers who ordered at least one Bike.
-- Approach: EXISTS terminates on the first match, which is efficient for large tables.
SELECT c.CustomerID
FROM Sales.Customer AS c
WHERE EXISTS (
    SELECT 1
    FROM Sales.SalesOrderHeader  AS soh
    JOIN Sales.SalesOrderDetail  AS sod ON sod.SalesOrderID        = soh.SalesOrderID
    JOIN Production.Product      AS p   ON p.ProductID             = sod.ProductID
    JOIN Production.ProductSubcategory AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
    WHERE soh.CustomerID = c.CustomerID
      AND ps.ProductCategoryID = 1
);

-- Exercise 3: CTE for top 10 customers, joined to Person.
-- Approach: CTE named TopCustomers isolates the aggregation; outer query adds the name.
WITH TopCustomers AS (
    SELECT TOP 10
        soh.CustomerID,
        SUM(soh.TotalDue) AS TotalSpend
    FROM Sales.SalesOrderHeader AS soh
    GROUP BY soh.CustomerID
    ORDER BY TotalSpend DESC
)
SELECT
    tc.CustomerID,
    p.FirstName + ' ' + p.LastName AS FullName,
    tc.TotalSpend
FROM TopCustomers AS tc
JOIN Sales.Customer AS c ON c.CustomerID       = tc.CustomerID
JOIN Person.Person  AS p ON p.BusinessEntityID = c.PersonID
ORDER BY tc.TotalSpend DESC;

-- Exercise 4: Most recent order date per customer via correlated subquery.
-- Approach: correlated subquery references the outer row's CustomerID each iteration.
SELECT
    CustomerID,
    (SELECT MAX(soh2.OrderDate)
     FROM Sales.SalesOrderHeader AS soh2
     WHERE soh2.CustomerID = soh.CustomerID) AS LatestOrderDate
FROM Sales.SalesOrderHeader AS soh
GROUP BY CustomerID   -- one row per customer
ORDER BY LatestOrderDate DESC;

-- Exercise 5: Recursive CTE to generate 1..10.
-- Approach: anchor returns 1; recursive member adds 1 each time until > 10.
WITH Numbers AS (
    SELECT 1 AS n
    UNION ALL
    SELECT n + 1 FROM Numbers WHERE n < 10
)
SELECT n FROM Numbers;
```

- [ ] **Step 5: Create `lessons/06-subqueries-and-ctes/README.md`**

```markdown
# Lesson 06 — Subqueries & CTEs

## What you'll learn
- Scalar subqueries in `SELECT` and `WHERE`
- `IN` with a subquery vs `EXISTS` / `NOT EXISTS`
- Correlated subqueries (reference the outer query)
- Non-recursive CTEs to organise complex queries
- Recursive CTEs for hierarchies and sequences

## Setup
Run `setup.sql` once (creates empty `lesson06` schema).

## Concepts

### Subquery placement

```sql
-- In SELECT (must return exactly one row/column)
SELECT col, (SELECT MAX(x) FROM other) AS Max FROM table;

-- In WHERE with IN
WHERE col IN (SELECT id FROM lookup);

-- In WHERE with EXISTS
WHERE EXISTS (SELECT 1 FROM other WHERE other.fk = outer.id);
```

### EXISTS vs IN

- `EXISTS` is short-circuit — it stops at the first match. Often faster on large sets.
- `IN` materialises the full subquery result. Use when the subquery is small and straightforward.
- `NOT IN` breaks silently when the subquery returns any NULL. Always prefer `NOT EXISTS`.

```sql
-- Dangerous: if subquery returns NULL, entire NOT IN result is empty
WHERE id NOT IN (SELECT foreign_id FROM other)  -- avoid

-- Safe:
WHERE NOT EXISTS (SELECT 1 FROM other WHERE other.foreign_id = outer.id)
```

### Common Table Expressions (CTEs)

```sql
WITH CTE_Name AS (
    SELECT ...
),
CTE2 AS (
    SELECT ... FROM CTE_Name
)
SELECT * FROM CTE2;
```

CTEs are not materialised by default — the optimizer may inline them. Use `OPTION (MAXRECURSION n)` on recursive CTEs if you expect deep hierarchies (default limit = 100).

### Recursive CTE pattern

```sql
WITH R AS (
    SELECT ...        -- anchor (base case)
    UNION ALL
    SELECT ...        -- recursive member (references R)
    FROM R
    WHERE <stop condition>
)
SELECT * FROM R;
```

## Worked Examples (AdventureWorks)
1. Scalar subquery: each order vs overall average.
2. `IN` subquery: products in the Bikes category.
3. Correlated subquery: order count per customer.
4. `EXISTS`: customers with at least one order.
5. `NOT EXISTS`: customers who have never ordered.
6. Non-recursive CTE: top spenders joined to customer table.
7. Recursive CTE: employee hierarchy.

## Pitfalls
- `NOT IN` with subqueries returning NULL — produces empty results silently. Use `NOT EXISTS`.
- Correlated subqueries execute once per outer row — can be slow on large tables. Consider a JOIN or CTE instead.
- CTEs are not guaranteed to be materialised — if you need repeated access, use a temp table.
- Recursive CTEs default to 100 recursion levels; set `OPTION (MAXRECURSION 0)` for unlimited (be careful).

## Cheatsheet link
See `cheatsheets/00-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/06-subqueries-and-ctes/
git commit -m "feat: add lesson 06 - subqueries and CTEs"
```

---

## Task 3: Lesson 07 — Window Functions

**Files:** `lessons/07-window-functions/` (5 files)

- [ ] **Step 1: Create `lessons/07-window-functions/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson07') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson07.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson07');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson07;
END
GO
CREATE SCHEMA lesson07;
GO

-- Monthly sales data for window function demos
CREATE TABLE lesson07.MonthlySales (
    SaleYear  INT  NOT NULL,
    SaleMonth INT  NOT NULL,
    Region    NVARCHAR(50) NOT NULL,
    Revenue   DECIMAL(14,2) NOT NULL,
    PRIMARY KEY (SaleYear, SaleMonth, Region)
);

INSERT lesson07.MonthlySales (SaleYear, SaleMonth, Region, Revenue) VALUES
    (2023, 1,  'North', 12000), (2023, 2,  'North', 15000), (2023, 3,  'North', 11000),
    (2023, 1,  'South', 8000),  (2023, 2,  'South', 9500),  (2023, 3,  'South', 10200),
    (2023, 4,  'North', 18000), (2023, 5,  'North', 16500), (2023, 6,  'North', 19000),
    (2023, 4,  'South', 11000), (2023, 5,  'South', 12400), (2023, 6,  'South', 13100);

PRINT 'Lesson 07 setup complete.';
```

- [ ] **Step 2: Create `lessons/07-window-functions/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- Example 1: ROW_NUMBER — unique row per customer ordered by spend
SELECT
    CustomerID,
    TotalDue,
    ROW_NUMBER() OVER (
        PARTITION BY CustomerID
        ORDER BY TotalDue DESC
    ) AS OrderRankForCustomer
FROM Sales.SalesOrderHeader;

-- Example 2: RANK vs DENSE_RANK — ties handled differently
SELECT
    ProductID,
    ListPrice,
    RANK()       OVER (ORDER BY ListPrice DESC) AS Rnk,       -- gaps after ties
    DENSE_RANK() OVER (ORDER BY ListPrice DESC) AS DenseRnk   -- no gaps
FROM Production.Product
WHERE ListPrice > 0;

-- Example 3: Top-N per group using ROW_NUMBER
SELECT *
FROM (
    SELECT
        CustomerID,
        SalesOrderID,
        TotalDue,
        ROW_NUMBER() OVER (
            PARTITION BY CustomerID
            ORDER BY TotalDue DESC
        ) AS rn
    FROM Sales.SalesOrderHeader
) AS ranked
WHERE rn <= 2;  -- top 2 orders per customer

-- Example 4: LAG and LEAD — month-over-month change
SELECT
    SaleYear,
    SaleMonth,
    Region,
    Revenue,
    LAG(Revenue,  1, 0) OVER (PARTITION BY Region ORDER BY SaleYear, SaleMonth) AS PrevMonthRevenue,
    LEAD(Revenue, 1, 0) OVER (PARTITION BY Region ORDER BY SaleYear, SaleMonth) AS NextMonthRevenue,
    Revenue - LAG(Revenue, 1, 0) OVER (PARTITION BY Region ORDER BY SaleYear, SaleMonth) AS MoMChange
FROM lesson07.MonthlySales
ORDER BY Region, SaleYear, SaleMonth;

-- Example 5: Running total (cumulative sum)
SELECT
    SaleYear,
    SaleMonth,
    Region,
    Revenue,
    SUM(Revenue) OVER (
        PARTITION BY Region
        ORDER BY SaleYear, SaleMonth
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS CumulativeRevenue
FROM lesson07.MonthlySales
ORDER BY Region, SaleYear, SaleMonth;

-- Example 6: 3-month moving average
SELECT
    SaleYear,
    SaleMonth,
    Region,
    Revenue,
    AVG(Revenue) OVER (
        PARTITION BY Region
        ORDER BY SaleYear, SaleMonth
        ROWS BETWEEN 2 PRECEDING AND CURRENT ROW
    ) AS Moving3MonthAvg
FROM lesson07.MonthlySales
ORDER BY Region, SaleYear, SaleMonth;

-- Example 7: FIRST_VALUE and LAST_VALUE
SELECT
    SaleYear,
    SaleMonth,
    Region,
    Revenue,
    FIRST_VALUE(Revenue) OVER (
        PARTITION BY Region, SaleYear
        ORDER BY SaleMonth
        ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING
    ) AS JanRevenue,
    LAST_VALUE(Revenue) OVER (
        PARTITION BY Region, SaleYear
        ORDER BY SaleMonth
        ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING
    ) AS LastMonthRevenue
FROM lesson07.MonthlySales
ORDER BY Region, SaleYear, SaleMonth;
```

- [ ] **Step 3: Create `lessons/07-window-functions/exercises.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Rank all products by ListPrice descending within each subcategory.
--             Use DENSE_RANK so there are no gaps in the ranking.
-- Expected columns: SubcategoryName, ProductName, ListPrice, PriceRank
-- Tables: Production.Product, Production.ProductSubcategory
-- Your query here:


-- Exercise 2: For each sales order, show TotalDue and the running total of TotalDue
--             ordered by OrderDate for ALL orders (no partition — one window for everything).
-- Expected columns: SalesOrderID, OrderDate, TotalDue, RunningTotal
-- Your query here:


-- Exercise 3: Using LAG, show each order's TotalDue and the TotalDue of the
--             PREVIOUS order for the SAME customer (ordered by OrderDate).
--             Show NULL when there is no previous order.
-- Expected columns: CustomerID, SalesOrderID, OrderDate, TotalDue, PrevOrderAmount
-- Your query here:


-- Exercise 4: Return only the MOST RECENT order for each customer
--             (use ROW_NUMBER to pick the latest order).
-- Expected columns: CustomerID, SalesOrderID, OrderDate, TotalDue
-- Your query here:


-- Exercise 5: Using lesson07.MonthlySales, calculate each month's Revenue
--             as a percentage of that region's TOTAL revenue across all months.
-- Expected columns: Region, SaleYear, SaleMonth, Revenue, PctOfRegionTotal
-- Your query here:
```

- [ ] **Step 4: Create `lessons/07-window-functions/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: DENSE_RANK by ListPrice within subcategory.
-- Approach: PARTITION BY subcategory; ORDER BY ListPrice DESC; join for name.
SELECT
    ps.Name  AS SubcategoryName,
    p.Name   AS ProductName,
    p.ListPrice,
    DENSE_RANK() OVER (
        PARTITION BY p.ProductSubcategoryID
        ORDER BY p.ListPrice DESC
    ) AS PriceRank
FROM Production.Product             AS p
JOIN Production.ProductSubcategory  AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
WHERE p.ListPrice > 0
ORDER BY SubcategoryName, PriceRank;

-- Exercise 2: Running total of TotalDue across all orders by OrderDate.
-- Approach: No PARTITION; ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW for cumsum.
SELECT
    SalesOrderID,
    OrderDate,
    TotalDue,
    SUM(TotalDue) OVER (
        ORDER BY OrderDate
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS RunningTotal
FROM Sales.SalesOrderHeader
ORDER BY OrderDate;

-- Exercise 3: Previous order amount per customer using LAG.
-- Approach: PARTITION BY CustomerID; LAG with no default returns NULL for first order.
SELECT
    CustomerID,
    SalesOrderID,
    OrderDate,
    TotalDue,
    LAG(TotalDue) OVER (
        PARTITION BY CustomerID
        ORDER BY OrderDate
    ) AS PrevOrderAmount
FROM Sales.SalesOrderHeader
ORDER BY CustomerID, OrderDate;

-- Exercise 4: Most recent order per customer with ROW_NUMBER.
-- Approach: ROW_NUMBER descending by date; filter rn = 1 in outer query.
SELECT CustomerID, SalesOrderID, OrderDate, TotalDue
FROM (
    SELECT
        CustomerID,
        SalesOrderID,
        OrderDate,
        TotalDue,
        ROW_NUMBER() OVER (
            PARTITION BY CustomerID
            ORDER BY OrderDate DESC
        ) AS rn
    FROM Sales.SalesOrderHeader
) AS ranked
WHERE rn = 1
ORDER BY CustomerID;

-- Exercise 5: Revenue as % of region total.
-- Approach: SUM() OVER (PARTITION BY Region) with no ORDER BY = full partition sum.
SELECT
    Region,
    SaleYear,
    SaleMonth,
    Revenue,
    CAST(Revenue * 100.0
         / SUM(Revenue) OVER (PARTITION BY Region)
         AS DECIMAL(5,2)) AS PctOfRegionTotal
FROM lesson07.MonthlySales
ORDER BY Region, SaleYear, SaleMonth;
```

- [ ] **Step 5: Create `lessons/07-window-functions/README.md`**

```markdown
# Lesson 07 — Window Functions

## What you'll learn
- `ROW_NUMBER`, `RANK`, `DENSE_RANK`, `NTILE`
- `LAG`, `LEAD`, `FIRST_VALUE`, `LAST_VALUE`
- Aggregate window functions: `SUM() OVER`, `AVG() OVER`
- `PARTITION BY` and `ORDER BY` inside `OVER()`
- Framing clauses: `ROWS BETWEEN`

## Setup
Run `setup.sql` once. It creates the `lesson07` schema and a `MonthlySales` table used in several examples.

## Concepts

### OVER() clause

```sql
function() OVER (
    [PARTITION BY col, ...]   -- divides rows into groups
    [ORDER BY col ASC|DESC]   -- ordering within each partition
    [ROWS BETWEEN start AND end]  -- which rows to include in the aggregate
)
```

Without `PARTITION BY`, all rows form one window.

### Ranking functions

| Function | Ties | Gaps |
|---|---|---|
| `ROW_NUMBER()` | unique (arbitrary for ties) | n/a |
| `RANK()` | same rank | yes — next rank skips |
| `DENSE_RANK()` | same rank | no |
| `NTILE(n)` | distributed across buckets | n/a |

### Framing

```sql
ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW  -- running total
ROWS BETWEEN 2 PRECEDING AND CURRENT ROW          -- 3-row moving window
ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING  -- full partition (for LAST_VALUE)
```

Use `ROWS` (physical), not `RANGE` (logical), unless you specifically want ties included.

## Worked Examples (AdventureWorks + lesson07.MonthlySales)
1. `ROW_NUMBER()` per customer ordered by spend.
2. `RANK` vs `DENSE_RANK` — tie handling.
3. Top-2 orders per customer with `ROW_NUMBER` in a subquery.
4. `LAG`/`LEAD` — month-over-month change.
5. Running total with `ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW`.
6. 3-month moving average with `ROWS BETWEEN 2 PRECEDING AND CURRENT ROW`.
7. `FIRST_VALUE`/`LAST_VALUE` with explicit unbounded frame.

## Pitfalls
- `LAST_VALUE` default frame is `RANGE UNBOUNDED PRECEDING TO CURRENT ROW` — you'll get the current row's value, not the partition's last. Always specify the frame explicitly.
- Omitting `PARTITION BY` makes all rows one window — intentional for running totals, wrong for per-group rankings.
- `ROW_NUMBER()` requires `ORDER BY` in `OVER()` — it doesn't have a meaningful default order.
- Window functions cannot be used in `WHERE` — wrap in a subquery or CTE.

## Cheatsheet link
See `cheatsheets/03-window-functions.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/07-window-functions/
git commit -m "feat: add lesson 07 - window functions"
```

---

## Task 4: Lesson 08 — DML: INSERT / UPDATE / DELETE / MERGE

**Files:** `lessons/08-dml-insert-update-delete-merge/` (5 files)

- [ ] **Step 1: Create `lessons/08-dml-insert-update-delete-merge/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson08') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson08.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson08');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson08;
END
GO
CREATE SCHEMA lesson08;
GO

-- Staging table for INSERT/UPDATE/DELETE exercises (re-seeded on each setup run)
CREATE TABLE lesson08.ProductStaging (
    StagingID   INT          IDENTITY(1,1) PRIMARY KEY,
    ProductID   INT          NOT NULL,
    Name        NVARCHAR(50) NOT NULL,
    ListPrice   MONEY        NOT NULL,
    IsActive    BIT          NOT NULL DEFAULT 1,
    LoadedAt    DATETIME2    NOT NULL DEFAULT SYSDATETIME()
);

-- Target table for MERGE demo
CREATE TABLE lesson08.InventoryTarget (
    ProductID  INT          NOT NULL PRIMARY KEY,
    StockQty   INT          NOT NULL DEFAULT 0,
    LastSyncAt DATETIME2    NOT NULL DEFAULT SYSDATETIME()
);

-- Source data — simulates an incoming feed
CREATE TABLE lesson08.InventorySource (
    ProductID  INT NOT NULL PRIMARY KEY,
    StockQty   INT NOT NULL
);

INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
SELECT TOP 10 ProductID, Name, ListPrice
FROM Production.Product
WHERE ListPrice > 0
ORDER BY ProductID;

INSERT lesson08.InventoryTarget (ProductID, StockQty)
VALUES (1, 100), (2, 200), (3, 300);

INSERT lesson08.InventorySource (ProductID, StockQty)
VALUES (1, 95),    -- update
       (3, 310),   -- update
       (4, 50),    -- insert (new)
       (5, 0);     -- insert (new, zero stock)

PRINT 'Lesson 08 setup complete.';
```

- [ ] **Step 2: Create `lessons/08-dml-insert-update-delete-merge/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- Example 1: INSERT with explicit column list (safe against schema changes)
INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
VALUES (9999, N'Demo Product', 99.99);

-- Example 2: INSERT ... SELECT — bulk load from another table
INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
SELECT ProductID, Name, ListPrice
FROM Production.Product
WHERE ProductSubcategoryID = 1
  AND ProductID NOT IN (SELECT ProductID FROM lesson08.ProductStaging);

-- Example 3: UPDATE with FROM (joining another table)
UPDATE ps
SET    ps.ListPrice = p.ListPrice,   -- sync to source
       ps.Name      = p.Name
FROM   lesson08.ProductStaging AS ps
JOIN   Production.Product      AS p ON p.ProductID = ps.ProductID;

-- Example 4: UPDATE with OUTPUT — capture what changed
DECLARE @Changed TABLE (
    ProductID    INT,
    OldPrice     MONEY,
    NewPrice     MONEY
);

UPDATE lesson08.ProductStaging
SET    ListPrice = ListPrice * 1.05   -- 5% price increase
OUTPUT inserted.ProductID,
       deleted.ListPrice  AS OldPrice,
       inserted.ListPrice AS NewPrice
INTO   @Changed;

SELECT * FROM @Changed;

-- Example 5: DELETE with OUTPUT — capture deleted rows
DELETE lesson08.ProductStaging
OUTPUT deleted.ProductID, deleted.Name
WHERE  IsActive = 0;

-- Example 6: MERGE — upsert InventoryTarget from InventorySource
MERGE lesson08.InventoryTarget AS t
USING lesson08.InventorySource AS s
   ON s.ProductID = t.ProductID
WHEN MATCHED AND s.StockQty <> t.StockQty THEN
    UPDATE SET t.StockQty = s.StockQty, t.LastSyncAt = SYSDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ProductID, StockQty, LastSyncAt)
    VALUES (s.ProductID, s.StockQty, SYSDATETIME())
WHEN NOT MATCHED BY SOURCE THEN
    DELETE
OUTPUT $action AS MergeAction, inserted.ProductID, deleted.ProductID;
```

- [ ] **Step 3: Create `lessons/08-dml-insert-update-delete-merge/exercises.sql`**

```sql
USE AdventureWorks2022;
GO
-- Re-run setup.sql first if you need a clean slate: .\scripts\reset-db.ps1 or re-run setup.sql

-- Exercise 1: Insert 3 new rows into lesson08.ProductStaging with ProductIDs 8001, 8002, 8003,
--             names 'Test A', 'Test B', 'Test C', and ListPrices 10.00, 20.00, 30.00.
-- Your query here:


-- Exercise 2: Update the ListPrice of ALL rows in lesson08.ProductStaging
--             where ProductID >= 8001 to ListPrice * 2 (double them).
--             Use OUTPUT to return the ProductID, old price, and new price.
-- Expected columns in output: ProductID, OldPrice, NewPrice
-- Your query here:


-- Exercise 3: Delete all rows from lesson08.ProductStaging where ListPrice = 0.
--             Use OUTPUT to capture the deleted ProductIDs and Names.
-- Your query here:


-- Exercise 4: Write a MERGE statement that uses lesson08.InventorySource as the source
--             and lesson08.InventoryTarget as the target.
--             - WHEN MATCHED AND stock differs: update StockQty and LastSyncAt
--             - WHEN NOT MATCHED BY TARGET: insert the new row
--             - WHEN NOT MATCHED BY SOURCE: do nothing (no DELETE this time)
-- Your query here:


-- Exercise 5: Using INSERT...SELECT and OUTPUT, copy all lesson08.ProductStaging rows
--             with ListPrice > 50 into a table variable, then SELECT from it.
-- Expected columns (in table variable and final SELECT): ProductID, Name, ListPrice
-- Your query here:
```

- [ ] **Step 4: Create `lessons/08-dml-insert-update-delete-merge/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Insert 3 test rows.
-- Approach: multi-row VALUES list with explicit columns.
INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
VALUES (8001, N'Test A', 10.00),
       (8002, N'Test B', 20.00),
       (8003, N'Test C', 30.00);

-- Exercise 2: Double prices for 8001+ with OUTPUT.
-- Approach: OUTPUT deleted.ListPrice (before) and inserted.ListPrice (after).
UPDATE lesson08.ProductStaging
SET    ListPrice = ListPrice * 2
OUTPUT inserted.ProductID,
       deleted.ListPrice  AS OldPrice,
       inserted.ListPrice AS NewPrice
WHERE  ProductID >= 8001;

-- Exercise 3: Delete zero-price rows with OUTPUT.
-- Approach: OUTPUT deleted.* captures the rows being removed.
DELETE lesson08.ProductStaging
OUTPUT deleted.ProductID, deleted.Name
WHERE  ListPrice = 0;

-- Exercise 4: MERGE without the DELETE branch.
-- Approach: omit WHEN NOT MATCHED BY SOURCE to leave orphan target rows alone.
MERGE lesson08.InventoryTarget AS t
USING lesson08.InventorySource AS s
   ON s.ProductID = t.ProductID
WHEN MATCHED AND s.StockQty <> t.StockQty THEN
    UPDATE SET t.StockQty = s.StockQty, t.LastSyncAt = SYSDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ProductID, StockQty, LastSyncAt)
    VALUES (s.ProductID, s.StockQty, SYSDATETIME());

-- Exercise 5: INSERT...SELECT into table variable with OUTPUT.
-- Approach: declare the table variable first; use OUTPUT INTO to fill it.
DECLARE @Copied TABLE (ProductID INT, Name NVARCHAR(50), ListPrice MONEY);

INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
OUTPUT inserted.ProductID, inserted.Name, inserted.ListPrice
INTO   @Copied
SELECT ProductID, Name, ListPrice
FROM   lesson08.ProductStaging
WHERE  ListPrice > 50
  AND  ProductID NOT IN (SELECT ProductID FROM lesson08.ProductStaging WHERE ProductID > 9000);
-- Note: this would insert duplicates in practice; the exercise focuses on the OUTPUT syntax.

SELECT * FROM @Copied;
```

- [ ] **Step 5: Create `lessons/08-dml-insert-update-delete-merge/README.md`**

```markdown
# Lesson 08 — DML: INSERT / UPDATE / DELETE / MERGE

## What you'll learn
- `INSERT` with explicit column lists and `INSERT...SELECT`
- `UPDATE` with `FROM` (joining another table)
- `DELETE` with `FROM`
- `OUTPUT` clause to capture what changed
- `MERGE` for upsert / synchronisation patterns
- `MERGE` pitfalls

## Setup
Run `setup.sql` once. It creates the `lesson08` schema with `ProductStaging`, `InventoryTarget`, and `InventorySource` tables pre-seeded for exercises. Re-run `setup.sql` to reset data to a clean state between exercise attempts.

## Concepts

### INSERT

```sql
-- Explicit columns — required for INSERT...SELECT; recommended always
INSERT dbo.MyTable (Col1, Col2)
VALUES (1, 'a'), (2, 'b');

-- From another table
INSERT dbo.MyTable (Col1, Col2)
SELECT ColA, ColB FROM dbo.Source WHERE condition;
```

### UPDATE with FROM

```sql
UPDATE t
SET    t.Price = s.Price
FROM   dbo.Target AS t
JOIN   dbo.Source AS s ON s.ID = t.ID;
```

### DELETE with FROM

```sql
DELETE t
FROM   dbo.Target AS t
JOIN   dbo.Source AS s ON s.ID = t.ID
WHERE  s.IsExpired = 1;
```

### OUTPUT clause

```sql
INSERT ... OUTPUT inserted.ID INTO @tbl;
UPDATE ... OUTPUT deleted.Price AS OldPrice, inserted.Price AS NewPrice;
DELETE ... OUTPUT deleted.*;
```

`inserted` = the row after the change; `deleted` = the row before.

### MERGE

```sql
MERGE Target AS t
USING Source AS s ON (s.ID = t.ID)
WHEN MATCHED THEN
    UPDATE SET t.Col = s.Col
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ID, Col) VALUES (s.ID, s.Col)
WHEN NOT MATCHED BY SOURCE THEN
    DELETE;
```

## Worked Examples (lesson08 tables)
1. `INSERT` with VALUES list.
2. `INSERT...SELECT` — bulk load from Production.Product.
3. `UPDATE FROM` — sync prices from the source table.
4. `UPDATE` with `OUTPUT` — capture old and new price.
5. `DELETE` with `OUTPUT` — capture deleted rows.
6. `MERGE` full upsert with OUTPUT showing the action.

## Pitfalls
- `MERGE` has a known race condition under concurrent workloads — use a holdlock hint (`WITH (HOLDLOCK)`) or use explicit `IF EXISTS / UPDATE / INSERT` instead for high-concurrency scenarios.
- `UPDATE` without `WHERE` updates every row — always double-check the predicate.
- `INSERT` without an explicit column list breaks when columns are added to the table.
- `OUTPUT INTO` cannot be used with tables that have triggers.
- `DELETE` from a table with FK constraints will fail if child rows exist.

## Cheatsheet link
See `cheatsheets/00-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Re-run `setup.sql` to reset data. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Commit**

```powershell
git add lessons/08-dml-insert-update-delete-merge/
git commit -m "feat: add lesson 08 - DML insert update delete merge"
```

---

## Self-Review

**Spec coverage check:**

| Lesson | Spec topics | Covered |
|---|---|---|
| 05 | GROUP BY, HAVING, aggregate functions, GROUPING SETS, ROLLUP, CUBE | ✓ |
| 06 | Scalar/correlated subqueries, EXISTS vs IN, non-recursive + recursive CTEs | ✓ |
| 07 | ROW_NUMBER, RANK, DENSE_RANK, LAG/LEAD, SUM() OVER, framing clauses | ✓ |
| 08 | OUTPUT clause, MERGE pitfalls, bulk inserts, INSERT/UPDATE/DELETE patterns | ✓ |

All five lesson files per lesson. `setup.sql` idempotent. DML exercises operate on setup-seeded tables and can be reset by re-running `setup.sql`. All exercises have solutions with approach comments. No placeholders.
