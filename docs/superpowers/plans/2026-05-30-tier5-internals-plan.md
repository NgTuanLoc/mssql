# Tier 5 Lessons Implementation Plan (Lessons 14–18 — Engine Internals & Diagnostics)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write the five Tier 5 "Under the Hood" lessons — Storage Internals, Query Lifecycle & Optimizer, Memory/Buffer Pool/Log, Locking/Blocking/Waits, and a Diagnosing-Common-Issues capstone — each containing README.md, setup.sql, examples.sql, exercises.sql, and exercises-solutions.sql; then register the tier in the root README and CLAUDE.md.

**Architecture:** Each lesson directory is self-contained and uses a dedicated `lessonNN` schema inside AdventureWorks2022. `setup.sql` is idempotent (schema-scoped drop/recreate) and copies/inflates AdventureWorks tables so internals can be fragmented, bloated, and locked safely. Internals demos use DMVs, `DBCC PAGE`/`IND` (trace flag 3604), and two-session blocking/deadlock walkthroughs via a second `connect.ps1`. Exercises are "investigate and explain" / "diagnose and fix"; every exercise still ships a solution plus a root-cause explanation.

**Tech Stack:** T-SQL (MSSQL 2022 Developer), AdventureWorks2022, DMVs, DBCC, Markdown. SSMS recommended for graphical plan/deadlock views; sqlcmd path always provided.

**Prerequisite:** Base curriculum (Plans 01–06) complete — container running, AdventureWorks restored, lessons 12–13 done (Tier 5 deepens them).

**Design spec:** `docs/superpowers/specs/2026-05-30-mssql-internals-tier5-design.md`

---

## File Map

| Action | Path |
|--------|------|
| Create | `lessons/14-storage-internals/` (5 files) |
| Create | `lessons/15-query-lifecycle-optimizer/` (5 files) |
| Create | `lessons/16-memory-bufferpool-log/` (5 files) |
| Create | `lessons/17-locking-blocking-waits/` (5 files) |
| Create | `lessons/18-diagnosing-common-issues/` (5 files) |
| Modify | `README.md` (add Tier 5 row + "second session" note) |
| Modify | `CLAUDE.md` (add Tier 5 to curriculum map) |

**Verification note (applies to every lesson task):** "Run setup.sql / examples.sql" means execute the file against the running container. Pipe it via stdin to match the repo's existing podman/docker pattern, e.g.:

```powershell
Get-Content lessons\14-storage-internals\setup.sql | docker exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "<your .env password>" -No -b
```

`-b` makes sqlcmd return a non-zero exit code on error so failures are visible. Expected success ends with the `PRINT 'Lesson NN setup complete.'` line and no `Msg`/error output.

---

## Task 1: Lesson 14 — Storage & Access Internals

**Files:** `lessons/14-storage-internals/` (5 files)

- [ ] **Step 1: Create `lessons/14-storage-internals/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson14') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson14.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson14');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson14;
END
GO
CREATE SCHEMA lesson14;
GO

-- A heap copy (no clustered index) to show heap structure + forwarded records
SELECT * INTO lesson14.SalesOrderHeap
FROM Sales.SalesOrderHeader;

-- A clustered copy to inspect B-tree pages
SELECT * INTO lesson14.SalesOrderCI
FROM Sales.SalesOrderHeader;

ALTER TABLE lesson14.SalesOrderCI
    ADD CONSTRAINT PK_lesson14_SalesOrderCI PRIMARY KEY CLUSTERED (SalesOrderID);

-- A small table with a non-sequential clustered key to demonstrate page splits.
-- Low fill factor + wide rows make a split easy to trigger and observe.
CREATE TABLE lesson14.SplitDemo (
    Id        INT          NOT NULL,
    Filler    CHAR(2000)   NOT NULL DEFAULT REPLICATE('x', 2000),
    CONSTRAINT PK_lesson14_SplitDemo PRIMARY KEY CLUSTERED (Id)
        WITH (FILLFACTOR = 100)
);

-- Seed gaps (10, 20, 30, ...) so we can later insert a middle value (e.g. 15) and split a page
INSERT lesson14.SplitDemo (Id)
SELECT TOP 12 ROW_NUMBER() OVER (ORDER BY object_id) * 10
FROM sys.all_objects;

UPDATE STATISTICS lesson14.SalesOrderHeap;
UPDATE STATISTICS lesson14.SalesOrderCI;

PRINT 'Lesson 14 setup complete.';
```

- [ ] **Step 2: Create `lessons/14-storage-internals/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- Example 1: Find the physical location (file:page:slot) of specific rows
-- %%physloc%% is an undocumented-but-stable virtual column; fn_PhysLocFormatter decodes it.
SELECT TOP 5
    SalesOrderID,
    sys.fn_PhysLocFormatter(%%physloc%%) AS PhysicalLocation   -- format: (file:page:slot)
FROM lesson14.SalesOrderCI
ORDER BY SalesOrderID;

-- Example 2: List the pages allocated to a table and their types
-- page_type_desc shows DATA_PAGE, INDEX_PAGE, IAM_PAGE, etc.
SELECT
    allocated_page_file_id  AS FileId,
    allocated_page_page_id  AS PageId,
    page_type_desc,
    index_id
FROM sys.dm_db_database_page_allocations(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderCI'), NULL, NULL, 'DETAILED')
WHERE page_type_desc IS NOT NULL
ORDER BY page_type_desc, PageId;

-- Example 3: Crack open a real data page with DBCC PAGE
-- Trace flag 3604 redirects DBCC output to the client instead of the error log.
-- Pick any DATA_PAGE PageId from Example 2 and substitute it below.
DBCC TRACEON(3604);
-- DBCC PAGE ('AdventureWorks2022', <FileId>, <PageId>, 3);  -- style 3 = page header + each row
-- Example with a placeholder page number — replace 0 with a real PageId from Example 2:
-- DBCC PAGE ('AdventureWorks2022', 1, 0, 3);
PRINT 'Replace the FileId/PageId above with a real DATA_PAGE from Example 2, then run DBCC PAGE.';
DBCC TRACEOFF(3604);

-- Example 4: Fragmentation and page density of the clustered index
SELECT
    i.name                              AS IndexName,
    ips.index_type_desc,
    ips.page_count,
    ips.avg_fragmentation_in_percent,
    ips.avg_page_space_used_in_percent  AS AvgPageFullnessPct,
    ips.forwarded_record_count          -- only meaningful for heaps
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderCI'), NULL, NULL, 'DETAILED') AS ips
JOIN sys.indexes AS i
  ON i.object_id = ips.object_id AND i.index_id = ips.index_id;

-- Example 5: Heaps create FORWARDED RECORDS when an updated row no longer fits in place
-- Inspect the heap before the update
SELECT 'before' AS Phase, forwarded_record_count, page_count
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderHeap'), 0, NULL, 'DETAILED');

-- Widen rows in place by appending to a variable-length column (PurchaseOrderNumber)
UPDATE lesson14.SalesOrderHeap
SET PurchaseOrderNumber = REPLICATE('Z', 30)
WHERE SalesOrderID % 5 = 0;

-- Inspect again — forwarded_record_count rises because moved rows leave a forwarding pointer
SELECT 'after' AS Phase, forwarded_record_count, page_count
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderHeap'), 0, NULL, 'DETAILED');

-- Example 6: Trigger and observe a PAGE SPLIT on a clustered B-tree
-- Page count BEFORE inserting a middle key
SELECT 'before split' AS Phase, page_count, avg_page_space_used_in_percent
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SplitDemo'), 1, NULL, 'DETAILED');

-- Insert a key that lands in the MIDDLE of a full page (rows are 2000 bytes; ~4 fit per 8KB page)
INSERT lesson14.SplitDemo (Id) VALUES (15), (25), (35);

-- Page count AFTER — the engine splits full pages to make room, raising page_count
SELECT 'after split' AS Phase, page_count, avg_page_space_used_in_percent
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SplitDemo'), 1, NULL, 'DETAILED');
```

- [ ] **Step 3: Create `lessons/14-storage-internals/exercises.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: For SalesOrderID 43659 in lesson14.SalesOrderCI, report its physical
--             location (file:page:slot). Then list all DATA_PAGE pages of the table.
-- Expected: one location string, then a list of data page ids.
-- Hint: %%physloc%% + sys.fn_PhysLocFormatter; sys.dm_db_database_page_allocations.
-- Your query here:


-- Exercise 2: Using DBCC PAGE, crack open the data page that holds SalesOrderID 43659
--             (use the PageId you found in Exercise 1). Turn on trace flag 3604 first.
-- Your statements here:


-- Exercise 3: Measure fragmentation of lesson14.SalesOrderCI's clustered index.
--             Then REBUILD it and measure again. Report avg_fragmentation_in_percent
--             before and after.
-- Your statements here:


-- Exercise 4: lesson14.SalesOrderHeap is a heap. Report its forwarded_record_count.
--             Explain in a comment why forwarded records hurt read performance.
-- Your query + comment here:


-- Exercise 5: Add 3 more middle-valued keys to lesson14.SplitDemo (e.g. 5, 12, 18)
--             and show that page_count increased compared to before the insert.
--             In a comment, explain what a page split is and one way to reduce them.
-- Your statements + comment here:
```

