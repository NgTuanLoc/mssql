# Lesson 10 — Views, Stored Procedures, Functions

**Prerequisites:** Lessons 01–05 (SELECT, joins, GROUP BY aggregation), lesson 07 (window
functions — `ROW_NUMBER` powers the top-N pattern below), and lesson 08 (INSERT/UPDATE,
which the view demos read and write).

A nightly report calls a scalar function once per row to price out order lines — fine in
dev on a few hundred rows, dramatically slower in production, because a scalar UDF forces
SQL Server onto a single thread no matter how many cores sit idle. This lesson is about
picking the right reusable object *before* that call comes in: views, inline table-valued
functions (iTVFs), scalar UDFs, and stored procedures behave completely differently once
real data volume shows up.

## What you'll learn

- `CREATE OR ALTER VIEW`, updatable views, and `WITH CHECK OPTION`
- Stored procedure conventions: `SET NOCOUNT ON`, typed params, `OUTPUT` params, return codes
- Why a scalar UDF can silently block parallelism — and the CPU-time evidence for it
- Inline Table-Valued Functions (iTVFs) with `CROSS APPLY` as the top-N-per-group tool
- The traps: stale `SELECT *` views, `ORDER BY` inside a view, table-typed proc params

## The mental model

Four kinds of reusable object, four different contracts. This is the decision table:

```
OBJECT TOOLBOX
---------------------------------------------------------------------------
object        composable in a query      inlined into plan   side effects
---------------------------------------------------------------------------
VIEW          yes — FROM / JOIN target   yes, always          none (SELECT only)
iTVF          yes — FROM / CROSS APPLY   yes, always          none (SELECT only)
scalar UDF    yes — any expression       only if simple*      hidden per-row cost
STORED PROC   no — EXEC only, never      n/a — own plan       yes: DML, TRY/CATCH,
              inside a query                                  transactions, RETURN
---------------------------------------------------------------------------
* SQL 2019+ auto-inlines a simple scalar UDF; a second RETURN, a loop, or
  TRY/CATCH disqualifies it — see Concept 3. A fifth kind, the multi-statement
  TVF (RETURNS @t TABLE + BEGIN/END), is never inlined — usually avoid it;
  prefer an iTVF or a proc.
```

Views and iTVFs are just named, parameterised `SELECT`s — the optimizer folds their text
straight into the caller's plan, so they cost nothing beyond the query they wrap. A scalar
UDF *looks* like a value but is really a black box the optimizer only inlines under strict
conditions. A proc is not a query fragment at all — a program called with `EXEC`, and the
only thing that leaves it is what it explicitly returns.

## Setup

Run `setup.sql` once. It creates the `lesson10` schema plus one seed table —
`lesson10.Account` (`AccountID`, `AccountName`, `Status`) with a mix of `Active` and
`Inactive` rows — used later for the updatable-view challenge.

## Concepts

### 1. `CREATE OR ALTER VIEW` + `WITH CHECK OPTION`

The classic view use-cases: simplify complex joins for callers, restrict column access
(security), and give callers a stable interface while the underlying tables change. A view
over a single table is also updatable by default; `WITH CHECK OPTION` additionally rejects
any `INSERT`/`UPDATE` through the view that the view's own `WHERE` would filter out:

```sql
CREATE OR ALTER VIEW lesson10.vw_AccountsC AS
SELECT AccountID, AccountName, Status
FROM lesson10.Account
WHERE AccountName LIKE N'C%'
WITH CHECK OPTION;
GO

INSERT lesson10.vw_AccountsC (AccountName, Status) VALUES (N'Contoso Europe', 'Active');
SELECT * FROM lesson10.vw_AccountsC ORDER BY AccountID;
```

```
AccountID   AccountName          Status
----------- -------------------- ----------
          1 Contoso Retail       Active
          7 Contoso Europe       Active
```

Without `WITH CHECK OPTION`, that same `INSERT` would succeed and then vanish from the view
on the very next `SELECT` (no longer matching `LIKE N'C%'`) — a silent, confusing write.
With it in place, inserting a row the filter would reject fails outright instead:

```sql
INSERT lesson10.vw_AccountsC (AccountName, Status) VALUES (N'Wingtip Europe', 'Active');
```
```
Msg 550, Level 16, State 1
The attempted insert or update failed because the target view either specifies WITH CHECK
OPTION or spans a view that specifies WITH CHECK OPTION and one or more rows resulting from
the operation did not qualify under the CHECK OPTION constraint.
```

**Reach for this when** a view is a restricted, writable window onto a table — "only
active rows" — and a write that breaks the restriction should fail loudly, not vanish.

### 2. Stored procedure conventions

`SET NOCOUNT ON` suppresses the "N rows affected" message every statement would otherwise
emit (extra chatter that confuses some client libraries); typed params, an `OUTPUT`
parameter, and a `RETURN` code are the standard proc contract:

