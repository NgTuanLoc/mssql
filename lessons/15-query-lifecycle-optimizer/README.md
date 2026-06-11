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
See `cheatsheets/06-execution-plans.md`

## Exercises
Open `exercises.sql` and work them in order. Solutions in `exercises-solutions.sql`.