- [ ] **Step 4: Create `lessons/14-storage-internals/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Physical location of a row + list of data pages.
-- Approach: %%physloc%% gives the row's RID; page allocations DMV lists every page.
SELECT SalesOrderID, sys.fn_PhysLocFormatter(%%physloc%%) AS PhysicalLocation
FROM lesson14.SalesOrderCI
WHERE SalesOrderID = 43659;

SELECT allocated_page_file_id AS FileId, allocated_page_page_id AS PageId, page_type_desc
FROM sys.dm_db_database_page_allocations(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderCI'), NULL, NULL, 'DETAILED')
WHERE page_type_desc = 'DATA_PAGE'
ORDER BY PageId;

-- Exercise 2: Crack the page holding SalesOrderID 43659.
-- Approach: read file/page from the physloc formatter output (file:page:slot), then DBCC PAGE.
-- Replace 1 and 0 below with the FileId/PageId from Exercise 1's PhysicalLocation.
DBCC TRACEON(3604);
-- DBCC PAGE ('AdventureWorks2022', 1, 0, 3);   -- <-- substitute real FileId/PageId
DBCC TRACEOFF(3604);
-- The output shows the page header (m_type=1 data page) and each slot's row contents.

-- Exercise 3: Fragmentation before and after a REBUILD.
-- Approach: REBUILD recreates the index contiguously, dropping fragmentation toward 0%.
SELECT 'before' AS Phase, avg_fragmentation_in_percent, page_count
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderCI'), 1, NULL, 'DETAILED');

ALTER INDEX PK_lesson14_SalesOrderCI ON lesson14.SalesOrderCI REBUILD;

SELECT 'after' AS Phase, avg_fragmentation_in_percent, page_count
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderCI'), 1, NULL, 'DETAILED');

-- Exercise 4: Heap forwarded records.
-- Approach: index_id 0 = the heap. Forwarded records appear after in-place updates grow rows.
SELECT forwarded_record_count, page_count
FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SalesOrderHeap'), 0, NULL, 'DETAILED');
-- Why they hurt: a forwarded record leaves a pointer at the original slot to the new location.
-- A scan must follow the pointer (an extra random I/O) to read the row, so reads do more work.
-- Fix: give the table a clustered index (B-trees relocate via the key, not forwarding pointers),
-- or run ALTER TABLE ... REBUILD to remove existing forwarding pointers.

-- Exercise 5: More splits on SplitDemo.
-- Approach: middle-valued inserts split full pages; page_count rises.
SELECT 'before' AS Phase, page_count FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SplitDemo'), 1, NULL, 'DETAILED');

INSERT lesson14.SplitDemo (Id) VALUES (5), (12), (18);

SELECT 'after' AS Phase, page_count FROM sys.dm_db_index_physical_stats(
        DB_ID(), OBJECT_ID('lesson14.SplitDemo'), 1, NULL, 'DETAILED');
-- A page split happens when a row must go onto a full page: SQL Server allocates a new page,
-- moves ~half the rows to it, then inserts. It causes fragmentation and extra log.
-- Reduce splits with a lower FILLFACTOR (leave free space) or an ever-increasing key
-- (appends go to the end and rarely split mid-index).
```

- [ ] **Step 5: Create `lessons/14-storage-internals/README.md`**

```markdown
# Lesson 14 — Storage & Access Internals

> **Prerequisite:** Tiers 1–4, especially lesson 12 (indexes) and 13 (execution plans). This lesson explains the *why* under those topics.

## What you'll learn
- The 8KB page: header, slot array, and rows
- Extents (mixed vs uniform) and how tables are allocated
- Heaps vs clustered B-trees, and how a row is physically located
- Reading real pages with `DBCC PAGE` and the allocation DMVs
- Why page splits, fragmentation, and heap forwarded records happen

## Setup
Run `setup.sql` once. It creates the `lesson14` schema with a heap copy and a clustered copy of `SalesOrderHeader`, plus a small `SplitDemo` table for page-split experiments. Re-run to reset.

## Concepts

### The 8KB page
Everything in SQL Server is stored in 8KB pages. A page has a 96-byte header, then rows growing from the top, and a slot array growing from the bottom that points to each row. Eight contiguous pages form an **extent** (64KB).

### Heap vs clustered index
- A **heap** has no clustered index — rows live wherever they fit, located via a Row ID (file:page:slot).
- A **clustered index** sorts the data pages by the key in a B-tree (root → intermediate → leaf). The leaf level *is* the data.

### How a row is located
`%%physloc%%` exposes a row's physical RID; `sys.fn_PhysLocFormatter` decodes it to `(file:page:slot)`. `sys.dm_db_database_page_allocations` lists every page of an object. `DBCC PAGE` (with trace flag 3604) prints a page's raw contents.

### Why things go wrong
- **Page split:** inserting into a full page forces SQL Server to allocate a new page and move ~half the rows — causing fragmentation and extra logging.
- **Fragmentation:** logical page order drifts from physical order; scans do more random I/O. `sys.dm_db_index_physical_stats` measures it.
- **Forwarded records:** in a *heap*, an updated row that no longer fits leaves a forwarding pointer to its new location — an extra I/O on every read.

## Worked Investigations (lesson14 schema)
1. `%%physloc%%` — the physical address of a row.
2. `sys.dm_db_database_page_allocations` — list a table's pages and types.
3. `DBCC PAGE` — crack open a real data page.
4. `sys.dm_db_index_physical_stats` — fragmentation and page fullness.
5. Heap forwarded records — before/after an in-place update.
6. Page split — before/after a middle-key insert.

## Common issues this explains
- Why heaps can be slow to read after updates (forwarding pointers).
- Why random-key inserts (e.g., GUIDs) fragment a clustered index.
- Why a low fill factor trades space for fewer splits.

## Pitfalls
- `DBCC PAGE` is a learning/diagnostic tool, not a documented production API — don't build on it.
- `'LIMITED'` mode of `dm_db_index_physical_stats` does not report `forwarded_record_count` or page fullness; use `'DETAILED'` (slower) when you need them.
- `%%physloc%%` changes when a row moves — it is not a stable key.

## Cheatsheet link
See `cheatsheets/04-indexes.md`

## Exercises
Open `exercises.sql` and work them in order. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Run setup.sql and examples.sql to verify they execute without errors**

Run `setup.sql` (expect `Lesson 14 setup complete.`), then run `examples.sql`. DBCC PAGE lines are commented placeholders, so examples should complete with no `Msg` errors. Page-split and forwarded-record examples should show the "after" numbers ≥ the "before" numbers.

- [ ] **Step 7: Commit**

```powershell
git add lessons/14-storage-internals/
git commit -m "feat: add lesson 14 - storage and access internals"
```

---

## Task 2: Lesson 15 — Query Lifecycle & the Optimizer

**Files:** `lessons/15-query-lifecycle-optimizer/` (5 files)

- [ ] **Step 1: Create `lessons/15-query-lifecycle-optimizer/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson15') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson15.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson15');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson15;
END
GO
CREATE SCHEMA lesson15;
GO

-- Indexed copy so we can manipulate statistics independently of the real table
SELECT * INTO lesson15.SalesOrderHeader
FROM Sales.SalesOrderHeader;

ALTER TABLE lesson15.SalesOrderHeader
    ADD CONSTRAINT PK_lesson15_SOH PRIMARY KEY CLUSTERED (SalesOrderID);

CREATE INDEX IX_lesson15_SOH_CustomerID
    ON lesson15.SalesOrderHeader (CustomerID) INCLUDE (OrderDate, TotalDue);

CREATE INDEX IX_lesson15_SOH_OrderDate
    ON lesson15.SalesOrderHeader (OrderDate) INCLUDE (TotalDue);

UPDATE STATISTICS lesson15.SalesOrderHeader WITH FULLSCAN;

PRINT 'Lesson 15 setup complete.';
```

- [ ] **Step 2: Create `lessons/15-query-lifecycle-optimizer/examples.sql`**

```sql
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
-- SET FNMTONLY / SHOWPLAN produce a plan without running. Use estimated plan in SSMS (Ctrl+L),
-- or the text form below.
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
```

- [ ] **Step 3: Create `lessons/15-query-lifecycle-optimizer/exercises.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Use DBCC SHOW_STATISTICS on IX_lesson15_SOH_OrderDate. From the histogram,
--             pick a RANGE_HI_KEY date and report its EQ_ROWS (estimated rows equal to that key).
-- Your statements + the value you read here:


-- Exercise 2: Run the query below with the ACTUAL plan on (Ctrl+M in SSMS). Report the
--             Estimated vs Actual number of rows on the seek/scan operator.
SELECT SalesOrderID, TotalDue FROM lesson15.SalesOrderHeader WHERE OrderDate >= '2014-01-01';
-- Your observed estimated/actual here:


-- Exercise 3: Find the cached plan(s) for any query touching lesson15.SalesOrderHeader and
--             report the highest usecounts (reuse count).
-- Your query here:


-- Exercise 4: Deliberately make statistics stale: reassign a large batch of rows to
--             CustomerID = 11001 via UPDATE (do NOT update stats yet). Then run a query
--             filtering CustomerID = 11001 with the actual plan on and observe the estimate is
--             now too low. Finally fix it with UPDATE STATISTICS and re-run.
-- Your statements here:


-- Exercise 5: In a comment, explain the four phases of the query lifecycle and name the phase
--             where statistics are used.
-- Your comment here:
```

- [ ] **Step 4: Create `lessons/15-query-lifecycle-optimizer/exercises-solutions.sql`**

```sql
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
```

- [ ] **Step 5: Create `lessons/15-query-lifecycle-optimizer/README.md`**

```markdown
# Lesson 15 — Query Lifecycle & the Optimizer

