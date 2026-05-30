USE AdventureWorks2022;
GO

-- =========================================================================
-- THE QUERY LIFECYCLE: parse -> bind -> optimize -> execute
-- =========================================================================

-- Example 1: Parsing only (no binding/optimization/execution)
-- SET PARSEONLY checks syntax. A typo here raises a parse error; a valid query returns nothing.
SET PARSEONLY ON;
SELECT SalesOrderID FROM lesson15.SalesOrderHeader WHERE CustomerID = 11000;
SET PARSEONLY OFF;
GO

-- Example 2: Compile without executing (parse + bind + optimize, no execute)
-- SHOWPLAN_TEXT produces a plan without running. Use estimated plan in SSMS (Ctrl+L),
-- or the text form below. It must be the only statement in its batch (separated by GO).
SET SHOWPLAN_TEXT ON;
GO
SELECT SalesOrderID, OrderDate, TotalDue
FROM lesson15.SalesOrderHeader
WHERE CustomerID = 11000;
GO
SET SHOWPLAN_TEXT OFF;
GO

-- =========================================================================
-- STATISTICS & CARDINALITY ESTIMATION
-- =========================================================================

-- Example 3: Read a statistics histogram
-- The histogram is how the optimizer guesses how many rows a predicate returns.
DBCC SHOW_STATISTICS ('lesson15.SalesOrderHeader', 'IX_lesson15_SOH_CustomerID');
-- Three result sets: header (rows, updated), density vector, and the histogram steps.

-- Example 4: Estimated vs actual rows
-- Turn on the actual plan in SSMS (Ctrl+M) and compare the Estimated vs Actual Number of Rows
-- on the Index Seek. With fresh FULLSCAN stats they should match closely.
SELECT SalesOrderID, OrderDate, TotalDue
FROM lesson15.SalesOrderHeader
WHERE CustomerID = 11000;

-- =========================================================================
-- PLAN CACHE
-- =========================================================================

-- Example 5: Find cached plans and their reuse counts for this database
SELECT
    cp.usecounts                       AS TimesReused,
    cp.objtype,
    SUBSTRING(st.text, 1, 120)         AS QueryTextStart
FROM sys.dm_exec_cached_plans      AS cp
CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) AS st
WHERE st.text LIKE '%lesson15.SalesOrderHeader%'
  AND st.text NOT LIKE '%dm_exec_cached_plans%'
ORDER BY cp.usecounts DESC;

-- Example 6: Aggregate runtime stats per query from the plan cache
SELECT TOP 5
    qs.execution_count,
    qs.total_logical_reads,
    qs.total_logical_reads / qs.execution_count AS AvgLogicalReads,
    SUBSTRING(st.text, 1, 120)                  AS QueryTextStart
FROM sys.dm_exec_query_stats        AS qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
WHERE st.text LIKE '%lesson15.SalesOrderHeader%'
  AND st.text NOT LIKE '%dm_exec_query_stats%'
ORDER BY qs.total_logical_reads DESC;

-- =========================================================================
-- WHEN ESTIMATES GO WRONG: STALE STATISTICS
-- =========================================================================

-- Example 7: Make statistics stale, watch the estimate diverge, then fix it
-- Skew the distribution by reassigning many rows to CustomerID 11000 WITHOUT updating stats.
-- (UPDATE avoids the identity/PK conflict that re-inserting rows would cause: SalesOrderID is
--  an IDENTITY column copied by SELECT INTO and is the clustered PK.)
UPDATE TOP (10000) lesson15.SalesOrderHeader
SET CustomerID = 11000
WHERE CustomerID <> 11000;
GO

-- The optimizer still believes the OLD (small) row count for CustomerID = 11000.
-- Run with the actual plan (Ctrl+M) — Estimated rows will be far below Actual rows.
SELECT SalesOrderID, OrderDate FROM lesson15.SalesOrderHeader WHERE CustomerID = 11000;

-- Refresh statistics; the estimate now matches reality again.
UPDATE STATISTICS lesson15.SalesOrderHeader WITH FULLSCAN;
SELECT SalesOrderID, OrderDate FROM lesson15.SalesOrderHeader WHERE CustomerID = 11000;