```sql
CREATE OR ALTER PROCEDURE lesson10.usp_GetCategoryProductCount
    @CategoryID   INT,
    @ProductCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @ProductCount = COUNT(*)
    FROM Production.Product AS p
    JOIN Production.ProductSubcategory AS ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
    WHERE ps.ProductCategoryID = @CategoryID;

    IF @ProductCount = 0
        RETURN 1;      -- not found
    RETURN 0;           -- ok
END;
GO

DECLARE @Cnt INT, @Rc INT;
EXEC @Rc = lesson10.usp_GetCategoryProductCount @CategoryID = 1, @ProductCount = @Cnt OUTPUT;
SELECT @Rc AS ReturnCode, @Cnt AS ProductCount;
```

```
ReturnCode  ProductCount
----------- ------------
          0           97
```

Call it with `@CategoryID = 999` (no products) and `@ProductCount` comes back `0` with
`ReturnCode = 1` — the caller branches on the return code without parsing a result set.
The `RETURN` value is a status code, not data — never overload it to carry a real number.

**Reach for this when** a caller needs both a status ("did this work?") and one or two
scalar values back, without a result set to parse — and for procs generally: business
logic spanning multiple statements, DML called by applications, long-running batch work.

### 3. Scalar UDF cost — the same logic, two ways

Same arithmetic, one version wrapped in a scalar UDF, one written inline. Run both with
`SET STATISTICS TIME ON` over all 121,317 `Sales.SalesOrderDetail` rows:

```sql
CREATE OR ALTER FUNCTION lesson10.fn_LineNetAmount
    (@UnitPrice MONEY, @OrderQty SMALLINT, @UnitPriceDiscount MONEY)
RETURNS MONEY
AS
BEGIN
    IF @OrderQty < 0        -- a second RETURN disqualifies this from SQL 2019+ auto-inlining
        RETURN NULL;
    RETURN @UnitPrice * (1.0 - @UnitPriceDiscount) * @OrderQty;
END;
GO

SET STATISTICS TIME ON;
SELECT SUM(lesson10.fn_LineNetAmount(UnitPrice, OrderQty, UnitPriceDiscount)) AS TotalNet
FROM Sales.SalesOrderDetail;

SELECT SUM(UnitPrice * (1.0 - UnitPriceDiscount) * OrderQty) AS TotalNet
FROM Sales.SalesOrderDetail;
SET STATISTICS TIME OFF;
```

```
-- scalar UDF version
 SQL Server Execution Times:
   CPU time = 205 ms,  elapsed time = 276 ms.

-- inline expression version
 SQL Server Execution Times:
   CPU time = 25 ms,  elapsed time = 26 ms.
```

Roughly 8x the CPU for the UDF version (times vary by run/hardware — see Pitfalls for the
plan-level proof of *why*). The totals also differ slightly — `109846381.4250` (UDF) vs
`109846381.399888` (inline): `RETURNS MONEY` rounds every row to MONEY's 4 decimals
*before* the `SUM`, while the inline `MONEY × DECIMAL` expression promotes to
`NUMERIC(38,6)` per row and never loses those extra decimals. The two `RETURN`s above (a
routine input guard) are what disqualify this function from SQL 2019+ auto-inlining;
query `sys.sql_modules.is_inlineable` to check any function.

**Reach for this when** you're tempted to wrap row-level math in a scalar UDF "for
reuse" — for anything beyond a handful of rows, an inline expression or a view is cheaper
and the optimizer can actually see through it.

### 4. iTVF + `CROSS APPLY` — the top-N-per-group tool

An iTVF that takes a group key and a count, called once per outer row via `CROSS APPLY`, is
the standard way to get "top N per group" without a window-function subquery:

```sql
CREATE OR ALTER FUNCTION lesson10.fn_TopProductsInSubcategory (@SubcategoryID INT, @N INT)
RETURNS TABLE
AS
RETURN (
    SELECT TOP (@N)
        p.ProductID, p.Name, p.ListPrice,
        ROW_NUMBER() OVER (ORDER BY p.ListPrice DESC) AS PriceRank
    FROM Production.Product AS p
    WHERE p.ProductSubcategoryID = @SubcategoryID AND p.ListPrice > 0
);
GO

SELECT ps.Name AS Subcategory, t.Name AS ProductName, t.ListPrice, t.PriceRank
FROM Production.ProductSubcategory AS ps
CROSS APPLY lesson10.fn_TopProductsInSubcategory(ps.ProductSubcategoryID, 2) AS t
ORDER BY Subcategory, t.PriceRank;
```

```
Subcategory        ProductName                   ListPrice   PriceRank
------------------ ----------------------------- ----------- ---------
Bib-Shorts         Men's Bib-Shorts, S               89.9900         1
Bib-Shorts         Men's Bib-Shorts, M               89.9900         2
Bike Racks         Hitch Rack - 4-Bike              120.0000         1
Bike Stands        All-Purpose Bike Stand           159.0000         1
Bottles and Cages  Mountain Bottle Cage               9.9900         1
Bottles and Cages  Road Bottle Cage                   8.9900         2
(rows omitted — 65 rows total across 37 subcategories)
```