> **Prerequisite:** Tiers 1–4, especially lesson 13 (execution plans). This lesson explains how plans are *chosen*.

## What you'll learn
- The four phases a query goes through: parse → bind → optimize → execute
- What statistics are and how histograms drive cardinality estimates
- How to read a histogram with `DBCC SHOW_STATISTICS`
- Inspecting the plan cache (`sys.dm_exec_cached_plans`, `sys.dm_exec_query_stats`)
- Why estimates diverge from actuals — and how stale stats cause bad plans

## Setup
Run `setup.sql` once. It creates the `lesson15` schema with an indexed copy of `SalesOrderHeader` and fresh full-scan statistics. Re-run to reset.

## Concepts

### The lifecycle
1. **Parse** — syntax check, parse tree.
2. **Bind (algebrize)** — resolve names and types into a logical tree.
3. **Optimize** — the cost-based optimizer enumerates plans and picks the cheapest, using **statistics** to estimate how many rows each operator will process.
4. **Execute** — run the plan; cache it keyed by the statement so it can be reused.

### Statistics & cardinality estimation
A statistics object carries a **histogram** (up to 200 steps) describing the distribution of a column's values. The optimizer reads it to estimate rows for a predicate (cardinality). Good estimates → good plans. `DBCC SHOW_STATISTICS` prints the header, density vector, and histogram.

### The plan cache
Compiled plans live in memory and are reused. `sys.dm_exec_cached_plans` shows plans and reuse counts; `sys.dm_exec_query_stats` aggregates runtime metrics (reads, CPU, executions) per cached statement.

### When estimates go wrong
If data changes a lot but statistics don't refresh, the optimizer plans for the *old* distribution — estimates fall far from actuals and it may pick a bad plan (wrong join type, too little memory). `UPDATE STATISTICS` refreshes them.

## Worked Investigations (lesson15 schema)
1. `SET PARSEONLY` — parse phase in isolation.
2. `SET SHOWPLAN_TEXT` — compile to a plan without executing.
3. `DBCC SHOW_STATISTICS` — read a histogram.
4. Estimated vs actual rows in the plan.
5. `sys.dm_exec_cached_plans` — reuse counts.
6. `sys.dm_exec_query_stats` — per-query runtime stats.
7. Stale-stats demo — make the estimate wrong, then fix it.

## Common issues this explains
- Stale statistics causing wildly wrong estimates and bad plans.
- Why the same query can flip to a worse plan after a big data change.
- The cardinality-estimation root of most "why did it pick *that* plan?" questions.

## Pitfalls
- `SHOWPLAN_TEXT`/`SHOWPLAN_ALL` must be the only statement in the batch — separate with `GO`.
- Auto-update stats triggers on a row-change threshold; large tables can go stale between triggers.
- A cached plan compiled for one parameter value may be reused for a very different one — that is parameter sniffing (covered in lessons 13 and 18).

## Cheatsheet link
See `cheatsheets/05-execution-plans.md`

## Exercises
Open `exercises.sql` and work them in order. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Run setup.sql and examples.sql to verify they execute without errors**

Run `setup.sql` (expect `Lesson 15 setup complete.`), then `examples.sql`. Expect three result sets from `DBCC SHOW_STATISTICS`, plan-cache rows, and no `Msg` errors. The stale-stats example inserts duplicate rows — that is intentional and reset by re-running `setup.sql`.

- [ ] **Step 7: Commit**

```powershell
git add lessons/15-query-lifecycle-optimizer/
git commit -m "feat: add lesson 15 - query lifecycle and optimizer"
```

---

## Task 3: Lesson 16 — Memory, Buffer Pool & the Log

**Files:** `lessons/16-memory-bufferpool-log/` (5 files)

- [ ] **Step 1: Create `lessons/16-memory-bufferpool-log/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson16') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson16.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson16');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson16;
END
GO
CREATE SCHEMA lesson16;
GO

-- A table large enough to make buffer-pool and spill behaviour visible.
-- One-shot SELECT INTO with a CROSS JOIN multiplier (~121k rows x 4 = ~485k rows).
-- Because the SELECT contains a JOIN, the IDENTITY property of SalesOrderDetailID is NOT
-- carried to BigDetail, so there is no IDENTITY_INSERT conflict; the computed LineTotal
-- column is materialised as a plain column. SELECT INTO copies no constraints/indexes.
-- (First run may take a minute or two — this is expected.)
SELECT sod.*
INTO lesson16.BigDetail
FROM Sales.SalesOrderDetail AS sod
CROSS JOIN (SELECT TOP 4 object_id FROM sys.all_objects) AS m;

UPDATE STATISTICS lesson16.BigDetail;

PRINT 'Lesson 16 setup complete.';
```

- [ ] **Step 2: Create `lessons/16-memory-bufferpool-log/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- =========================================================================
-- THE BUFFER POOL (data cache)
-- =========================================================================

-- Example 1: How many pages of each database are currently cached?
SELECT
    CASE database_id WHEN 32767 THEN 'ResourceDB'
         ELSE DB_NAME(database_id) END AS DatabaseName,
    COUNT(*)                            AS CachedPages,
    COUNT(*) * 8 / 1024                 AS CachedMB
FROM sys.dm_os_buffer_descriptors
GROUP BY database_id
ORDER BY CachedPages DESC;

-- Example 2: Show a table being pulled into cache
-- Clear THIS database's cached pages first (dev only — never in production)
-- DBCC DROPCLEANBUFFERS clears clean pages server-wide; CHECKPOINT flushes dirty ones first.
CHECKPOINT;
DBCC DROPCLEANBUFFERS;

-- How much of BigDetail is cached now? (expect ~0)
SELECT COUNT(*) AS BigDetailPagesCached
FROM sys.dm_os_buffer_descriptors AS bd
WHERE bd.database_id = DB_ID()
  AND bd.allocation_unit_id IN (
        SELECT au.allocation_unit_id
        FROM sys.allocation_units AS au
        JOIN sys.partitions AS p ON p.partition_id = au.container_id
        WHERE p.object_id = OBJECT_ID('lesson16.BigDetail'));

-- Scan the table to load it into the buffer pool
SELECT COUNT(*) FROM lesson16.BigDetail;

-- Now many more pages are cached
SELECT COUNT(*) AS BigDetailPagesCached
FROM sys.dm_os_buffer_descriptors AS bd
WHERE bd.database_id = DB_ID()
  AND bd.allocation_unit_id IN (
        SELECT au.allocation_unit_id
        FROM sys.allocation_units AS au
        JOIN sys.partitions AS p ON p.partition_id = au.container_id
        WHERE p.object_id = OBJECT_ID('lesson16.BigDetail'));

-- Example 3: Page Life Expectancy (seconds a page is expected to stay in cache)
-- Higher is better; a sharply falling PLE signals memory pressure.
SELECT object_name, counter_name, cntr_value AS PLE_Seconds
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Page life expectancy'
  AND object_name LIKE '%Buffer Manager%';

-- =========================================================================
-- THE TRANSACTION LOG
-- =========================================================================

-- Example 4: Inspect the Virtual Log Files (VLFs) of the current database
SELECT COUNT(*) AS VLF_Count
FROM sys.dm_db_log_info(DB_ID());

-- Example 5: Why can't the log be truncated right now?
-- log_reuse_wait_desc tells you what the log is waiting on (NOTHING, ACTIVE_TRANSACTION, etc.)
SELECT name, log_reuse_wait_desc, recovery_model_desc
FROM sys.databases
WHERE name = 'AdventureWorks2022';

-- Example 6: An open transaction holds the log hostage
BEGIN TRANSACTION;
UPDATE lesson16.BigDetail SET OrderQty = OrderQty WHERE SalesOrderDetailID % 1000 = 0;

-- While the transaction is OPEN, check log reuse wait again — expect ACTIVE_TRANSACTION
SELECT name, log_reuse_wait_desc FROM sys.databases WHERE name = 'AdventureWorks2022';

-- See the open transaction
SELECT
    DB_NAME(dt.database_id)   AS DatabaseName,
    st.session_id,
    at.name                   AS TranName,
    at.transaction_begin_time
FROM sys.dm_tran_active_transactions  AS at
JOIN sys.dm_tran_session_transactions AS st ON st.transaction_id = at.transaction_id
JOIN sys.dm_tran_database_transactions AS dt ON dt.transaction_id = at.transaction_id
WHERE dt.database_id = DB_ID();

COMMIT TRANSACTION;   -- release it; log_reuse_wait_desc returns to NOTHING

-- =========================================================================
-- MEMORY GRANTS & TEMPDB SPILLS
-- =========================================================================

-- Example 7: Force a tempdb spill by starving the sort of memory
-- A big sort needs a memory grant. MAX_GRANT_PERCENT caps it tiny, forcing a spill to tempdb.
-- Run with the ACTUAL plan (Ctrl+M): the Sort operator shows a warning triangle "spilled to tempdb".
SELECT *
FROM lesson16.BigDetail
ORDER BY ProductID, ModifiedDate, OrderQty, UnitPrice
OPTION (MAX_GRANT_PERCENT = 0.0);   -- starve the grant to guarantee a spill

-- Inspect active/last memory grants
SELECT TOP 5
    mg.session_id,
    mg.requested_memory_kb,
    mg.granted_memory_kb,
    mg.required_memory_kb,
    mg.dop
FROM sys.dm_exec_query_memory_grants AS mg;
```

