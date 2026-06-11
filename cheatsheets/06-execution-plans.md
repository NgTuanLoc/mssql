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

| Operator | What it does | When it's bad |
|---|---|---|
| **Table Scan** | Reads every row of a heap (no clustered index) | Almost always — add a clustered index |
| **Clustered Index Scan** | Reads entire clustered index | Query needs most rows (OK) or missing a nonclustered index (bad) |
| **Index Seek** | Navigates B-tree to matching rows | Generally good — what you want |
| **Key Lookup** | Goes back to clustered index to fetch non-index columns | Appears with nonclustered seeks; add INCLUDE columns to eliminate |
| **Nested Loops** | For each outer row, probe inner side | Good for small outer sets; bad with large row counts |
| **Hash Match** | Builds hash table of one input, probes with other | Good for large unsorted sets; watch for memory spills |
| **Merge Join** | Both inputs sorted on join key; merge in one pass | Efficient but requires sorted inputs |
| **Sort** | Explicit sort | Often a sign of missing index; check for memory spills |
| **Parallelism (Repartition Streams / Gather Streams)** | Distributes/collects parallel threads | Fine for large queries; unexpected parallelism on small queries suggests bad estimates |

---

## Warning Signs

- **Yellow warning triangle** on any operator → hover to see: missing statistics, implicit conversion, memory grant issue, etc.
- **Thick arrows** (many estimated rows) into a cheap operator → may be a fan-out from a bad join.
- **Thin arrows** (few rows estimated) but many actual rows → outdated statistics; run `UPDATE STATISTICS tablename`.
- **Key Lookup** present → add `INCLUDE` columns to the nonclustered index.
- **Spill to TempDB** (on Sort or Hash Match) → increase `max server memory`, rewrite to avoid the sort, or add an index that delivers sorted rows.

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
