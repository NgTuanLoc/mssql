# Execution Plans & Query Tuning Cheatsheet

---

## Getting Plans

```sql
-- Estimated plan (no execution): press Ctrl+L in SSMS, or:
SET SHOWPLAN_ALL ON;
GO
SELECT ...;
GO
SET SHOWPLAN_ALL OFF;

-- Actual plan (requires execution): press Ctrl+M in SSMS before running, or:
SET STATISTICS PROFILE ON;
GO
SELECT ...;
GO
SET STATISTICS PROFILE OFF;

-- I/O and time stats
SET STATISTICS IO   ON;
SET STATISTICS TIME ON;
GO
SELECT ...;
GO
SET STATISTICS IO   OFF;
SET STATISTICS TIME OFF;
```

In SSMS: Query → Include Actual Execution Plan (Ctrl+M) is the most common workflow.

---

## Reading Plans

Plans are read **right to left, top to bottom**. Data flows from leaf operators (scans/seeks) up through intermediate operators to the root (SELECT/INSERT/etc.).

**Key numbers on each operator:**
- **Cost %** — estimated share of total query cost.
- **Estimated vs Actual rows** — large discrepancy → stale statistics.
- **Estimated vs Actual executions** — nested-loops inner side shows how many times an operator ran.

---

## Common Operators

**Data access:**

| Operator | What it does | When it's bad |
|---|---|---|
| **Table Scan** | Reads every row of a heap (no clustered index) | Almost always — add a clustered index |
| **Clustered Index Scan** | Reads entire clustered index | Query needs most rows (OK) or missing a nonclustered index (bad) |
| **Nonclustered Index Scan** | Reads an entire nonclustered index — far fewer pages than the clustered scan if the index is narrow | Still a scan; check why no seek (non-SARGable predicate?) |
| **Index Seek** | Navigates B-tree to matching rows | Generally good — what you want. But hover it: a **residual predicate** (Predicate in addition to Seek Predicates) means it seeks broadly then filters row-by-row |
| **Key Lookup** | Goes back to clustered index to fetch non-index columns | Appears with nonclustered seeks; add INCLUDE columns to eliminate. Each row pays one lookup — at thousands of rows a scan would be cheaper |
| **RID Lookup** | Key Lookup's heap sibling: fetches rows by row ID | Same fix as Key Lookup — or add the missing clustered index |
| **Columnstore Index Scan** | Scans a columnstore in batch mode | Rarely bad for analytics; wrong tool for selective OLTP lookups |

**Joins:**

| Operator | What it does | When it's bad |
|---|---|---|
| **Nested Loops** | For each outer row, probe inner side | Good for small outer sets; bad with large row counts — check **Number of Executions** on the inner side, that's the multiplier |
| **Hash Match (join)** | Builds hash table of one input, probes with other | Good for large unsorted sets; blocks until the build input finishes; watch for memory spills |
| **Merge Join** | Both inputs sorted on join key; merge in one pass | Efficient but requires sorted inputs — if a Sort was injected to enable it, the Sort eats the savings |
| **Adaptive Join** (2017+) | Defers Hash-vs-Loops choice until runtime row counts are known (batch mode) | Rarely — it exists to avoid the misestimation failure modes above |
| *Semi-join variants* | `EXISTS` / `IN` show up as Nested Loops or Hash Match labelled **Left Semi Join** (or Anti Semi Join for `NOT EXISTS`) | Not bad — just know what you're looking at |

**Aggregation & plumbing:**

| Operator | What it does | When it's bad |
|---|---|---|
| **Stream Aggregate** | GROUP BY over already-sorted input — cheap, no memory | Fine; its presence tells you sorted input was available |
| **Hash Match (Aggregate)** | GROUP BY via hash table — input unsorted | Fine for big sets; memory grant + spill risk, like the hash join |
| **Sort** | Explicit sort | Often a sign of missing index; check for memory spills |
| **Table Spool / Index Spool (Eager or Lazy)** | Caches an intermediate result in tempdb because it will be re-read | Usually a warning: a CTE referenced twice, or an unindexed inner side being re-scanned |
| **Compute Scalar** | Evaluates expressions (computed columns, string ops) | Almost never — plumbing |
| **Filter** | Row-by-row predicate applied mid-plan | Suspicious when late in the plan: the predicate couldn't be pushed down to the seek/scan |
| **Top** | Cuts the stream after N rows | Fine; pairs with ORDER BY upstream |
| **Concatenation** | Glues inputs together (`UNION ALL`) | Fine |
| **Parallelism (Repartition Streams / Gather Streams)** | Distributes/collects parallel threads | Fine for large queries; unexpected parallelism on small queries suggests bad estimates |

---

## Ranking: Best to Worst

**Data access, for selective queries** (a query that needs a small fraction of the table):

| # | Access pattern | Why |
|---|---|---|
| 1 | **Clustered Index Seek** | Straight to the rows; the row data is right there — no lookup ever needed |
| 2 | **Nonclustered Index Seek (covering)** | Seek on a narrower index; `INCLUDE` columns mean no lookup |
| 3 | **Nonclustered Index Seek + Key Lookup** | Fine for a handful of rows; each row pays an extra lookup, so it loses to a scan at volume |
| 4 | **Nonclustered Index Scan** | Still a scan, but over a narrow index = far fewer pages than the table |
| 5 | **Clustered Index Scan** | Whole table — acceptable only when you really need most rows |
| 6 | **Table Scan (heap)** | Whole table, no ordering, plus forwarded-record overhead |
| 7 | **NCI Seek + RID Lookup at volume** | Per-row lookups into a heap — the worst per-row cost |