- [ ] **Step 3: Create `lessons/16-memory-bufferpool-log/exercises.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Report how many MB of the buffer pool AdventureWorks2022 currently occupies.
-- Hint: sys.dm_os_buffer_descriptors grouped by database_id; 8KB per page.
-- Your query here:


-- Exercise 2: Clear the cache (CHECKPOINT then DBCC DROPCLEANBUFFERS), show that few pages of
--             lesson16.BigDetail are cached, scan the table, then show many more are cached.
-- Your statements here:


-- Exercise 3: Report the current Page Life Expectancy in seconds. In a comment, say whether a
--             value of 50 on a busy server would worry you and why.
-- Your query + comment here:


-- Exercise 4: Open a transaction that updates some rows of lesson16.BigDetail and DO NOT commit
--             yet. In another batch (or just below), show that
--             sys.databases.log_reuse_wait_desc for AdventureWorks2022 is ACTIVE_TRANSACTION.
--             Then commit and show it returns to NOTHING.
-- Your statements here:


-- Exercise 5: Force a tempdb spill on a large sort of lesson16.BigDetail using
--             OPTION (MAX_GRANT_PERCENT = 0.0). In a comment, explain what a spill is and one
--             non-hint way to avoid it in real workloads.
-- Your statements + comment here:
```

- [ ] **Step 4: Create `lessons/16-memory-bufferpool-log/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: AdventureWorks cache footprint.
-- Approach: count cached pages for this database_id and convert to MB.
SELECT DB_NAME(database_id) AS DatabaseName,
       COUNT(*)             AS CachedPages,
       COUNT(*) * 8 / 1024  AS CachedMB
FROM sys.dm_os_buffer_descriptors
WHERE database_id = DB_ID('AdventureWorks2022')
GROUP BY database_id;

-- Exercise 2: Watch a table load into cache.
-- Approach: drop clean buffers, measure, scan, measure again.
CHECKPOINT;
DBCC DROPCLEANBUFFERS;

SELECT COUNT(*) AS CachedBefore
FROM sys.dm_os_buffer_descriptors AS bd
WHERE bd.database_id = DB_ID()
  AND bd.allocation_unit_id IN (
        SELECT au.allocation_unit_id FROM sys.allocation_units au
        JOIN sys.partitions p ON p.partition_id = au.container_id
        WHERE p.object_id = OBJECT_ID('lesson16.BigDetail'));

SELECT COUNT(*) FROM lesson16.BigDetail;   -- forces a scan into cache

SELECT COUNT(*) AS CachedAfter
FROM sys.dm_os_buffer_descriptors AS bd
WHERE bd.database_id = DB_ID()
  AND bd.allocation_unit_id IN (
        SELECT au.allocation_unit_id FROM sys.allocation_units au
        JOIN sys.partitions p ON p.partition_id = au.container_id
        WHERE p.object_id = OBJECT_ID('lesson16.BigDetail'));

-- Exercise 3: Page Life Expectancy.
-- Approach: read the Buffer Manager PLE counter.
SELECT cntr_value AS PLE_Seconds
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Page life expectancy'
  AND object_name LIKE '%Buffer Manager%';
-- A PLE of 50 on a busy server is alarming: pages survive only ~50s in cache, meaning SQL Server
-- is constantly re-reading from disk (memory pressure). On a quiet dev container PLE can be low
-- simply because nothing is keeping it warm — context matters; watch the trend, not one number.

-- Exercise 4: Open transaction blocks log truncation.
-- Approach: an uncommitted transaction sets log_reuse_wait_desc = ACTIVE_TRANSACTION.
BEGIN TRANSACTION;
UPDATE lesson16.BigDetail SET OrderQty = OrderQty WHERE SalesOrderDetailID % 1000 = 0;

SELECT log_reuse_wait_desc FROM sys.databases WHERE name = 'AdventureWorks2022';
-- Expect: ACTIVE_TRANSACTION

COMMIT TRANSACTION;
SELECT log_reuse_wait_desc FROM sys.databases WHERE name = 'AdventureWorks2022';
-- Expect: NOTHING

-- Exercise 5: Force and explain a tempdb spill.
-- Approach: starve the memory grant so the sort cannot complete in memory.
SELECT *
FROM lesson16.BigDetail
ORDER BY ProductID, ModifiedDate, OrderQty, UnitPrice
OPTION (MAX_GRANT_PERCENT = 0.0);
-- A spill happens when an operator (sort/hash) is granted too little memory and must write
-- intermediate data to tempdb, doing extra slow I/O. The actual plan flags the Sort with a
-- warning. Non-hint fixes: refresh statistics so the optimizer requests the right grant, reduce
-- the rows/columns being sorted, or add an index that delivers rows already sorted (no sort).
```

- [ ] **Step 5: Create `lessons/16-memory-bufferpool-log/README.md`**

```markdown
# Lesson 16 — Memory, Buffer Pool & the Log

> **Prerequisite:** Tiers 1–4. Pairs with lesson 13 (plans) for the spill discussion.

## What you'll learn
- The buffer pool (data cache): how data lives in memory, clean vs dirty pages
- Checkpoint and the lazy writer; Page Life Expectancy as a memory-pressure signal
- Write-ahead logging (WAL), the transaction log, and Virtual Log Files (VLFs)
- Why the log won't truncate (`log_reuse_wait_desc`)
- Memory grants and tempdb spills

## Setup
Run `setup.sql` once. It creates the `lesson16` schema with `BigDetail` — an inflated copy of `SalesOrderDetail` large enough to make caching and spills visible. **First run may take a minute or two** to build the table. Re-run to reset.

## Concepts

### The buffer pool
SQL Server reads/writes 8KB pages in memory, not on disk directly. The **buffer pool** is that data cache. A page modified in memory is **dirty** until a **checkpoint** (or the **lazy writer** under memory pressure) flushes it to disk. `sys.dm_os_buffer_descriptors` shows what is cached.

### Page Life Expectancy (PLE)
The expected seconds a page stays cached before eviction. A healthy busy server keeps PLE high and stable; a sharp drop means SQL Server is churning the cache (memory pressure). Read it from `sys.dm_os_performance_counters`. On an idle dev box a low PLE can be meaningless — watch the *trend*.

### Write-ahead logging & the log
Before a data page change is written, the change is written to the **transaction log** (WAL). The log is divided into **VLFs** (`sys.dm_db_log_info`). The log can only be truncated (reused) once a portion is no longer needed; `sys.databases.log_reuse_wait_desc` tells you what it's waiting on — most commonly `ACTIVE_TRANSACTION` (an open transaction) or a pending backup in FULL recovery.

### Memory grants & spills
Sorts and hash joins request a **memory grant** before executing, sized from cardinality estimates. If the grant is too small (bad estimate, or forced), the operator **spills to tempdb** — extra slow I/O flagged on the plan operator. `sys.dm_exec_query_memory_grants` shows grants.

## Worked Investigations (lesson16 schema)
1. Cached pages per database.
2. A table loading into cache before/after a scan.
3. Page Life Expectancy.
4. VLF count.
5. `log_reuse_wait_desc`.
6. An open transaction holding the log (ACTIVE_TRANSACTION).
7. A forced tempdb spill + memory-grant DMV.

## Common issues this explains
- Low PLE / memory pressure on busy systems.
- A transaction log that grows uncontrollably because of a long-open transaction.
- tempdb spills slowing big sorts/joins.

## Pitfalls
- `DBCC DROPCLEANBUFFERS` / `CHECKPOINT` are dev-only diagnostics — never run them to "fix" production.
- Many tiny VLFs (log autogrown in small increments) slow recovery and log operations.
- A single forgotten open transaction can stop log truncation for the whole database.
- Memory-grant numbers vary by server memory and DOP — compare direction, not absolute KB.

## Cheatsheet link
See `cheatsheets/05-execution-plans.md`

## Exercises
Open `exercises.sql` and work them in order. Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Run setup.sql and examples.sql to verify they execute without errors**

Run `setup.sql` (expect `Lesson 16 setup complete.`, allow 1–2 minutes). Then run `examples.sql`. Expect: cache counts rising after the scan, a VLF count, `log_reuse_wait_desc` flipping to `ACTIVE_TRANSACTION` inside the open transaction and back to `NOTHING` after commit, and no `Msg` errors. The spill query returns many rows — that is expected.

- [ ] **Step 7: Commit**

```powershell
git add lessons/16-memory-bufferpool-log/
git commit -m "feat: add lesson 16 - memory, buffer pool and the log"
```

---

## Task 4: Lesson 17 — Locking, Blocking, Deadlocks & Waits

**Files:** `lessons/17-locking-blocking-waits/` (5 files)

- [ ] **Step 1: Create `lessons/17-locking-blocking-waits/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson17') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson17.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson17');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson17;
END
GO
CREATE SCHEMA lesson17;
GO

-- Two small tables for deterministic blocking and deadlock demos
CREATE TABLE lesson17.Account (
    AccountID INT NOT NULL PRIMARY KEY,
    Owner     NVARCHAR(50) NOT NULL,
    Balance   DECIMAL(14,2) NOT NULL
);
INSERT lesson17.Account VALUES (1, N'Alice', 1000), (2, N'Bob', 500), (3, N'Carol', 250);

-- A larger table so a big UPDATE triggers lock escalation (row/page locks escalate to a table lock).
-- Single SELECT INTO copy (no re-insert), then add the clustered PK.
SELECT * INTO lesson17.BigOrders FROM Sales.SalesOrderHeader;
ALTER TABLE lesson17.BigOrders ADD CONSTRAINT PK_lesson17_BigOrders PRIMARY KEY CLUSTERED (SalesOrderID);

