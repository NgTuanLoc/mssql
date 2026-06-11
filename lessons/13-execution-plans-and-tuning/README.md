# Lesson 13 — Execution Plans & Query Tuning

## What you'll learn
- Getting estimated vs actual execution plans in SSMS (Ctrl+L / Ctrl+M)
- Reading plan operators: Seek, Scan, Key Lookup, Nested Loops, Hash Match, Merge Join, Sort
- `SET STATISTICS IO` and `SET STATISTICS TIME` output
- SARGability — what it means and how to fix non-SARGable predicates
- Parameter sniffing — what it is and three ways to address it
- Common rewrites: correlated subquery → JOIN, function-on-column → range predicate

## Setup
Run `setup.sql` once. It creates the `lesson13` schema with an indexed copy of `SalesOrderHeader` and pre-built indexes for the examples.

**SSMS is strongly recommended for this lesson** — its graphical plan viewer shows operator costs, tooltips, and memory grant warnings that Azure Data Studio does not expose as richly.

## Concepts

### Getting plans

| Method | What you get |
|---|---|
| Ctrl+L (SSMS) | Estimated plan — no execution |
| Ctrl+M then run | Actual plan — with real row counts |
| `SET STATISTICS IO ON` | Logical reads per table (Messages tab) |
| `SET STATISTICS TIME ON` | CPU and elapsed time |

### Reading plans — key rules

1. **Read right to left, top to bottom** — data flows from leaves (scans/seeks) to root (SELECT).
2. **Arrow width** = estimated row count. Thick arrow into a cheap operator = row count mismatch.
3. **Cost %** is relative to the query, not to other queries.
4. **Yellow triangle** = optimizer warning — hover to see it.

### Operator quick reference

| Operator | Good | Investigate when |
|---|---|---|
| Index Seek | Yes — finds rows via B-tree | Rarely bad |
| Key Lookup | Sometimes | Appears per row — add INCLUDE columns |
| Clustered Index Scan | OK for full reads | Bad when a seek was expected |
| Nested Loops | Small outer set | Large outer set × large inner = slow |
| Hash Match | Large unsorted inputs | Memory spill → TempDB |
| Sort | Unavoidable sometimes | Missing index that delivers sorted rows |

### SARGability

A predicate is SARGable if the optimizer can use an index seek to satisfy it.

```sql
-- Non-SARGable (scan)                        -- SARGable rewrite (seek)
WHERE YEAR(col) = 2024                  →  WHERE col >= '2024-01-01' AND col < '2025-01-01'
WHERE CONVERT(VARCHAR,col) = '42'       →  Fix the type mismatch at the source
WHERE col LIKE '%suffix'                →  No rewrite — leading wildcard always scans
WHERE col + 1 = 5                       →  WHERE col = 4
WHERE ISNULL(col,0) = 0                 →  WHERE col = 0 OR col IS NULL
```

### `SET STATISTICS IO` output

```
Table 'SalesOrderHeader'. Scan count 1, logical reads 4, ...
```
- **logical reads** — pages read from buffer cache. Lower = better. Primary I/O metric.
- **scan count** — > 1 on the inner side of a nested-loops join.

### Parameter sniffing

SQL Server compiles the plan for the first parameter value it sees. If that value is unrepresentative:

```sql
-- Option 1: OPTIMIZE FOR UNKNOWN — use average statistics
OPTION (OPTIMIZE FOR (@param UNKNOWN))

-- Option 2: OPTIMIZE FOR specific value — if one value is typical
OPTION (OPTIMIZE FOR (@param = 1))

-- Option 3: RECOMPILE — new plan every execution (most expensive)
OPTION (RECOMPILE)
-- or: WITH RECOMPILE on the procedure
```

## Worked Examples (lesson13 schema)
1. Clustered Index Seek on `SalesOrderID` — minimal logical reads.
2. Clustered Index Scan on `COUNT(*)` — all pages read.
3. Nonclustered seek with `INCLUDE` — no key lookup.
4. SARGable vs non-SARGable date filter — plan and I/O comparison.
5. Hash Match join on a large result set.
6. Parameter sniffing demo with `usp_OrdersByTerritory`.
7. `OPTIMIZE FOR UNKNOWN` fix in `usp_OrdersByTerritory_Fixed`.

## Pitfalls
- Trusting estimated plan cost % across different queries — it's not comparable.
- Adding indexes based on missing-index hints without checking overlap with existing indexes.
- `NOLOCK` (`READ UNCOMMITTED`) hint — reads dirty data and can even skip or duplicate rows due to page splits. Almost never the right fix.
- Comparing logical reads on a cold cache — use a warm cache (run the query twice, measure the second).
- Fixing parameter sniffing with `RECOMPILE` on every execution — expensive; use `OPTIMIZE FOR UNKNOWN` first.

## Cheatsheet link
See `cheatsheets/06-execution-plans.md`

## Exercises
Open `exercises.sql` with SSMS and enable the actual execution plan (Ctrl+M) before running. Solutions in `exercises-solutions.sql`.