> Flip side: for a query that genuinely needs *most* of the table, a scan beats millions of seeks. "Seek good, scan bad" is a heuristic about selective queries, not a law.

**Joins can't be ranked absolutely** — each is best in its niche and worst outside it:

| Operator | Best when | Worst when |
|---|---|---|
| **Merge Join** | Inputs already sorted on the join key (cheapest per row, no memory) | A Sort had to be injected to enable it |
| **Nested Loops** | Small outer input + indexed inner side | Optimizer guessed small but got millions — inner side re-executes per outer row |
| **Hash Match** | Large, unsorted inputs, one pass each | Memory misestimate → tempdb spill |
| **Adaptive Join** | Batch mode available — picks at runtime | Rarely bad |

The real rule: **a join operator is rarely bad by itself — it's bad when the row estimate that chose it was wrong.** Always check estimated vs actual rows on its inputs before blaming the operator.

**Always-suspicious, whatever the query:** Eager/Lazy **Spool** (work being re-read), **Sort** or **Hash Match** with a spill warning, a late **Filter** (predicate that couldn't be pushed down), **Parallelism** on a query that should be trivial.

---

## Warning Signs

- **Yellow warning triangle** on any operator → hover to see: missing statistics, implicit conversion, memory grant issue, etc.
- **Thick arrows** (many estimated rows) into a cheap operator → may be a fan-out from a bad join.
- **Thin arrows** (few rows estimated) but many actual rows → outdated statistics; run `UPDATE STATISTICS tablename`.
- **Key Lookup** present → add `INCLUDE` columns to the nonclustered index.
- **Spill to TempDB** (on Sort or Hash Match) → increase `max server memory`, rewrite to avoid the sort, or add an index that delivers sorted rows.
- **Table/Index Spool** → something is being computed once and re-read; common causes: a CTE referenced twice (inline it or use a `#temp` table) or a nested-loops inner side with no usable index.
- **Seek with a residual Predicate** (hover the seek: a `Predicate` listed *besides* `Seek Predicates`) → the seek lands on a range, then filters row by row; the index column order doesn't match the query.
- **Number of Executions ≫ 1** on a nested-loops inner-side operator → that subtree runs once per outer row; multiply its cost mentally by the execution count.

---

## SARGability Checklist

A predicate is **SARGable** (Search ARGument able) if SQL Server can use an index seek to satisfy it.

| Non-SARGable (full scan) | SARGable rewrite |
|---|---|
| `WHERE YEAR(OrderDate) = 2024` | `WHERE OrderDate >= '2024-01-01' AND OrderDate < '2025-01-01'` |
| `WHERE CONVERT(VARCHAR, col) = '42'` | `WHERE col = 42` (fix type mismatch at source) |
| `WHERE col LIKE '%suffix'` | Cannot rewrite — leading wildcard always scans |
| `WHERE col + 1 = 5` | `WHERE col = 4` |
| `WHERE ISNULL(col, 0) = 0` | `WHERE col = 0 OR col IS NULL` |
| `WHERE LEN(col) > 5` | Cannot rewrite simply — consider a computed column + index |

---

## Parameter Sniffing

SQL Server compiles a plan for the first set of parameter values it sees. If that plan is poor for other values:

```sql
-- Force recompile on each execution (diagnoses, but expensive)
EXEC dbo.MyProc @param = 1 WITH RECOMPILE;

-- Optimize for a specific value
CREATE OR ALTER PROCEDURE dbo.MyProc @param INT
AS
    SELECT * FROM dbo.Orders WHERE CustomerID = @param
    OPTION (OPTIMIZE FOR (@param = 1));

-- Optimize for unknown (uses average statistics)
    OPTION (OPTIMIZE FOR (@param UNKNOWN));

-- Local variable trick (breaks sniffing — use as last resort)
CREATE OR ALTER PROCEDURE dbo.MyProc @param INT
AS
    DECLARE @local INT = @param;
    SELECT * FROM dbo.Orders WHERE CustomerID = @local;
```

---

## `SET STATISTICS IO` Output

```
Table 'SalesOrderHeader'. Scan count 1, logical reads 689, physical reads 0, ...
```

- **logical reads** — pages read from buffer cache. Lower is better. This is the primary I/O metric to optimize.
- **physical reads** — pages read from disk (only on cold cache). Typically 0 in dev.
- **scan count** — number of times the index was scanned. > 1 often means nested-loops inner side.

---

## Common Mistakes

- Reading plans left to right — always right to left.
- Treating estimated cost % as absolute — it's relative to the query, not to other queries.
- Adding indexes based on missing-index hints alone — always check whether the suggested index already exists or overlaps an existing one.
- Ignoring implicit conversion warnings — they kill index seeks silently.
- Comparing estimated plans across different servers — statistics differ, so plans differ.