PRINT 'Lesson 17 setup complete.';
```

- [ ] **Step 2: Create `lessons/17-locking-blocking-waits/examples.sql`**

```sql
USE AdventureWorks2022;
GO

-- =========================================================================
-- BLOCKING (needs TWO sessions)
-- Open a second terminal and run .\scripts\connect.ps1 to get Session B.
-- =========================================================================

-- ---- Session A: start a transaction and hold an exclusive lock (do NOT commit yet) ----
-- BEGIN TRANSACTION;
-- UPDATE lesson17.Account SET Balance = Balance - 100 WHERE AccountID = 1;
-- (leave this open, switch to Session B)

-- ---- Session B: try to read the same row — it BLOCKS waiting for Session A's lock ----
-- SELECT * FROM lesson17.Account WHERE AccountID = 1;   -- hangs until A commits/rolls back

-- ---- Session A (or a third session): see the blocking chain ----
-- Example 1: Who is blocking whom?
SELECT
    r.session_id        AS WaitingSession,
    r.blocking_session_id AS BlockedBy,
    r.wait_type,
    r.wait_time         AS WaitMs,
    r.wait_resource,
    t.text              AS WaitingSQL
FROM sys.dm_exec_requests AS r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE r.blocking_session_id <> 0;

-- Example 2: What locks are currently held / requested?
SELECT
    l.request_session_id  AS SessionId,
    l.resource_type,
    l.request_mode,         -- S, X, U, IS, IX, etc.
    l.request_status,       -- GRANT or WAIT
    OBJECT_NAME(p.object_id) AS ObjectName
FROM sys.dm_tran_locks AS l
LEFT JOIN sys.partitions AS p ON p.hobt_id = l.resource_associated_entity_id
WHERE l.resource_database_id = DB_ID()
ORDER BY l.request_session_id, l.resource_type;

-- Example 3: Waiting tasks (the live view of what is stuck)
SELECT
    wt.session_id,
    wt.wait_type,
    wt.wait_duration_ms,
    wt.blocking_session_id
FROM sys.dm_os_waiting_tasks AS wt
WHERE wt.session_id > 50;   -- skip system sessions

-- ---- Session A: release the lock ----
-- COMMIT TRANSACTION;   -- Session B's SELECT now returns immediately

-- =========================================================================
-- LOCK MODES & ESCALATION
-- =========================================================================

-- Example 4: A large UPDATE escalates many row/page locks to a single TABLE lock
-- Run this and inspect sys.dm_tran_locks (in another session) — you'll see an OBJECT-level X lock.
BEGIN TRANSACTION;
UPDATE lesson17.BigOrders SET Status = Status WHERE SalesOrderID > 0;  -- touches every row
-- In Session B: SELECT resource_type, request_mode FROM sys.dm_tran_locks
--               WHERE request_session_id = <this SPID>;  -- expect an OBJECT X lock (escalated)
ROLLBACK TRANSACTION;

-- =========================================================================
-- DEADLOCK (needs TWO sessions) — deterministic ordering
-- =========================================================================

-- ---- Session A ----
-- BEGIN TRAN;
-- UPDATE lesson17.Account SET Balance = Balance - 1 WHERE AccountID = 1;  -- locks row 1
-- (pause)                                                                  -- then run the next line
-- UPDATE lesson17.Account SET Balance = Balance - 1 WHERE AccountID = 2;  -- wants row 2

