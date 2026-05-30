USE AdventureWorks2022;
GO

-- Exercise 1: Read the OrderDate histogram.
-- Approach: the third result set lists RANGE_HI_KEY steps; EQ_ROWS is the estimate for "= key".
DBCC SHOW_STATISTICS ('lesson15.SalesOrderHeader', 'IX_lesson15_SOH_OrderDate');
-- Read any RANGE_HI_KEY row from the histogram and note its EQ_ROWS column.
-- (Exact value depends on AdventureWorks data; e.g. a 2014 date with its EQ_ROWS estimate.)

-- Exercise 2: Estimated vs actual rows.
-- Approach: with FULLSCAN stats the optimizer's estimate should be close to actual.
SELECT SalesOrderID, TotalDue FROM lesson15.SalesOrderHeader WHERE OrderDate >= '2014-01-01';
-- In the actual plan, the operator tooltip shows "Estimated Number of Rows" vs
-- "Actual Number of Rows" — record both; they should be close on fresh statistics.

-- Exercise 3: Cached plan reuse count.
-- Approach: join cached plans to their SQL text and sort by usecounts.
SELECT TOP 5 cp.usecounts, cp.objtype, SUBSTRING(st.text, 1, 120) AS QueryTextStart
FROM sys.dm_exec_cached_plans AS cp
CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) AS st
WHERE st.text LIKE '%lesson15.SalesOrderHeader%'
  AND st.text NOT LIKE '%dm_exec_cached_plans%'
ORDER BY cp.usecounts DESC;

-- Exercise 4: Make stats stale, observe, fix.
-- Approach: reassign many rows to one customer via UPDATE (no identity/PK conflict); the
-- estimate lags reality until stats refresh.
UPDATE TOP (10000) lesson15.SalesOrderHeader
SET CustomerID = 11001
WHERE CustomerID <> 11001;
GO
-- Run with actual plan on: Estimated rows << Actual rows for this customer.
SELECT SalesOrderID, OrderDate FROM lesson15.SalesOrderHeader WHERE CustomerID = 11001;
-- Fix: refresh stats so the estimate matches again.
UPDATE STATISTICS lesson15.SalesOrderHeader WITH FULLSCAN;
SELECT SalesOrderID, OrderDate FROM lesson15.SalesOrderHeader WHERE CustomerID = 11001;

-- Exercise 5: The query lifecycle.
-- 1) Parse  - check syntax, build a parse tree.
-- 2) Bind (algebrize) - resolve object/column names, types; build a logical tree.
-- 3) Optimize - the cost-based optimizer uses STATISTICS to estimate cardinality and pick a plan.
-- 4) Execute - the chosen plan runs; the plan is cached for reuse.
-- Statistics are used in the OPTIMIZE phase, driving cardinality estimates and plan choice.