`CROSS APPLY` invokes the iTVF once per row of `ProductSubcategory`, passing that row's ID
in — a plain `JOIN` can't do that because its right side can't reference the left side's
columns. Because it's an iTVF (single `RETURN (SELECT ...)`, no `BEGIN/END`), the whole
thing inlines into one plan, unlike the scalar UDF above.

**Reach for this when** you need "top N per group" or any per-row parameterised lookup —
use `OUTER APPLY` instead when a group with zero matches should still produce a row (NULLs
where the iTVF found nothing).

## Worked examples

`examples.sql` runs these against `lesson10` + AdventureWorks:

1. View `vw_CustomerOrderSummary` — per-customer totals; notice the top 5 by spend all have
   `OrderCount = 12`, so the ranking genuinely comes from `TotalSpend`, not order count.
2. Procedure `usp_GetOrdersByCustomer` with optional `@MinAmount` — notice raising it from
   the default `0` to `500` trims out the customer's smaller orders.
3. Procedure `usp_GetCustomerOrderCount` with an `OUTPUT` parameter — notice the count comes
   back through `@Count OUTPUT`, not as a result set; nothing to `SELECT`.
4. Scalar UDF `fn_AgeInYears` applied per employee row — fine at `HumanResources.Employee`
   scale (~290 rows); Concept 3 shows what changes at `SalesOrderDetail` scale.
5. iTVF `fn_GetProductsByCategory` + `CROSS APPLY ... TOP 1` — notice one row per category,
   each one that category's single most expensive product.

## Pitfalls

- **A `SELECT *` view goes stale silently after the base table changes.** The view's column
  list is captured at creation time, not re-derived on every query:

  ```sql
  CREATE TABLE lesson10.Widget (WidgetID INT IDENTITY PRIMARY KEY, WidgetName NVARCHAR(50));
  INSERT lesson10.Widget (WidgetName) VALUES (N'Sprocket'), (N'Gasket');
  GO
  CREATE OR ALTER VIEW lesson10.vw_Widget AS SELECT * FROM lesson10.Widget;
  GO
  ALTER TABLE lesson10.Widget ADD WidgetPrice MONEY NULL;
  GO
  UPDATE lesson10.Widget SET WidgetPrice = 12.50;
  SELECT * FROM lesson10.vw_Widget;
  ```
  ```
  WidgetID    WidgetName
  ----------- --------------------------------------------------
            1 Sprocket
            2 Gasket
  ```
  `WidgetPrice` exists on the table but doesn't even appear as a column through the view —
  the view's column list is cached from creation time. Fix with `EXEC sp_refreshview
  'lesson10.vw_Widget'`, after which `SELECT *` picks up all three columns. This is also why
  a view over a complex join can still be slow: a view is a stored *query*, not stored
  *data* (unless you create an *indexed* view, which does materialise the result) — every
  call re-runs the underlying `SELECT`.

- **A scalar UDF forces a serial plan — even on a 32-core box.** Concept 3's ~8x CPU gap
  isn't an accident of hardware; the optimizer states the reason directly. Capturing the
  plan with `SET STATISTICS XML ON` for the UDF query shows:
  ```
  NonParallelPlanReason="TSQLUserDefinedFunctionsNotParallelizable"
  ```
  The inline-expression version's plan carries no such attribute. A T-SQL scalar UDF
  blocks parallelism whenever it isn't auto-inlined, no matter how much CPU is free.

- **`ORDER BY` inside a view is a compile-time error, not a runtime surprise** — unless
  paired with `TOP`, `OFFSET`, or `FOR XML`:

  ```sql
  CREATE OR ALTER VIEW lesson10.vw_WidgetOrdered AS
  SELECT WidgetID, WidgetName FROM lesson10.Widget ORDER BY WidgetName;
  ```
  ```
  Msg 1033, Level 15, State 1
  The ORDER BY clause is invalid in views, inline functions, derived tables, subqueries,
  and common table expressions, unless TOP, OFFSET or FOR XML is also specified.
  ```
  Add `TOP (100) PERCENT` (or any `TOP`) and it compiles — but a view's output order is
  never guaranteed to callers regardless; `TOP` without `ORDER BY` inside a view is
  particularly dangerous because it picks an *arbitrary* subset of rows. If a view must
  carry `TOP`, pair it with `ORDER BY ... WITH TIES` inside the view so tie rows aren't cut
  arbitrarily — and still put the `ORDER BY` you actually want in the calling query.

- **Table-typed parameters need a Table-Valued Parameter (TVP), not a plain `TABLE` param.**
  A proc can't accept a caller-supplied result set without a user-defined table type
  declared first and passed `READONLY` — out of scope for this lesson, but the reason
  `EXEC MyProc @Rows = (SELECT ...)` doesn't work the way it looks like it should.

## Exercises

Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`. The
CHALLENGES section at the bottom is harder: those problems combine this lesson's topic with
earlier lessons and usually need 2+ concepts in one script.

## Cheatsheet link

See `cheatsheets/01-tsql-syntax.md`.