-- ---- Session B (run its first UPDATE during A's pause) ----
-- BEGIN TRAN;
-- UPDATE lesson17.Account SET Balance = Balance - 1 WHERE AccountID = 2;  -- locks row 2
-- UPDATE lesson17.Account SET Balance = Balance - 1 WHERE AccountID = 1;  -- wants row 1 -> DEADLOCK

-- One session becomes the victim (Msg 1205) and is rolled back automatically.

-- Example 5: Read the most recent deadlock graphs from the system_health Extended Event session
SELECT
    XEvent.value('(@timestamp)[1]', 'datetime2')           AS DeadlockTime,
    XEvent.query('.')                                       AS DeadlockGraphXml
FROM (
    SELECT CAST(target_data AS XML) AS TargetData
    FROM sys.dm_xe_session_targets st
    JOIN sys.dm_xe_sessions s ON s.address = st.event_session_address
    WHERE s.name = 'system_health' AND st.target_name = 'ring_buffer'
) AS Data
CROSS APPLY TargetData.nodes('RingBufferTarget/event[@name="xml_deadlock_report"]') AS XEventData(XEvent)
ORDER BY DeadlockTime DESC;

-- =========================================================================
-- WAITS-AND-QUEUES METHODOLOGY
-- =========================================================================

-- Example 6: Top cumulative wait types since the last restart (the master triage query)
SELECT TOP 10
    wait_type,
    wait_time_ms,
    waiting_tasks_count,
    wait_time_ms / NULLIF(waiting_tasks_count, 0) AS AvgWaitMs
FROM sys.dm_os_wait_stats
WHERE wait_type NOT IN (   -- filter benign/idle waits
        'SLEEP_TASK','BROKER_TASK_STOP','XE_TIMER_EVENT','CHECKPOINT_QUEUE',
        'LAZYWRITER_SLEEP','REQUEST_FOR_DEADLOCK_SEARCH','SQLTRACE_INCREMENTAL_FLUSH_SLEEP',
        'WAITFOR','DIRTY_PAGE_POLL','HADR_FILESTREAM_IOMGR_IOCOMPLETION','BROKER_RECEIVE_WAITFOR')
ORDER BY wait_time_ms DESC;

-- Example 7: Reset wait stats (dev only) so you can measure waits for a specific workload
-- DBCC SQLPERF('sys.dm_os_wait_stats', CLEAR);
PRINT 'Uncomment the DBCC SQLPERF line to reset wait stats before measuring a workload.';
```

- [ ] **Step 3: Create `lessons/17-locking-blocking-waits/exercises.sql`**

```sql
USE AdventureWorks2022;
GO
-- Several exercises need TWO sessions. Open a second terminal: .\scripts\connect.ps1

-- Exercise 1: Create a blocking situation.
--   Session A: BEGIN TRAN; UPDATE lesson17.Account SET Balance = Balance - 50 WHERE AccountID = 2; (leave open)
--   Session B: SELECT * FROM lesson17.Account WHERE AccountID = 2;  (this blocks)
-- Then, from a third session (or Session A in a new batch), write a query that reports the
-- waiting session, who is blocking it, and the wait_type.
-- Your diagnostic query here:


-- Exercise 2: While the block from Exercise 1 is active, list the locks held by Session A,
--             showing resource_type and request_mode. Identify the exclusive (X) lock.
-- Your query here:  (remember to COMMIT Session A afterwards)


-- Exercise 3: Run a large UPDATE on lesson17.BigOrders inside a transaction and show, from
--             another session, that lock escalation produced an OBJECT-level lock.
--             ROLLBACK afterwards.
-- Your statements here:


-- Exercise 4: Produce a deadlock using two sessions and the AccountID 1 / 2 ordering from the
--             examples. Then query the system_health ring buffer for the deadlock graph.
-- Your statements + query here:


-- Exercise 5: Show the top 5 non-idle cumulative wait types. In a comment, name what each of
--             your top 2 waits generally indicates.
-- Your query + comment here:
```

- [ ] **Step 4: Create `lessons/17-locking-blocking-waits/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO

-- Exercise 1: Diagnose blocking.
-- Approach: sys.dm_exec_requests exposes blocking_session_id for waiting requests.
SELECT r.session_id AS WaitingSession, r.blocking_session_id AS BlockedBy,
       r.wait_type, r.wait_resource, t.text AS WaitingSQL
FROM sys.dm_exec_requests AS r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE r.blocking_session_id <> 0;
-- The waiting session is Session B; BlockedBy is Session A's SPID; wait_type is typically LCK_M_S
-- (it wants a Shared lock that Session A's Exclusive lock is holding off).

-- Exercise 2: Locks held by the blocker.
-- Approach: sys.dm_tran_locks; the X lock on the KEY/RID is the one causing the block.
SELECT request_session_id, resource_type, request_mode, request_status,
       OBJECT_NAME(p.object_id) AS ObjectName
FROM sys.dm_tran_locks AS l
LEFT JOIN sys.partitions AS p ON p.hobt_id = l.resource_associated_entity_id
WHERE resource_database_id = DB_ID()
ORDER BY request_session_id, resource_type;
-- Look for request_mode = 'X' on a KEY (or RID) resource held by Session A.
-- Remember to COMMIT/ROLLBACK Session A to clear the block.

-- Exercise 3: Lock escalation.
-- Approach: an UPDATE touching every row escalates to a single OBJECT (table) X lock.
BEGIN TRANSACTION;
UPDATE lesson17.BigOrders SET Status = Status WHERE SalesOrderID > 0;
-- In another session, for this SPID:
--   SELECT resource_type, request_mode FROM sys.dm_tran_locks WHERE request_session_id = <SPID>;
--   Expect an 'OBJECT' resource_type with request_mode 'X' (escalated from many KEY locks).
ROLLBACK TRANSACTION;

-- Exercise 4: Deadlock + read the graph.
-- Approach: two sessions lock rows in opposite order; one is chosen as victim (Msg 1205).
-- Run the A/B ordering from examples.sql, then read the graph:
SELECT XEvent.value('(@timestamp)[1]', 'datetime2') AS DeadlockTime,
       XEvent.query('.')                            AS DeadlockGraphXml
FROM (
    SELECT CAST(target_data AS XML) AS TargetData
    FROM sys.dm_xe_session_targets st
    JOIN sys.dm_xe_sessions s ON s.address = st.event_session_address
    WHERE s.name = 'system_health' AND st.target_name = 'ring_buffer'
) AS Data
CROSS APPLY TargetData.nodes('RingBufferTarget/event[@name="xml_deadlock_report"]') AS X(XEvent)
ORDER BY DeadlockTime DESC;
-- The XML shows both processes, the resources each held and wanted, and which was the victim.

-- Exercise 5: Top waits.
-- Approach: aggregate sys.dm_os_wait_stats, filtering idle waits.
SELECT TOP 5 wait_type, wait_time_ms, waiting_tasks_count
FROM sys.dm_os_wait_stats
WHERE wait_type NOT IN ('SLEEP_TASK','LAZYWRITER_SLEEP','WAITFOR','XE_TIMER_EVENT',
        'REQUEST_FOR_DEADLOCK_SEARCH','CHECKPOINT_QUEUE','DIRTY_PAGE_POLL','BROKER_TASK_STOP')
ORDER BY wait_time_ms DESC;
-- Common interpretations:
--   PAGEIOLATCH_* -> waiting on data pages from disk (I/O or memory pressure).
--   LCK_M_*       -> blocking/locking contention.
--   CXPACKET/CXCONSUMER -> parallelism coordination (often benign; investigate if dominant).
--   WRITELOG      -> transaction log write latency.
```

- [ ] **Step 5: Create `lessons/17-locking-blocking-waits/README.md`**

```markdown
# Lesson 17 — Locking, Blocking, Deadlocks & Waits

> **Prerequisite:** Tiers 1–4, especially lesson 11 (transactions & isolation). This lesson shows the *internals and diagnosis* of the concurrency behaviour introduced there.

## What you'll learn
- Lock modes (S, X, U, IS, IX), granularity, and lock escalation
- How to read a blocking chain (who is blocking whom, and on what)
- Deadlocks: how they form, the victim, and reading the deadlock graph
- The waits-and-queues methodology — the master lens for "what is the server waiting on?"

## Setup
Run `setup.sql` once. It creates the `lesson17` schema with a small `Account` table (blocking/deadlock demos) and a larger `BigOrders` table (lock-escalation demo). Re-run to reset.

**Several demos need two sessions.** Open a second terminal and run `.\scripts\connect.ps1` again — the lesson labels code blocks `-- Session A` and `-- Session B`.

## Concepts

### Lock modes & granularity
Locks protect data during transactions. Common modes: **S** (shared, reads), **X** (exclusive, writes), **U** (update, the intermediate step), and intent locks **IS/IX** at coarser levels. Locks are taken at row (KEY/RID), page, or object (table) granularity.

### Lock escalation
When a single statement holds too many fine-grained locks (~5000), SQL Server **escalates** them to one coarse lock (usually a table X lock) to save memory — which can suddenly block everyone else.

### Blocking
**Blocking** is normal, temporary contention: session B waits for a lock session A holds. `sys.dm_exec_requests.blocking_session_id` and `sys.dm_os_waiting_tasks` reveal the chain; `sys.dm_tran_locks` shows the exact locks.

### Deadlocks
A **deadlock** is a *cycle*: A holds a lock B wants, while B holds a lock A wants. SQL Server detects the cycle and kills one transaction (the **victim**, error 1205). Deadlock graphs are captured automatically in the `system_health` Extended Events session.

### Waits & queues
Every time a task can't proceed it records a **wait**. `sys.dm_os_wait_stats` (cumulative) and `sys.dm_os_waiting_tasks` (live) tell you what the server spends its time waiting on — the fastest path to a root cause.

## Worked Investigations (lesson17 schema)
1. Blocking chain via `sys.dm_exec_requests`.
2. Held/requested locks via `sys.dm_tran_locks`.
3. Live waiting tasks.
4. Lock escalation to an OBJECT lock.
5. Deadlock graph from `system_health`.
6. Top cumulative waits (the triage query).
7. Resetting wait stats to measure a specific workload.

## Common issues this explains
- A query "hanging" (it's blocked, not broken).
- Intermittent deadlock errors (1205) under concurrency.
- A bulk update suddenly blocking everyone (lock escalation).

## Pitfalls
- `NOLOCK` "fixes" blocking by reading dirty/duplicated/missing rows — almost never correct.
- `sys.dm_os_wait_stats` is cumulative since restart; reset it (dev) or snapshot-diff it to study one workload.
- Filter idle/benign waits (SLEEP_TASK, LAZYWRITER_SLEEP, etc.) or they drown the signal.
- Deadlock graphs age out of the `system_health` ring buffer — capture promptly.

## Cheatsheet link
See `cheatsheets/05-execution-plans.md` (and lesson 11 for isolation levels)

## Exercises
Open `exercises.sql` and work them in order (some need two sessions). Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Run setup.sql and the single-session parts of examples.sql to verify they execute without errors**

Run `setup.sql` (expect `Lesson 17 setup complete.`). Then run the **single-session** queries in `examples.sql` (Examples 1–3 diagnostics, 6, 7) — they execute and return rows (possibly empty when nothing is blocked) with no `Msg` errors. The two-session blocking/deadlock blocks are commented walkthroughs and are exercised manually per the README.

- [ ] **Step 7: Commit**

```powershell
git add lessons/17-locking-blocking-waits/
git commit -m "feat: add lesson 17 - locking, blocking, deadlocks and waits"
```

---

## Task 5: Lesson 18 — Capstone: Diagnosing Common Issues

**Files:** `lessons/18-diagnosing-common-issues/` (5 files)

- [ ] **Step 1: Create `lessons/18-diagnosing-common-issues/setup.sql`**

```sql
USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson18') IS NOT NULL
BEGIN
    -- Drop procedures first, then tables
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP PROCEDURE lesson18.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.procedures WHERE schema_id = SCHEMA_ID('lesson18');
    EXEC sp_executesql @sql;

    SET @sql = N'';
    SELECT @sql += 'DROP TABLE lesson18.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson18');
    EXEC sp_executesql @sql;

    DROP SCHEMA lesson18;
END
GO
CREATE SCHEMA lesson18;
GO

-- Orders table seeded with a deliberately SKEWED distribution to provoke parameter sniffing:
-- one customer (99999) owns a large block of rows; everyone else has a handful.
-- Single SELECT INTO copy (no re-insert), then add the clustered PK.
SELECT * INTO lesson18.Orders FROM Sales.SalesOrderHeader;
ALTER TABLE lesson18.Orders ADD CONSTRAINT PK_lesson18_Orders PRIMARY KEY CLUSTERED (SalesOrderID);

-- Re-key a large block to a single "whale" customer to create skew
UPDATE TOP (20000) lesson18.Orders SET CustomerID = 99999;

-- Nonclustered index that the sniffing demo will use
CREATE INDEX IX_lesson18_Orders_CustomerID
    ON lesson18.Orders (CustomerID) INCLUDE (OrderDate, TotalDue);

-- A VARCHAR-keyed table to demonstrate the implicit-conversion scan
CREATE TABLE lesson18.Customer (
    CustomerCode VARCHAR(20) NOT NULL PRIMARY KEY,   -- note: VARCHAR, not NVARCHAR
    FullName     NVARCHAR(100) NOT NULL
);
INSERT lesson18.Customer (CustomerCode, FullName)
SELECT TOP 20000
    RIGHT('0000000000' + CAST(ROW_NUMBER() OVER (ORDER BY a.object_id, b.object_id) AS VARCHAR(10)), 10),
    N'Customer ' + CAST(ROW_NUMBER() OVER (ORDER BY a.object_id, b.object_id) AS NVARCHAR(10))
FROM sys.all_objects a CROSS JOIN sys.all_objects b;

-- A stored proc whose plan is sensitive to the first @CustomerID it is called with
CREATE OR ALTER PROCEDURE lesson18.usp_OrdersByCustomer @CustomerID INT
AS
    SELECT SalesOrderID, OrderDate, TotalDue
    FROM lesson18.Orders
    WHERE CustomerID = @CustomerID;
GO

UPDATE STATISTICS lesson18.Orders WITH FULLSCAN;
UPDATE STATISTICS lesson18.Customer WITH FULLSCAN;

PRINT 'Lesson 18 setup complete.';
```

- [ ] **Step 2: Create `lessons/18-diagnosing-common-issues/examples.sql`**

```sql
USE AdventureWorks2022;
GO
SET STATISTICS IO ON;
GO

-- =========================================================================
-- WORKED DIAGNOSIS (the model the exercises follow):
--   symptom -> behind-the-scenes cause -> how to detect -> how to fix
-- =========================================================================

-- -------------------------------------------------------------------------
-- PATTERN 1: PARAMETER SNIFFING
-- Symptom: usp_OrdersByCustomer is fast for some customers, slow for others.
-- Cause: the plan is compiled & cached for the FIRST @CustomerID seen. A plan good for the
--        "whale" (99999, scan) is bad for a tiny customer (seek), and vice versa.
-- Detect: compare plans/reads when called with a tiny vs the whale customer first.
-- -------------------------------------------------------------------------
DBCC FREEPROCCACHE;  -- dev only: clear cache so the next call sets the sniffed plan
EXEC lesson18.usp_OrdersByCustomer @CustomerID = 11000;   -- tiny customer compiles a SEEK plan
EXEC lesson18.usp_OrdersByCustomer @CustomerID = 99999;   -- whale reuses the SEEK plan -> slow

DBCC FREEPROCCACHE;
EXEC lesson18.usp_OrdersByCustomer @CustomerID = 99999;   -- whale compiles a SCAN plan
EXEC lesson18.usp_OrdersByCustomer @CustomerID = 11000;   -- tiny reuses the SCAN plan -> wasteful

-- Fix: OPTIMIZE FOR UNKNOWN (use average density) or RECOMPILE (fresh plan each call)
CREATE OR ALTER PROCEDURE lesson18.usp_OrdersByCustomer_Fixed @CustomerID INT
AS
    SELECT SalesOrderID, OrderDate, TotalDue
    FROM lesson18.Orders
    WHERE CustomerID = @CustomerID
    OPTION (OPTIMIZE FOR UNKNOWN);
GO
EXEC lesson18.usp_OrdersByCustomer_Fixed @CustomerID = 11000;
EXEC lesson18.usp_OrdersByCustomer_Fixed @CustomerID = 99999;

-- -------------------------------------------------------------------------
-- PATTERN 2: IMPLICIT CONVERSION SCAN
-- Symptom: a point lookup on an indexed VARCHAR column scans instead of seeks.
-- Cause: comparing a VARCHAR column to an NVARCHAR literal forces SQL Server to convert
--        every row's column value (NVARCHAR has higher precedence) -> non-SARGable -> scan.
-- Detect: high logical reads + a yellow warning on the plan operator (CONVERT_IMPLICIT).
-- -------------------------------------------------------------------------
SELECT CustomerCode, FullName FROM lesson18.Customer WHERE CustomerCode = N'0000000123';  -- N'' = NVARCHAR -> scan
-- Fix: match the column's type (no N prefix) so the seek is restored.
SELECT CustomerCode, FullName FROM lesson18.Customer WHERE CustomerCode = '0000000123';   -- VARCHAR -> seek

-- -------------------------------------------------------------------------
-- PATTERN 3: KEY LOOKUP BLOWUP
-- Symptom: a selective query still does many logical reads.
-- Cause: the nonclustered index covers the predicate but not the SELECT columns, so the engine
--        does one Key Lookup per matching row back to the clustered index.
-- Detect: a Key Lookup operator in the plan; reads scale with matched rows.
-- -------------------------------------------------------------------------
SELECT CustomerID, OrderDate, TotalDue, SubTotal     -- SubTotal is NOT in the index
FROM lesson18.Orders
WHERE CustomerID = 11000;
-- Fix: add the missing column to the index's INCLUDE list (covering index).
CREATE INDEX IX_lesson18_Orders_CustomerID_Covering
    ON lesson18.Orders (CustomerID) INCLUDE (OrderDate, TotalDue, SubTotal);
SELECT CustomerID, OrderDate, TotalDue, SubTotal
FROM lesson18.Orders
WHERE CustomerID = 11000;   -- now covered: no Key Lookup

-- -------------------------------------------------------------------------
-- PATTERN 4: STALE STATISTICS (recap from lesson 15, as a diagnosis)
-- Detect: large gap between Estimated and Actual rows in the actual plan.
-- Fix: UPDATE STATISTICS.
-- -------------------------------------------------------------------------
SELECT name AS StatsName, STATS_DATE(object_id, stats_id) AS LastUpdated
FROM sys.stats
WHERE object_id = OBJECT_ID('lesson18.Orders');

GO
SET STATISTICS IO OFF;
```

- [ ] **Step 3: Create `lessons/18-diagnosing-common-issues/exercises.sql`**

```sql
USE AdventureWorks2022;
GO
-- Re-run setup.sql for a clean slate before starting. Use SSMS (Ctrl+M) to see plans.
SET STATISTICS IO ON;
GO

-- Exercise 1 (Parameter sniffing): Demonstrate that lesson18.usp_OrdersByCustomer produces a
--   plan sensitive to the first parameter. Use DBCC FREEPROCCACHE, call it with the whale
--   (99999) first then a tiny customer (11000), and compare logical reads. Then write a fixed
--   version and show it behaves consistently.
-- Your statements here:


-- Exercise 2 (Implicit conversion): Write a query against lesson18.Customer that accidentally
--   causes an implicit-conversion SCAN, then the corrected version that SEEKS. Report the
--   logical reads for each and explain the difference in a comment.
-- Your statements + comment here:


-- Exercise 3 (Key lookup): Write a query on lesson18.Orders that triggers a Key Lookup, then
--   create the covering index that eliminates it. Confirm via logical reads / the plan.
-- Your statements here:


-- Exercise 4 (Blocking — two sessions): Reproduce blocking on lesson18.Orders (Session A holds
--   an uncommitted UPDATE; Session B reads the same row). From a third session, identify the
--   blocker and the wait_type. Then resolve it.
-- Your statements + diagnostic query here:


-- Exercise 5 (Root-cause workflow): You are told "the report proc is sometimes slow." Outline,
--   in a comment, the order of checks you would run using the Tier 5 toolkit (plan cache,
--   estimated vs actual, waits, blocking, statistics) and what each would rule in or out.
-- Your comment here:
```

- [ ] **Step 4: Create `lessons/18-diagnosing-common-issues/exercises-solutions.sql`**

```sql
USE AdventureWorks2022;
GO
SET STATISTICS IO ON;
GO

-- Exercise 1: Parameter sniffing.
-- Approach: the first call after a cache clear sets the cached plan; reuse may be wrong for others.
DBCC FREEPROCCACHE;
EXEC lesson18.usp_OrdersByCustomer @CustomerID = 99999;   -- compiles a SCAN plan (whale)
EXEC lesson18.usp_OrdersByCustomer @CustomerID = 11000;   -- reuses SCAN -> excess reads for a tiny customer
-- Fix: OPTIMIZE FOR UNKNOWN uses average density instead of the sniffed value.
CREATE OR ALTER PROCEDURE lesson18.usp_OrdersByCustomer_Fixed @CustomerID INT
AS
    SELECT SalesOrderID, OrderDate, TotalDue
    FROM lesson18.Orders WHERE CustomerID = @CustomerID
    OPTION (OPTIMIZE FOR UNKNOWN);
GO
EXEC lesson18.usp_OrdersByCustomer_Fixed @CustomerID = 99999;
EXEC lesson18.usp_OrdersByCustomer_Fixed @CustomerID = 11000;
-- The fixed proc gives a stable, average-case plan for both. (RECOMPILE is the alternative,
-- trading compile cost for a perfect plan every call.)

-- Exercise 2: Implicit conversion.
-- Approach: NVARCHAR literal forces per-row CONVERT on the VARCHAR column -> scan.
SELECT CustomerCode, FullName FROM lesson18.Customer WHERE CustomerCode = N'0000000123';  -- SCAN (high reads)
SELECT CustomerCode, FullName FROM lesson18.Customer WHERE CustomerCode = '0000000123';   -- SEEK (few reads)
-- The first compares VARCHAR to NVARCHAR; NVARCHAR has higher datatype precedence, so the COLUMN
-- is converted for every row, defeating the index (non-SARGable). Matching the literal's type
-- (no N) keeps the predicate SARGable and restores the seek. Watch logical reads drop sharply.

-- Exercise 3: Key lookup.
-- Approach: select a column not in the index -> Key Lookup per row; fix with a covering index.
SELECT CustomerID, OrderDate, TotalDue, SubTotal
FROM lesson18.Orders WHERE CustomerID = 11000;   -- Key Lookup present
CREATE INDEX IX_lesson18_Orders_CustomerID_Covering
    ON lesson18.Orders (CustomerID) INCLUDE (OrderDate, TotalDue, SubTotal);
SELECT CustomerID, OrderDate, TotalDue, SubTotal
FROM lesson18.Orders WHERE CustomerID = 11000;   -- covered: no Key Lookup, fewer reads

-- Exercise 4: Blocking (two sessions).
-- Session A:  BEGIN TRAN; UPDATE lesson18.Orders SET TotalDue = TotalDue WHERE SalesOrderID = 43659;  (leave open)
-- Session B:  SELECT * FROM lesson18.Orders WHERE SalesOrderID = 43659;  (blocks)
-- Third session diagnostic:
SELECT r.session_id AS WaitingSession, r.blocking_session_id AS BlockedBy, r.wait_type, r.wait_resource
FROM sys.dm_exec_requests AS r
WHERE r.blocking_session_id <> 0;
-- wait_type is typically LCK_M_S (B wants a Shared lock blocked by A's Exclusive lock).
-- Resolve by committing/rolling back Session A.

-- Exercise 5: Root-cause workflow (model answer).
-- 1) sys.dm_os_wait_stats / waiting_tasks: is the server CPU-bound, I/O-bound, or blocked?
--    LCK_* -> go to blocking; PAGEIOLATCH_* -> I/O/memory; SOS_SCHEDULER_YIELD -> CPU.
-- 2) sys.dm_exec_query_stats: find the proc's plan; check execution_count and avg reads/CPU.
-- 3) Actual plan: compare Estimated vs Actual rows. Big gap -> statistics or parameter sniffing.
-- 4) sys.stats / STATS_DATE: are statistics stale? UPDATE STATISTICS and re-test.
-- 5) Plan operators: Key Lookup -> covering index; Scan with CONVERT_IMPLICIT -> type mismatch;
--    spill warning -> memory grant/estimate problem.
-- 6) If "sometimes" slow with stable data -> suspect parameter sniffing; test with RECOMPILE.
-- This narrows symptom -> cause -> fix without guessing.
```

- [ ] **Step 5: Create `lessons/18-diagnosing-common-issues/README.md`**

```markdown
# Lesson 18 — Capstone: Diagnosing Common Issues

> **Prerequisite:** All of Tier 5 (lessons 14–17). This capstone integrates storage, the optimizer, memory, and concurrency into a single troubleshooting workflow.

## What you'll learn
- A repeatable diagnosis workflow: **symptom → behind-the-scenes cause → how to detect → how to fix**
- Recognising and fixing the most common recurring issues
- Driving a root-cause investigation with the Tier 5 toolkit (plans, DMVs, statistics, waits)

## Setup
Run `setup.sql` once. It creates the `lesson18` schema with deliberately misbehaving objects: a **skewed** `Orders` table (one "whale" customer) for parameter sniffing, a **VARCHAR-keyed** `Customer` table for implicit conversion, and a sniff-sensitive stored procedure. Re-run to reset.

## The pattern catalog

| Pattern | Symptom | Behind-the-scenes cause | Detect | Fix |
|---|---|---|---|---|
| **Parameter sniffing** | Proc fast for some params, slow for others | Plan cached for the first param value; wrong for skewed data | Compare reads/plan across params; check `dm_exec_query_stats` | `OPTIMIZE FOR UNKNOWN`, `RECOMPILE`, or fix the index |
| **Implicit conversion** | Indexed lookup scans | Type mismatch (e.g. VARCHAR col vs `N''` literal) converts every row | `CONVERT_IMPLICIT` warning on the plan; high reads | Match the literal's type to the column |
| **Key lookup** | Selective query, many reads | NC index covers predicate but not SELECT columns | Key Lookup operator in plan | Add the missing columns to `INCLUDE` |
| **Stale statistics** | Bad plan after data change | Optimizer estimates from old distribution | Estimated vs Actual gap; `STATS_DATE` | `UPDATE STATISTICS` |
| **Blocking** | Query "hangs" | Another transaction holds a conflicting lock | `dm_exec_requests.blocking_session_id` | Shorten transactions; fix the blocker; right isolation |
| **tempdb spill** | Slow sort/hash | Memory grant too small (bad estimate) | Spill warning on operator; `dm_exec_query_memory_grants` | Fix stats; reduce rows/cols; supporting index |

## Worked Investigations (lesson18 schema)
1. Parameter sniffing — sniff a SEEK vs SCAN plan and fix with `OPTIMIZE FOR UNKNOWN`.
2. Implicit conversion — `N''` vs `''` literal on a VARCHAR key.
3. Key lookup — uncovered column, then a covering index.
4. Stale statistics — `STATS_DATE` and the estimate gap.

## The root-cause workflow
1. **Waits first** — `dm_os_wait_stats` / `dm_os_waiting_tasks`: I/O-bound, CPU-bound, or blocked?
2. **Find the query** — `dm_exec_query_stats` for the heaviest statements.
3. **Estimated vs actual** — a big gap points at statistics or parameter sniffing.
4. **Statistics freshness** — `STATS_DATE`; refresh and retest.
5. **Plan operators** — Key Lookup, implicit-conversion scan, or spill warnings each have a specific fix.
6. **"Sometimes slow" with stable data** — suspect parameter sniffing; test with `RECOMPILE`.

## Common issues this explains
This *is* the common-issues lesson — each catalog row is a real production pattern with a concrete detection and fix.

## Pitfalls
- `DBCC FREEPROCCACHE` is a **dev-only** demo tool — it clears the whole cache and hurts production.
- Don't fix parameter sniffing reflexively with `RECOMPILE` everywhere — it adds compile cost; prefer `OPTIMIZE FOR UNKNOWN` first.
- A covering index has a write cost — add columns deliberately, not "just in case".
- Always confirm a fix by re-measuring (logical reads / plan), not by assumption.

## Cheatsheet link
See `cheatsheets/05-execution-plans.md` and `cheatsheets/04-indexes.md`

## Exercises
Open `exercises.sql` and work them in order (Exercise 4 needs two sessions). Solutions in `exercises-solutions.sql`.
```

- [ ] **Step 6: Run setup.sql and examples.sql to verify they execute without errors**

Run `setup.sql` (expect `Lesson 18 setup complete.`). Then run `examples.sql`. Expect: the implicit-conversion and key-lookup pairs show markedly different logical reads (scan vs seek; with vs without covering index), the `STATS_DATE` query returns rows, and no `Msg` errors. `DBCC FREEPROCCACHE` is dev-only and intentional here.

- [ ] **Step 7: Commit**

```powershell
git add lessons/18-diagnosing-common-issues/
git commit -m "feat: add lesson 18 - capstone diagnosing common issues"
```

---

## Task 6: Register Tier 5 in the root README and CLAUDE.md

**Files:**
- Modify: `README.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update the curriculum table in `README.md`**

Find the curriculum table (the rows for Tiers 1–4) and add a Tier 5 row immediately after the Tier 4 row:

```markdown
| 5 — Under the Hood | 14–18 | Storage internals, query lifecycle & optimizer, memory/buffer pool/log, locking/blocking/waits, diagnosing common issues |
```

- [ ] **Step 2: Add a "second session" note to `README.md`**

In the Helper Scripts section of `README.md`, add this note after the scripts table:

```markdown
> **Two sessions:** Some Tier 5 lessons (locking, blocking, deadlocks) need two
> simultaneous connections. Open a second terminal and run `.\scripts\connect.ps1`
> again — lessons label the blocks `-- Session A` and `-- Session B`.
```

- [ ] **Step 3: Update the Curriculum Map in `CLAUDE.md`**

In the `## Curriculum Map` table in `CLAUDE.md`, add a Tier 5 row after the Tier 4 row:

```markdown
| 5 — Under the Hood | 14–18 | Storage Internals, Query Lifecycle & Optimizer, Memory/Buffer Pool/Log, Locking/Blocking/Waits, Diagnosing Common Issues |
```

- [ ] **Step 4: Note the two-session and DMV conventions in `CLAUDE.md`**

In the `## Critical Authoring Rules` section of `CLAUDE.md`, add these bullets:

```markdown
- **Tier 5 internals lessons** lean on DMVs, `DBCC PAGE`/`IND` (trace flag 3604), and two-session
  walkthroughs (a second `connect.ps1`). Where a metric is environment-dependent (PLE, fragmentation,
  memory grants), the README must say so and point at the operator/wait/direction, not absolute values.
- **Diagnose-style exercises** still ship a solution in `exercises-solutions.sql`: the diagnostic
  query/steps PLUS a 2–4 line root-cause-and-fix explanation, not just the answer.
```

- [ ] **Step 5: Verify the edits render correctly**

Open `README.md` and `CLAUDE.md` and confirm the new Tier 5 rows sit in the existing tables without breaking the Markdown table formatting, and the notes read cleanly.

- [ ] **Step 6: Commit**

```powershell
git add README.md CLAUDE.md
git commit -m "docs: register Tier 5 (engine internals) in README and CLAUDE.md"
```

---

## Self-Review

**Spec coverage check:**

| Spec requirement | Covered in |
|---|---|
| Tier 5, lessons 14–18, 5-file format, dedicated `lessonNN` schemas | Tasks 1–5 |
| Lesson 14 — storage & access internals (pages, extents, heaps/B-trees, page splits, fragmentation, forwarded records) | Task 1 |
| Lesson 15 — query lifecycle & optimizer (phases, statistics, histograms, plan cache, stale-stats) | Task 2 |
| Lesson 16 — memory/buffer pool/log (PLE, WAL, VLFs, log truncation, tempdb spills) | Task 3 |
| Lesson 17 — locking/blocking/deadlocks/waits (modes, escalation, blocking chains, deadlock graph, waits methodology) | Task 4 |
| Lesson 18 — capstone pattern catalog + root-cause workflow (sniffing, implicit conversion, key lookup, stale stats, blocking, spill) | Task 5 |
| Per-lesson "common issues" section + diagnose-style exercises with explained solutions | Tasks 1–5 (README "Common issues this explains" + solutions with explanations) |
| Copy/inflate AdventureWorks tables; idempotent schema-scoped setup | Tasks 1, 3, 4, 5 setup.sql |
| Two-session demos via a second `connect.ps1` | Tasks 4, 5 + Task 6 README note |
| Register tier in README + CLAUDE.md; extend authoring rules | Task 6 |
| Environment-dependent-numbers caveat | READMEs in Tasks 1, 3, 4 + CLAUDE.md rule in Task 6 |

**Placeholder scan:** No "TBD"/"TODO"/"implement later". DBCC PAGE page-number placeholders are intentional (the page id is data-dependent and discovered in the prior example/exercise) and are clearly flagged with instructions to substitute a real value.

**Type/identifier consistency:** Schema names `lesson14`–`lesson18` consistent; index/proc/constraint names referenced in later steps match their CREATE statements (e.g. `PK_lesson14_SalesOrderCI`, `IX_lesson18_Orders_CustomerID_Covering`, `usp_OrdersByCustomer` / `usp_OrdersByCustomer_Fixed`). DMV and DBCC names verified against SQL Server 2022.

All spec requirements map to a task. No gaps.
